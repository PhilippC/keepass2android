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

using System.Net;
using System.Reflection;
using System.Text;
using Android.Content;
using Android.Util;
using KeePass.Util;
using keepass2android.Io.ItemLocation;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using DriveItemRequestBuilder = Microsoft.Graph.Drives.Item.Items.Item.DriveItemItemRequestBuilder;


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
      private readonly string? _specialFolder;
      public GraphServiceClient client;
      public OneDrive2ItemLocation<OneDrive2PrefixContainerType> itemLocation;
      public bool verbose;

      public PathItemBuilder(string? specialFolder)
      {
        _specialFolder = specialFolder;
      }

      /// <summary>
      /// Wraps the different DriveItemRequestBuilder classes and allows accessing the different types easily
      /// </summary>
      /// NOTE: even though CustomDriveItemItemRequestBuilder derives from Kp2aDriveItemRequestBuilder, we cannot use polymorphism here because the methods are declared with "new".
      /// If you cast assign an CustomDriveItemItemRequestBuilder object to a variable declared as Kp2aDriveItemRequestBuilder and then call a method on it, it will fail.
      public class DriveItemRequestBuilderWrapper
      {

        public class DriveItemRequestBuilderResult<T>
        {
          private readonly DriveItemRequestBuilderWrapper _req;

          public DriveItemRequestBuilderResult(DriveItemRequestBuilderWrapper req)
          {
            _req = req;
          }

          public Task<T?> Result { get; set; }


          public DriveItemRequestBuilderResult<T> ForDriveItemRequestBuilder(Func<DriveItemRequestBuilder, Task<T?>> action)
          {
            if (_req.DriveItemRequestBuilder != null)
            {
              Result = action(_req.DriveItemRequestBuilder);
            }
            return this;
          }

          public DriveItemRequestBuilderResult<T> ForCustomDriveItemRequestBuilder(Func<CustomDriveItemItemRequestBuilder, Task<T?>> action)
          {
            if (_req.CustomDriveItemRequestBuilder != null)
            {
              Result = action(_req.CustomDriveItemRequestBuilder);
            }
            return this;
          }



        }

        public class DriveItemRequestBuilderAsyncTask
        {
          private readonly DriveItemRequestBuilderWrapper _req;

          public DriveItemRequestBuilderAsyncTask(DriveItemRequestBuilderWrapper req)
          {
            _req = req;
            Task = Task.CompletedTask;
          }

          public Task Task { get; private set; }

          public DriveItemRequestBuilderAsyncTask ForDriveItemRequestBuilder(Func<DriveItemRequestBuilder, Task> action)
          {
            if (_req.DriveItemRequestBuilder != null)
            {
              Task = action(_req.DriveItemRequestBuilder);
            }
            return this;
          }

          public DriveItemRequestBuilderAsyncTask ForCustomDriveItemRequestBuilder(Func<CustomDriveItemItemRequestBuilder, Task> action)
          {
            if (_req.CustomDriveItemRequestBuilder != null)
            {
              Task = action(_req.CustomDriveItemRequestBuilder);
            }
            return this;
          }
        }


        public DriveItemRequestBuilder? DriveItemRequestBuilder { get; set; }

        public CustomDriveItemItemRequestBuilder? CustomDriveItemRequestBuilder { get; set; }


        public DriveItemRequestBuilderResult<T> ToAsyncResult<T>()
        {
          return new DriveItemRequestBuilderResult<T>(this);
        }

        public DriveItemRequestBuilderAsyncTask ToAsyncTask()
        {
          return new DriveItemRequestBuilderAsyncTask(this);
        }


      };


      public async Task<DriveItemRequestBuilderWrapper> BuildPathItemAsync()
      {
        Kp2aLog.Log("buildPathItem for " + itemLocation.ToString());
        DriveItemRequestBuilderWrapper result = new DriveItemRequestBuilderWrapper();

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

            DriveItemRequestBuilder specialRoot = client.Drives[itemLocation.DriveId].Items[_specialFolder];


            if (itemLocation.LocalPath.Any())
            {
              result.CustomDriveItemRequestBuilder = specialRoot.ItemWithPath(itemLocation.LocalPathString);
            }
            else
            {
              result.DriveItemRequestBuilder = specialRoot;
            }
          }

        }
        else
        {
          if (verbose) Kp2aLog.Log("Path share is not me");
          if (!itemLocation.LocalPath.Any())
          {
            result.DriveItemRequestBuilder = client.Drives[itemLocation.DriveId].Items[itemLocation.Share.Id];
            return result;
          }
          if (verbose) Kp2aLog.Log("Using driveId=" + itemLocation.DriveId + " and item id=" + itemLocation.LocalPath.Last().Id);
          result.DriveItemRequestBuilder = client.Drives[itemLocation.DriveId].Items[itemLocation.LocalPath.Last().Id];
        }

        return result;
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

    protected class GraphServiceClientWithState
    {
      public GraphServiceClient Client { get; set; }
      public DateTime TokenExpiryDate { get; set; }
      public bool RequiresUserInteraction { get; set; }
    }

    protected readonly Dictionary<String /*userid*/, GraphServiceClientWithState> _mClientByUser = new();

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


    protected abstract Task<string?> GetSpecialFolder(
        OneDrive2ItemLocation<OneDrive2PrefixContainerType> itemLocation, GraphServiceClient client);

    private async Task<PathItemBuilder> GetPathItemBuilder(String path)
    {
      var itemLocation = OneDrive2ItemLocation<OneDrive2PrefixContainerType>.FromString(path);
      var client = await TryGetMsGraphClient(path, true);


      PathItemBuilder result = new PathItemBuilder(await GetSpecialFolder(itemLocation, client));


      result.itemLocation = itemLocation;
      if (string.IsNullOrEmpty(result.itemLocation.User?.Name))
      {
        throw new Exception("path does not contain user");
      }

      result.client = client;

      if (result.client == null)
        throw new Exception("Failed to connect or authenticate to OneDrive!");


      return result;

    }


    private Exception convertException(ServiceException e)
    {

      if (e.IsMatch(GraphErrorCode.ItemNotFound.ToString()))
        return new FileNotFoundException(ExceptionUtil.GetErrorMessage(e));
      if (e.Message.Contains("\n\n404 : ")
      ) //hacky solution to check for not found. errorCode was null in my tests so I had to find a workaround.
        return new FileNotFoundException(ExceptionUtil.GetErrorMessage(e));
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
          PathItemBuilder.DriveItemRequestBuilderWrapper pathItem = await pathItemBuilder.BuildPathItemAsync();
          await pathItem.ToAsyncTask()
                      .ForDriveItemRequestBuilder(builder => builder.DeleteAsync())
                      .ForCustomDriveItemRequestBuilder(b => b.DeleteAsync())
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
                      .ForDriveItemRequestBuilder((b) => b.Content.GetAsync())
                      .ForCustomDriveItemRequestBuilder(b => b.Content.GetAsync())
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
                            .ForDriveItemRequestBuilder((b) => b.Content.PutAsync(stream))
                            .ForCustomDriveItemRequestBuilder(b => b.Content.PutAsync(stream))
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
                              .ForDriveItemRequestBuilder(b => b.CreateUploadSession.PostAsync(uploadProps))
                              .ForCustomDriveItemRequestBuilder(b => b.CreateUploadSession.PostAsync(uploadProps))

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
                      .ForDriveItemRequestBuilder(b => b.Children.PostAsync(driveItem))
                      .ForCustomDriveItemRequestBuilder(b => b.Children.PostAsync(driveItem))
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
      var response = await pathItem
          .ToAsyncResult<DriveItemCollectionResponse>()
          .ForDriveItemRequestBuilder(b => b.Children.GetAsync())
          .ForCustomDriveItemRequestBuilder(b => b.Children.GetAsync())
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
          .ForDriveItemRequestBuilder(b => b.GetAsync())
          .ForCustomDriveItemRequestBuilder(b => b.GetAsync())
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
    public static async Task<DriveItem> GetOrCreateAppRootAsync(GraphServiceClient client, string dummyFileName = "welcome_at_kp2a.txt")
    {


      try
      {
        return await client.RequestAdapter.SendAsync(
            new Microsoft.Graph.Drives.Item.Items.Item.DriveItemItemRequestBuilder(
                new Dictionary<string, object> {
                            { "drive%2Did", "me" },
                            { "driveItem%2Did", "special/approot" }
                },
                client.RequestAdapter
            ).ToGetRequestInformation(),
            static (p) => DriveItem.CreateFromDiscriminatorValue(p)
        );
      }
      catch (Microsoft.Kiota.Abstractions.ApiException ex) when (ex.ResponseStatusCode == 404)
      {
        // App folder doesn’t exist yet → create it by uploading a dummy file
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("init"));

        var uploadRequest = new RequestInformation
        {
          HttpMethod = Method.PUT,
          UrlTemplate = "{+baseurl}/me/drive/special/approot:/{filename}:/content",
          PathParameters = new Dictionary<string, object>
                    {
                        { "baseurl", client.RequestAdapter.BaseUrl },
                        { "filename", dummyFileName }
                    },
          Content = stream
        };

        var uploadedItem = await client.RequestAdapter.SendAsync<DriveItem>(
            uploadRequest,
            DriveItem.CreateFromDiscriminatorValue
        );

        var parentId = uploadedItem.ParentReference.Id;

        var parentItemRequest = new DriveItemRequestBuilder(
            $"{client.RequestAdapter.BaseUrl}/me/drive/items/{parentId}",
            client.RequestAdapter
        );

        return await parentItemRequest.GetAsync();
      }
    }

    protected virtual async Task<List<FileDescription>> ListShares(OneDrive2ItemLocation<OneDrive2PrefixContainerType> parentPath, GraphServiceClient client)
    {

      List<FileDescription> result = [];

      var drives = (await client.Me.Drives.GetAsync()).Value;
      if (drives != null)
      {
        drives.ForEach(drive =>
        {
          var e = new FileDescription()
          {
            DisplayName = GetDriveDisplayName(drive),
            IsDirectory = true,
            CanRead = true,
            CanWrite = true,
            Path = parentPath.BuildShare("me", "me", "me", drive.Id).ToString()
          };
          result.Add(e);
        });
      }


      if (!CanListShares)
        return result;


      try
      {
        string? driveId = parentPath.DriveId;
        if (string.IsNullOrEmpty(driveId))
        {
          driveId = (await client.Me.Drive.GetAsync()).Id;
        }
        if ((string.IsNullOrEmpty(driveId)) && (drives?.Any() == true))
        {
          driveId = drives.First().Id;
        }

        var sharedWithMeResponse = await client.Drives[driveId].SharedWithMe.GetAsSharedWithMeGetResponseAsync();

        foreach (DriveItem i in sharedWithMeResponse?.Value ?? [])
        {
          var oneDrive2ItemLocation = parentPath.BuildShare(i.RemoteItem.Id, i.RemoteItem.Name, i.RemoteItem.WebUrl, i.RemoteItem.ParentReference.DriveId);
          FileDescription sharedFileEntry = new FileDescription()
          {
            CanWrite = true,
            CanRead = true,
            DisplayName = i.Name,
            IsDirectory = (i.Folder != null) || ((i.RemoteItem != null) && (i.RemoteItem.Folder != null)),
            Path = oneDrive2ItemLocation.ToString()
          };
          result.Add(sharedFileEntry);

        }
      }
      catch (Exception e)
      {
        logDebug("Failed to list shares: " + e);
      }



      return result;
    }

    protected virtual string GetDriveDisplayName(Drive drive)
    {
      return drive.Name ?? drive.DriveType ?? "(unnamed drive)";
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


      PathItemBuilder targetPathItemBuilder = await GetPathItemBuilder(pathItemBuilder.itemLocation.BuildLocalChildLocation(newFilename, "", pathItemBuilder.itemLocation.DriveId ?? "").ToString());


      var emptyStream = new MemoryStream();
      var driveItemReq = await targetPathItemBuilder.BuildPathItemAsync();
      DriveItem? res = await driveItemReq
          .ToAsyncResult<DriveItem>()
          .ForDriveItemRequestBuilder(b => b.Content.PutAsync(emptyStream))
          .ForCustomDriveItemRequestBuilder(b => b.Content.PutAsync(emptyStream))
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

    protected override async Task<string?> GetSpecialFolder(
        OneDrive2ItemLocation<OneDrive2FullPrefixContainer> itemLocation, GraphServiceClient client)
    {
      return null;
    }

    public override bool CanListShares { get { return true; } }
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

    protected override async Task<string?> GetSpecialFolder(
        OneDrive2ItemLocation<OneDrive2MyFilesPrefixContainer> itemLocation, GraphServiceClient client)
    {
      return null;
    }

    public override bool CanListShares { get { return false; } }
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



    protected override async Task<string?> GetSpecialFolder(
        OneDrive2ItemLocation<OneDrive2AppFolderPrefixContainer> itemLocation, GraphServiceClient client)
    {
      if (string.IsNullOrEmpty(itemLocation.DriveId))
        return null; //can happen if we are accessing the root
      if (!_specialFolderIdByDriveId.ContainsKey(itemLocation.DriveId))
      {
        try
        {
          var specialFolder = await client.Drives[itemLocation.DriveId].Special[SpecialFolderName].GetAsync();
          _specialFolderIdByDriveId[itemLocation.DriveId] = specialFolder.Id;
        }
        catch (Exception e)
        {
          Console.WriteLine(e);
          throw;
        }


      }
      return _specialFolderIdByDriveId[itemLocation.DriveId];
    }


    protected string SpecialFolderName { get { return "approot"; } }

    private readonly Dictionary<string, string> _specialFolderIdByDriveId = new Dictionary<string, string>();

    protected override string GetDriveDisplayName(Drive drive)
    {
      return drive.Name ?? MyOneDriveDisplayName;
    }
    public static async Task GetOrCreateAppRootAsync(GraphServiceClient client, string dummyFileName = "welcome_at_kp2a_app_folder.txt")
    {


      try
      {
        await client.RequestAdapter.SendAsync(
            new Microsoft.Graph.Drives.Item.Items.Item.DriveItemItemRequestBuilder(
                new Dictionary<string, object> {
                            { "drive%2Did", "me" },
                            { "driveItem%2Did", "special/approot" }
                },
                client.RequestAdapter
            ).ToGetRequestInformation(),
            static (p) => DriveItem.CreateFromDiscriminatorValue(p)
        );
        //if this is successful, approot seems to exist
      }
      catch (Microsoft.Kiota.Abstractions.ApiException ex) when (ex.ResponseStatusCode == 404)
      {
        // App folder doesn’t exist yet → create it by uploading a dummy file
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("init"));

        var uploadRequest = new RequestInformation
        {
          HttpMethod = Method.PUT,
          UrlTemplate = "{+baseurl}/me/drive/special/approot:/{filename}:/content",
          PathParameters = new Dictionary<string, object>
                    {
                        { "baseurl", client.RequestAdapter.BaseUrl },
                        { "filename", dummyFileName }
                    },
          Content = stream
        };

        await client.RequestAdapter.SendAsync<DriveItem>(
            uploadRequest,
            DriveItem.CreateFromDiscriminatorValue
        );

      }
    }

    protected override async Task<List<FileDescription>> ListShares(OneDrive2ItemLocation<OneDrive2AppFolderPrefixContainer> parentPath, GraphServiceClient client)
    {
      await GetOrCreateAppRootAsync(client);
      return await base.ListShares(parentPath, client);
    }

    public override bool CanListShares { get { return false; } }
    protected override string MyOneDriveDisplayName => "Keepass2Android App Folder";
  }
}