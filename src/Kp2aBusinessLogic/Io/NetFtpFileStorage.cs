using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.FtpClient;
using Android.Content;
using Android.OS;
using Android.Preferences;
using KeePassLib;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace keepass2android.Io
{
	public class NetFtpFileStorage: IFileStorage
	{
		struct ConnectionSettings
		{
			public FtpEncryptionMode EncryptionMode {get; set; }

			public static ConnectionSettings FromIoc(IOConnectionInfo ioc)
			{
				if (ioc.Path.StartsWith(Kp2AAccountPathPrefix))
					throw new InvalidOperationException("cannot extract settings from account-path");
				string path = ioc.Path;
				int schemeLength = path.IndexOf("://", StringComparison.Ordinal);
				path = path.Substring(schemeLength + 3);
				string settings = path.Substring(0, path.IndexOf("/", StringComparison.Ordinal));
				return new ConnectionSettings()
				{
					EncryptionMode = (FtpEncryptionMode) int.Parse(settings)
				};

			}
		}

		private const string Kp2AAccountPathPrefix = "__kp2a_account__";
		private readonly Context _context;

		public NetFtpFileStorage(Context context)
		{
			_context = context;
		}

		public IEnumerable<string> SupportedProtocols
		{
			get { yield return "ftp"; }
		}

		public void Delete(IOConnectionInfo ioc)
		{
			using (FtpClient client = GetClient(ioc))
			{
				client.DeleteFile(IocPathToUri(ioc.Path).PathAndQuery);
			}
		}


		internal FtpClient GetClient(IOConnectionInfo ioc, bool enableCloneClient = true)
		{
			FtpClient client = new FtpClient();
			if ((ioc.UserName.Length > 0) || (ioc.Password.Length > 0))
				client.Credentials = new NetworkCredential(ioc.UserName, ioc.Password);
			else
				client.Credentials = new NetworkCredential("anonymous", ""); //TODO TEST

			Uri uri = IocPathToUri(ioc.Path);
			client.Host = uri.Host;
			if (!uri.IsDefaultPort) //TODO test
				client.Port = uri.Port;
			
			//TODO Validate 
			//client.ValidateCertificate += app...

			// we don't need to be thread safe in a classic sense, but OpenRead and OpenWrite don't 
			//perform the actual stream operation so we'd need to wrap the stream (or just enable this:)
			client.EnableThreadSafeDataConnections = enableCloneClient;

			client.EncryptionMode = ConnectionSettings.FromIoc(ioc).EncryptionMode;

			client.Connect();
			return client;
		}

		internal Uri IocPathToUri(string path)
		{
			//remove addition stuff like TLS param
			int schemeLength = path.IndexOf("://", StringComparison.Ordinal);
			string scheme = path.Substring(0, schemeLength);
			path = path.Substring(schemeLength + 3);
			string settings = path.Substring(0, path.IndexOf("/", StringComparison.Ordinal));
			path = path.Substring(settings.Length + 1);
			return new Uri(scheme + "://" + path);
		}

		private string IocPathFromUri(IOConnectionInfo baseIoc, Uri uri)
		{
			string basePath = baseIoc.Path;
			int schemeLength = basePath.IndexOf("://", StringComparison.Ordinal);
			string scheme = basePath.Substring(0, schemeLength);
			basePath = basePath.Substring(schemeLength + 3);
			string baseSettings = basePath.Substring(0, basePath.IndexOf("/", StringComparison.Ordinal));
			return scheme + "://" + baseSettings + "/" + uri.AbsolutePath; //TODO does this contain Query?
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
			using (var cl = GetClient(ioc))
			{
				return cl.OpenRead(IocPathToUri(ioc.Path).PathAndQuery, FtpDataType.Binary, 0);
			}
		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			if (useFileTransaction)
				return new UntransactedWrite(ioc, this);
			else
				return new TransactedWrite(ioc, this);
		}

		public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
		{
			//TODO does this work when flags are encoded in the iocPath?
			return UrlUtil.StripExtension(
				UrlUtil.GetFileName(ioc.Path));
		}

		public bool RequiresCredentials(IOConnectionInfo ioc)
		{
			return ioc.CredSaveMode != IOCredSaveMode.SaveCred;
		}

		public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
		{
			using (var client = GetClient(ioc))
			{
				client.CreateDirectory(IocPathToUri(ioc.Path).PathAndQuery);
			}
		}

		public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
		{
			using (var client = GetClient(ioc))
			{
				List<FileDescription> files = new List<FileDescription>();
				foreach (FtpListItem item in client.GetListing(IocPathToUri(ioc.Path).PathAndQuery,
					FtpListOption.Modify | FtpListOption.Size | FtpListOption.DerefLinks))
				{

					switch (item.Type)
					{
						case FtpFileSystemObjectType.Directory:
							files.Add(new FileDescription()
							{
								CanRead =  true, 
								CanWrite = true,
								DisplayName = item.Name,
								IsDirectory = true,
								LastModified = item.Modified,
								Path = IocPathFromUri(ioc, new Uri(item.FullName))
							});
							break;
						case FtpFileSystemObjectType.File:
							files.Add(new FileDescription()
							{
								CanRead =  true, 
								CanWrite = true,
								DisplayName = item.Name,
								IsDirectory = false,
								LastModified = item.Modified,
								Path = IocPathFromUri(ioc, new Uri(item.FullName)),
								SizeInBytes = item.Size
							});
							break;
						
					}
				}
				return files;
			}
		}

		
		public FileDescription GetFileDescription(IOConnectionInfo ioc)
		{
			//TODO when is this called? 
			//is it very inefficient to connect for each description?

			using (FtpClient client = GetClient(ioc))
			{
				var uri = IocPathToUri(ioc.Path);
				string path = uri.PathAndQuery;
				return new FileDescription()
				{
					CanRead = true,
					CanWrite = true,
					Path = ioc.Path,
					LastModified = client.GetModifiedTime(path),
					SizeInBytes = client.GetFileSize(path),
					DisplayName = UrlUtil.GetFileName(path),
					IsDirectory = false
				};
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

		public void StartSelectFile(IFileStorageSetupInitiatorActivity activity, bool isForSave, int requestCode, string protocolId)
		{
			throw new NotImplementedException();
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
			var uri = IocPathToUri(ioc.Path);
			return uri.ToString(); //TODO is this good?
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

		public bool IsReadOnly(IOConnectionInfo ioc, OptionalOut<UiStringKey> reason = null)
		{
			return false;
		}

		public void ResolveAccount(IOConnectionInfo ioc)
		{
			string path = ioc.Path;
			int schemeLength = path.IndexOf("://", StringComparison.Ordinal);
			string scheme = path.Substring(0, schemeLength);
			path = path.Substring(schemeLength+3);
			if (path.StartsWith(Kp2AAccountPathPrefix))
			{
				string accountId = path.Substring(0, path.IndexOf("/", StringComparison.Ordinal));
				path = path.Substring(accountId.Length + 1);

				var prefs = PreferenceManager.GetDefaultSharedPreferences(_context);
				string host = prefs.GetString(accountId + "_Host", null);
				int port = prefs.GetInt(accountId + "_Port", 0 /* auto*/);
				string initialPath = prefs.GetString(accountId + "_InitPath", "");
				if (initialPath.StartsWith("/"))
					initialPath = initialPath.Substring(1);
				if ((!initialPath.EndsWith("/") && (initialPath != "")))
					initialPath += "/";
				int encMode = prefs.GetInt(accountId + "_EncMode", (int) FtpEncryptionMode.None);
				string settings = encMode.ToString();
				ioc.Path = scheme + "://" + settings + "/" + host + (port == 0 ? "" : (":" + port)) + "/" + initialPath + path;
			}
		}

		public Stream OpenWrite(IOConnectionInfo ioc)
		{
			using (var client = GetClient(ioc))
			{
				return client.OpenWrite(IocPathToUri(ioc.Path).PathAndQuery);
			}
		}
	}

	public class TransactedWrite : IWriteTransaction
	{
		private readonly IOConnectionInfo _ioc;
		private readonly NetFtpFileStorage _fileStorage;
		private readonly IOConnectionInfo _iocTemp;
		private FtpClient _client;


		public TransactedWrite(IOConnectionInfo ioc, NetFtpFileStorage fileStorage)
		{
			_ioc = ioc;
			_iocTemp = _ioc.CloneDeep();
			_iocTemp.Path += "." + new PwUuid(true).ToHexString().Substring(0, 6) + ".tmp";

			_fileStorage = fileStorage;
		}

		public void Dispose()
		{
			if (_client != null)
				_client.Dispose();
			_client = null;
		}

		public Stream OpenFile()
		{
			_client = _fileStorage.GetClient(_ioc, false);
			return _client.OpenWrite(_fileStorage.IocPathToUri(_iocTemp.Path).PathAndQuery);
		}

		public void CommitWrite()
		{
			_client.DeleteFile(_fileStorage.IocPathToUri(_ioc.Path).PathAndQuery);
			_client.Rename(_fileStorage.IocPathToUri(_iocTemp.Path).PathAndQuery, _fileStorage.IocPathToUri(_ioc.Path).PathAndQuery);
		}
	}

	public class UntransactedWrite : IWriteTransaction
	{
		private readonly IOConnectionInfo _ioc;
		private readonly NetFtpFileStorage _fileStorage;

		public UntransactedWrite(IOConnectionInfo ioc, NetFtpFileStorage fileStorage)
		{
			_ioc = ioc;
			_fileStorage = fileStorage;
		}

		public void Dispose()
		{
			
		}

		public Stream OpenFile()
		{
			return _fileStorage.OpenWrite(_ioc);
		}

		public void CommitWrite()
		{
			
		}
	}
}