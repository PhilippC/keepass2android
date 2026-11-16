// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Android;
using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;
using Java.IO;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using Console = System.Console;

namespace keepass2android.Io
{
  /** Helper for surviving revoked per-document access.
   * When another app (e.g. a sync tool like Syncthing) replaces a document,
   * Android revokes the persisted permission for the document uri and the uri
   * becomes inaccessible. Permissions for a containing folder (tree) survive
   * such replacements, so we try to resolve documents through an existing
   * tree permission. */
  public static class TreeDocumentAccess
  {
    private static bool IsLollipopOrLater
    {
      get { return (int)Build.VERSION.SdkInt >= 21; }
    }

    /** Exceptions of these types usually mean the document uri is no longer
     * accessible: the document was removed or replaced by another app (which
     * makes Android revoke our persisted permission for it), or the permission
     * was revoked otherwise. */
    public static bool IsAccessFailure(Exception e)
    {
      for (Exception ex = e; ex != null; ex = ex.InnerException)
      {
        if (ex is Java.Lang.SecurityException || ex is System.Security.SecurityException)
          return true;
        if (ex is Java.IO.FileNotFoundException || ex is System.IO.FileNotFoundException)
          return true;
        string message = ex.Message;
        if ((message != null) && (message.Contains("requires that you obtain access using ACTION_OPEN_DOCUMENT")))
          return true;
      }
      return false;
    }

    public static bool IsTreeUri(Android.Net.Uri uri)
    {
      if (!IsLollipopOrLater)
        return false;
      try
      {
        DocumentsContract.GetTreeDocumentId(uri);
        return true;
      }
      catch (Exception)
      {
        return false;
      }
    }

    public static bool HasCoveringTreePermission(ContentResolver contentResolver, Android.Net.Uri documentUri)
    {
      Android.Net.Uri treeUri;
      string documentId;
      return TryFindCoveringTreePermission(contentResolver, documentUri, out treeUri, out documentId);
    }

    /** Returns a uri for the same document but resolved through a persisted
     * tree permission, or null if no covering tree permission is available. */
    public static Android.Net.Uri TryResolveThroughTree(ContentResolver contentResolver, Android.Net.Uri documentUri)
    {
      Android.Net.Uri treeUri;
      string documentId;
      if (!TryFindCoveringTreePermission(contentResolver, documentUri, out treeUri, out documentId))
        return null;
      try
      {
        Android.Net.Uri resolved = DocumentsContract.BuildDocumentUriUsingTree(treeUri, documentId);
        Kp2aLog.Log("Resolved " + documentUri + " through tree permission as " + resolved);
        return resolved;
      }
      catch (Exception e)
      {
        Kp2aLog.Log("Failed to build tree-resolved uri for " + documentUri + ": " + e);
        return null;
      }
    }

    private static bool TryFindCoveringTreePermission(ContentResolver contentResolver, Android.Net.Uri documentUri,
        out Android.Net.Uri treeUri, out string documentId)
    {
      treeUri = null;
      documentId = null;
      if (!IsLollipopOrLater)
        return false;
      try
      {
        documentId = DocumentsContract.GetDocumentId(documentUri);
      }
      catch (Exception e)
      {
        Kp2aLog.Log("Cannot get document id of " + documentUri + ": " + e);
        return false;
      }
      if (string.IsNullOrEmpty(documentId))
        return false;
      var persistedPermissions = contentResolver.PersistedUriPermissions;
      if (persistedPermissions == null)
        return false;
      foreach (UriPermission permission in persistedPermissions)
      {
        Android.Net.Uri persistedUri = permission.Uri;
        if (persistedUri == null)
          continue;
        string treeDocumentId;
        try
        {
          treeDocumentId = DocumentsContract.GetTreeDocumentId(persistedUri);
        }
        catch (Exception)
        {
          //not a tree permission
          continue;
        }
        if (string.IsNullOrEmpty(treeDocumentId))
          continue;
        string documentAuthority = documentUri.Authority;
        string treeAuthority = persistedUri.Authority;
        if ((documentAuthority == null) || (!documentAuthority.Equals(treeAuthority, StringComparison.OrdinalIgnoreCase)))
          continue;
        if (documentId.Equals(treeDocumentId, StringComparison.Ordinal)
            || documentId.StartsWith(treeDocumentId + "/", StringComparison.Ordinal))
        {
          treeUri = persistedUri;
          return true;
        }
      }
      return false;
    }
  }

  /** FileStorage to work with content URIs
   * Supports both "old" system where data is available only temporarily as
   * well as the SAF system. Assumes that persistable permissions are "taken" by
   * the activity which receives OnActivityResult from the system file picker.*/
  public class AndroidContentStorage : IFileStorage
  {
    private readonly Context _ctx;

    public AndroidContentStorage(Context ctx)
    {
      _ctx = ctx;
    }

