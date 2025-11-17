// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

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
	public class OneDriveFileStorage : IFileStorage
    {
        public OneDriveFileStorage(IKp2aApp app)
        {
            _app = app;
        }

        private readonly IKp2aApp _app;

        public IEnumerable<string> SupportedProtocols
        {
            get
            {
                yield return "skydrive";
                yield return "onedrive";
            }
        }

        string GetDeprecatedMessage()
        {
            return
                "You have opened your file through a deprecated Microsoft API. Please select Change database, Open Database and then select OneDrive again.";
        }

        private Exception GetDeprecatedException()
        {
            return new Exception(
                GetDeprecatedMessage());
        }

        public bool UserShouldBackup
        {
            get { return false; }
        }

        public void Delete(IOConnectionInfo ioc)
        {
            throw GetDeprecatedException();
        }

        public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
        {
            throw GetDeprecatedException();
        }

        public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
        {
            throw GetDeprecatedException();
        }

        public Stream OpenFileForRead(IOConnectionInfo ioc)
        {
            throw GetDeprecatedException();
        }

        public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
        {
            throw GetDeprecatedException();
        }

        public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
        {
            throw GetDeprecatedException();
        }

        public string GetFileExtension(IOConnectionInfo ioc)
        {
            throw GetDeprecatedException();
        }

        public bool RequiresCredentials(IOConnectionInfo ioc)
        {
            return false;
        }

        public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
        {
            throw GetDeprecatedException();
        }

        public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
        {
            throw GetDeprecatedException();
        }

        public FileDescription GetFileDescription(IOConnectionInfo ioc)
        {
            throw GetDeprecatedException();
        }

        public bool RequiresSetup(IOConnectionInfo ioConnection)
        {
            return false;
        }

        public string IocToPath(IOConnectionInfo ioc)
        {
            throw GetDeprecatedException();
        }

        public void StartSelectFile(IFileStorageSetupInitiatorActivity activity, bool isForSave, int requestCode, string protocolId)
        {

        }

        public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode,
            bool alwaysReturnSuccess)
        {
            _app.ShowMessage(activity.Activity, GetDeprecatedMessage(), MessageSeverity.Error);

        }

        public void PrepareFileUsage(Context ctx, IOConnectionInfo ioc)
        {

        }

        public void OnCreate(IFileStorageSetupActivity activity, Bundle savedInstanceState)
        {

        }

        public void OnResume(IFileStorageSetupActivity activity)
        {

        }

        public void OnStart(IFileStorageSetupActivity activity)
        {
        }

        public void OnActivityResult(IFileStorageSetupActivity activity, int requestCode, int resultCode, Intent data)
        {
        }

        public string GetDisplayName(IOConnectionInfo ioc)
        {
            return "File using deprecated Microsoft API. Please update.";
        }

        public string CreateFilePath(string parent, string newFilename)
        {
            throw GetDeprecatedException();
        }

        public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
        {
            throw GetDeprecatedException();
        }

        public IOConnectionInfo GetFilePath(IOConnectionInfo folderPath, string filename)
        {
            throw GetDeprecatedException();
        }

        public bool IsPermanentLocation(IOConnectionInfo ioc)
        {
            throw GetDeprecatedException();
        }

        public bool IsReadOnly(IOConnectionInfo ioc, OptionalOut<UiStringKey> reason = null)
        {
            throw GetDeprecatedException();
        }
    }
}
