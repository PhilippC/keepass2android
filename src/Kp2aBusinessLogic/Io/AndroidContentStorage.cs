using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Android.Content;
using Android.OS;
using Android.Provider;
using KeePassLib.Serialization;

namespace keepass2android.Io
{
	//TODOC,TOTEST, TODO: unimplemented methods?
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
			return _ctx.ContentResolver.OpenInputStream(Android.Net.Uri.Parse(ioc.Path));
		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			return new AndroidContentWriteTransaction(ioc.Path, _ctx);
		}

		public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
		{
			return "";
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

		public string CreateFilePath(string parent, string newFilename)
		{
			if (!parent.EndsWith("/"))
				parent += "/";
			return parent + newFilename;
		}

		public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
		{
			//TODO: required for OTP Aux file retrieval
			throw new NotImplementedException();
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

		public bool IsReadOnly(IOConnectionInfo ioc)
		{
			//on pre-Kitkat devices, we can't write content:// files
			if (!IsKitKatOrLater)
			{
				Kp2aLog.Log("File is read-only because we're not on KitKat or later.");
				return true;
			}
				

			//KitKat or later...
			var uri = Android.Net.Uri.Parse(ioc.Path);
			var cursor = _ctx.ContentResolver.Query(uri, null, null, null, null, null);

			try
			{
				if (cursor != null && cursor.MoveToFirst())
				{
					int flags = cursor.GetInt(cursor.GetColumnIndex(DocumentsContract.Document.ColumnFlags));
					Kp2aLog.Log("File flags: " + flags);
					return (flags & (long) DocumentContractFlags.SupportsWrite) == 0;
				}
			}
			finally
			{
				if (cursor != null)
					cursor.Close();
			}
			return true;
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
			using (Stream outputStream = _ctx.ContentResolver.OpenOutputStream(Android.Net.Uri.Parse(_path)))
			{
				byte[] data = _memoryStream.ToArray();
				outputStream.Write(data, 0, data.Length);
			}
			
			
		}
	}

	
}