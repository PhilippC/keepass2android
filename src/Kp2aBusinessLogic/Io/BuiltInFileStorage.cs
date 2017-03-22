using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;

using System.Security;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Java.IO;
using KeePassLib.Serialization;
using KeePassLib.Utility;

using File = System.IO.File;
using FileNotFoundException = System.IO.FileNotFoundException;
using IOException = System.IO.IOException;

namespace keepass2android.Io
{
	public abstract class BuiltInFileStorage : IFileStorage, IPermissionRequestingFileStorage
	{
		private const string PermissionGrantedKey = "PermissionGranted";

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

		public BuiltInFileStorage(IKp2aApp app)
		{
			_app = app;
			//use the obsolute CertificatePolicy because the ServerCertificateValidationCallback isn't called in Mono for Android (?)
			//ServicePointManager.CertificatePolicy = new CertificatePolicity(app);
			IOConnection.CertificateValidationCallback = app.CertificateValidationCallback;

		}


		public abstract IEnumerable<string> SupportedProtocols { get; }

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
			var response = ex.Response as HttpWebResponse;
			if ((response != null) && (response.StatusCode == HttpStatusCode.NotFound))
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
			//check if we need to request the external-storage-permission at runtime
			if (ioc.IsLocalFile())
			{
				bool requiresPermission = !(ioc.Path.StartsWith(activity.Activity.FilesDir.CanonicalPath)
												|| ioc.Path.StartsWith(IoUtil.GetInternalDirectory(activity.Activity).CanonicalPath));
				
				var extDirectory = activity.Activity.GetExternalFilesDir(null);
				if ((extDirectory != null) && (ioc.Path.StartsWith(extDirectory.CanonicalPath)))
					requiresPermission = false;

				if (requiresPermission && (Build.VERSION.SdkInt >= BuildVersionCodes.M))
				{
					if (activity.Activity.CheckSelfPermission(Manifest.Permission.WriteExternalStorage) ==
						Permission.Denied)
					{
						activity.StartFileUsageProcess(ioc, requestCode, alwaysReturnSuccess);
						return;
					}	
				}
				
			}
			Intent intent = new Intent();
			activity.IocToIntent(intent, ioc);
			activity.OnImmediateResult(requestCode, (int) FileStorageResults.FileUsagePrepared, intent);
		}

		public void PrepareFileUsage(Context ctx, IOConnectionInfo ioc)
		{
			//nothing to do, we're ready to go
		}

		public void OnCreate(IFileStorageSetupActivity fileStorageSetupActivity, Bundle savedInstanceState)
		{
			((Activity)fileStorageSetupActivity).RequestPermissions(new[] { Manifest.Permission.WriteExternalStorage }, 0);
		}

		public void OnResume(IFileStorageSetupActivity activity)
		{
			if (activity.State.ContainsKey(PermissionGrantedKey))
			{
				if (activity.State.GetBoolean(PermissionGrantedKey))
				{
					Intent data = new Intent();
					data.PutExtra(FileStorageSetupDefs.ExtraIsForSave, activity.IsForSave);
					data.PutExtra(FileStorageSetupDefs.ExtraPath, IocToPath(activity.Ioc));
					((Activity) activity).SetResult((Result) FileStorageResults.FileUsagePrepared, data);
					((Activity) activity).Finish();
				}
				else
				{
					Intent data = new Intent();
					data.PutExtra(FileStorageSetupDefs.ExtraErrorMessage, "Permission denied. Please grant file access permission for this app.");
					((Activity)activity).SetResult(Result.Canceled, data);
					((Activity)activity).Finish();
					
				}
			}
		}

		public void OnStart(IFileStorageSetupActivity activity)
		{
			
		}

		public void OnActivityResult(IFileStorageSetupActivity activity, int requestCode, int resultCode, Intent data)
		{
			
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

		public bool IsPermanentLocation(IOConnectionInfo ioc)
		{
			return true;
		}

		public bool IsReadOnlyBecauseKitkatRestrictions(IOConnectionInfo ioc)
		{
			if (IsLocalFileFlaggedReadOnly(ioc))
				return false; //it's not read-only because of the restrictions introduced in kitkat
			try
			{
				//test if we can open 
				//http://www.doubleencore.com/2014/03/android-external-storage/#comment-1294469517
				using (var writer = new FileOutputStream(ioc.Path, true))
				{
					writer.Close();
					return false; //we can write
				}
			}
			catch (Java.IO.IOException)
			{
				//seems like we can't write to that location even though it's not read-only
				return true;
			}
			
		}

		public bool IsReadOnly(IOConnectionInfo ioc, OptionalOut<UiStringKey> reason = null)
		{
			if (ioc.IsLocalFile())
			{
				if (IsLocalFileFlaggedReadOnly(ioc))
				{
					if (reason != null)
						reason.Result = UiStringKey.ReadOnlyReason_ReadOnlyFlag;
					return true;
				}

				if (IsReadOnlyBecauseKitkatRestrictions(ioc))
				{
					if (reason != null)
						reason.Result = UiStringKey.ReadOnlyReason_ReadOnlyKitKat;
					return true;
				}
					

				return false;
			}
			//for remote files assume they can be written: (think positive! :-) )
			return false;
		}

		private bool IsLocalFileFlaggedReadOnly(IOConnectionInfo ioc)
		{
			//see http://stackoverflow.com/a/33292700/292233
			try
			{
				return new FileInfo(ioc.Path).IsReadOnly;
			}
			catch (SecurityException)
			{
				return true;
			}
			catch (UnauthorizedAccessException)
			{
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public void OnRequestPermissionsResult(IFileStorageSetupActivity fileStorageSetupActivity, int requestCode,
			string[] permissions, Permission[] grantResults)
		{
			fileStorageSetupActivity.State.PutBoolean(PermissionGrantedKey, grantResults[0] == Permission.Granted);
		}
	}


	public class LocalFileStorage : BuiltInFileStorage
	{
		public LocalFileStorage(IKp2aApp app)
			: base(app)
		{
		}

		public override IEnumerable<string> SupportedProtocols
		{
			get
			{
				yield return "file";
			}
		}
	}
#if !NoNet
	public class LegacyFtpStorage : BuiltInFileStorage
	{
		public LegacyFtpStorage(IKp2aApp app) : base(app)
		{
		}

		public override IEnumerable<string> SupportedProtocols
		{
			get
			{

				yield return "ftp";

			}
		}
	}

	public class LegacyWebDavStorage : BuiltInFileStorage
	{
		public LegacyWebDavStorage(IKp2aApp app) : base(app)
		{
		}

		public override IEnumerable<string> SupportedProtocols
		{
			get
			{

				yield return "http";
				yield return "https";


			}
		}
	}

#endif
}