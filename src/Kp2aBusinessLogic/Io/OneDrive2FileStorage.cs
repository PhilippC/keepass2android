using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Java.Net;
using keepass2android.Io.ItemLocation;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Exception = System.Exception;
using File = Microsoft.Graph.File;
using String = System.String;

namespace keepass2android.Io
{
    namespace ItemLocation
    {
        public class User
        {
            public string Name { get; set; }
            public string Id { get; set; }
        }
        public class Share
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public string WebUrl { get; set; }
        }

        public class Item
        {
            public string Name { get; set; }
            public string Id { get; set; }

        }

        public class OneDrive2ItemLocation<OneDrive2PrefixContainerType> where OneDrive2PrefixContainerType: OneDrive2PrefixContainer, new()
        {
            
            public User User { get; set; } = new User();
            public Share Share { get; set; } = new Share();
            public string DriveId { get; set; }

            public List<Item> LocalPath { get; set; } = new List<Item>();
            public string LocalPathString { get { return string.Join("/", LocalPath.Select(i => i.Name)); } }

            public OneDrive2ItemLocation<OneDrive2PrefixContainerType> Parent
            {
                get
                {
                    OneDrive2ItemLocation< OneDrive2PrefixContainerType> copy = OneDrive2ItemLocation< OneDrive2PrefixContainerType>.FromString(this.ToString());
                    if (copy.LocalPath.Any())
                    {
                        //pop last:
                        copy.LocalPath.RemoveAt(copy.LocalPath.Count - 1);
                    }
                    else if (copy.Share.Id != null)
                    {
                        copy.Share = new Share();
                    }
                    else copy.User = new User();
                    return copy;
                }
            }

            public override string ToString()
            {
                string path = (new OneDrive2PrefixContainerType()).Onedrive2Prefix + string.Join("\\", (new List<string> { User.Id, User.Name,
                    Share.Id, Share.Name,Share.WebUrl,
                    string.Join("/", LocalPath.Select(i => Encode(i.Id)+":"+Encode(i.Name))),
                    DriveId
                }).Select(Encode));
                path += "?" + path.Length;
                return path;
            }

            private string Encode(string s)
            {
                return WebUtility.UrlEncode(s);
            }

            public static OneDrive2ItemLocation<OneDrive2PrefixContainerType> FromString(string p)
            {
                if ((p == null) || (p == (new OneDrive2PrefixContainerType()).Onedrive2Prefix))
                    return new OneDrive2ItemLocation<OneDrive2PrefixContainerType>();

                if (!p.StartsWith((new OneDrive2PrefixContainerType()).Onedrive2Prefix))
                    throw new Exception("path not starting with prefix!");
                if (!p.Contains("?"))
                    throw new Exception("not found postfix");
                var lengthParts = p.Split("?");
                p = lengthParts[0];
                if (int.Parse(lengthParts[1]) != p.Length)
                    throw new Exception("Invalid length postfix in " + p);

                p = p.Substring((new OneDrive2PrefixContainerType()).Onedrive2Prefix.Length);
                if (p == "")
                    return new OneDrive2ItemLocation<OneDrive2PrefixContainerType>();
                OneDrive2ItemLocation<OneDrive2PrefixContainerType> result = new OneDrive2ItemLocation<OneDrive2PrefixContainerType>();
                var parts = p.Split("\\");
                if (parts.Length != 7)
                {
                    throw new Exception("Wrong number of parts in path " + p + " (" + parts.Length + ")");
                }
                result.User.Id = Decode(parts[0]);
                result.User.Name = Decode(parts[1]);
                result.Share.Id = Decode(parts[2]);
                result.Share.Name = Decode(parts[3]);
                result.Share.WebUrl = Decode(parts[4]);
                string localPath = Decode(parts[5]);
                if (localPath != "")
                {
                    var localPathParts = localPath.Split("/");
                    foreach (var lpp in localPathParts)
                    {
                        var lppsubParts = lpp.Split(":");
                        if (lppsubParts.Length != 2)
                            throw new Exception("Wrong number of subparts in in path " + p + ", " + lppsubParts);
                        result.LocalPath.Add(new Item { Id = Decode(lppsubParts[0]), Name = Decode(lppsubParts[1]) });
                    }
                }
                result.DriveId = Decode(parts[6]);

                return result;
            }

