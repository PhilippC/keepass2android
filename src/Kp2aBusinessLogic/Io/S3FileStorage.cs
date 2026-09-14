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

#if !NoNet
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Android.Content;
using Android.OS;
using KeePass.Util;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace keepass2android.Io
{
  /// <summary>
  /// IFileStorage implementation for Amazon S3 and S3-compatible object stores
  /// (Wasabi, Backblaze B2, Cloudflare R2, MinIO, ...).
  /// </summary>
  /// <remarks>
  /// Like the FTP/SMB storages, all connection data (provider, region/endpoint,
  /// bucket, access key, secret key) is encoded into the IOConnectionInfo.Path so
  /// the recent-files store works without extra plumbing. The path format is:
  /// <code>
  /// s3://SET&lt;accessKey&gt;:&lt;secret&gt;:&lt;provider&gt;:&lt;region&gt;:&lt;endpointOrAccount&gt;#&lt;bucket&gt;/&lt;objectKey&gt;
  /// </code>
  /// The five settings tokens after the "SET" marker are each URL-encoded so that a
  /// ':' '#' or '/' inside a value cannot break parsing; the first '#' separates the
  /// (encoded) settings segment from the raw "&lt;bucket&gt;/&lt;objectKey&gt;" location.
  /// Because the path embeds the secret key, never log or display it raw (see
  /// <see cref="GetDisplayName"/> for the redacted form shown to the user).
  /// </remarks>
  public class S3FileStorage : IFileStorage
  {
    public const string ProtocolId = "s3";

    /// <summary>
    /// Holds the credentials and endpoint configuration for an S3 connection. The actual
    /// encode/decode of the path format lives in the platform-agnostic, unit-tested
    /// <see cref="S3PathCodec"/>; this is a thin Android-side wrapper mapping to/from an
    /// <see cref="IOConnectionInfo"/>.
    /// </summary>
    public struct ConnectionSettings
    {
      public string AccessKey { get; set; }
      public string SecretKey { get; set; }
      public S3Provider Provider { get; set; }

      /// <summary>region (AWS/Wasabi/B2). For R2 it is unused ("auto"); for Custom it is the optional SigV4 signing region.</summary>
      public string Region { get; set; }

      /// <summary>custom endpoint URL (Custom/MinIO) or the account id (Cloudflare R2). Empty otherwise.</summary>
      public string EndpointOrAccount { get; set; }

      public static ConnectionSettings FromIoc(IOConnectionInfo ioc)
      {
        S3PathCodec.ParsePath(ioc.Path, out string settings, out _, out _);
        S3PathCodec.ParseSettings(settings, out string accessKey, out string secretKey,
            out S3Provider provider, out string region, out string endpointOrAccount);
        return new ConnectionSettings()
        {
          AccessKey = accessKey,
          SecretKey = secretKey,
          Provider = provider,
          Region = region,
          EndpointOrAccount = endpointOrAccount
        };
      }

      /// <summary>
      /// Serializes into the settings segment of an s3:// path. Deliberately NOT a ToString()
      /// override: the output contains the secret key, and we don't want an accidental log to leak it.
      /// </summary>
      public string Serialize()
      {
        return S3PathCodec.SerializeSettings(AccessKey, SecretKey, Provider, Region, EndpointOrAccount);
      }
    }

    private readonly ICertificateValidationHandler _app;

    /// <summary>
    /// Last known ETag per "bucket/key", captured on read and refreshed on write,
    /// used for conditional (If-Match) writes to avoid clobbering concurrent updates.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _lastKnownETags = new();

    public S3FileStorage(Context context, ICertificateValidationHandler app)
    {
      _app = app;
    }

    public IEnumerable<string> SupportedProtocols
    {
      get { yield return ProtocolId; }
    }

    public bool UserShouldBackup
    {
      get { return true; }
    }

    #region path <-> bucket/key helpers

    /// <summary>
    /// Exposes the bucket and (fully qualified) object key. Also used to prefill the
    /// credentials dialog when editing an existing connection. Parsing lives in
    /// <see cref="S3PathCodec"/>.
    /// </summary>
    public static void GetBucketAndObjectKey(IOConnectionInfo ioc, out string bucket, out string objectKey)
    {
      S3PathCodec.ParsePath(ioc.Path, out _, out bucket, out objectKey);
    }

    private static string ETagCacheKey(string bucket, string key)
    {
      return bucket + "/" + key;
    }

    #endregion

    private AmazonS3Client GetClient(IOConnectionInfo ioc)
    {
      var settings = ConnectionSettings.FromIoc(ioc);
      var config = new AmazonS3Config();
      switch (settings.Provider)
      {
        case S3Provider.Aws:
          config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region);
          break;
        case S3Provider.Wasabi:
          config.ServiceURL = "https://s3." + settings.Region + ".wasabisys.com";
          config.AuthenticationRegion = settings.Region;
          break;
        case S3Provider.BackblazeB2:
          config.ServiceURL = "https://s3." + settings.Region + ".backblazeb2.com";
          config.AuthenticationRegion = settings.Region;
          break;
        case S3Provider.CloudflareR2:
          config.ServiceURL = "https://" + settings.EndpointOrAccount + ".r2.cloudflarestorage.com";
          config.AuthenticationRegion = "auto";
          break;
        case S3Provider.Custom:
          config.ServiceURL = settings.EndpointOrAccount;
          config.ForcePathStyle = true;
          if (!string.IsNullOrEmpty(settings.Region))
            config.AuthenticationRegion = settings.Region;
          break;
      }

      return new AmazonS3Client(new BasicAWSCredentials(settings.AccessKey, settings.SecretKey), config);
    }

    /// <summary>
    /// Maps an AmazonS3Exception from a read/list/delete call to a clearer, user-facing
    /// exception (these messages can be shown to the user, so they avoid raw AWS text):
    /// 404 -> FileNotFoundException; 403 -> an explanation of the missing-vs-forbidden
    /// ambiguity that arises when the policy omits s3:ListBucket. The write path handles its
    /// own 403 in <see cref="UploadFile"/>, where the actionable permission is s3:PutObject.
    /// </summary>
    private static Exception ConvertException(Exception exception)
    {
      if (exception is AmazonS3Exception s3Ex)
      {
        if (s3Ex.StatusCode == HttpStatusCode.NotFound)
          return new FileNotFoundException("The database was not found at this S3 location.", exception);

        //A 403 here is ambiguous: when the IAM policy omits s3:ListBucket (recommended least
        //privilege), S3 returns "403 AccessDenied" both for an object that does not exist and
        //for one the key may not read. We surface a clear message rather than the raw
        //"not authorized: s3:ListBucket" text, which would otherwise mislead the user into
        //granting s3:ListBucket.
        if (s3Ex.StatusCode == HttpStatusCode.Forbidden)
          return new IOException(
              "Access denied, or the database does not exist. The access key may not be allowed to read this " +
              "object (s3:GetObject), or the object key may be wrong. Note: without s3:ListBucket, S3 reports a " +
              "missing object and a forbidden object identically, so check the object key first.", exception);
      }
      return exception;
    }

    public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
    {
      if (string.IsNullOrEmpty(previousFileVersion))
        return false;
      string? current = GetCurrentFileVersionFast(ioc);
      //treat a deleted/unreadable file as "changed"
      return current != previousFileVersion;
    }

    public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
    {
      try
      {
        GetBucketAndObjectKey(ioc, out string bucket, out string key);
        using (var client = GetClient(ioc))
        {
          var meta = client.GetObjectMetadataAsync(bucket, key).GetAwaiter().GetResult();
          _lastKnownETags[ETagCacheKey(bucket, key)] = meta.ETag;
          return meta.ETag;
        }
      }
      catch (Exception)
      {
        return null;
      }
    }

    public Stream OpenFileForRead(IOConnectionInfo ioc)
    {
      try
      {
        GetBucketAndObjectKey(ioc, out string bucket, out string key);
        using (var client = GetClient(ioc))
        using (var response = client.GetObjectAsync(bucket, key).GetAwaiter().GetResult())
        {
          var memStream = new MemoryStream();
          response.ResponseStream.CopyTo(memStream);
          memStream.Seek(0, SeekOrigin.Begin);
          _lastKnownETags[ETagCacheKey(bucket, key)] = response.ETag;
          return memStream;
        }
      }
      catch (AmazonS3Exception ex)
      {
        throw ConvertException(ex);
      }
    }

    public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
    {
      //S3 PutObject is atomic per object, so we never need a temp-file/rename transaction.
      return new S3WriteTransaction(ioc, this);
    }

    /// <summary>
    /// Uploads the given bytes to the object referenced by ioc. When the base ETag is known
    /// (captured on the preceding read), the write is conditional (If-Match), so a concurrent
    /// server-side change is detected (412 -&gt; "changed on the server") instead of being
    /// silently overwritten.
    /// Gotcha: if the provider does not support conditional writes it returns 501 and we retry
    /// WITHOUT the precondition — in that (rare) case the concurrent-overwrite protection is
    /// lost and a racing change could be clobbered. Amazon S3 and the major S3-compatibles
    /// support If-Match, so this only affects older/limited servers.
    /// </summary>
    internal void UploadFile(IOConnectionInfo ioc, byte[] content)
    {
      GetBucketAndObjectKey(ioc, out string bucket, out string key);
      string cacheKey = ETagCacheKey(bucket, key);
      _lastKnownETags.TryGetValue(cacheKey, out string? lastKnownETag);

      using (var client = GetClient(ioc))
      {
        PutObjectResponse response;
        try
        {
          response = PutObject(client, bucket, key, content, lastKnownETag);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotImplemented && lastKnownETag != null)
        {
          //provider does not support conditional writes: retry without the precondition
          //(this drops the concurrent-overwrite protection — see the method summary)
          response = PutObject(client, bucket, key, content, null);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
          throw new IOException("The file was changed on the server since it was loaded.", ex);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
          //write-specific message: the actionable permission on a save is s3:PutObject
          //(the shared ConvertException talks about s3:GetObject, which would mislead here)
          throw new IOException(
              "Could not save: access denied. The access key may not be allowed to write this object " +
              "(s3:PutObject), or the bucket / object key may be wrong.", ex);
        }
        _lastKnownETags[cacheKey] = response.ETag;
      }
    }

    private static PutObjectResponse PutObject(AmazonS3Client client, string bucket, string key, byte[] content, string? lastKnownETag)
    {
      var request = new PutObjectRequest
      {
        BucketName = bucket,
        Key = key,
        InputStream = new MemoryStream(content)
      };
      //conditional write: only overwrite if the object still matches the ETag we last saw
      if (!string.IsNullOrEmpty(lastKnownETag))
        request.IfMatch = lastKnownETag;
      return client.PutObjectAsync(request).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Deletes the single object referenced by ioc. This is an <see cref="IFileStorage"/>
    /// contract method; KP2A calls it for its own housekeeping (e.g. removing a local
    /// cached/backup copy, or cleaning up the just-created object when a "create database"
    /// save fails — see CreateDatabaseActivity), not as a user-facing "delete my database"
    /// action. It only ever targets the exact object key, never a prefix/bucket.
    /// </summary>
    public void Delete(IOConnectionInfo ioc)
    {
      try
      {
        GetBucketAndObjectKey(ioc, out string bucket, out string key);
        using (var client = GetClient(ioc))
        {
          client.DeleteObjectAsync(bucket, key).GetAwaiter().GetResult();
        }
      }
      catch (AmazonS3Exception ex)
      {
        throw ConvertException(ex);
      }
    }

    public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
    {
      return UrlUtil.StripExtension(UrlUtil.GetFileName(GetObjectKeyForName(ioc)));
    }

    public string GetFileExtension(IOConnectionInfo ioc)
    {
      return UrlUtil.GetExtension(GetObjectKeyForName(ioc));
    }

    private static string GetObjectKeyForName(IOConnectionInfo ioc)
    {
      GetBucketAndObjectKey(ioc, out _, out string key);
      //a "folder" key ends in '/'; trim it so GetFileName/StripExtension see the last segment
      return key.TrimEnd('/');
    }

    public bool RequiresCredentials(IOConnectionInfo ioc)
    {
      return false;
    }

    public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
    {
      //No-op: S3 has no real directories (a "folder" is just a key prefix). The direct-object
      //flow never browses or creates folders, so there is nothing to do here. Creating a
      //zero-byte marker object would also require an extra PutObject the user didn't ask for.
    }

    /// <summary>
    /// Not supported by the S3 backend. The backend opens a fully qualified object directly and
    /// never browses, which is precisely what lets a least-privilege user skip s3:ListBucket.
    /// Browsing/listing is used by other storage backends through the file chooser
    /// (FileChooserFileProvider.ListContents), but that flow never reaches S3, so rather than
    /// keep a dead, ListBucket-requiring code path we fail loudly here.
    /// </summary>
    public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
    {
      throw new NotSupportedException(
          "Browsing is not supported for S3; open the database object directly. " +
          "(Listing would require the s3:ListBucket permission, which this backend avoids.)");
    }

    public FileDescription GetFileDescription(IOConnectionInfo ioc)
    {
      try
      {
        GetBucketAndObjectKey(ioc, out string bucket, out string key);
        using (var client = GetClient(ioc))
        {
          var meta = client.GetObjectMetadataAsync(bucket, key).GetAwaiter().GetResult();
          return new FileDescription()
          {
            CanRead = true,
            CanWrite = true,
            Path = ioc.Path,
            LastModified = meta.LastModified,
            SizeInBytes = meta.ContentLength,
            //trim a trailing '/' so a "folder"-style key yields its last segment as the display name
            DisplayName = UrlUtil.GetFileName(key.TrimEnd('/')),
            //in S3 a key ending in '/' is still a regular object, not a directory; the backend
            //never represents directories, so this is always a file
            IsDirectory = false
          };
        }
      }
      catch (AmazonS3Exception ex)
      {
        throw ConvertException(ex);
      }
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
      activity.PerformManualFileSelect(isForSave, requestCode, ProtocolId);
    }

    public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode,
        bool alwaysReturnSuccess)
    {
      Intent intent = new Intent();
      activity.IocToIntent(intent, ioc);
      activity.OnImmediateResult(requestCode, (int)FileStorageResults.FileUsagePrepared, intent);
    }

    public void PrepareFileUsage(Context ctx, IOConnectionInfo ioc)
    {
    }

    public void OnCreate(IFileStorageSetupActivity activity, Bundle savedInstanceState)
    {
    }

    public void OnResume(IFileStorageSetupActivity activity)
    {
    }

    public void OnStart(IFileStorageSetupActivity activity)
    {
    }

    public void OnActivityResult(IFileStorageSetupActivity activity, int requestCode, int resultCode, Intent data)
    {
    }

    /// <summary>
    /// Redacted, user-facing label for the connection: "s3://bucket/key (Provider)".
    /// Deliberately contains only the bucket, object key and provider — NOT the access/secret
    /// key — so it is safe to show or log (unlike the raw ioc.Path, which embeds the secret).
    /// </summary>
    public string GetDisplayName(IOConnectionInfo ioc)
    {
      var settings = ConnectionSettings.FromIoc(ioc);
      GetBucketAndObjectKey(ioc, out string bucket, out string key);
      return ProtocolId + "://" + bucket + "/" + key + " (" + settings.Provider + ")";
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
      IOConnectionInfo res = folderPath.CloneDeep();
      if (!res.Path.EndsWith("/"))
        res.Path += "/";
      res.Path += filename;
      return res;
    }

    public bool IsPermanentLocation(IOConnectionInfo ioc)
    {
      return true;
    }

    public bool IsReadOnly(IOConnectionInfo ioc, OptionalOut<UiStringKey> reason = null)
    {
      return false;
    }
  }

  /// <summary>
  /// Buffers the database in memory and uploads it with a single (atomic) PutObject on commit.
  /// Not thread-safe and not intended to be: like the other IWriteTransaction implementations,
  /// a transaction is created, written and committed by a single save operation on one thread.
  /// The only shared state across operations is S3FileStorage._lastKnownETags, which is a
  /// ConcurrentDictionary.
  /// </summary>
  public class S3WriteTransaction : IWriteTransaction
  {
    private readonly IOConnectionInfo _ioc;
    private readonly S3FileStorage _fileStorage;
    private MemoryStream _stream;

    public S3WriteTransaction(IOConnectionInfo ioc, S3FileStorage fileStorage)
    {
      _ioc = ioc;
      _fileStorage = fileStorage;
    }

    public void Dispose()
    {
      if (_stream != null)
        _stream.Dispose();
      _stream = null;
    }

    public Stream OpenFile()
    {
      _stream = new MemoryStream();
      return _stream;
    }

    public void CommitWrite()
    {
      //MemoryStream.ToArray() is valid even after the stream has been closed.
      _fileStorage.UploadFile(_ioc, _stream.ToArray());
    }
  }
}
#endif
