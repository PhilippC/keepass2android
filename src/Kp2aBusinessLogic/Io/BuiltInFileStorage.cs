using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using Android.Content;
using Android.OS;
using Java.Security.Cert;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace keepass2android.Io
{
	public class BuiltInFileStorage: IFileStorage
	{
		public enum CertificateProblem :long
		{
			CertEXPIRED = 0x800B0101,
			CertVALIDITYPERIODNESTING = 0x800B0102,
			CertROLE = 0x800B0103,
			CertPATHLENCONST = 0x800B0104,
			CertCRITICAL = 0x800B0105,
			CertPURPOSE = 0x800B0106,
			CertISSUERCHAINING = 0x800B0107,
			CertMALFORMED = 0x800B0108,
			CertUNTRUSTEDROOT = 0x800B0109,
			CertCHAINING = 0x800B010A,
			CertREVOKED = 0x800B010C,
			CertUNTRUSTEDTESTROOT = 0x800B010D,
			CertREVOCATION_FAILURE = 0x800B010E,
			CertCN_NO_MATCH = 0x800B010F,
			CertWRONG_USAGE = 0x800B0110,
			CertUNTRUSTEDCA = 0x800B0112
		}


		private readonly IKp2aApp _app;

		class CertificatePolicity: ICertificatePolicy 
		{
			private readonly IKp2aApp _app;

			public CertificatePolicity(IKp2aApp app)
			{
				_app = app;
			}

			public bool CheckValidationResult(ServicePoint srvPoint, System.Security.Cryptography.X509Certificates.X509Certificate certificate, WebRequest request,
			                                  int certificateProblem)
			{
				if (certificateProblem == 0) //ok
					return true;
				return _app.OnServerCertificateError(certificateProblem);
			}
		}


		public BuiltInFileStorage(IKp2aApp app)
		{
			_app = app;
			//use the obsolute CertificatePolicy because the ServerCertificateValidationCallback isn't called in Mono for Android (?)
			ServicePointManager.CertificatePolicy = new CertificatePolicity(app);
			
		}

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
				ConvertException(ioc, ex);
				throw;
			}
		}

		private void ConvertException(IOConnectionInfo ioc, WebException ex)
		{
			if ((ex.Response is HttpWebResponse) && (((HttpWebResponse) ex.Response).StatusCode == HttpStatusCode.NotFound))
			{
				throw new FileNotFoundException(ex.Message, ioc.Path, ex);
			}
			if (ex.Status == WebExceptionStatus.TrustFailure)
			{
				throw new Exception(_app.GetResourceString(UiStringKey.CertificateFailure), ex);
			}
			var inner1 = ex.InnerException as IOException;
			if (inner1 != null)
			{
				var inner2 = inner1.InnerException;
				if (inner2 != null)
				{
					if (inner2.Message.Contains("Invalid certificate received from server."))
					{
						throw new Exception(_app.GetResourceString(UiStringKey.CertificateFailure), ex);
					}
				}
				 
			}
		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			return new BuiltInFileTransaction(ioc, useFileTransaction, this);
		}

		public class BuiltInFileTransaction : IWriteTransaction
		{
			private readonly IOConnectionInfo _ioc;
			private readonly BuiltInFileStorage _fileStorage;
			private readonly FileTransactionEx _transaction;

			public BuiltInFileTransaction(IOConnectionInfo ioc, bool useFileTransaction, BuiltInFileStorage fileStorage)
			{
				_ioc = ioc;
				_fileStorage = fileStorage;
				_transaction = new FileTransactionEx(ioc, useFileTransaction);
			}

			public void Dispose()
			{
				
			}

			public Stream OpenFile()
			{
				try
				{
					return _transaction.OpenWrite();
				}
				catch (WebException ex)
				{
					_fileStorage.ConvertException(_ioc, ex);
					throw;
				}
				
			}

			public void CommitWrite()
			{
				try
				{
					_transaction.CommitWrite();
				}
				catch (WebException ex)
				{
					_fileStorage.ConvertException(_ioc, ex);
					throw;
				}
				
			}
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

		public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
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

		public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode, bool alwaysReturnSuccess)
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

		public string GetDisplayName(IOConnectionInfo ioc)
		{
			return ioc.GetDisplayName();
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
	}
}