            private static string Decode(string p0)
            {
                return WebUtility.UrlDecode(p0);
            }

            public OneDrive2ItemLocation<OneDrive2PrefixContainerType> BuildLocalChildLocation(string name, string id, string parentReferenceDriveId)
            {
                //copy this:
                OneDrive2ItemLocation<OneDrive2PrefixContainerType> copy = OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(this.ToString());
                copy.LocalPath.Add(new Item { Name = name, Id = id });
                copy.DriveId = parentReferenceDriveId;
                return copy;
            }

            public static OneDrive2ItemLocation<OneDrive2PrefixContainerType> RootForUser(string accountUsername, string accountHomeAccountId)
            {
                OneDrive2ItemLocation<OneDrive2PrefixContainerType> loc = new OneDrive2ItemLocation<OneDrive2PrefixContainerType>
                {
                    User =
                    {
                        Id = accountHomeAccountId,
                        Name = accountUsername
                    }
                };

                return loc;
            }

            public OneDrive2ItemLocation<OneDrive2PrefixContainerType> BuildShare(string id, string name, string webUrl, string driveId)
            {
                OneDrive2ItemLocation<OneDrive2PrefixContainerType> copy = OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(this.ToString());
                copy.Share.Id = id;
                copy.Share.Name = name;
                copy.Share.WebUrl = webUrl;
                copy.DriveId = driveId;

                return copy;
            }


        }
    }
   


    public abstract class OneDrive2FileStorage<OneDrive2PrefixContainerType> : IFileStorage where OneDrive2PrefixContainerType: OneDrive2PrefixContainer, new()
    {

        public static IPublicClientApplication _publicClientApp = null;
        private string ClientID = "8374f801-0f55-407d-80cc-9a04fe86d9b2";
        
        
        public abstract IEnumerable<string> Scopes
        {
            get;
        }

        public OneDrive2FileStorage()
        {
            _publicClientApp = PublicClientApplicationBuilder.Create(ClientID)
                .WithRedirectUri($"msal{ClientID}://auth")
                .Build();
        }

        class PathItemBuilder
        {
            private readonly string _specialFolder;
            public IGraphServiceClient client;
            public OneDrive2ItemLocation<OneDrive2PrefixContainerType> itemLocation;

            public PathItemBuilder(string specialFolder)
            {
                _specialFolder = specialFolder;
            }


            public IDriveItemRequestBuilder getPathItem()
            {
                IDriveItemRequestBuilder pathItem;
                if (!hasShare())
                {
                    throw new Exception("Cannot get path item without share");
                }
                if ("me".Equals(itemLocation.Share.Id))
                {
                    if (_specialFolder == null)
                        pathItem = client.Me.Drive.Root;
                    else
                        pathItem = client.Me.Drive.Special[_specialFolder];
                    if (itemLocation.LocalPath.Any())
                    {
                        pathItem = pathItem.ItemWithPath(itemLocation.LocalPathString);
                    }
                }
                else
                {
                    if (!itemLocation.LocalPath.Any())
                    {
                        String webUrl = itemLocation.Share.WebUrl;
                        var encodedShareId = CalculateEncodedShareId(webUrl);
                        return client.Shares[encodedShareId].Root;
                    }
                    /*String webUrl = itemLocation.Share.WebUrl;
                    if ("".Equals(itemLocation.LocalPath) == false)
                    {
                        if (!webUrl.EndsWith("/")) webUrl += "/";
                        webUrl += itemLocation.LocalPath;
                    }
                    Android.Util.Log.Debug("KP2A","webUrl = " + Encoding.UTF8.GetBytes(webUrl));
                    //calculate shareid according to https://docs.microsoft.com/en-us/graph/api/shares-get?view=graph-rest-1.0&tabs=java
                    var encodedShareId = CalculateEncodedShareId(webUrl);
                    Android.Util.Log.Debug("KP2A", "encodedShareId = " + encodedShareId);
                    pathItem = client.Shares[encodedShareId].Root;
                    */
                    return client.Drives[itemLocation.DriveId].Items[itemLocation.LocalPath.Last().Id];
                }


                return pathItem;
            }

            private static string CalculateEncodedShareId(string webUrl)
            {
                String encodedShareId = "u!" + Base64.EncodeToString(Encoding.UTF8.GetBytes(webUrl),
                                                Base64Flags.NoPadding).Replace('/', '_').Replace('+', '_')
                                            .Replace("\n", ""); //encodeToString adds a newline character add the end - remove
                return encodedShareId;
            }

            public bool hasShare()
            {
                return !string.IsNullOrEmpty(itemLocation?.Share?.Id);
            }

            public bool hasOneDrivePath()
            {
                return itemLocation.LocalPath.Any();
            }
        }

        private string protocolId;

        protected string ProtocolId
        {
            get
            {
                if (protocolId == null)
                {
                    protocolId = (new OneDrive2PrefixContainerType()).Onedrive2ProtocolId;
                }
                return protocolId;
            }
        }

        public IEnumerable<string> SupportedProtocols
        {
            get { yield return ProtocolId; }
        }

        class GraphServiceClientWithState
        {
            public IGraphServiceClient Client { get; set; }
            public DateTime TokenExpiryDate { get; set; }
            public bool RequiresUserInteraction { get; set; }
        }

        readonly Dictionary<String /*userid*/, GraphServiceClientWithState> mClientByUser =
            new Dictionary<String /*userid*/, GraphServiceClientWithState>();

        private async Task<IGraphServiceClient> TryGetMsGraphClient(String path, bool tryConnect)
        {
            String userId = OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(path).User.Id;
            if (mClientByUser.ContainsKey(userId))
            {
                GraphServiceClientWithState clientWithState = mClientByUser[userId];
                if (!(clientWithState.RequiresUserInteraction || (clientWithState.TokenExpiryDate < DateTime.Now) || (clientWithState.Client == null)))
                    return clientWithState.Client;
            }
            if (tryConnect)
            {
                if (await TryLoginSilent(path) != null)
                {
                    return mClientByUser[userId].Client;
                }
            }
            return null;
        }


        private IGraphServiceClient BuildClient(AuthenticationResult authenticationResult)
        {

            logDebug("buildClient...");


            //DeviceCodeProvider authenticationProvider = new DeviceCodeProvider(_publicClientApp, Scopes);
            var authenticationProvider = new DelegateAuthenticationProvider(
                (requestMessage) =>
                {
                    var access_token = authenticationResult.AccessToken;
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", access_token);
                    return Task.FromResult(0);
                });

            GraphServiceClientWithState clientWithState = new GraphServiceClientWithState()
            {
                Client = new GraphServiceClient(authenticationProvider),
                RequiresUserInteraction = false,
                TokenExpiryDate = authenticationResult.ExpiresOn.LocalDateTime
            };
            


            if (authenticationResult.Account == null)
                throw new Exception("authenticationResult.Account == null!");
            mClientByUser[authenticationResult.Account.HomeAccountId.Identifier] = clientWithState;
       
            return clientWithState.Client;
        }


        
        private void logDebug(string str)
        {
            Log.Debug("KP2A", str);
        }
        

        protected abstract string SpecialFolder { get; }

        private async Task<PathItemBuilder> GetPathItemBuilder(String path)
        {
            PathItemBuilder result = new PathItemBuilder(SpecialFolder);

            
            result.itemLocation = OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(path);
            if (string.IsNullOrEmpty(result.itemLocation.User?.Name))
            {
                throw new Exception("path does not contain user");
            }
            
            result.client = await TryGetMsGraphClient(path, true);

            if (result.client == null)
                throw new Exception("Failed to connect or authenticate to OneDrive!");
            
            
            return result;

        }


        private Exception convertException(ClientException e)
        {

            if (e.IsMatch(GraphErrorCode.ItemNotFound.ToString()))
                return new FileNotFoundException(e.Message);
            if (e.Message.Contains("\n\n404 : ")
            ) //hacky solution to check for not found. errorCode was null in my tests so I had to find a workaround.
                return new FileNotFoundException(e.Message);
            return e;
        }


        private Exception convertException(Exception e)
        {
            if (e is ClientException)
                return convertException((ClientException)e);
            if (e is AggregateException aggregateException)
            {
                foreach (var inner in aggregateException.InnerExceptions)
                {
                    return convertException(inner);
                }
            }

            return e;
        }



        public bool UserShouldBackup
        {
            get { return false; }
        }

        public void Delete(IOConnectionInfo ioc)
        {
            try
            {
                
                Task.Run(async () =>
                {
                    PathItemBuilder pathItemBuilder = await GetPathItemBuilder(ioc.Path);
                    await pathItemBuilder.getPathItem()
                        .Request()
                        .DeleteAsync();
                }).Wait();
            }
            catch (Exception e)
            {
                throw convertException(e);
            }
        }

        public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
        {
            return false;
        }

        public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
        {
            return null;
        }

        public Stream OpenFileForRead(IOConnectionInfo ioc)
        {
            try
            {
                string path = ioc.Path;
                
                logDebug("openFileForRead. Path=" + path);
                Stream result = Task.Run(async () =>
                {
                    PathItemBuilder clientAndpath = await GetPathItemBuilder(path);
                    return await clientAndpath
                        .getPathItem()
                        .Content
                        .Request()
                        .GetAsync();
                }).Result;
                logDebug("ok");
                return result;

            }
            catch (Exception e)
            {
                throw convertException(e);
            }
        }



        class OneDrive2FileStorageWriteTransaction : IWriteTransaction
        {
            private readonly string _path;
            private readonly OneDrive2FileStorage<OneDrive2PrefixContainerType> _filestorage;
            private MemoryStream _memoryStream;

            public OneDrive2FileStorageWriteTransaction(string path, OneDrive2FileStorage<OneDrive2PrefixContainerType> filestorage)
            {
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
                _filestorage.UploadFile(_path, new MemoryStream(_memoryStream.ToArray()));

            }
        }

        private void UploadFile(string path, MemoryStream stream)
        {
            try
            {
                Task.Run(async () =>
                {
                    PathItemBuilder pathItemBuilder = await GetPathItemBuilder(path);
                    return await
                        pathItemBuilder
                            .getPathItem()
                            .Content
                            .Request()
                            .PutAsync<DriveItem>(stream);
                }).Wait();

            }
            catch (Exception e)
            {
                throw convertException(e);
            }
        }

        public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
        {
            return new OneDrive2FileStorageWriteTransaction(ioc.Path, this);
        }

        public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
        {
            return UrlUtil.StripExtension(
                GetFilename(IocToPath(ioc)));
        }

        public string GetFileExtension(IOConnectionInfo ioc)
        {
            return UrlUtil.GetExtension(OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(ioc.Path).LocalPathString);
        }

        private string GetFilename(string path)
        {
            string localPath = "/"+OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(path).LocalPathString;
            return localPath.Substring(localPath.LastIndexOf("/", StringComparison.Ordinal) + 1);
        }

        public bool RequiresCredentials(IOConnectionInfo ioc)
        {
            return false;
        }

        public void CreateDirectory(IOConnectionInfo parentIoc, string newDirName)
        {
            try
            {
                DriveItem driveItem = new DriveItem();
                driveItem.Name = newDirName;
                driveItem.Folder = new Folder();

                DriveItem res = Task.Run(async () =>
                {

                    PathItemBuilder pathItemBuilder = await GetPathItemBuilder(parentIoc.Path);
                    
                    return await pathItemBuilder.getPathItem()
                        .Children
                        .Request()
                        .AddAsync(driveItem);
                }).Result;


            }
            catch (Exception e)
            {
                throw convertException(e);
            }
        }

        public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
        {
            try
            {

                return Task.Run(async () => await ListContentsAsync(ioc)).Result;
            }
            catch (Exception e)
            {
                throw convertException(e);
            }
        }

        private async Task<IEnumerable<FileDescription>> ListContentsAsync(IOConnectionInfo ioc)
        {
            PathItemBuilder pathItemBuilder = await GetPathItemBuilder(ioc.Path);

            logDebug("listing files for " + ioc.Path);
            if (!pathItemBuilder.hasShare() && !pathItemBuilder.hasOneDrivePath())
            {
                logDebug("listing shares.");
                return await ListShares(pathItemBuilder.itemLocation, pathItemBuilder.client);
            }

            logDebug("listing regular children.");
            List<FileDescription> result = new List<FileDescription>();
            /*logDebug("parent before:" + parentPath);
            parentPath = parentPath.substring(getProtocolPrefix().length());
            logDebug("parent after: " + parentPath);*/

            IDriveItemChildrenCollectionPage itemsPage = await pathItemBuilder.getPathItem()
                .Children
                .Request()
                .GetAsync();
            while (true)
            {
                IList<DriveItem> items = itemsPage.CurrentPage;
                if (!items.Any())
                    return result;

                foreach (DriveItem i in items)
                {
                    var e = GetFileDescription(pathItemBuilder.itemLocation.BuildLocalChildLocation(i.Name, i.Id, i.ParentReference?.DriveId), i);
                    result.Add(e);
                }
                var nextPageReqBuilder = itemsPage.NextPageRequest;
                if (nextPageReqBuilder == null)
                    return result;
                itemsPage = Task.Run(async () => await nextPageReqBuilder.GetAsync()).Result;

            }
        }


        private FileDescription GetFileDescription(OneDrive2ItemLocation<OneDrive2PrefixContainerType> path, DriveItem i)
        {
            FileDescription e = new FileDescription();
            if (i.Size != null)
                e.SizeInBytes = (long)i.Size;
            else if ((i.RemoteItem != null) && (i.RemoteItem.Size != null))
                e.SizeInBytes = (long)i.RemoteItem.Size;

            e.DisplayName = i.Name;
            e.CanRead = e.CanWrite = true;
            e.Path = path.ToString();
            if (i.LastModifiedDateTime != null)
                e.LastModified = i.LastModifiedDateTime.Value.LocalDateTime;
            else if ((i.RemoteItem != null) && (i.RemoteItem.LastModifiedDateTime != null))
                e.LastModified = i.RemoteItem.LastModifiedDateTime.Value.LocalDateTime;
            e.IsDirectory = (i.Folder != null) || ((i.RemoteItem != null) && (i.RemoteItem.Folder != null));
            return e;
        }

        public FileDescription GetFileDescription(IOConnectionInfo ioc)
        {
            try
            {
                return Task.Run(async() => await GetFileDescriptionAsync(ioc)).Result;
            }
            catch (Exception e)
            {
                throw convertException(e);
            }
        }

        private async Task<FileDescription> GetFileDescriptionAsync(IOConnectionInfo ioc)
        {
            string filename = ioc.Path;
            PathItemBuilder pathItemBuilder = await GetPathItemBuilder(filename);

            if (!pathItemBuilder.itemLocation.LocalPath.Any()
                && !pathItemBuilder.hasShare())
            {
                FileDescription rootEntry = new FileDescription();
                rootEntry.CanRead = rootEntry.CanWrite = true;
                rootEntry.Path = filename;
                rootEntry.DisplayName = pathItemBuilder.itemLocation.User.Name;
                rootEntry.IsDirectory = true;
                return rootEntry;
            }

            IDriveItemRequestBuilder pathItem = pathItemBuilder.getPathItem();

            DriveItem item = await pathItem.Request().GetAsync();
            return GetFileDescription(pathItemBuilder.itemLocation, item);
        }

        public bool RequiresSetup(IOConnectionInfo ioConnection)
        {
            return false;
        }

        public string IocToPath(IOConnectionInfo ioc)
        {
            return ioc.Path;
        }

        public void StartSelectFile(IFileStorageSetupInitiatorActivity activity, bool isForSave, int requestCode,
            string protocolId)
        {
            String path = ProtocolId+ "://";
            activity.StartSelectFileProcess(IOConnectionInfo.FromPath(path), isForSave, requestCode);
        }


        private async Task<bool> IsConnectedAsync(string path, bool tryConnect)
        {
            try
            {
                logDebug("isConnected? " + path);

                return (await TryGetMsGraphClient(path, tryConnect)) != null;
            }
            catch (Exception e)
            {
                logDebug("exception in isConnected: " + e);
                return false;
            }

        }

        public bool IsConnected(string path)
        {
            return Task.Run(async () => await IsConnectedAsync(path, false)).Result;
        }

        public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode,
            bool alwaysReturnSuccess)
        {
            if (IsConnected(ioc.Path))
            {
                Intent intent = new Intent();
                intent.PutExtra(FileStorageSetupDefs.ExtraPath, ioc.Path);
                activity.OnImmediateResult(requestCode, (int)FileStorageResults.FileUsagePrepared, intent);
            }
            else
            {
                activity.StartFileUsageProcess(ioc, requestCode, alwaysReturnSuccess);
            }

        }

        public void PrepareFileUsage(Context ctx, IOConnectionInfo ioc)
        {
            if (!Task.Run(async() => await IsConnectedAsync(ioc.Path, true)).Result)
            {
                throw new Exception("MsGraph login required");
            }
        }

        public void OnCreate(IFileStorageSetupActivity activity, Bundle savedInstanceState)
        {

        }

        public void OnResume(IFileStorageSetupActivity activity)
        {

        }

        protected void FinishActivityWithSuccess(
            IFileStorageSetupActivity setupActivity)
        {
            //Log.d("KP2AJ", "Success with authenticating!");
            Activity activity = (Activity)setupActivity;

            if (setupActivity.ProcessName
                .Equals(FileStorageSetupDefs.ProcessNameFileUsageSetup))
            {
                Intent data = new Intent();
                data.PutExtra(FileStorageSetupDefs.ExtraIsForSave, setupActivity.IsForSave);
                data.PutExtra(FileStorageSetupDefs.ExtraPath, setupActivity.Ioc.Path);
                activity.SetResult((Result)FileStorageResults.FileUsagePrepared, data);
                activity.Finish();
                return;
            }
            if (setupActivity.ProcessName.Equals(FileStorageSetupDefs.ProcessNameSelectfile))
            {
                Intent data = new Intent();

                String path = setupActivity.State.GetString(FileStorageSetupDefs.ExtraPath);
                if (path != null)
                    data.PutExtra(FileStorageSetupDefs.ExtraPath, path);
                activity.SetResult((Result)FileStorageResults.FileChooserPrepared, data);
                activity.Finish();
                return;
            }

            logDebug("Unknown process: " + setupActivity.ProcessName);

        }

        public async void OnStart(IFileStorageSetupActivity activity)
        {

            if (activity.ProcessName.Equals(FileStorageSetupDefs.ProcessNameFileUsageSetup))
                activity.State.PutString(FileStorageSetupDefs.ExtraPath, activity.Ioc.Path);
            string rootPathForUser = await TryLoginSilent(activity.Ioc.Path);
            if (rootPathForUser != null)
            {
                FinishActivityWithSuccess(activity, rootPathForUser);
            }

            try
            {

                logDebug("try interactive");
                AuthenticationResult res = await _publicClientApp.AcquireTokenInteractive(Scopes)
                    .WithParentActivityOrWindow((Activity)activity)
                    .ExecuteAsync();
                logDebug("ok interactive");
                BuildClient(res);
                FinishActivityWithSuccess(activity, BuildRootPathForUser(res));


            }
            catch (Exception e)
            {
                logDebug("authenticating not successful: " + e);
                Intent data = new Intent();
                data.PutExtra(FileStorageSetupDefs.ExtraErrorMessage, "authenticating not successful");
                ((Activity)activity).SetResult(Result.Canceled, data);
                ((Activity)activity).Finish();
            }

        }

        private async Task<string> TryLoginSilent(string iocPath)
        {

            IAccount account = null;
            try
            {
                
                if (IsConnected(iocPath))
                {
                    return iocPath;
                }
                String userId = OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(iocPath).User?.Id;
                logDebug("needs acquire token");
                logDebug("trying silent login " + iocPath);

                account = Task.Run(async () => await _publicClientApp.GetAccountAsync(userId)).Result;
                logDebug("getting user ok.");

            }
            catch (Exception e)
            {
                logDebug(e.ToString());
            }
            if (account != null)
            {
                try
                {

                    logDebug("AcquireTokenSilent...");
                    AuthenticationResult authResult = await _publicClientApp.AcquireTokenSilent(Scopes, account)
                        .ExecuteAsync();

                    logDebug("AcquireTokenSilent ok.");
                    BuildClient(authResult);
                    /*User me = await graphClient.Me.Request().WithForceRefresh(true).GetAsync();
                    logDebug("received name " + me.DisplayName);*/

                    return BuildRootPathForUser(authResult);

                }
                catch (MsalUiRequiredException ex)
                {

                    GraphServiceClientWithState clientWithState = new GraphServiceClientWithState()
                    {
                        Client = null,
                        RequiresUserInteraction = true
                    };


                    mClientByUser[account.HomeAccountId.Identifier] = clientWithState;
                    logDebug("ui required");
                    return null;
                }
                catch (Exception ex)
                {
                    logDebug("silent login failed: " + ex.ToString());
                    return null;
                }
            }
            return null;
        }

        string BuildRootPathForUser(AuthenticationResult res)
        {
            return OneDrive2ItemLocation<OneDrive2PrefixContainerType>.RootForUser(res.Account.Username, res.Account.HomeAccountId.Identifier).ToString();
        }

        
        private void FinishActivityWithSuccess(IFileStorageSetupActivity activity, string rootPathForUser)
        {
            activity.State.PutString(FileStorageSetupDefs.ExtraPath, rootPathForUser);
            FinishActivityWithSuccess(activity);
        }

        public void OnActivityResult(IFileStorageSetupActivity activity, int requestCode, int resultCode, Intent data)
        {
            AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(requestCode, (Result)resultCode,
                data);
        }

        public string GetDisplayName(IOConnectionInfo ioc)
        {
            try
            {
                var itemLocation = OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(ioc.Path);
                string result = ProtocolId+ "://";
                if (!string.IsNullOrEmpty(itemLocation.User?.Id))
                {
                    result += itemLocation.User?.Name;
                    if (itemLocation.Share != null)
                    {
                        result += "/" + (itemLocation.Share?.Name ?? itemLocation.Share?.Id);

                        if (itemLocation.LocalPath.Any())
                        {
                            result += "/" + itemLocation.LocalPathString;
                        }
                    }

                }
                return result;
            }
            catch (Exception e)
            {
                Kp2aLog.Log("Invalid OneDrive location " + ioc.Path +
                            ". Note that SprEnging expressions like {DB_PATH} are not supported with OneDrive!");
                return ProtocolId + "://(invalid)";
            }
            
        }


        private async Task<List<FileDescription>> ListShares(OneDrive2ItemLocation<OneDrive2PrefixContainerType> parentPath, IGraphServiceClient client)
        {

            List<FileDescription> result = new List<FileDescription>();

            
            DriveItem root = await client.Me.Drive.Root.Request().GetAsync();
            FileDescription myEntry = GetFileDescription(parentPath.BuildShare("me","me","me", root.ParentReference?.DriveId), root);
            myEntry.DisplayName = MyOneDriveDisplayName;

            result.Add(myEntry);

            if (!CanListShares)
                return result;



            IDriveSharedWithMeCollectionPage sharedWithMeCollectionPage = await client.Me.Drive.SharedWithMe().Request().GetAsync();

            while (true)
            {
                IList<DriveItem> sharedWithMeItems = sharedWithMeCollectionPage.CurrentPage;
                if (!sharedWithMeItems.Any())
                    break;

                foreach (DriveItem i in sharedWithMeItems)
                {
                    FileDescription sharedFileEntry = GetFileDescription(parentPath.BuildShare(i.Id, i.Name, i.WebUrl, i.ParentReference?.DriveId), i);
                    result.Add(sharedFileEntry);

                }
                var b = sharedWithMeCollectionPage.NextPageRequest;
                if (b == null) break;
                sharedWithMeCollectionPage = await b.GetAsync();
            }
            return result;
        }

        protected virtual string MyOneDriveDisplayName { get { return "My OneDrive"; } }

        public abstract bool CanListShares { get;  }

        DriveItem TryFindFile(PathItemBuilder parent, string filename)
        {
            IDriveItemChildrenCollectionPage itemsPage = Task.Run(async () => await parent.getPathItem()
                .Children
                .Request()
                .GetAsync()).Result;
            while (true)
            {
                IList<DriveItem> items = itemsPage.CurrentPage;
                if (!items.Any())
                    return null;

                foreach (DriveItem i in items)
                {
                    if (i.Name == filename)
                        return i;
                }
                var nextPageReqBuilder = itemsPage.NextPageRequest;
                if (nextPageReqBuilder == null)
                    return null;
                itemsPage = Task.Run(async () => await nextPageReqBuilder.GetAsync()).Result;

            }

        }


        public string CreateFilePath(string parent, string newFilename)
        {
            try
            {
                return Task.Run(async() => await CreateFilePathAsync(parent, newFilename)).Result;
            }
            catch (Exception e)
            {
                throw convertException(e);
            }
            
        }

        private async Task<string> CreateFilePathAsync(string parent, string newFilename)
        {
            DriveItem driveItem = new DriveItem();
            driveItem.Name = newFilename;
            driveItem.File = new File();
            PathItemBuilder pathItemBuilder = await GetPathItemBuilder(parent);

            //see if such a file exists already:
            var item = TryFindFile(pathItemBuilder, newFilename);
            if (item != null)
            {
                return pathItemBuilder.itemLocation.BuildLocalChildLocation(item.Name, item.Id, item.ParentReference?.DriveId)
                    .ToString();
            }
            //doesn't exist. Create:
            logDebug("building request for " + pathItemBuilder.itemLocation);

            DriveItem res = await pathItemBuilder.getPathItem()
                .Children
                .Request()
                .AddAsync(driveItem);

            return pathItemBuilder.itemLocation.BuildLocalChildLocation(res.Name, res.Id, res.ParentReference?.DriveId)
                .ToString();
        }

        public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
        {
            return IOConnectionInfo.FromPath(OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(ioc.Path).Parent.ToString());
        }

        public IOConnectionInfo GetFilePath(IOConnectionInfo folderPath, string filename)
        {
            return IOConnectionInfo.FromPath(CreateFilePath(folderPath.Path, filename));
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

    public class OneDrive2FullFileStorage: OneDrive2FileStorage<OneDrive2FullPrefixContainer> 
    {
        public override IEnumerable<string> Scopes
        {
            get
            {
                return new List<string>
                    {
                        "https://graph.microsoft.com/Files.ReadWrite",
                        "https://graph.microsoft.com/Files.ReadWrite.All"
                    };
                
            }
        }

        public override bool CanListShares { get { return true; } }
        protected override string SpecialFolder { get { return null; } }
    }


    public class OneDrive2MyFilesFileStorage : OneDrive2FileStorage<OneDrive2MyFilesPrefixContainer>
    {
        public override IEnumerable<string> Scopes
        {
            get
            {
                return new List<string>
                {
                    "https://graph.microsoft.com/Files.ReadWrite"
                };

            }
        }
        public override bool CanListShares { get { return false; } }
        protected override string SpecialFolder { get { return null; } }
    }


    public class OneDrive2AppFolderFileStorage : OneDrive2FileStorage<OneDrive2AppFolderPrefixContainer>
    {
        public override IEnumerable<string> Scopes
        {
            get
            {
                return new List<string>
                {
                    "https://graph.microsoft.com/Files.ReadWrite.AppFolder"
                };

            }
        }

        protected override string SpecialFolder { get { return "approot"; } }
        public override bool CanListShares { get { return false; } }
        protected override string MyOneDriveDisplayName { get { return "Keepass2Android App Folder"; } }
    }
}