using System.Collections.Generic;
using System.IO;
using Android.Content;
using Android.OS;
using KeePassLib.Serialization;
using Exception = Java.Lang.Exception;

namespace keepass2android.Io
{
    /// <summary>
    /// This IFileStorage implementation becomes picked if a user is using a skydrive:// or onedrive:// file.
    /// These refer to an old (Java) implementation which was replaced starting in 2019. The successor uses onedrive2:// (see OneDrive2FileStorage)
    /// The Java implementation was removed in 2024 when the jar files became unavailable. We are keeping this file to notify any user who haven't updated their
    /// file storage within 5 years.
    /// This file should be removed around mid 2025.
    /// </summary>
	public class OneDriveFileStorage: IFileStorage
	{
		
		public IEnumerable<string> SupportedProtocols
		{
			get
			{
				yield return "skydrive";
				yield return "onedrive";
			}
		}

        private Exception GetDeprecatedMessage()
        {
            return new Exception(
                "You have opened your file through a deprecated Microsoft API. Please select Change database, Open Database and then select One Drive again.");
        }

	    public bool UserShouldBackup
	    {
	        get { return false; }
	    }

        public void Delete(IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
        {
            throw GetDeprecatedMessage();
        }

        public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public Stream OpenFileForRead(IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
        {
            throw GetDeprecatedMessage();
        }

        public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public string GetFileExtension(IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public bool RequiresCredentials(IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
        {
            throw GetDeprecatedMessage();
        }

        public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public FileDescription GetFileDescription(IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public bool RequiresSetup(IOConnectionInfo ioConnection)
        {
            throw GetDeprecatedMessage();
        }

        public string IocToPath(IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public void StartSelectFile(IFileStorageSetupInitiatorActivity activity, bool isForSave, int requestCode, string protocolId)
        {
            throw GetDeprecatedMessage();
        }

        public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode,
            bool alwaysReturnSuccess)
        {
            throw GetDeprecatedMessage();
        }

        public void PrepareFileUsage(Context ctx, IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public void OnCreate(IFileStorageSetupActivity activity, Bundle savedInstanceState)
        {
            throw GetDeprecatedMessage();
        }

        public void OnResume(IFileStorageSetupActivity activity)
        {
            throw GetDeprecatedMessage();
        }

        public void OnStart(IFileStorageSetupActivity activity)
        {
            throw GetDeprecatedMessage();
        }

        public void OnActivityResult(IFileStorageSetupActivity activity, int requestCode, int resultCode, Intent data)
        {
            throw GetDeprecatedMessage();
        }

        public string GetDisplayName(IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public string CreateFilePath(string parent, string newFilename)
        {
            throw GetDeprecatedMessage();
        }

        public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public IOConnectionInfo GetFilePath(IOConnectionInfo folderPath, string filename)
        {
            throw GetDeprecatedMessage();
        }

        public bool IsPermanentLocation(IOConnectionInfo ioc)
        {
            throw GetDeprecatedMessage();
        }

        public bool IsReadOnly(IOConnectionInfo ioc, OptionalOut<UiStringKey> reason = null)
        {
            throw GetDeprecatedMessage();
        }
    }
}
