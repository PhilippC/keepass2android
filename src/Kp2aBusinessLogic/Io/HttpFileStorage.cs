using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using ModernHttpClient;

namespace keepass2android.Io
{
	public class HttpFileStorage: IFileStorage
	{
		public IEnumerable<string> SupportedProtocols
		{
			get
			{
				yield return "http";
				yield return "https";
			}
		}

		HttpClient GetHttpClient(IOConnectionInfo ioc)
		{
			var handler = new NativeMessageHandler();
			//var handler = new HttpClientHandler();
			
			if ((ioc.UserName.Length > 0) || (ioc.Password.Length > 0))
			{
				int backslashPos = ioc.UserName.IndexOf("\\", StringComparison.Ordinal);
				if (backslashPos > 0)
				{
					string domain = ioc.UserName.Substring(0, backslashPos);
					string user = ioc.UserName.Substring(backslashPos + 1);
					handler.Credentials = new NetworkCredential(user, ioc.Password, domain);
				}
				else
				{
					handler.PreAuthenticate = true;
					handler.Credentials = new NetworkCredential(ioc.UserName, ioc.Password);
				}
			}

			return new HttpClient(handler);
			
		}

		public void Delete(IOConnectionInfo ioc)
		{
			GetHttpClient(ioc).DeleteAsync(ioc.Path).Result.EnsureSuccessStatusCode();
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
			return GetHttpClient(ioc).GetStreamAsync(ioc.Path).Result;
		}


		class UploadOnCloseMemoryStream : MemoryStream
		{
			IOConnectionInfo ioc;
			private HttpClient client;


			public UploadOnCloseMemoryStream(IOConnectionInfo _ioc, HttpClient _client)
			{
				this.ioc = _ioc;
				this.client = _client;
			}

			public override void Close()
			{
				base.Close();
				var msg = new HttpRequestMessage(HttpMethod.Put, ioc.Path);
				msg.Headers.Add("Translate", "f");
				msg.Content = new StreamContent(new MemoryStream(this.ToArray()));

				client.SendAsync(msg).Result.EnsureSuccessStatusCode();
			}

		}
		public class UntransactedWrite : IWriteTransaction
		{
			private readonly IOConnectionInfo _ioc;
			private readonly HttpClient _client;


			public UntransactedWrite(IOConnectionInfo ioc, HttpClient client)
			{
				_ioc = ioc;
				_client = client;
			}

			public void Dispose()
			{

			}

			public Stream OpenFile()
			{
				return new UploadOnCloseMemoryStream(_ioc, _client);
			}

			public void CommitWrite()
			{

			}
		}

		public class TransactedWrite : IWriteTransaction
		{
			private readonly IOConnectionInfo _ioc;
			private readonly HttpFileStorage _fileStorage;
			private readonly IOConnectionInfo _iocTemp;

			

			public TransactedWrite(IOConnectionInfo ioc, HttpFileStorage fileStorage)
			{
				_ioc = ioc;
				_iocTemp = _ioc.CloneDeep();
				_iocTemp.Path += "." + new PwUuid(true).ToHexString().Substring(0, 6) + ".tmp";

				_fileStorage = fileStorage;
			}

			public void Dispose()
			{
				
			}

			public Stream OpenFile()
			{
				return new UploadOnCloseMemoryStream(_ioc, _fileStorage.GetHttpClient(_ioc));
			}

			public void CommitWrite()
			{
				var client = _fileStorage.GetHttpClient(_ioc);
				client.DeleteAsync(_ioc.Path).Result.EnsureSuccessStatusCode();
				var msg = new HttpRequestMessage(new HttpMethod("MOVE"), _iocTemp.Path);
				msg.Headers.Add("Destination",  _ioc.Path);
				client.SendAsync(msg).Result.EnsureSuccessStatusCode();
			}
		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			if (useFileTransaction)
				return new TransactedWrite(ioc, this);
			else
				return new UntransactedWrite(ioc, GetHttpClient(ioc));
		}

		public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
		{
			return UrlUtil.StripExtension(
				UrlUtil.GetFileName(ioc.Path));
		}

		public bool RequiresCredentials(IOConnectionInfo ioc)
		{
			return ioc.CredSaveMode != IOCredSaveMode.SaveCred;
		}

		public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
		{
			throw new NotImplementedException();
		}

		public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public FileDescription GetFileDescription(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
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
			return ioc.GetDisplayName();
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
		
		}
	}
}