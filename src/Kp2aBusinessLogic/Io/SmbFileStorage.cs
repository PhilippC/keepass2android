#if !NoNet
using System.Net;
using Android.Content;
using keepass2android;
using keepass2android.Io;
using KeePassLib.Serialization;
using SMBLibrary.Client;
using SMBLibrary;
using FileAttributes = SMBLibrary.FileAttributes;
using KeePassLib.Utility;
using Java.Nio.FileNio;

namespace Kp2aBusinessLogic.Io
{
    public class SmbFileStorage : IFileStorage
    {
        public IEnumerable<string> SupportedProtocols
        {
            get { yield return "smb"; }
        }

        public bool UserShouldBackup
        {
            get { return false; }
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

        public struct SmbConnectionInfo
        {
            public string Host;
            public string Username;
            public string Password;
            public string? Domain;
            public string? Share;
            public string? LocalPath;

            public static SmbConnectionInfo FromUrlAndCredentials(string url, string username, string password, string? domain)
            {
                string userDomain = username;
                if (domain != null)
                {
                    userDomain = domain + "\\" + username;
                }
                if (url.StartsWith("smb://"))
                {
                    url = url.Substring(6);
                }

                if (url.StartsWith("\\\\"))
                {
                    url = url.Substring(2);
                }

                url = url.Replace("\\", "/");

                string fullPath = "smb://" + WebUtility.UrlEncode(userDomain) + ":" + WebUtility.UrlEncode(password) + "@" + url;
                return new SmbConnectionInfo(new IOConnectionInfo() { Path = fullPath });
            }


            public SmbConnectionInfo(IOConnectionInfo ioc)
            {
                string fullpath = ioc.Path;
                if (!fullpath.StartsWith("smb://"))
                {
                    throw new Exception("Invalid smb path!");
                }

                fullpath = fullpath.Substring(6);
                string[] authAndPath = fullpath.Split('@');
                if (authAndPath.Length != 2)
                {
                    throw new Exception("Invalid smb path!");
                }

                string[] userAndPwd = authAndPath[0].Split(':');
                if (userAndPwd.Length != 2)
                {
                    throw new Exception("Invalid smb path!");
                }

                string[] pathParts = authAndPath[1].Split('/');
                if (pathParts.Length < 1)
                {
                    throw new Exception("Invalid smb path!");
                }

                Host = pathParts[0];
                if (pathParts.Length > 1)
                {
                    Share = pathParts[1];
                }
                LocalPath = String.Join("/", pathParts.Skip(2));
                if (LocalPath.EndsWith("/"))
                {
                    LocalPath = LocalPath.Substring(0, LocalPath.Length - 1);
                }

                Username = WebUtility.UrlDecode(userAndPwd[0]);
                if (Username.Contains("\\"))
                {
                    string[] domainAndUser = Username.Split('\\');
                    Domain = domainAndUser[0];
                    Username = domainAndUser[1];
                }
                else Domain = null;

                Password = WebUtility.UrlDecode(userAndPwd[1]);
            }

            public string ToPath()
            {
                string domainUser = Username;
                if (Domain != null)
                {
                    domainUser = Domain + "\\" + Username;
                }

                return "smb://" + WebUtility.UrlEncode(domainUser) + ":" + WebUtility.UrlEncode(Password) + "@" + Host +
                       "/" + Share + "/" + LocalPath;
            }

            public string GetPathWithoutCredentials()
            {
                return "smb://" + Host + "/" + Share + "/" + LocalPath;
            }

            public string GetLocalSmbPath()
            {
                return LocalPath?.Replace("/", "\\") ?? "";
            }

            public SmbConnectionInfo GetParent()
            {
                SmbConnectionInfo parent = new SmbConnectionInfo
                {
                    Host = Host,
                    Username = Username,
                    Password = Password,
                    Domain = Domain,
                    Share = Share
                };
                string[] pathParts = LocalPath?.Split('/') ?? [];
                if (pathParts.Length > 0)
                {
                    parent.LocalPath = string.Join("/", pathParts.Take(pathParts.Length - 1));
                }
                else
                {
                    parent.LocalPath = "";
                    parent.Share = "";
                }

                return parent;
            }

            public string Stem()
            {
                return LocalPath?.Split('/').Last() ?? "";
            }


            public SmbConnectionInfo GetChild(string childName)
            {
                SmbConnectionInfo child = new SmbConnectionInfo();
                child.Host = Host;
                child.Username = Username;
                child.Password = Password;
                child.Domain = Domain;
                if (string.IsNullOrEmpty(Share))
                {
                    child.Share = childName;
                }
                else
                {

                    child.Share = Share;
                    var pathPartsList = LocalPath?.Split('/').Where(p => !string.IsNullOrEmpty(p)).ToList() ?? [];
                    pathPartsList.Add(childName);
                    child.LocalPath = string.Join("/", pathPartsList);
                }

                return child;
            }

            public string ToDisplayString()
            {
                return "smb://" + Host + "/" + Share + "/" + LocalPath;

            }
        }


        class SmbConnection : IDisposable
        {
            public SmbConnection(SmbConnectionInfo info)
            {
                _isLoggedIn = false;
                var isConnected = Client.Connect(info.Host, SMBTransportType.DirectTCPTransport);
                if (!isConnected)
                {
                    throw new Exception($"Failed to connect to SMB server {info.Host}");
                }

                var status = Client.Login(info.Domain ?? string.Empty, info.Username, info.Password);
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    throw new Exception($"Failed to login to SMB as {info.Username}");
                }

                _isLoggedIn = true;

                if (!string.IsNullOrEmpty(info.Share))
                {
                    FileStore = Client.TreeConnect(info.Share, out status);
                }

            }



            public readonly SMB2Client Client = new SMB2Client();


            public readonly ISMBFileStore? FileStore;
            private readonly bool _isLoggedIn;

            public void Dispose()
            {
                FileStore?.Disconnect();

                if (_isLoggedIn)
                    Client.Logoff();

                if (!Client.IsConnected) return;
                Client.Disconnect();


            }
        }



