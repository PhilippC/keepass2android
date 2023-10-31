#if !NoNet
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using Android.Content;
using Android.OS;
using Android.Preferences;
using FluentFTP;
using KeePassLib;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace keepass2android.Io
{
	public class NetFtpFileStorage: IFileStorage
	{
		public struct ConnectionSettings
		{
			public FtpEncryptionMode EncryptionMode {get; set; }

			public string Username
			{
				get;set;
			}
			public string Password
			{
				get;
				set;
			}
			
			public static ConnectionSettings FromIoc(IOConnectionInfo ioc)
			{
				if (!string.IsNullOrEmpty(ioc.UserName))
				{
					//legacy support
					return new ConnectionSettings()
					{
						EncryptionMode = FtpEncryptionMode.None,
						Username = ioc.UserName,
						Password = ioc.Password
					};
				}

				string path = ioc.Path;
				int schemeLength = path.IndexOf("://", StringComparison.Ordinal);
				path = path.Substring(schemeLength + 3);
				string settings = path.Substring(0, path.IndexOf(SettingsPostFix, StringComparison.Ordinal));
				if (!settings.StartsWith(SettingsPrefix))
					throw new Exception("unexpected settings in path");
				settings = settings.Substring(SettingsPrefix.Length);
				var tokens = settings.Split(Separator);
				return new ConnectionSettings()
				{
					EncryptionMode = (FtpEncryptionMode) int.Parse(tokens[2]),
					Username = WebUtility.UrlDecode(tokens[0]),
					Password = WebUtility.UrlDecode(tokens[1])
				};

			}

			public const string SettingsPrefix = "SET";
			public const string SettingsPostFix = "#";
			public const char Separator = ':';
			public override string ToString()
			{
				return SettingsPrefix + 
					System.Net.WebUtility.UrlEncode(Username) + Separator +
					WebUtility.UrlEncode(Password) + Separator +
				       (int) EncryptionMode;
				;
			}
		}

		private readonly ICertificateValidationHandler _app;

		public MemoryStream traceStream;

		public NetFtpFileStorage(Context context, ICertificateValidationHandler app)
		{
            _app = app;
			traceStream = new MemoryStream();
			
		}

		public IEnumerable<string> SupportedProtocols
		{
			get 
			{ 
				yield return "ftp";
			}
		}

	    public bool UserShouldBackup
	    {
	        get { return true; }
	    }

	    public void Delete(IOConnectionInfo ioc)
		{
			try
			{
				using (FtpClient client = GetClient(ioc))
				{
					string localPath = IocToLocalPath(ioc);
					if (client.DirectoryExists(localPath))
						client.DeleteDirectory(localPath);
					else
						client.DeleteFile(localPath);
				}
				
			}
			catch (FtpCommandException ex)
				{
					throw ConvertException(ex);
				}
		
	}

		public static Exception ConvertException(Exception exception)
		{
			if (exception is FtpCommandException)
			{
				var ftpEx = (FtpCommandException) exception;

				if (ftpEx.CompletionCode == "550")
					throw new FileNotFoundException(exception.Message, exception);
			}

			return exception;
		}


		internal FtpClient GetClient(IOConnectionInfo ioc, bool enableCloneClient = true)
		{
			var settings = ConnectionSettings.FromIoc(ioc);
			
			FtpClient client = new FtpClient();
		    client.RetryAttempts = 3;
			if ((settings.Username.Length > 0) || (settings.Password.Length > 0))
				client.Credentials = new NetworkCredential(settings.Username, settings.Password);
			else
				client.Credentials = new NetworkCredential("anonymous", ""); //TODO TEST

			Uri uri = IocToUri(ioc);
			client.Host = uri.Host;
			if (!uri.IsDefaultPort) //TODO test
				client.Port = uri.Port;
			
			client.ValidateCertificate += (control, args) =>
			{
				args.Accept = _app.CertificateValidationCallback(control, args.Certificate, args.Chain, args.PolicyErrors);
			};

			client.EncryptionMode = settings.EncryptionMode;
			
			client.Connect();
			return client;
			
		}


		

		public static Uri IocToUri(IOConnectionInfo ioc)
		{
			if (!string.IsNullOrEmpty(ioc.UserName))
			{
				//legacy support.
				return new Uri(ioc.Path);
			}
			string path = ioc.Path;
			//remove additional stuff like TLS param
			int schemeLength = path.IndexOf("://", StringComparison.Ordinal);
			string scheme = path.Substring(0, schemeLength);
			path = path.Substring(schemeLength + 3);
			if (path.StartsWith(ConnectionSettings.SettingsPrefix))
			{
				//this should always be the case. However, in rare cases we might get an ioc with legacy path but no username set (if they only want to get a display name)
				string settings = path.Substring(0, path.IndexOf(ConnectionSettings.SettingsPostFix, StringComparison.Ordinal));
				path = path.Substring(settings.Length + 1);
				
			}
            Kp2aLog.Log("FTP: IocToUri out = " + scheme + "://" + path);
            return new Uri(scheme + "://" + path);
		}

		private string IocPathFromUri(IOConnectionInfo baseIoc, string uri)
		{
            string basePath = baseIoc.Path;
			int schemeLength = basePath.IndexOf("://", StringComparison.Ordinal);
			string scheme = basePath.Substring(0, schemeLength);
			basePath = basePath.Substring(schemeLength + 3);
			string baseSettings = basePath.Substring(0, basePath.IndexOf(ConnectionSettings.SettingsPostFix, StringComparison.Ordinal));
			basePath = basePath.Substring(baseSettings.Length+1);
			string baseHost = basePath.Substring(0, basePath.IndexOf("/", StringComparison.Ordinal));
			string result = scheme + "://" + baseSettings + ConnectionSettings.SettingsPostFix + baseHost + uri; //TODO does this contain Query?
            return result;
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
				using (var cl = GetClient(ioc))
                {
                    var memStream = new MemoryStream();
                    cl.OpenRead(IocToLocalPath(ioc), FtpDataType.Binary, 0).CopyTo(memStream);
                    memStream.Seek(0, SeekOrigin.Begin);
					return memStream;
                }
			}
			catch (FtpCommandException ex)
			{
				throw ConvertException(ex);
			}
		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			try
			{


				if (!useFileTransaction)
					return new UntransactedWrite(ioc, this);
				else
					return new TransactedWrite(ioc, this);
			}
			catch (FtpCommandException ex)
			{
				throw ConvertException(ex);
			}
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
			try
			{
				using (var client = GetClient(ioc))
				{
					client.CreateDirectory(IocToLocalPath(GetFilePath(ioc, newDirName)));
				}
			}
			catch (FtpCommandException ex)
			{
				throw ConvertException(ex);
			}
		}

        public static string IocToLocalPath(IOConnectionInfo ioc)
        {
            return WebUtility.UrlDecode(IocToUri(ioc).PathAndQuery);
        }

        public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
		{
			try
			{
                using (var client = GetClient(ioc))
				{
                    /*
					 * For some reason GetListing(path) does not always return the contents of the directory.
					 * However, calling SetWorkingDirectory(path) followed by GetListing(null, options) to
					 * list the contents of the working directory does consistently work.
					 * 
					 * Similar behavior was confirmed using ncftp client. I suspect this is a strange
					 * bug/nuance in the server's implementation of the LIST command?
					 *
					 * [bug #2423]
					 */
                    client.SetWorkingDirectory(IocToLocalPath(ioc));

					List<FileDescription> files = new List<FileDescription>();
					foreach (FtpListItem item in client.GetListing(null,
						FtpListOption.Modify | FtpListOption.Size | FtpListOption.DerefLinks))
					{

						switch (item.Type)
						{
							case FtpFileSystemObjectType.Directory:
								files.Add(new FileDescription()
								{
									CanRead = true,
									CanWrite = true,
									DisplayName = item.Name,
									IsDirectory = true,
									LastModified = item.Modified,
									Path = IocPathFromUri(ioc, item.FullName)
								});
								break;
							case FtpFileSystemObjectType.File:
								files.Add(new FileDescription()
								{
									CanRead = true,
									CanWrite = true,
									DisplayName = item.Name,
									IsDirectory = false,
									LastModified = item.Modified,
									Path = IocPathFromUri(ioc, item.FullName),
									SizeInBytes = item.Size
								});
								break;

						}
					}
					return files;
				}
			}
			catch (FtpCommandException ex)
			{
				throw ConvertException(ex);
			}
		}

		
		public FileDescription GetFileDescription(IOConnectionInfo ioc)
		{
			try
			{
				//TODO when is this called? 
				//is it very inefficient to connect for each description?

				using (FtpClient client = GetClient(ioc))
                {


                    string path = IocToLocalPath(ioc);
					if (!client.FileExists(path) && (!client.DirectoryExists(path)))
						throw new FileNotFoundException();
					var fileDesc = new FileDescription()
					{
						CanRead = true,
						CanWrite = true,
						Path = ioc.Path,
						LastModified = client.GetModifiedTime(path),
						SizeInBytes = client.GetFileSize(path),
						DisplayName = UrlUtil.GetFileName(path)
					};
					fileDesc.IsDirectory = fileDesc.Path.EndsWith("/");
					return fileDesc;
				}
			}
			catch (FtpCommandException ex)
			{
				throw ConvertException(ex);
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
			activity.PerformManualFileSelect(isForSave, requestCode, "ftp");
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
			var uri = IocToUri(ioc);
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
		public Stream OpenWrite(IOConnectionInfo ioc)
		{
			try
			{
				using (var client = GetClient(ioc))
				{
					return client.OpenWrite(IocToLocalPath(ioc));

				}
			}
			catch (FtpCommandException ex)
			{
				throw ConvertException(ex);
			}
		}

		public static int GetDefaultPort(FtpEncryptionMode encryption)
		{
			return new FtpClient() { EncryptionMode =  encryption}.Port;
		}

		public string BuildFullPath(string host, int port, string initialPath, string user, string password, FtpEncryptionMode encryption)
		{
			var connectionSettings = new ConnectionSettings()
			{
				EncryptionMode = encryption,
				Username = user,
				Password = password
			};

			string scheme = "ftp";

			string fullPath = scheme + "://" + connectionSettings.ToString() + ConnectionSettings.SettingsPostFix + host;
			if (port != GetDefaultPort(encryption))
				fullPath += ":" + port;

			if (!initialPath.StartsWith("/"))
				initialPath = "/" + initialPath;
			fullPath += initialPath;

			return fullPath;
		}

	}

	public class TransactedWrite : IWriteTransaction
	{
		private readonly IOConnectionInfo _ioc;
		private readonly NetFtpFileStorage _fileStorage;
		private readonly IOConnectionInfo _iocTemp;
		private FtpClient _client;
		private Stream _stream;

		public TransactedWrite(IOConnectionInfo ioc, NetFtpFileStorage fileStorage)
		{
			_ioc = ioc;
			_iocTemp = _ioc.CloneDeep();
			_iocTemp.Path += "." + new PwUuid(true).ToHexString().Substring(0, 6) + ".tmp";

			_fileStorage = fileStorage;
		}

		public void Dispose()
		{
			if (_stream != null)
				_stream.Dispose();
			_stream = null;
		}

		public Stream OpenFile()
		{
			try
			{

				_client = _fileStorage.GetClient(_ioc, false);
				_stream = _client.OpenWrite(NetFtpFileStorage.IocToLocalPath(_iocTemp));
				return _stream;
			}
			catch (FtpCommandException ex)
			{
				throw NetFtpFileStorage.ConvertException(ex);
			}
		}

		public void CommitWrite()
		{
			try
			{
				Android.Util.Log.Debug("NETFTP","connected: " + _client.IsConnected.ToString());
				_stream.Close();
                _stream.Dispose();
                _client.GetReply();

				_client.MoveFile(NetFtpFileStorage.IocToLocalPath(_iocTemp),
				    NetFtpFileStorage.IocToLocalPath(_ioc));
				
			}
			catch (FtpCommandException ex)
			{
				throw NetFtpFileStorage.ConvertException(ex);
			}
		}
	}

	public class UntransactedWrite : IWriteTransaction
	{
		private readonly IOConnectionInfo _ioc;
		private readonly NetFtpFileStorage _fileStorage;
		private Stream _stream;

		public UntransactedWrite(IOConnectionInfo ioc, NetFtpFileStorage fileStorage)
		{
			_ioc = ioc;
			_fileStorage = fileStorage;
		}

		public void Dispose()
		{
			if (_stream != null)
				_stream.Dispose();
			_stream = null;
		}

		public Stream OpenFile()
		{
			_stream = _fileStorage.OpenWrite(_ioc);
			return _stream;
		}

		public void CommitWrite()
		{
			_stream.Close();
		}
	}
}
#endif