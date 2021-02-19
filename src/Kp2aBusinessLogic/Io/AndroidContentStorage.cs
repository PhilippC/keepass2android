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
	/** FileStorage to work with content URIs
	 * Supports both "old" system where data is available only temporarily as
	 * well as the SAF system. Assumes that persistable permissions are "taken" by
	 * the activity which receives OnActivityResult from the system file picker.*/
	public class AndroidContentStorage: IFileStorage
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
		    try
            { 
		        return _ctx.ContentResolver.OpenInputStream(Android.Net.Uri.Parse(ioc.Path));
		    }
		    catch (Exception e)
		    {
		        if (e.Message.Contains("requires that you obtain access using ACTION_OPEN_DOCUMENT"))
		        {
		            //looks like permission was revoked.
		            throw new DocumentAccessRevokedException();
		        }
		        throw;
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
			return _ctx.ContentResolver.PersistedUriPermissions.Any(p => p.Uri.ToString().Equals(ioc.Path));
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
		    ParcelFileDescriptor fileDescriptor = _ctx.ContentResolver.OpenFileDescriptor(Android.Net.Uri.Parse(_path), "rwt");
            
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