using System.Net;
using System.Reflection;
using System.Text;
using Android.Content;
using Android.Util;
using keepass2android.Io.ItemLocation;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

//there are several "DriveItemItemRequestBuilder" classes in different namespaces. Unfortunately they do not share a common interface anymore.
//Prefix the aliases with Kp2a to indicate that they are non-official aliases.
using Kp2aDriveItemRequestBuilder =    Microsoft.Graph.Drives.Item.Items.Item.DriveItemItemRequestBuilder;
using Kp2aSharedDriveItemRequestBuilder =  Microsoft.Graph.Shares.Item.Items.Item.DriveItemItemRequestBuilder;
using Kp2aSpecialDriveItemRequestBuilder = Microsoft.Graph.Drives.Item.Special.Item.DriveItemItemRequestBuilder;
//NOTE: even though CustomDriveItemItemRequestBuilder derives from Kp2aDriveItemRequestBuilder, we cannot use polymorphism here because the methods are declared with "new".
//If you cast assign an CustomDriveItemItemRequestBuilder object to a variable declared as Kp2aDriveItemRequestBuilder and then call a method on it, it will fail.

using Exception = System.Exception;
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

        public class OneDrive2ItemLocation<OneDrive2PrefixContainerType> where OneDrive2PrefixContainerType : OneDrive2PrefixContainer, new()
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
                    OneDrive2ItemLocation<OneDrive2PrefixContainerType> copy = OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(this.ToString());
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



    public abstract class OneDrive2FileStorage<OneDrive2PrefixContainerType> : IFileStorage where OneDrive2PrefixContainerType : OneDrive2PrefixContainer, new()
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
            public GraphServiceClient client;
            public OneDrive2ItemLocation<OneDrive2PrefixContainerType> itemLocation;
            public bool verbose;

            public PathItemBuilder(string specialFolder)
            {
                _specialFolder = specialFolder;
            }

            /// <summary>
            /// Wraps the different DriveItemRequestBuilder classes and allows accessing the different types easily
            /// </summary>
            public class DriveItemRequests
            {

                public class DriveItemRequestsResult<T>
                {
                    private readonly DriveItemRequests _req;

                    public DriveItemRequestsResult(DriveItemRequests req)
                    {
                        _req = req;
                    }

                    public Task<T?> Result { get; set; }


                    public DriveItemRequestsResult<T> ForSharedDriveItemRequests(Func<Kp2aSharedDriveItemRequestBuilder, Task<T?>> action)
                    {
                        if (_req.SharedDriveItemRequestBuilder != null)
                        {
                            Result = action(_req.SharedDriveItemRequestBuilder);
                        }
                        return this;
                    }
                    public DriveItemRequestsResult<T> ForDriveItemRequests(Func<Kp2aDriveItemRequestBuilder, Task<T?>> action)
                    {
                        if (_req.DriveItemRequestBuilder != null)
                        {
                            Result = action(_req.DriveItemRequestBuilder);
                        }
                        return this;
                    }

                    public DriveItemRequestsResult<T> ForCustomDriveItemRequests(Func<CustomDriveItemItemRequestBuilder, Task<T?>> action)
                    {
                        if (_req.CustomDriveItemRequestBuilder != null)
                        {
                            Result = action(_req.CustomDriveItemRequestBuilder);
                        }
                        return this;
                    }

                    public DriveItemRequestsResult<T> ForSpecialDriveItemRequests(Func<Kp2aSpecialDriveItemRequestBuilder, Task<T?>> action)
                    {
                        if (_req.SpecialDriveItemRequestBuilder != null)
                        {
                            Result = action(_req.SpecialDriveItemRequestBuilder);
                        }
                        return this;
                    }

                }

                public class DriveItemRequestsAsyncTask
                {
                    private readonly DriveItemRequests _req;

                    public DriveItemRequestsAsyncTask(DriveItemRequests req)
                    {
                        _req = req;
                        Task = Task.CompletedTask;
                    }

                    public Task Task { get; private set; }

                    public DriveItemRequestsAsyncTask ForSharedDriveItemRequests(Func<Kp2aSharedDriveItemRequestBuilder, Task> action)
                    {
                        if (_req.SharedDriveItemRequestBuilder != null)
                        {
                            Task = action(_req.SharedDriveItemRequestBuilder);
                        }
                        return this;
                    }
                    public DriveItemRequestsAsyncTask ForDriveItemRequests(Func<Kp2aDriveItemRequestBuilder, Task> action)
                    {
                        if (_req.DriveItemRequestBuilder != null)
                        {
                            Task = action(_req.DriveItemRequestBuilder);
                        }
                        return this;
                    }

                    public DriveItemRequestsAsyncTask ForCustomDriveItemRequests(Func<CustomDriveItemItemRequestBuilder, Task> action)
                    {
                        if (_req.CustomDriveItemRequestBuilder != null)
                        {
                            Task = action(_req.CustomDriveItemRequestBuilder);
                        }
                        return this;
                    }


                    public DriveItemRequestsAsyncTask ForSpecialDriveItemRequests(Func<Kp2aSpecialDriveItemRequestBuilder, Task> action)
                    {
                        if (_req.SpecialDriveItemRequestBuilder != null)
                        {
                            Task = action(_req.SpecialDriveItemRequestBuilder);
                        }
                        return this;
                    }
                    

                }

                public Kp2aSharedDriveItemRequestBuilder? SharedDriveItemRequestBuilder { get; set; }
                public Kp2aSpecialDriveItemRequestBuilder? SpecialDriveItemRequestBuilder { get; set; }

                public Kp2aDriveItemRequestBuilder? DriveItemRequestBuilder { get; set; }

                public CustomDriveItemItemRequestBuilder? CustomDriveItemRequestBuilder { get; set; }


                public DriveItemRequests ForDriveItemRequests(Action<Kp2aDriveItemRequestBuilder> action)
                {
                    if (DriveItemRequestBuilder != null)
                    {
                        action(DriveItemRequestBuilder);
                    }
                    return this;
                }

                public DriveItemRequests ForCustomDriveItemRequests(Action<CustomDriveItemItemRequestBuilder> action)
                {
                    if (CustomDriveItemRequestBuilder != null)
                    {
                        action(CustomDriveItemRequestBuilder);
                    }
                    return this;
                }

                public DriveItemRequests ForSpecialDriveItemRequests(Action<Kp2aSpecialDriveItemRequestBuilder> action)
                {
                    if (SpecialDriveItemRequestBuilder!= null)
                    {
                        action(SpecialDriveItemRequestBuilder );
                    }
                    return this;
                }

                public DriveItemRequests ForSharedDriveItemRequests(Action<Kp2aSharedDriveItemRequestBuilder> action)
                {
                    if (SharedDriveItemRequestBuilder != null)
                    {
                        action(SharedDriveItemRequestBuilder);
                    }
                    return this;
                }

                public DriveItemRequestsResult<T> ToAsyncResult<T>()
                {
                    return new DriveItemRequestsResult<T>(this);
                }

                public DriveItemRequestsAsyncTask ToAsyncTask()
                {
                    return new DriveItemRequestsAsyncTask(this);
                }
            

            };


            public async Task<DriveItemRequests> BuildPathItemAsync()
            {
                Kp2aLog.Log("buildPathItem for " + itemLocation.ToString());
                DriveItemRequests result = new DriveItemRequests();

                if (!hasShare())
                {
                    throw new Exception("Cannot get path item without share");
                }
                if ("me".Equals(itemLocation.Share.Id))
                {
                    if (verbose) Kp2aLog.Log("Path share is me");


                    if (_specialFolder == null)
                    {
                        if (verbose) Kp2aLog.Log("No special folder. Use drive root.");

                        if (itemLocation.LocalPath.Any())
                        {
                            if (verbose) Kp2aLog.Log("LocalPath = " + itemLocation.LocalPathString);
                            result.CustomDriveItemRequestBuilder = client.Drives[itemLocation.DriveId].Root
                                .ItemWithPath(itemLocation.LocalPathString);
                        }
                        else
                        {
                            
                            result.DriveItemRequestBuilder = client.Drives[itemLocation.DriveId].Items["root"];
                            
                        }

                        
                    }
                    else
                    {
                        if (verbose) Kp2aLog.Log("Special folder = " + _specialFolder);
                        
                        result.SpecialDriveItemRequestBuilder = client.Drives[itemLocation.DriveId].Special[_specialFolder];
                        if (itemLocation.LocalPath.Any())
                        {

                            var child = (await result.SpecialDriveItemRequestBuilder
                                .GetAsync(configuration => configuration.QueryParameters.Expand = new[] { "children" }))
                                .Children.FirstOrDefault(c => c.Name == itemLocation.LocalPathString);


                            foreach (var di in client.Drives[itemLocation.DriveId].Items.GetAsync().Result.Value)
                            {
                                Kp2aLog.Log("DriveItem: " + di.Name);

                            }


                            result.DriveItemRequestBuilder = client.Drives[itemLocation.DriveId].Items[child.Id];

                        }
                    }

                }
                else
                {
                    if (verbose) Kp2aLog.Log("Path share is not me");
                    if (!itemLocation.LocalPath.Any())
                    {
                        String webUrl = itemLocation.Share.WebUrl;
                        if (verbose) Kp2aLog.Log("Share WebUrl = " + webUrl);
                        var encodedShareId = CalculateEncodedShareId(webUrl);
                        result.SharedDriveItemRequestBuilder = client.Shares[encodedShareId].Items["root"];
                        return result;
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
                    if (verbose) Kp2aLog.Log("Using driveId=" + itemLocation.DriveId + " and item id=" + itemLocation.LocalPath.Last().Id);
                    result.DriveItemRequestBuilder = client.Drives[itemLocation.DriveId].Items[itemLocation.LocalPath.Last().Id];
                }

                return result;
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
            public GraphServiceClient Client { get; set; }
            public DateTime TokenExpiryDate { get; set; }
            public bool RequiresUserInteraction { get; set; }
        }

        readonly Dictionary<String /*userid*/, GraphServiceClientWithState> _mClientByUser =
            new Dictionary<String /*userid*/, GraphServiceClientWithState>();

        private async Task<GraphServiceClient> TryGetMsGraphClient(String path, bool tryConnect)
        {


            string userId = OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(path).User.Id;

            logDebug("TryGetMsGraphClient for " + userId);
            if (_mClientByUser.TryGetValue(userId, out var clientWithState))
            {
                logDebug("TryGetMsGraphClient found user " + userId);
                if (!(clientWithState.RequiresUserInteraction || (clientWithState.TokenExpiryDate < DateTime.Now) ||
                      (clientWithState.Client == null)))
                {
                    logDebug("TryGetMsGraphClient returning client");
                    return clientWithState.Client;
                }
                else
                {
                    logDebug("not returning client because " + clientWithState.RequiresUserInteraction + " " +
                             (clientWithState.TokenExpiryDate < DateTime.Now) + " " + (clientWithState.Client == null));
                }
            }
            if (tryConnect)
            {
                logDebug("trying to connect...");
                if (await TryLoginSilent(path) != null)
                {
                    logDebug("trying to connect ok");
                    return _mClientByUser.GetValueOrDefault(userId, null).Client;
                }
                logDebug("trying to connect failed");
            }
            logDebug("TryGetMsGraphClient for " + userId + " returns null");
            return null;
        }

        public class TokenFromAuthResultProvider : IAccessTokenProvider
        {
            public AuthenticationResult AuthenticationResult
            {
                get;
                set;
            }
            public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null,
                CancellationToken cancellationToken = new CancellationToken())
            {
                return AuthenticationResult.AccessToken;
            }

            public AllowedHostsValidator AllowedHostsValidator { get; }
        }

        private GraphServiceClient BuildClient(AuthenticationResult authenticationResult)
        {

            logDebug("buildClient...");


            var authenticationProvider = new BaseBearerTokenAuthenticationProvider(new TokenFromAuthResultProvider() { AuthenticationResult = authenticationResult });
            /*
            (requestMessage) =>

            {
                var access_token = authenticationResult.AccessToken;
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", access_token);
                return Task.FromResult(0);
            });*/

            GraphServiceClientWithState clientWithState = new GraphServiceClientWithState()
            {
                Client = new GraphServiceClient(new HttpClient(), authenticationProvider),
                RequiresUserInteraction = false,
                TokenExpiryDate = authenticationResult.ExpiresOn.LocalDateTime
            };



            if (authenticationResult.Account == null)
                throw new Exception("authenticationResult.Account == null!");
            _mClientByUser[authenticationResult.Account.HomeAccountId.Identifier] = clientWithState;
            logDebug("buildClient ok.");
            return clientWithState.Client;
        }



        private void logDebug(string str)
        {
#if DEBUG
            Log.Debug("KP2A", "OneDrive2: " + str);
#endif
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


        private Exception convertException(ServiceException e)
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
            if (e is ServiceException)
                return convertException((ServiceException)e);
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
                    PathItemBuilder.DriveItemRequests pathItem = await pathItemBuilder.BuildPathItemAsync();
                    await pathItem.ToAsyncTask()
                        .ForDriveItemRequests(builder => builder.DeleteAsync())
                        .ForCustomDriveItemRequests(b => b.DeleteAsync())
                        .Task;
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

        public Stream OpenFileForReadx(IOConnectionInfo ioc)
        {
            try
            {
                string filename = ioc.Path;

                Stream? result = Task.Run(async () =>
                {

                    logDebug("openFileForRead. Path=" + filename);
                    //var client = await TryGetMsGraphClient(filename, true);

                    PathItemBuilder clientAndPath = await GetPathItemBuilder(filename);
                    var client = clientAndPath.client;
                    var itemLocation = clientAndPath.itemLocation;

                    return await client.Drives[itemLocation.DriveId].Root.ItemWithPath(itemLocation.LocalPathString).Content
                        .GetAsync();
                    
                }).Result;
                logDebug("ok");
                return result;

            }
            catch (Exception e)
            {
                throw e;
            }
        }
        public Stream OpenFileForRead(IOConnectionInfo ioc)
        {
            try
            {
                string path = ioc.Path;

                logDebug("openFileForRead. Path=" + path);
                
                Stream? result = Task.Run(async () =>
                {
                    logDebug("openFileForRead. Path=" + path);

                    PathItemBuilder clientAndPath = await GetPathItemBuilder(path);
                    return await (await clientAndPath.BuildPathItemAsync())
                        .ToAsyncResult<Stream>()
                        .ForDriveItemRequests((b) => b.Content.GetAsync())
                        .ForCustomDriveItemRequests(b => b.Content.GetAsync())
                        .ForSpecialDriveItemRequests((b) => b.Content.GetAsync())
                        .ForSharedDriveItemRequests((b) => b.Content.GetAsync())
                        .Result;

                }).Result;
                if (result == null)
                    throw new Exception("failed to open stream");
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
                    //for small files <2MB use the direct upload:
                    if (stream.Length < 2 * 1024 * 1024)
                    {
                        await
                            (await pathItemBuilder
                                .BuildPathItemAsync())
                                .ToAsyncTask()
                                .ForDriveItemRequests((b) => b.Content.PutAsync(stream))
                                .ForCustomDriveItemRequests(b => b.Content.PutAsync(stream))
                                .ForSpecialDriveItemRequests(b => b.Content.PutAsync(stream))
                                .ForSharedDriveItemRequests(b => b.Content.PutAsync(stream))
                                .Task;
                        return;
                    }

                    //for larger files use an upload session. This is required for 4MB and beyond, but as the docs are not very clear about this
                    //limit, let's use it a bit more often to be safe.

                    var uploadProps = new CreateUploadSessionPostRequestBody
                    {
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "@microsoft.graph.conflictBehavior", "replace" }
                        }
                    };


                    var uploadSession = await (await pathItemBuilder
                                .BuildPathItemAsync())
                                .ToAsyncResult<UploadSession>()
                                .ForSharedDriveItemRequests(b => throw new Exception("Graph SDK doesn't seem to support large file uploads on shares.")) //TODO verify
                                .ForDriveItemRequests(b => b.CreateUploadSession.PostAsync(uploadProps))
                                .ForCustomDriveItemRequests(b => b.CreateUploadSession.PostAsync(uploadProps))

                                .Result;

                    // Max slice size must be a multiple of 320 KiB
                    int maxSliceSize = 320 * 1024;
                    var fileUploadTask = new LargeFileUploadTask<DriveItem>(uploadSession, stream, maxSliceSize);
                    var uploadResult = await fileUploadTask.UploadAsync();

                    if (!uploadResult.UploadSucceeded)
                    {
                        throw new Exception("Failed to upload data!");
                    }


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
            string localPath = "/" + OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(path).LocalPathString;
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

                    return await (await pathItemBuilder.BuildPathItemAsync())
                        .ToAsyncResult<DriveItem>()
                        .ForDriveItemRequests(b => b.Children.PostAsync(driveItem))
                        .ForCustomDriveItemRequests(b => b.Children.PostAsync(driveItem))
                        .Result;
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
            OneDrive2ItemLocation<OneDrive2PrefixContainerType> itemLocation = OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(ioc.Path);
            var client = await TryGetMsGraphClient(ioc.Path, true);
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

            var driveItems = await GetDriveItems(pathItemBuilder);

            if (driveItems != null)
                foreach (DriveItem? i in driveItems)
                {
                    var e = GetFileDescription(itemLocation.BuildLocalChildLocation(i.Name, i.Id, i.ParentReference?.DriveId), i);
                    result.Add(e);
                }

            return result;

        }

        private async Task<List<DriveItem?>> GetDriveItems(
            PathItemBuilder pathItemBuilder)
        {
            var pathItem = await pathItemBuilder.BuildPathItemAsync();
            if (pathItem.SpecialDriveItemRequestBuilder != null)
            {
                return pathItem.SpecialDriveItemRequestBuilder
                    .GetAsync(configuration => configuration.QueryParameters.Expand = new[] { "children" }).Result
                    .Children;
            }
            var response =  await pathItem
                .ToAsyncResult<DriveItemCollectionResponse>()
                .ForDriveItemRequests(b => b.Children.GetAsync())
                .ForCustomDriveItemRequests(b => b.Children.GetAsync())
                .ForSharedDriveItemRequests(b =>
                    throw new Java.Lang.Exception("TODO listing share children not implemented"))
                
                .Result;
            return response.Value;
        }


        private FileDescription GetFileDescription(OneDrive2ItemLocation<OneDrive2PrefixContainerType> path, DriveItem? i)
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
                return Task.Run(async () => await GetFileDescriptionAsync(ioc)).Result;
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

            var driveReq = await pathItemBuilder.BuildPathItemAsync();
            DriveItem? item = await driveReq.ToAsyncResult<DriveItem>()
                .ForDriveItemRequests(b => b.GetAsync())
                .ForCustomDriveItemRequests(b => b.GetAsync())
                .ForSharedDriveItemRequests(b => b.GetAsync())
                .ForSpecialDriveItemRequests(b => b.GetAsync())
                .Result;
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
            String path = ProtocolId + "://";
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
            if (!Task.Run(async () => await IsConnectedAsync(ioc.Path, true)).Result)
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
            logDebug("OneDrive2.OnStart");
            if (activity.ProcessName.Equals(FileStorageSetupDefs.ProcessNameFileUsageSetup))
                activity.State.PutString(FileStorageSetupDefs.ExtraPath, activity.Ioc.Path);
            string rootPathForUser = await TryLoginSilent(activity.Ioc.Path);
            if (rootPathForUser != null)
            {
                logDebug("rootPathForUser not null");
                FinishActivityWithSuccess(activity, rootPathForUser);
                return;
            }
            logDebug("rootPathForUser null");

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
            logDebug("Login Silent for " + iocPath);
            IAccount account = null;
            try
            {

                if (IsConnected(iocPath))
                {
                    logDebug("Login Silent ok, connected");
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

                    var rootFolder = BuildRootPathForUser(authResult);
                    logDebug("Found RootPath for user");
                    return rootFolder;

                }
                catch (MsalUiRequiredException ex)
                {

                    GraphServiceClientWithState clientWithState = new GraphServiceClientWithState()
                    {
                        Client = null,
                        RequiresUserInteraction = true
                    };


                    _mClientByUser[account.HomeAccountId.Identifier] = clientWithState;
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
                string result = ProtocolId + "://";
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


        private async Task<List<FileDescription>> ListShares(OneDrive2ItemLocation<OneDrive2PrefixContainerType> parentPath, GraphServiceClient client)
        {

            List<FileDescription> result = [];

            var drives = (await client.Me.Drives.GetAsync()).Value;
            if (drives != null)
            {
                drives.ForEach(drive =>
                {
                    var e = new FileDescription()
                    {
                        DisplayName = drive.Name ?? drive.DriveType ?? "(unnamed drive)",
                        IsDirectory = true,
                        CanRead = true,
                        CanWrite = true,
                        Path = parentPath.BuildShare("me","me","me", drive.Id).ToString()
                    };
                    result.Add(e);
                });
            }
            
            if (!CanListShares)
                return result;

            /*
             Not working. See https://stackoverflow.com/questions/79374963/list-shared-folders-with-graph-sdk.
             var sharedWithMeResponse = await client.Drives[parentPath.DriveId].SharedWithMe.GetAsSharedWithMeGetResponseAsync();

            foreach (DriveItem i in sharedWithMeResponse?.Value ?? [])
            {
                var oneDrive2ItemLocation = parentPath.BuildShare(i.Id, i.Name, i.WebUrl, i.ParentReference?.DriveId);
                FileDescription sharedFileEntry = new FileDescription()
                {
                    CanWrite = true, CanRead = true, DisplayName = i.Name,
                    IsDirectory = true,
                    Path = oneDrive2ItemLocation.ToString()
                };
                result.Add(sharedFileEntry);

            }*/

            return result;
        }

        protected virtual string MyOneDriveDisplayName { get { return "My OneDrive"; } }

        public abstract bool CanListShares { get; }

        async Task<DriveItem?> TryFindFileAsync(PathItemBuilder parent, string filename)
        {

            var driveItems = await GetDriveItems(parent);

            if (driveItems != null)
                foreach (DriveItem? i in driveItems)
                {
                    if (i.Name == filename)
                        return i;
                }

            return null;

        }


        public string CreateFilePath(string parent, string newFilename)
        {
            try
            {
                return Task.Run(async () => await CreateFilePathAsync(parent, newFilename)).Result;
            }
            catch (Exception e)
            {
                throw convertException(e);
            }

        }

        private async Task<string> CreateFilePathAsync(string parent, string newFilename)
        {
            PathItemBuilder pathItemBuilder = await GetPathItemBuilder(parent);

            //see if such a file exists already:
            var item = await TryFindFileAsync(pathItemBuilder, newFilename);
            if (item != null)
            {
                return pathItemBuilder.itemLocation.BuildLocalChildLocation(item.Name, item.Id, item.ParentReference?.DriveId)
                    .ToString();
            }
            //doesn't exist. Create:
            logDebug("building request for " + pathItemBuilder.itemLocation);

            var emptyStream = new MemoryStream();
            var driveItemReq = await pathItemBuilder.BuildPathItemAsync();
            DriveItem? res = await driveItemReq
                .ToAsyncResult<DriveItem>()
                .ForDriveItemRequests(b => b.Content.PutAsync(emptyStream))
                .ForCustomDriveItemRequests(b => b.Content.PutAsync(emptyStream))
                .ForSharedDriveItemRequests(b => b.Content.PutAsync(emptyStream))
                .Result;

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

    public class OneDrive2FullFileStorage : OneDrive2FileStorage<OneDrive2FullPrefixContainer>
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