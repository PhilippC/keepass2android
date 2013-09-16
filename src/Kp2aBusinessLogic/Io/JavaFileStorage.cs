using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Lang;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using Keepass2android.Javafilestorage;
using Exception = System.Exception;
using FileNotFoundException = Java.IO.FileNotFoundException;

namespace keepass2android.Io
{
	public abstract class JavaFileStorage: IFileStorage
	{
		public abstract IEnumerable<string> SupportedProtocols { get; }

		private IJavaFileStorage _jfs;

		public JavaFileStorage(IJavaFileStorage jfs)
		{
			this._jfs = jfs;
		}

		public void DeleteFile(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
		{
			try
			{
				return _jfs.CheckForFileChangeFast(ioc.Path, previousFileVersion);
			}
			catch (Java.Lang.Exception e)
			{
				throw LogAndConvertJavaException(e);
			}

		}

		public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
		{
			try
			{
				return _jfs.GetCurrentFileVersionFast(ioc.Path);
			}
			catch (Java.Lang.Exception e)
			{
				throw LogAndConvertJavaException(e);
			} 
		}

		public Stream OpenFileForRead(IOConnectionInfo ioc)
		{
			try
			{
				return _jfs.OpenFileForRead(IocToPath(ioc));
			}
			catch (FileNotFoundException e)
			{
				throw new System.IO.FileNotFoundException(e.Message, e);
			}
			catch (Java.Lang.Exception e)
			{
				throw LogAndConvertJavaException(e);
			}
		}


		private static Exception LogAndConvertJavaException(Java.Lang.Exception e)
		{
			Kp2aLog.Log(e.Message);
			var ex = new Exception(e.LocalizedMessage ?? e.Message, e);
			return ex; 
		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			return new JavaFileStorageWriteTransaction(IocToPath(ioc), useFileTransaction, _jfs);
		}

		public IFileStorageSetup RequiredSetup 
		{
			get
			{
				if (_jfs.IsConnected)
					return null;
				return new JavaFileStorageSetup(this);
			}
		}

		public class JavaFileStorageSetup : IFileStorageSetup, IFileStorageSetupOnResume
		{
			private readonly JavaFileStorage _javaFileStorage;

			public JavaFileStorageSetup(JavaFileStorage javaFileStorage)
			{
				_javaFileStorage = javaFileStorage;
			}

			public bool TrySetup(Activity activity)
			{
				try
				{
					return _javaFileStorage._jfs.TryConnect(activity);
				}
				catch (Java.Lang.Exception e)
				{
					throw LogAndConvertJavaException(e);
				}
			}

			public bool TrySetupOnResume(Activity activity)
			{
				try
				{
					_javaFileStorage._jfs.OnResume();
					return _javaFileStorage._jfs.IsConnected;
				}
				catch (Java.Lang.Exception e)
				{
					throw LogAndConvertJavaException(e);
				}
			}
		}

		class JavaFileStorageWriteTransaction: IWriteTransaction
		{
			private readonly string _path;
			private readonly bool _useFileTransaction;
			private readonly IJavaFileStorage _javaFileStorage;
			private MemoryStream _memoryStream;

			public JavaFileStorageWriteTransaction(string path, bool useFileTransaction, IJavaFileStorage javaFileStorage)
			{
				_path = path;
				_useFileTransaction = useFileTransaction;
				_javaFileStorage = javaFileStorage;
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
				try
				{
					_javaFileStorage.UploadFile(_path, _memoryStream.ToArray(), _useFileTransaction);
				}
				catch (Java.Lang.Exception e)
				{
					LogAndConvertJavaException(e);
				}
			}
		}

		public bool CompleteIoId()
		{
			throw new NotImplementedException();
		}

		public bool? FileExists()
		{
			throw new NotImplementedException();
		}

		public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
		{
			return UrlUtil.StripExtension(
					UrlUtil.GetFileName(IocToPath(ioc)));
		}

		public bool RequiresCredentials(IOConnectionInfo ioc)
		{
			return false;
		}

		private static string IocToPath(IOConnectionInfo ioc)
		{
			int protocolLength = ioc.Path.IndexOf("://", StringComparison.Ordinal);

			if (protocolLength < 0)
				return ioc.Path;
			else
				return ioc.Path.Substring(protocolLength + 3);
		}
	}
}