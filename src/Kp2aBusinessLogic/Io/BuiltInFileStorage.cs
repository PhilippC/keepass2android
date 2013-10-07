using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using Android.Content;
using Android.OS;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace keepass2android.Io
{
	public class BuiltInFileStorage: IFileStorage
	{
		public IEnumerable<string> SupportedProtocols 
		{ 
			get 
			{
				yield return "file";
				yield return "ftp";
				yield return "http";
				yield return "https";
			}
		}

		public void Delete(IOConnectionInfo ioc)
		{
			//todo check if directory
			IOConnection.DeleteFile(ioc);
		}
		public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
		{
			if (!ioc.IsLocalFile())
				return false;
			if (previousFileVersion == null)
				return false;
			DateTime previousDate;
			if (!DateTime.TryParse(previousFileVersion, CultureInfo.InvariantCulture, DateTimeStyles.None, out previousDate))
				return false;
			DateTime currentModificationDate = File.GetLastWriteTimeUtc(ioc.Path);
			TimeSpan diff = currentModificationDate - previousDate;
			return diff > TimeSpan.FromSeconds(1);
			//don't use > operator because milliseconds are truncated
			//return File.GetLastWriteTimeUtc(ioc.Path) - previousDate >= TimeSpan.FromSeconds(1);
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
			try
			{
				return IOConnection.OpenRead(ioc);
			}
			catch (WebException ex)
			{
				if ((ex.Response is HttpWebResponse) && (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound))
				{
					throw new FileNotFoundException(ex.Message, ioc.Path, ex);
				}
				throw;
			}
			
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

		public bool RequiresCredentials(IOConnectionInfo ioc)
		{
			return (!ioc.IsLocalFile()) && (ioc.CredSaveMode != IOCredSaveMode.SaveCred);
		}

		public void CreateDirectory(IOConnectionInfo ioc)
		{
			//TODO
			throw new NotImplementedException();
		}

		public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
		{
			//TODO
			throw new NotImplementedException();
		}

		public FileDescription GetFileDescription(IOConnectionInfo ioc)
		{
			//TODO
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
			if (protocolId != "file")
				activity.PerformManualFileSelect(isForSave, requestCode, protocolId);
			else
			{
				Intent intent = new Intent();
				activity.IocToIntent(intent, new IOConnectionInfo() { Path = protocolId+"://"});
				activity.OnImmediateResult(requestCode, (int) FileStorageResults.FileChooserPrepared, intent);
			}
		}

		public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode)
		{
			Intent intent = new Intent();
			activity.IocToIntent(intent, ioc);
			activity.OnImmediateResult(requestCode, (int) FileStorageResults.FileUsagePrepared, intent);
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
	}
}