    public IEnumerable<string> SupportedProtocols
    {
      get { yield return "content"; }
    }

    public bool UserShouldBackup
    {
      get { return true; }
    }

    public void Delete(IOConnectionInfo ioc)
    {
      throw new NotImplementedException();
    }

    public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
    {
      return false;
    }

    public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
    {
      return null;
    }

    public Stream OpenFileForRead(IOConnectionInfo ioc)
    {
      Android.Net.Uri uri = Android.Net.Uri.Parse(ioc.Path);
      try
      {
        return _ctx.ContentResolver.OpenInputStream(uri);
      }
      catch (Exception e)
      {
        if (TreeDocumentAccess.IsAccessFailure(e))
        {
          //looks like the permission was revoked, e.g. because the document was
          //removed or replaced by another app. Try to resolve through a tree permission:
          Stream recoveredStream = TryOpenResolvedThroughTree(uri);
          if (recoveredStream != null)
            return recoveredStream;
          throw new DocumentAccessRevokedException(e.Message, e);
        }
        throw;
      }
    }

    private Stream TryOpenResolvedThroughTree(Android.Net.Uri uri)
    {
      Android.Net.Uri treeResolvedUri = TreeDocumentAccess.TryResolveThroughTree(_ctx.ContentResolver, uri);
      if (treeResolvedUri == null)
        return null;
      try
      {
        return _ctx.ContentResolver.OpenInputStream(treeResolvedUri);
      }
      catch (Exception e)
      {
        Kp2aLog.Log("Failed to open " + treeResolvedUri + ": " + e);
        return null;
      }
    }

    public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
    {
      return new AndroidContentWriteTransaction(ioc.Path, _ctx);
    }

    public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
    {
      return UrlUtil.StripExtension(
          UrlUtil.GetFileName(ioc.Path));
    }

    public string GetFileExtension(IOConnectionInfo ioc)
    {
      return UrlUtil.GetExtension(ioc.Path);
    }

    public bool RequiresCredentials(IOConnectionInfo ioc)
    {
      return false;
    }

    public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
    {
      throw new NotImplementedException();
    }

    public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
    {
      throw new NotImplementedException();
    }

    public FileDescription GetFileDescription(IOConnectionInfo ioc)
    {
      throw new NotImplementedException();
    }

    public void PrepareFileUsage(Context ctx, IOConnectionInfo ioc)
    {

    }

    public bool RequiresSetup(IOConnectionInfo ioConnection)
    {
      return false;
    }

    public string IocToPath(IOConnectionInfo ioc)
    {
      return ioc.Path;
    }

    public void StartSelectFile(IFileStorageSetupInitiatorActivity activity, bool isForSave, int requestCode, string protocolId)
    {
      Intent intent = new Intent();
      activity.IocToIntent(intent, new IOConnectionInfo { Path = protocolId + "://" });
      activity.OnImmediateResult(requestCode, (int)FileStorageResults.FileChooserPrepared, intent);
    }

    public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode,
                                 bool alwaysReturnSuccess)
    {
      Intent intent = new Intent();
      activity.IocToIntent(intent, ioc);
      activity.OnImmediateResult(requestCode, (int)FileStorageResults.FileUsagePrepared, intent);
    }

    public void OnCreate(IFileStorageSetupActivity activity, Bundle savedInstanceState)
    {
      throw new NotImplementedException();
    }

    public void OnResume(IFileStorageSetupActivity activity)
    {
      throw new NotImplementedException();
    }

    public void OnStart(IFileStorageSetupActivity activity)
    {
      throw new NotImplementedException();
    }

    public void OnActivityResult(IFileStorageSetupActivity activity, int requestCode, int resultCode, Intent data)
    {
      throw new NotImplementedException();
    }

    public string GetDisplayName(IOConnectionInfo ioc)
    {
      string displayName = null;
      if (TryGetDisplayName(ioc, ref displayName))
        return "content://" + displayName; //make sure we return the protocol in the display name for consistency, also expected e.g. by CreateDatabaseActivity
      return ioc.Path;
    }

    private bool TryGetDisplayName(IOConnectionInfo ioc, ref string displayName)
    {
      var uri = Android.Net.Uri.Parse(ioc.Path);
      if (TryQueryDisplayName(uri, ref displayName))
        return true;
      //if the uri is not accessible, e.g. after the document was replaced by another
      //app, try to resolve it through a tree permission:
      Android.Net.Uri treeResolvedUri = TreeDocumentAccess.TryResolveThroughTree(_ctx.ContentResolver, uri);
      if ((treeResolvedUri != null) && (!treeResolvedUri.ToString().Equals(uri.ToString())))
        return TryQueryDisplayName(treeResolvedUri, ref displayName);
      return false;
    }

