using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Util;
using CG.Web.MegaApiClient;
using Group.Pals.Android.Lib.UI.Filechooser.Utils;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace keepass2android.Io
{
    public class MegaFileStorage : IFileStorage
    {
        private readonly Context _appContext;
        public const string ProtocolId = "mega";
        private const string PreferenceKey = "KP2A-Mega-Accounts";

        public MegaFileStorage(Context appContext)
        {
            _appContext = appContext;
        }

        //we don't want to store passwords in plain text, encrypt them with this key at least:
        public static readonly byte[] EncryptionKey = new byte[] { 86,239,128,218,160,22,245,114,193,92,151,10,134,104,121,170,
            183,110,60,38,179,181,24,206,169,43,125,193,142,156,47,45};

        public class AccountSettings
        {
            public Dictionary<string, string> PasswordByUsername { get; set; } = new Dictionary<string, string>();

            public static byte[] exclusiveOR(byte[] arr1, byte[] arr2)
            {
                byte[] result = new byte[arr1.Length];

                for (int i = 0; i < arr1.Length; ++i)
                    result[i] = (byte)(arr1[i] ^ arr2[i % arr2.Length]);

                return result;
            }

            static string Encrypt(string s)
            {
                var plainTextBytes = exclusiveOR(System.Text.Encoding.UTF8.GetBytes(s), EncryptionKey);
                return System.Convert.ToBase64String(plainTextBytes);

            }

            static string Decrypt(string s)
            {
                var base64EncodedBytes = System.Convert.FromBase64String(s);
                return System.Text.Encoding.UTF8.GetString(exclusiveOR(base64EncodedBytes, EncryptionKey));

            }

            public string Serialize()
            {
                Dictionary<string, string> encryptedPasswordByUsername = PasswordByUsername
                    .Select(kvp => new KeyValuePair<string, string>(kvp.Key, Encrypt(kvp.Value)))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                return Newtonsoft.Json.JsonConvert.SerializeObject(encryptedPasswordByUsername);
            }

            public void Deserialize(string data)
            {
                if (string.IsNullOrEmpty(data))
                {
                    PasswordByUsername = new Dictionary<string, string>();
                    return;
                }
                Dictionary<string, string> encryptedPasswordByUsername =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(data);
                PasswordByUsername = encryptedPasswordByUsername
                    .Select(kvp => new KeyValuePair<string, string>(kvp.Key, Decrypt(kvp.Value)))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        public IEnumerable<string> SupportedProtocols
        {
            get { yield return ProtocolId; }
        }

        public bool UserShouldBackup
        {
            get { return false; }
        }


        class MegaFileStorageWriteTransaction : IWriteTransaction
        {
            public bool UseFileTransaction { get; }
            private readonly string _path;
            private readonly MegaFileStorage _filestorage;
            private MemoryStream _memoryStream;

            public MegaFileStorageWriteTransaction(string path, MegaFileStorage filestorage, bool useFileTransaction)
            {
                UseFileTransaction = useFileTransaction;
                _path = path;
                _filestorage = filestorage;
            }

            public void Dispose()
            {
                _memoryStream.Dispose();
            }

            public Stream OpenFile()
            {
                _memoryStream = new MemoryStream();
                return _memoryStream;
            }

            public void CommitWrite()
            {
                _filestorage.UploadFile(_path, new MemoryStream(_memoryStream.ToArray()), UseFileTransaction);

            }
        }

        private void UploadFile(string path, MemoryStream memoryStream, bool useTransaction)
        {
            var accountData = GetAccountData(path);

            if (accountData.TryGetNode(path, out var node))
            {
                if (useTransaction)
                {
                    string temporaryName = node.Name + "." + new Guid().ToString() + ".tmp";
                    var newNode = accountData.Client.Upload(memoryStream, temporaryName, accountData.GetParentNode(node));
                    accountData.Client.Delete(node);
                    newNode = accountData.Client.Rename(newNode, node.Name);
                    accountData._nodes.Remove(node);
                    accountData._nodes.Add(newNode);
                }
                else
                {
                    var newNode = accountData.Client.Upload(memoryStream, node.Name, accountData.GetParentNode(node));
                    //we now have two nodes with the same name. Delete the old one:
                    accountData.Client.Delete(node);
                    accountData._nodes.Remove(node);
                    accountData._nodes.Add(newNode);

                }
            }
            else
            {
                //file did not exist yet
                string parentPath = GetParentPath(new IOConnectionInfo() { Path = path }).Path;
                string name = path.Substring(parentPath.Length + 1);
                var newNode = accountData.Client.Upload(memoryStream, name, accountData.GetNode(parentPath));
                accountData._nodes.Add(newNode);
            }

        }

        public void StartSelectFile(IFileStorageSetupInitiatorActivity activity, bool isForSave, int requestCode,
            string protocolId)
        {
            activity.PerformManualFileSelect(isForSave, requestCode, protocolId);
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

        public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
        {
            return new MegaFileStorageWriteTransaction(ioc.Path, this, useFileTransaction);
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

        public string CreateFilePath(string parent, string newFilename)
        {
            if (!parent.EndsWith("/"))
                parent += "/";
            return parent + newFilename;
        }

        public bool IsReadOnly(IOConnectionInfo ioc, OptionalOut<UiStringKey> reason = null)
        {
            return false;
        }

        public bool IsPermanentLocation(IOConnectionInfo ioc)
        {
            return true;
        }

        public IOConnectionInfo GetFilePath(IOConnectionInfo folderPath, string filename)
        {
            IOConnectionInfo res = folderPath.CloneDeep();
            if (!res.Path.EndsWith("/"))
                res.Path += "/";
            res.Path += filename;
            return res;
        }

        public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
        {
            return IoUtil.GetParentPath(ioc);
        }

        public string GetDisplayName(IOConnectionInfo ioc)
        {
            return ioc.GetDisplayName();
        }

        public void PrepareFileUsage(Context ctx, IOConnectionInfo ioc)
        {
            //nothing to do
        }

        public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode,
            bool alwaysReturnSuccess)
        {
            Intent intent = new Intent();
            activity.IocToIntent(intent, ioc);
            activity.OnImmediateResult(requestCode, (int)FileStorageResults.FileUsagePrepared, intent);
        }

        public string IocToPath(IOConnectionInfo ioc)
        {
            return ioc.Path;
        }

        public bool RequiresSetup(IOConnectionInfo ioConnection)
        {
            return false;
        }

        public FileDescription GetFileDescription(IOConnectionInfo ioc)
        {
            var accountData = GetAccountData(ioc);
            return MakeFileDescription(accountData, accountData.GetNode(ioc));
        }

        class AccountData
        {
            public string Account { get; set; }
            public IMegaApiClient Client { get; set; }

            public void RefreshMetadata()
            {
                //make sure we refresh meta data after one minute:
                if (DateTime.Now.Subtract(_nodesLoadingTime).TotalMinutes > 1.0)
                {
                    _nodes.Clear();
                    EnsureMetadataLoaded();
                }
            }

            public List<INode> _nodes = new List<INode>();
            private DateTime _nodesLoadingTime;
            private INode _rootNode;

            public INode GetNode(IOConnectionInfo ioc)
            {
                return GetNode(ioc.Path);
            }

            public bool TryGetNode(string path, out INode node)
            {
                try
                {
                    node = GetNode(path);
                    return true;
                }
                catch (Exception e)
                {
                    node = null;
                    return false;
                }
            }

            public INode GetNode(string path)
            {
                EnsureMetadataLoaded();
                if (!path.StartsWith(ProtocolId + "://"))
                    throw new Exception("Invalid Mega URL: " + path);
                path = path.Substring(ProtocolId.Length + 3);
                var parts = path.Split('/');
                if (parts.Length < 1 || parts[0] == "")
                    throw new Exception("Invalid Mega URL: " + path);

                INode node = _rootNode;
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i] == "")
                        continue;
                    var matchingChildren = _nodes.Where(n => n.ParentId == node.Id && n.Name == parts[i]).ToList();
                    if (matchingChildren.Count == 0)
                        throw new FileNotFoundException("Did not find " + path);
                    if (matchingChildren.Count > 1)
                        throw new Java.IO.FileNotFoundException(
                            $"Found more than one child with name {parts[i]} while trying to get node for {path}");
                    node = matchingChildren.Single();
                }

                return node;

            }

            private void EnsureMetadataLoaded()
            {
                if (_nodes.Any() == false)
                {
                    _nodes = Client.GetNodes().ToList();

                    _rootNode = _nodes.Single(n => n.Type == NodeType.Root);
                    _nodesLoadingTime = DateTime.Now;
                }
            }

            public INode GetParentNode(INode node)
            {
                return _nodes.Single(n => n.Id == node.ParentId);
            }

            internal void InvalidateMetaData()
            {
                _nodes.Clear();
            }

            public IEnumerable<INode> GetChildNodes(INode node)
            {
                EnsureMetadataLoaded();
                return _nodes.Where(n => n.ParentId == node.Id);
            }

            public string GetPath(INode node)
            {
                if (node.Type == NodeType.Root)
                    return ProtocolId + "://" + this.Account;
                var parent = _nodes.Single(n => n.Id == node.ParentId);
                return GetPath(parent) + "/" + node.Name;
            }
        }


        readonly Dictionary<string /*account*/, AccountData> _allAccountData = new Dictionary<string, AccountData>();

        public string GetAccount(IOConnectionInfo ioc)
        {
            return GetAccount(ioc.Path);
        }

        public static string GetAccount(string path)
        {
            if (!path.StartsWith(ProtocolId + "://"))
                throw new Exception("Invalid Mega URL: " + path);
            path = path.Substring(ProtocolId.Length + 3);
            var parts = path.Split('/');
            if (parts.Length < 1 || parts[0] == "")
                throw new Exception("Invalid Mega URL: " + path);
            return parts[0];


        }

        private AccountData GetAccountData(IOConnectionInfo ioc)
        {
            return GetAccountData(ioc.Path);
        }

        public static AccountSettings GetAccountSettings(Context ctx)
        {
            string accountSettingsString = PreferenceManager.GetDefaultSharedPreferences(ctx).GetString(PreferenceKey, null);
            AccountSettings settings = new AccountSettings();
            settings.Deserialize(accountSettingsString);
            return settings;
        }

        public static void UpdateAccountSettings(AccountSettings settings, Context ctx)
        {
            PreferenceManager.GetDefaultSharedPreferences(ctx).Edit().PutString(PreferenceKey, settings.Serialize())
                .Commit();
        }

        private AccountData GetAccountData(string path)
        {
            string account = GetAccount(path);
            if (_allAccountData.TryGetValue(account, out var accountData))
            {
                return accountData;
            }

            AccountData newAccountData = new AccountData()
            {
                Account = account,
                Client = new MegaApiClient()
            };

            var settings = GetAccountSettings(_appContext);
            if (!settings.PasswordByUsername.TryGetValue(account, out string password))
            {
                throw new Exception("No account configured with username = " + account);
            }

            try
            {
                newAccountData.Client.Login(account, password);
            }
            catch (CG.Web.MegaApiClient.ApiException e)
            {
                if (e.ApiResultCode == CG.Web.MegaApiClient.ApiResultCode.ResourceNotExists)
                {
                    throw new Exception("Failed to login to MEGA account. Please check username and password!");
                }
                    
            }
            

            _allAccountData[account] = newAccountData;
            return newAccountData;

        }

        public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
        {
            AccountData accountData = GetAccountData(ioc);
            accountData.RefreshMetadata();
            return accountData.GetChildNodes(accountData.GetNode(ioc)).Select(n => MakeFileDescription(accountData, n));


        }

        private FileDescription MakeFileDescription(AccountData account, INode n)
        {
            return new FileDescription()
            {
                CanRead = true,
                CanWrite = true,
                DisplayName = n.Name ?? (n.Type == NodeType.Root ? "root" : ""),
                IsDirectory = n.Type != NodeType.File,
                LastModified = n.ModificationDate ?? n.CreationDate ?? DateTime.MinValue,
                Path = account.GetPath(n),
                SizeInBytes = n.Size
            };
        }

        public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
        {
            var accountData = GetAccountData(ioc);
            var newNode = accountData.Client.CreateFolder(newDirName, accountData.GetNode(ioc));
            accountData._nodes.Add(newNode);
        }

        public bool RequiresCredentials(IOConnectionInfo ioc)
        {
            return false;
        }


        public Stream OpenFileForRead(IOConnectionInfo ioc)
        {
            var accountData = GetAccountData(ioc);
            return accountData.Client.Download(accountData.GetNode(ioc));
        }

        public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
        {
            return null;
        }

        public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
        {
            return false;
        }

        public void Delete(IOConnectionInfo ioc)
        {
            var accountData = GetAccountData(ioc);
            accountData.Client.Delete(accountData.GetNode(ioc));
        }

        
    }
}