        public Stream OpenFileForRead(IOConnectionInfo ioc)
        {

            SmbConnectionInfo info = new SmbConnectionInfo(ioc);
            using SmbConnection conn = new SmbConnection(info);

            if (conn.FileStore == null)
            {
                throw new Exception($"Failed to read to {info.GetPathWithoutCredentials()}");
            }


            NTStatus status = conn.FileStore.CreateFile(out var fileHandle, out _, info.GetLocalSmbPath(),
                AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE, FileAttributes.Normal, ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new Exception($"Failed to open file {info.LocalPath}");
            }

            var stream = new MemoryStream();
            long bytesRead = 0;
            while (true)
            {
                status = conn.FileStore.ReadFile(out var data, fileHandle, bytesRead, (int)conn.Client.MaxReadSize);
                if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_END_OF_FILE)
                {
                    throw new Exception("Failed to read from file");
                }

                if (status == NTStatus.STATUS_END_OF_FILE || data.Length == 0)
                {
                    break;
                }

                bytesRead += data.Length;
                stream.Write(data, 0, data.Length);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }


        class SmbFileStorageWriteTransaction : IWriteTransaction
        {
            private bool UseFileTransaction { get; }
            private readonly string _path;
            private readonly string _uploadPath;
            private readonly SmbFileStorage _fileStorage;
            private MemoryStream? _memoryStream;

            public SmbFileStorageWriteTransaction(string path, SmbFileStorage fileStorage, bool useFileTransaction)
            {
                UseFileTransaction = useFileTransaction;
                _path = path;
                if (useFileTransaction)
                {
                    _uploadPath = _path + Guid.NewGuid().ToString().Substring(0, 8) + ".tmp";
                }
                else
                {
                    _uploadPath = _path;
                }


                _fileStorage = fileStorage;
                _memoryStream = null;
            }

            public void Dispose()
            {
                _memoryStream?.Dispose();
            }

            public Stream OpenFile()
            {
                _memoryStream = new MemoryStream();
                return _memoryStream;
            }

