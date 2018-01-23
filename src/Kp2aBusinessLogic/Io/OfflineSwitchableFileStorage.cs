using System;
using System.Collections.Generic;
using System.IO;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using KeePassLib.Serialization;

namespace keepass2android.Io
{
	public interface IOfflineSwitchable
	{
		bool IsOffline { get; set; }
	}

/// <summary>
	/// Encapsulates another IFileStorage. Allows to switch to offline mode by throwing
	/// an exception when trying to read or write a file.
	/// </summary>
	public class OfflineSwitchableFileStorage : IFileStorage, IOfflineSwitchable, IPermissionRequestingFileStorage
	{
		private readonly IFileStorage _baseStorage;
		public bool IsOffline { get; set; }

		public OfflineSwitchableFileStorage(IFileStorage baseStorage)
		{
			_baseStorage = baseStorage;
		}

		public IEnumerable<string> SupportedProtocols
		{
			get { return _baseStorage.SupportedProtocols; }
		}

	    public bool UserShouldBackup
	    {
	        get { return _baseStorage.UserShouldBackup; }
	    }

	    public void Delete(IOConnectionInfo ioc)
		{
			_baseStorage.Delete(ioc);
		}

		public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
		{
			return _baseStorage.CheckForFileChangeFast(ioc, previousFileVersion);
		}

		public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
		{
			return _baseStorage.GetCurrentFileVersionFast(ioc);
		}

		public Stream OpenFileForRead(IOConnectionInfo ioc)
		{
			AssertOnline();
			return _baseStorage.OpenFileForRead(ioc);
		}

		private void AssertOnline()
		{
			if (IsOffline)
			{
				//throw new Exception(_app.GetResourceString(UiStringKey.InOfflineMode));
				throw new OfflineModeException();
			}
		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			AssertOnline();
			return _baseStorage.OpenWriteTransaction(ioc, useFileTransaction);
		}

		public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
		{
			return _baseStorage.GetFilenameWithoutPathAndExt(ioc);
		}

		public bool RequiresCredentials(IOConnectionInfo ioc)
		{
			return _baseStorage.RequiresCredentials(ioc);
		}

		public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
		{
			_baseStorage.CreateDirectory(ioc, newDirName);
		}

		public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
		{
			return _baseStorage.ListContents(ioc);
		}

		public FileDescription GetFileDescription(IOConnectionInfo ioc)
		{
			return _baseStorage.GetFileDescription(ioc);
		}

		public bool RequiresSetup(IOConnectionInfo ioConnection)
		{
			if (IsOffline)
				return false;
			return _baseStorage.RequiresSetup(ioConnection);
		}

		public string IocToPath(IOConnectionInfo ioc)
		{
			return _baseStorage.IocToPath(ioc);
		}

		public void StartSelectFile(IFileStorageSetupInitiatorActivity activity, bool isForSave, int requestCode, string protocolId)
		{
			_baseStorage.StartSelectFile(activity, isForSave, requestCode, protocolId);
		}

		public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode,
			bool alwaysReturnSuccess)
		{
			if (IsOffline)
			{
				Intent intent = new Intent();
				activity.IocToIntent(intent, ioc);
				activity.OnImmediateResult(requestCode, (int)FileStorageResults.FileUsagePrepared, intent);
				return;
			}
				
			_baseStorage.PrepareFileUsage(activity, ioc, requestCode, alwaysReturnSuccess);
		}

		public void PrepareFileUsage(Context ctx, IOConnectionInfo ioc)
		{
			if (IsOffline)
				return;
			_baseStorage.PrepareFileUsage(ctx, ioc);
		}

		public void OnCreate(IFileStorageSetupActivity activity, Bundle savedInstanceState)
		{
			_baseStorage.OnCreate(activity, savedInstanceState);
		}

		public void OnResume(IFileStorageSetupActivity activity)
		{
			_baseStorage.OnResume(activity);
		}

		public void OnStart(IFileStorageSetupActivity activity)
		{
			_baseStorage.OnStart(activity);
		}

		public void OnActivityResult(IFileStorageSetupActivity activity, int requestCode, int resultCode, Intent data)
		{
			_baseStorage.OnActivityResult(activity, requestCode, resultCode, data);
		}

		public string GetDisplayName(IOConnectionInfo ioc)
		{
			return _baseStorage.GetDisplayName(ioc);
		}

		public string CreateFilePath(string parent, string newFilename)
		{
			return _baseStorage.CreateFilePath(parent, newFilename);
		}

		public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
		{
			return _baseStorage.GetParentPath(ioc);
		}

		public IOConnectionInfo GetFilePath(IOConnectionInfo folderPath, string filename)
		{
			return _baseStorage.GetFilePath(folderPath, filename);
		}

		public bool IsPermanentLocation(IOConnectionInfo ioc)
		{
			return _baseStorage.IsPermanentLocation(ioc);
		}

		public bool IsReadOnly(IOConnectionInfo ioc, OptionalOut<UiStringKey> reason = null)
		{
			return _baseStorage.IsReadOnly(ioc, reason);
		}

	public void OnRequestPermissionsResult(IFileStorageSetupActivity fileStorageSetupActivity, int requestCode,
		string[] permissions, Permission[] grantResults)
	{
		if (_baseStorage is IPermissionRequestingFileStorage)
		{
			((IPermissionRequestingFileStorage)_baseStorage).OnRequestPermissionsResult(fileStorageSetupActivity, requestCode, permissions, grantResults);
		}
	}
	}

	public class OfflineModeException : Exception
	{
		public override string Message
		{
			get { return "Working offline."; }
		}
	}
}