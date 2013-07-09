using System;
using System.Collections.Generic;
using System.Globalization;
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
using KeePassLib.Utility;

namespace keepass2android.Io
{
	public class BuiltInFileStorage: IFileStorage
	{
		public void DeleteFile(IOConnectionInfo ioc)
		{
			IOConnection.DeleteFile(ioc);
		}

		public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
		{
			if (!ioc.IsLocalFile())
				return false;
			DateTime previousDate;
			if (!DateTime.TryParse(previousFileVersion, out previousDate))
				return false;
			return File.GetLastWriteTimeUtc(ioc.Path) > previousDate;
		}

		
		public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
		{

			if (ioc.IsLocalFile())
			{
				return File.GetLastWriteTimeUtc(ioc.Path).ToString(CultureInfo.InvariantCulture);
			}
			else
			{
				return DateTime.MinValue.ToString(CultureInfo.InvariantCulture);
			}


		}

		public Stream OpenFileForRead(IOConnectionInfo ioc)
		{
			return IOConnection.OpenRead(ioc);
		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			return new BuiltInFileTransaction(ioc, useFileTransaction);
		}

		public class BuiltInFileTransaction : IWriteTransaction
		{
			private readonly FileTransactionEx _transaction;

			public BuiltInFileTransaction(IOConnectionInfo ioc, bool useFileTransaction)
			{
				_transaction = new FileTransactionEx(ioc, useFileTransaction);
			}

			public void Dispose()
			{
				
			}

			public Stream OpenFile()
			{
				return _transaction.OpenWrite();
			}

			public void CommitWrite()
			{
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
			return UrlUtil.StripExtension(
				UrlUtil.GetFileName(ioc.Path));

		}
	}
}