    private bool TryQueryDisplayName(Android.Net.Uri uri, ref string displayName)
    {
      try
      {
        var cursor = _ctx.ContentResolver.Query(uri, null, null, null, null, null);
        try
        {

          if (cursor != null && cursor.MoveToFirst())
          {
            displayName = cursor.GetString(cursor.GetColumnIndex(OpenableColumns.DisplayName));
            if (!string.IsNullOrEmpty(displayName))
            {
              return true;
            }

          }
        }
        finally
        {
          if (cursor != null)
            cursor.Close();
        }

        return false;
      }
      catch (Exception e)
      {
        Kp2aLog.Log(e.ToString());

        return false;
      }
    }

    public string CreateFilePath(string parent, string newFilename)
    {
      if (!parent.EndsWith("/"))
        parent += "/";
      return parent + newFilename;
    }

    public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
    {
      return IoUtil.GetParentPath(ioc);
    }

    public IOConnectionInfo GetFilePath(IOConnectionInfo folderPath, string filename)
    {
      throw new NotImplementedException();
    }

    private static bool IsKitKatOrLater
    {
      get { return (int)Build.VERSION.SdkInt >= 19; }
    }

    public bool IsPermanentLocation(IOConnectionInfo ioc)
    {
      //on pre-Kitkat devices, content:// is always temporary:
      if (!IsKitKatOrLater)
        return false;

      //try to get a persisted permission for the file
      if (_ctx.ContentResolver.PersistedUriPermissions.Any(p => (p.Uri != null) && p.Uri.ToString().Equals(ioc.Path)))
        return true;

      //a persisted tree permission for a containing folder also makes the location permanent.
      //In contrast to a document permission, it survives replacement of the file by other apps.
      return TreeDocumentAccess.HasCoveringTreePermission(_ctx.ContentResolver, Android.Net.Uri.Parse(ioc.Path));
    }


    public bool IsReadOnly(IOConnectionInfo ioc, OptionalOut<UiStringKey> reason = null)
    {
      ICursor cursor = null;
      try
      {
        //on pre-Kitkat devices, we can't write content:// files
        if (!IsKitKatOrLater)
        {
          Kp2aLog.Log("File is read-only because we're not on KitKat or later.");
          if (reason != null)
            reason.Result = UiStringKey.ReadOnlyReason_PreKitKat;
          return true;
        }

        //in previous implementations, we were checking for FLAG_SUPPORTS_WRITE in the document flags,
        //but it seems like this is very poorly supported, e.g. Dropbox and OneDrive return !FLAG_SUPPORTS_WRITE
        //even though writing work.
        return false;
      }
      catch (Exception e)
      {
        Kp2aLog.LogUnexpectedError(e);
        //better return false here. We don't really know what happened (as this is unexpected).
        //let the user try to write the file. If it fails they will get an exception string.
        return false;
      }
      finally
      {
        if (cursor != null)
          cursor.Close();
      }

    }

  }

  public class DocumentAccessRevokedException : Exception
  {
    public DocumentAccessRevokedException()
    {
    }

    public DocumentAccessRevokedException(string message) : base(message)
    {
    }

    public DocumentAccessRevokedException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected DocumentAccessRevokedException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
  }

  class AndroidContentWriteTransaction : IWriteTransaction
  {
    private readonly string _path;
    private readonly Context _ctx;
    private MemoryStream _memoryStream;

    public AndroidContentWriteTransaction(string path, Context ctx)
    {
      _path = path;
      _ctx = ctx;
    }

    public void Dispose()
    {
      _memoryStream.Dispose();
    }

    public Stream OpenFile()
    {
      _memoryStream = new MemoryStream();
      return _memoryStream;
    }

    public void CommitWrite()
    {
      Android.Net.Uri uri = Android.Net.Uri.Parse(_path);
      try
      {
        WriteTo(uri);
        return;
      }
      catch (Exception e)
      {
        if (!TreeDocumentAccess.IsAccessFailure(e))
          throw;
        //access to the document uri seems to be revoked, e.g. because another
        //app replaced the file. Try to resolve through a tree permission:
        Android.Net.Uri treeResolvedUri = TreeDocumentAccess.TryResolveThroughTree(_ctx.ContentResolver, uri);
        if (treeResolvedUri == null)
          throw new DocumentAccessRevokedException(e.Message, e);
        WriteTo(treeResolvedUri);
      }
    }

    private void WriteTo(Android.Net.Uri uri)
    {
      ParcelFileDescriptor fileDescriptor = _ctx.ContentResolver.OpenFileDescriptor(uri, "rwt");

      using (var outputStream = new FileOutputStream(fileDescriptor.FileDescriptor))
      {
        byte[] data = _memoryStream.ToArray();

        outputStream.Write(data);
        outputStream.Close();
      }
      fileDescriptor.Close();
    }
  }


}