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
using KeePassLib.Serialization;
using KeePassLib.Utility;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Exception = System.Exception;
using String = System.String;

namespace keepass2android.Io
{
    public class OneDrive2FileStorage : IFileStorage
    {

        public static IPublicClientApplication _publicClientApp = null;
        private string ClientID = "8374f801-0f55-407d-80cc-9a04fe86d9b2";
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
        public class OneDrive2ItemLocation
        {
            private const string Onedrive2Prefix = "onedrive2://";
            public User User { get; set; } = new User();
            public Share Share { get; set; } = new Share();
            public string DriveId { get; set; }

            public List<Item> LocalPath { get; set; } = new List<Item>();
            public string LocalPathString { get { return string.Join("/", LocalPath.Select(i => i.Name)); }}

            public OneDrive2ItemLocation Parent
            {
                get
                {
                    OneDrive2ItemLocation copy = OneDrive2ItemLocation.FromString(this.ToString());
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
                string path = Onedrive2Prefix + string.Join("\\", (new List<string> { User.Id, User.Name,
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

            public static OneDrive2ItemLocation FromString(string p)
            {
                if ((p == null) || (p == Onedrive2Prefix))
                    return new OneDrive2ItemLocation();

                if (!p.StartsWith(Onedrive2Prefix))
                    throw new Exception("path not starting with prefix!");
                if (!p.Contains("?"))
                    throw new Exception("not found postfix");
                var lengthParts = p.Split("?");
                p = lengthParts[0];
                if (int.Parse(lengthParts[1]) != p.Length)
                    throw new Exception("Invalid length postfix in " + p);

                p = p.Substring(Onedrive2Prefix.Length);
                if (p == "")
                    return new OneDrive2ItemLocation();
                OneDrive2ItemLocation result = new OneDrive2ItemLocation();
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
                        result.LocalPath.Add(new Item {Id = Decode(lppsubParts[0]), Name = Decode(lppsubParts[1])});
                    }
                }
                result.DriveId = Decode(parts[6]);

                return result;
            }

            private static string Decode(string p0)
            {
                return WebUtility.UrlDecode(p0);
            }

            public OneDrive2ItemLocation BuildLocalChildLocation(string name, string id, string parentReferenceDriveId)
            {
                //copy this:
                OneDrive2ItemLocation copy = OneDrive2ItemLocation.FromString(this.ToString());
                copy.LocalPath.Add(new Item { Name = name, Id = id});
                copy.DriveId = parentReferenceDriveId;
                return copy;
            }

            public static OneDrive2ItemLocation RootForUser(string accountUsername, string accountHomeAccountId)
            {
                OneDrive2ItemLocation loc = new OneDrive2ItemLocation
                {
                    User =
                    {
                        Id = accountHomeAccountId,
                        Name = accountUsername
                    }
                };

                return loc;
            }

            public OneDrive2ItemLocation BuildShare(string id, string name, string webUrl, string driveId)
            {
                OneDrive2ItemLocation copy = OneDrive2ItemLocation.FromString(this.ToString());
                copy.Share.Id = id;
                copy.Share.Name = name;
                copy.Share.WebUrl = webUrl;
                copy.DriveId = driveId;
               
                return copy;
            }

            
        }

        public static IEnumerable<string> Scopes
        {
            get
            {
                return new List<string>
                {
                    "https://graph.microsoft.com/Files.Read",
                    "https://graph.microsoft.com/Files.Read.All",
                    "https://graph.microsoft.com/Files.ReadWrite",
                    "https://graph.microsoft.com/Files.ReadWrite.All",
                    "https://graph.microsoft.com/User.Read"
                };
            }
        }

        public OneDrive2FileStorage()
        {
            _publicClientApp = PublicClientApplicationBuilder.Create(ClientID)
                .WithRedirectUri($"msal{ClientID}://auth")
                .Build();
        }

        class PathItemBuilder
        {
            public IGraphServiceClient client;
            public OneDrive2ItemLocation itemLocation;

            
            public IDriveItemRequestBuilder getPathItem()
            {
                IDriveItemRequestBuilder pathItem;
                if (!hasShare())
                {
                    throw new Exception("Cannot get path item without share");
                }
                if ("me".Equals(itemLocation.Share.Id))
                {
                    pathItem = client.Me.Drive.Root;
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


        private const string protocolId = "onedrive2";

        private string getProtocolId()
        {
            return protocolId;
        }

        public IEnumerable<string> SupportedProtocols
        {
            get { yield return protocolId; }
        }

        Dictionary<String /*userid*/, IGraphServiceClient> mClientByUser =
            new Dictionary<String /*userid*/, IGraphServiceClient>();

        private IGraphServiceClient tryGetMsGraphClient(String path)
        {
            String userId = OneDrive2ItemLocation.FromString(path).User.Id;
            if (mClientByUser.ContainsKey(userId))
                return mClientByUser[userId];
            return null;
        }


        private IGraphServiceClient buildClient(AuthenticationResult authenticationResult)
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

            GraphServiceClient graphClient = new GraphServiceClient(authenticationProvider);


            if (authenticationResult.Account == null)
                throw new Exception("authenticationResult.Account == null!");
            mClientByUser[authenticationResult.Account.HomeAccountId.Identifier] = graphClient;
       
            return graphClient;
        }


        
        private void logDebug(string str)
        {
            Log.Debug("KP2A", str);
        }
        



        private PathItemBuilder GetPathItemBuilder(String path)
        {
            PathItemBuilder result = new PathItemBuilder();

            
            result.itemLocation = OneDrive2ItemLocation.FromString(path);
            if (string.IsNullOrEmpty(result.itemLocation.User?.Name))
            {
                throw new Exception("path does not contain user");
            }
            result.client = null;
            if (!mClientByUser.TryGetValue(result.itemLocation.User.Id, out result.client))
            {
                 throw new Exception("failed to get client for " + result.itemLocation.User.Id);
            }

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
                PathItemBuilder pathItemBuilder = GetPathItemBuilder(ioc.Path);
                Task.Run(async () => await pathItemBuilder.getPathItem()
                    .Request()
                    .DeleteAsync()).Wait();
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
                PathItemBuilder clientAndpath = GetPathItemBuilder(path);
                logDebug("openFileForRead. Path=" + path);
                Stream result = Task.Run(async () => await clientAndpath
                    .getPathItem()
                    .Content
                    .Request()
                    .GetAsync()).Result;
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
            private readonly OneDrive2FileStorage _filestorage;
            private MemoryStream _memoryStream;

            public OneDrive2FileStorageWriteTransaction(string path, OneDrive2FileStorage filestorage)
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

                PathItemBuilder pathItemBuilder = GetPathItemBuilder(path);
                Task.Run(async () => await
                    pathItemBuilder
                        .getPathItem()
                        .Content
                        .Request()
                        .PutAsync<DriveItem>(stream)).Wait();

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
            return UrlUtil.GetExtension(OneDrive2ItemLocation.FromString(ioc.Path).LocalPathString);
        }

        private string GetFilename(string path)
        {
            string localPath = "/"+OneDrive2ItemLocation.FromString(path).LocalPath;
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

                PathItemBuilder pathItemBuilder = GetPathItemBuilder(parentIoc.Path);


                logDebug("building request for " + pathItemBuilder.itemLocation);

                DriveItem res = Task.Run(async () => await pathItemBuilder.getPathItem()
                    .Children
                    .Request()
                    .AddAsync(driveItem)).Result;


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
                
                PathItemBuilder pathItemBuilder = GetPathItemBuilder(ioc.Path);

                logDebug("listing files for " + ioc.Path);
                if (!pathItemBuilder.hasShare() && !pathItemBuilder.hasOneDrivePath())
                {
                    logDebug("listing shares.");
                    return ListShares(pathItemBuilder.itemLocation, pathItemBuilder.client);
                }

                logDebug("listing regular children.");
                List<FileDescription> result = new List<FileDescription>();
                /*logDebug("parent before:" + parentPath);
                parentPath = parentPath.substring(getProtocolPrefix().length());
                logDebug("parent after: " + parentPath);*/

                IDriveItemChildrenCollectionPage itemsPage = Task.Run(async () => await pathItemBuilder.getPathItem()
                    .Children
                    .Request()
                    .GetAsync()).Result;
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
            catch (Exception e)
            {
                throw convertException(e);
            }
        }


        private FileDescription GetFileDescription(OneDrive2ItemLocation path, DriveItem i)
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
                string filename = ioc.Path;
                PathItemBuilder pathItemBuilder = GetPathItemBuilder(filename);

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

                DriveItem item = Task.Run(async () => await pathItem.Request().GetAsync()).Result;
                return GetFileDescription(pathItemBuilder.itemLocation, item);
            }
            catch (Exception e)
            {
                throw convertException(e);
            }
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
            String path = getProtocolId() + "://";
            activity.StartSelectFileProcess(IOConnectionInfo.FromPath(path), isForSave, requestCode);
        }


        private bool isConnected(String path)
        {
            try
            {
                logDebug("isConnected? " + path);

                return tryGetMsGraphClient(path) != null;
            }
            catch (Exception e)
            {
                logDebug("exception in isConnected: " + e);
                return false;
            }

        }


        public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode,
            bool alwaysReturnSuccess)
        {
            if (isConnected(ioc.Path))
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
            if (!isConnected(ioc.Path))
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

        protected void finishActivityWithSuccess(
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

            IAccount account = null;
            try
            {
                String userId = OneDrive2ItemLocation.FromString(activity.Ioc.Path).User?.Id;
                if (mClientByUser.ContainsKey(userId))
                {
                    finishActivityWithSuccess(activity);
                    return;
                }

                logDebug("needs acquire token");
                logDebug("trying silent login " + activity.Ioc.Path);

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
                    var graphClient = buildClient(authResult);
                    /*User me = await graphClient.Me.Request().WithForceRefresh(true).GetAsync();
                    logDebug("received name " + me.DisplayName);*/

                    finishActivityWithSuccess(activity, authResult);
                    return;

                }
                catch (MsalUiRequiredException ex)
                {
                    logDebug("ui required");

                }
            }
            try
            {

                logDebug("try interactive");
                AuthenticationResult res = await _publicClientApp.AcquireTokenInteractive(Scopes)
                    .WithParentActivityOrWindow((Activity)activity)
                    .ExecuteAsync();
                logDebug("ok interactive");
                buildClient(res);
                finishActivityWithSuccess(activity, res);


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

        string buildRootPathForUser(AuthenticationResult res)
        {
            return OneDrive2ItemLocation.RootForUser(res.Account.Username, res.Account.HomeAccountId.Identifier).ToString();
        }

        
        private void finishActivityWithSuccess(IFileStorageSetupActivity activity, AuthenticationResult authResult)
        {
            activity.State.PutString(FileStorageSetupDefs.ExtraPath, buildRootPathForUser(authResult));
            finishActivityWithSuccess(activity);
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
                var itemLocation = OneDrive2ItemLocation.FromString(ioc.Path);
                string result = getProtocolId() + "://";
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
                return e.ToString(); //TODO throw
            }
            
        }


        private List<FileDescription> ListShares(OneDrive2ItemLocation parentPath, IGraphServiceClient client)
        {
            List<FileDescription> result = new List<FileDescription>();
            
            DriveItem root = Task.Run(async () => await client.Me.Drive.Root.Request().GetAsync()).Result;
            FileDescription myEntry = GetFileDescription(parentPath.BuildShare("me","me","me", root.ParentReference?.DriveId), root);
            myEntry.DisplayName = "My OneDrive";

            
            result.Add(myEntry);

            IDriveSharedWithMeCollectionPage sharedWithMeCollectionPage =
                Task.Run(async () => await client.Me.Drive.SharedWithMe().Request().GetAsync()).Result;

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
                sharedWithMeCollectionPage = Task.Run(async () => await b.GetAsync()).Result;
            }
            return result;
        }

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
                DriveItem driveItem = new DriveItem();
                driveItem.Name = newFilename;

                PathItemBuilder pathItemBuilder = GetPathItemBuilder(parent);

                //see if such a file exists already:
                var item = TryFindFile(pathItemBuilder, newFilename);
                if (item != null)
                {
                    return pathItemBuilder.itemLocation.BuildLocalChildLocation(item.Name, item.Id, item.ParentReference?.DriveId).ToString();

                }
                //doesn't exist. Create:
                logDebug("building request for " + pathItemBuilder.itemLocation);

                DriveItem res = Task.Run(async () => await pathItemBuilder.getPathItem()
                    .Children
                    .Request()
                    .AddAsync(driveItem)).Result;

                return pathItemBuilder.itemLocation.BuildLocalChildLocation(res.Name, res.Id, res.ParentReference?.DriveId).ToString();


            }
            catch (Exception e)
            {
                throw convertException(e);
            }
            
        }

        public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
        {
            return IOConnectionInfo.FromPath(OneDrive2ItemLocation.FromString(ioc.Path).Parent.ToString());
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
}