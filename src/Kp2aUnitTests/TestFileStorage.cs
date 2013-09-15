using System;
using System.Collections.Generic;
using System.IO;
using KeePassLib.Serialization;
using keepass2android.Io;

namespace Kp2aUnitTests
{
	internal class TestFileStorage: IFileStorage
	{
		private BuiltInFileStorage _builtIn = new BuiltInFileStorage();

		public bool Offline { get; set; }

		public IEnumerable<string> SupportedProtocols { get { yield return "test"; } }

		public void DeleteFile(IOConnectionInfo ioc)
		{
			if (Offline)
				throw new IOException("offline");
			_builtIn.DeleteFile(ioc);
		}

		public IFileStorageSetup RequiredSetup { get { return null; } }

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

		public bool CompleteIoId()
		{
			throw new NotImplementedException();
		}

		public bool? FileExists()
		{
			throw new NotImplementedException();
		}

		public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
		{
			return _builtIn.GetFilenameWithoutPathAndExt(ioc);
		}

		public bool RequiresCredentials(IOConnectionInfo ioc)
		{
			return _builtIn.RequiresCredentials(ioc);
		}
	}
}