using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
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
			activity.IocToIntent(intent, new IOConnectionInfo() { Path = protocolId + "://" });
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
			return ioc.Path;
		}

		public string CreateFilePath(string parent, string newFilename)
		{
			throw new NotImplementedException();
		}

		public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public IOConnectionInfo GetFilePath(IOConnectionInfo folderPath, string filename)
		{
			throw new NotImplementedException();
		}

		public bool IsPermanentLocation(IOConnectionInfo ioc)
		{
			//on pre-Kitkat devices, content:// is always temporary:
			return false;
		}

		public bool IsReadOnly(IOConnectionInfo ioc)
		{
			//on pre-Kitkat devices, we can't write content:// files
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
				outputStream.Write(_memoryStream.ToArray(), 0, (int)_memoryStream.Length);
			}
			
			
		}
	}

	
}