            public void CommitWrite()
            {
                _fileStorage.UploadData(new MemoryStream(_memoryStream!.ToArray()), new SmbConnectionInfo(new IOConnectionInfo() { Path = _uploadPath }));
                if (UseFileTransaction)
                {
                    SmbConnectionInfo uploadPath = new SmbConnectionInfo(new IOConnectionInfo() { Path = _uploadPath });
                    SmbConnectionInfo finalPath = new SmbConnectionInfo(new IOConnectionInfo() { Path = _path });
                    _fileStorage.RenameFile(uploadPath, finalPath);
                }


            }
        }

        private void RenameFile(SmbConnectionInfo fromPath, SmbConnectionInfo toPath)
        {
            using var connection = new SmbConnection(fromPath);

            // Open existing file
            var status = connection.FileStore!.CreateFile(out var handle, out _, fromPath.GetLocalSmbPath(), AccessMask.MAXIMUM_ALLOWED, 0, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_NON_DIRECTORY_FILE, null);
            if (status != NTStatus.STATUS_SUCCESS)
                throw new Exception($"Failed to open {fromPath.LocalPath} for renaming!");

            FileRenameInformationType2 renameInfo = new FileRenameInformationType2
            {
                FileName = toPath.GetLocalSmbPath(),
                ReplaceIfExists = true
            };
            connection.FileStore.SetFileInformation(handle, renameInfo);
            connection.FileStore.CloseFile(handle);

        }

        private void UploadData(Stream data, SmbConnectionInfo uploadPath)
        {
            using var connection = new SmbConnection(uploadPath);
            var status = connection.FileStore!.CreateFile(out var fileHandle, out _, uploadPath.GetLocalSmbPath(), AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE, FileAttributes.Normal, ShareAccess.None, CreateDisposition.FILE_CREATE, CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
            if (status == NTStatus.STATUS_OBJECT_NAME_COLLISION)
                status = connection.FileStore!.CreateFile(out fileHandle, out _, uploadPath.GetLocalSmbPath(), AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE, FileAttributes.Normal, ShareAccess.None, CreateDisposition.FILE_OVERWRITE, CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new Exception($"Failed to open {uploadPath.LocalPath} for writing!");
            }

            long writeOffset = 0;
            while (data.Position < data.Length)
            {
                byte[] buffer = new byte[(int)connection.Client.MaxWriteSize];
                int bytesRead = data.Read(buffer, 0, buffer.Length);
                if (bytesRead < (int)connection.Client.MaxWriteSize)
                {
                    Array.Resize(ref buffer, bytesRead);
                }

                status = connection.FileStore.WriteFile(out _, fileHandle, writeOffset, buffer);
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    throw new Exception("Failed to write to file");
                }
                writeOffset += bytesRead;
            }

        }

        public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
        {
            return new SmbFileStorageWriteTransaction(ioc.Path, this, useFileTransaction);
        }

        public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
        {
            return UrlUtil.StripExtension(
                UrlUtil.GetFileName(ioc.Path));

        }

        public string GetFileExtension(IOConnectionInfo ioc)
        {
            return UrlUtil.GetExtension(ioc.Path);
        }

        public bool RequiresCredentials(IOConnectionInfo ioc)
        {
            return false;
        }

        public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
        {
            throw new NotImplementedException();
        }

        private static IEnumerable<FileDescription> ListShares(SmbConnection conn, SmbConnectionInfo parent)
        {
            foreach (string share in conn.Client.ListShares(out _))
            {
                yield return new FileDescription()
                {
                    CanRead = true,
                    CanWrite = true,
                    DisplayName = share,
                    IsDirectory = true,
                    Path = parent.GetChild(share).ToPath()
                };
            }

        }

