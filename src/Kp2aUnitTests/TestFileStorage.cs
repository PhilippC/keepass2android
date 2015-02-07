using System;
using System.Collections.Generic;
using System.IO;
using Android.Content;
using Android.OS;
using KeePassLib.Serialization;
using keepass2android;
using keepass2android.Io;

namespace Kp2aUnitTests
{
	internal class TestFileStorage: IFileStorage
	{
		public TestFileStorage(IKp2aApp app)
		{
			_builtIn = new BuiltInFileStorage(app);
		}
		private BuiltInFileStorage _builtIn;

		public static bool Offline { get; set; }

		public IEnumerable<string> SupportedProtocols { get { yield return "test"; } }

		public void DeleteFile(IOConnectionInfo ioc)
		{
			if (Offline)
				throw new IOException("offline");
			_builtIn.Delete(ioc);
		}

		public void Delete(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
		{
			if (Offline)
				return false;
			return _builtIn.CheckForFileChangeFast(ioc, previousFileVersion);
		}

		public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
		{
			if (Offline)
				throw new IOException("offline");
			return _builtIn.GetCurrentFileVersionFast(ioc);
		}

		public Stream OpenFileForRead(IOConnectionInfo ioc)
		{
			if (Offline)
				throw new IOException("offline");
			return _builtIn.OpenFileForRead(ioc);
		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			if (Offline)
				throw new IOException("offline");
			return new TestFileTransaction(ioc, useFileTransaction, Offline);
		}

		public class TestFileTransaction : IWriteTransaction
		{
			private readonly bool _offline;
			private readonly FileTransactionEx _transaction;

			public TestFileTransaction(IOConnectionInfo ioc, bool useFileTransaction, bool offline)
			{
				_offline = offline;
				_transaction = new FileTransactionEx(ioc, useFileTransaction);
			}

			public void Dispose()
			{

			}

			public Stream OpenFile()
			{
				if (_offline)
					throw new IOException("offline");
				return _transaction.OpenWrite();
			}

			public void CommitWrite()
			{
				if (_offline)
					throw new IOException("offline");
				_transaction.CommitWrite();
			}
		}


		public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
		{
			return _builtIn.GetFilenameWithoutPathAndExt(ioc);
		}

		public bool RequiresCredentials(IOConnectionInfo ioc)
		{
			return _builtIn.RequiresCredentials(ioc);
		}

		public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
		{
			throw new NotImplementedException();
		}

		public void CreateDirectory(IOConnectionInfo ioc)
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
			throw new NotImplementedException();
		}

		public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode,
		                             bool alwaysReturnSuccess)
		{
			throw new NotImplementedException();
		}

		public void PrepareFileUsage(Context ctx, IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
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
			return true;	
		}

		public bool IsReadOnly(IOConnectionInfo ioc)
		{
			return false;
		}
	}
}