        public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
        {
            List<FileDescription> result = [];
            SmbConnectionInfo info = new SmbConnectionInfo(ioc);
            using SmbConnection conn = new SmbConnection(info);
            if (string.IsNullOrEmpty(info.Share))
            {
                var shares = ListShares(conn, info).ToList();
                return shares;
            }

            NTStatus status = conn.FileStore!.CreateFile(out var directoryHandle, out _, info.GetLocalSmbPath(), AccessMask.GENERIC_READ, FileAttributes.Directory, ShareAccess.Read | ShareAccess.Write, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
            if (status == NTStatus.STATUS_SUCCESS)
            {
                conn.FileStore.QueryDirectory(out List<QueryDirectoryFileInformation> fileList, directoryHandle, "*", FileInformationClass.FileDirectoryInformation);
                foreach (var fi in fileList)
                {
                    var fileDirectoryInformation = fi as FileDirectoryInformation;
                    if (fileDirectoryInformation == null)
                        continue;

                    if (fileDirectoryInformation.FileName is "." or "..")
                        continue;

                    var fileDescription = FileDescriptionConvert(ioc, fileDirectoryInformation);

                    result.Add(fileDescription);
                }
                conn.FileStore.CloseFile(directoryHandle);
            }

            return result;
        }

        private FileDescription FileDescriptionConvert(IOConnectionInfo parentIoc,
            FileDirectoryInformation fileDirectoryInformation)
        {
            FileDescription fileDescription = new FileDescription
            {
                CanRead = true,
                CanWrite = true,
                IsDirectory = (fileDirectoryInformation.FileAttributes & FileAttributes.Directory) != 0,
                DisplayName = fileDirectoryInformation.FileName
            };
            fileDescription.Path = CreateFilePath(parentIoc.Path, fileDescription.DisplayName);
            fileDescription.LastModified = fileDirectoryInformation.LastWriteTime;

            fileDescription.SizeInBytes = fileDirectoryInformation.EndOfFile;
            return fileDescription;
        }

        public FileDescription GetFileDescription(IOConnectionInfo ioc)
        {
            SmbConnectionInfo info = new SmbConnectionInfo(ioc);

            if (string.IsNullOrEmpty(info.Share))
            {
                return new FileDescription
                {
                    CanRead = true,
                    CanWrite = true,
                    DisplayName = info.Host,
                    IsDirectory = true,
                    Path = info.ToPath()
                };
            }

            using SmbConnection conn = new SmbConnection(info);
            NTStatus status = conn.FileStore!.CreateFile(out var directoryHandle, out _, info.GetParent().GetLocalSmbPath(), AccessMask.GENERIC_READ, FileAttributes.Directory, ShareAccess.Read | ShareAccess.Write, CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);
            if (status != NTStatus.STATUS_SUCCESS) throw new Exception($"Failed to query details for {info.LocalPath}");
            conn.FileStore.QueryDirectory(out List<QueryDirectoryFileInformation> fileList, directoryHandle, info.Stem(), FileInformationClass.FileDirectoryInformation);
            foreach (var fi in fileList)
            {
                var fileDirectoryInformation = fi as FileDirectoryInformation;
                if (fileDirectoryInformation == null)
                    continue;

                if (fileDirectoryInformation.FileName is "." or "..")
                    continue;

                return FileDescriptionConvert(ioc, fileDirectoryInformation);


            }
            conn.FileStore.CloseFile(directoryHandle);

            throw new Exception($"Failed to query details for {info.LocalPath}");
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
            activity.PerformManualFileSelect(isForSave, requestCode, protocolId);

        }

        public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode,
            bool alwaysReturnSuccess)
        {
            Intent intent = new Intent();
            activity.IocToIntent(intent, ioc);
            activity.OnImmediateResult(requestCode, (int)FileStorageResults.FileUsagePrepared, intent);
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
            return new SmbConnectionInfo(ioc).ToDisplayString();
        }

        public string CreateFilePath(string parent, string newFilename)
        {
            return new SmbConnectionInfo(new IOConnectionInfo() { Path = parent }).GetChild(newFilename).ToPath();
        }

        public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
        {
            SmbConnectionInfo connectionInfo = new SmbConnectionInfo(ioc);
            return new IOConnectionInfo() { Path = connectionInfo.GetParent().ToPath() };
        }

        public IOConnectionInfo GetFilePath(IOConnectionInfo folderPath, string filename)
        {
            return new IOConnectionInfo() { Path = CreateFilePath(folderPath.Path, filename) };
        }

        public bool IsPermanentLocation(IOConnectionInfo ioc)
        {
            return true;
        }

        public bool IsReadOnly(IOConnectionInfo ioc, OptionalOut<UiStringKey> reason = null)
        {
            return false;
        }
    }
}
#endif