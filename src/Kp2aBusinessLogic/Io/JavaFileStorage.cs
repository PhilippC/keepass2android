using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using KeePassLib.Serialization;
using KeePassLib.Utility;
#if !EXCLUDE_JAVAFILESTORAGE
using Keepass2android.Javafilestorage;
#endif
using Exception = System.Exception;
using FileNotFoundException = Java.IO.FileNotFoundException;

namespace keepass2android.Io
{
	#if !EXCLUDE_JAVAFILESTORAGE
	public abstract class JavaFileStorage: IFileStorage, IPermissionRequestingFileStorage
	{
		protected string Protocol { get { return _jfs.ProtocolId; } }

		public virtual IEnumerable<string> SupportedProtocols { get { yield return Protocol; } }


		private readonly IJavaFileStorage _jfs;
		private readonly IKp2aApp _app;

		public JavaFileStorage(IJavaFileStorage jfs, IKp2aApp app)
		{
			_jfs = jfs;
			_app = app;
		}

		public void Delete(IOConnectionInfo ioc)
		{
			try
			{
				Jfs.Delete(IocToPath(ioc));
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

		public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
		{
			return false;

			//commented because this currently might use the network which is not permitted here
			/*try
			{
				return Jfs.CheckForFileChangeFast(ioc.Path, previousFileVersion);
			}
			catch (Java.Lang.Exception e)
			{
				throw LogAndConvertJavaException(e);
			}*/

		}

		public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
		{
			try
			{
				return Jfs.GetCurrentFileVersionFast(IocToPath(ioc));
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
				return Jfs.OpenFileForRead(IocToPath(ioc));
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


		private Exception LogAndConvertJavaException(Exception e)
		{
			Kp2aLog.Log(e.Message);

			if (e is UserInteractionRequiredException)
				return e;
			//seems like UserInteractionRequiredException is not propagated correctly into the C# world, we can't catch it
			// -> rethrow correctly here
			// Note: the Contains-check looks a bit broad, but it should be safe
			if (e.ToString().Contains("keepass2android.javafilestorage.UserInteractionRequiredException"))
			{
				throw new UserInteractionRequiredException();
			}

			Java.Lang.Exception exception = e as Java.Lang.Exception;
			if (exception != null)
			{
				var ex = new Exception(exception.LocalizedMessage ??
					e.Message ??
					_app.GetResourceString(UiStringKey.ErrorOcurred) + exception.GetType().Name, e);
				return ex; 	
			}

			return e;

		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			return new JavaFileStorageWriteTransaction(IocToPath(ioc), useFileTransaction, this);
		}

		internal IJavaFileStorage Jfs
		{
			get { return _jfs; }
		}


		class JavaFileStorageWriteTransaction: IWriteTransaction
		{
			private readonly string _path;
			private readonly bool _useFileTransaction;
			private readonly JavaFileStorage _javaFileStorage;
			private MemoryStream _memoryStream;

			public JavaFileStorageWriteTransaction(string path, bool useFileTransaction, JavaFileStorage javaFileStorage)
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
					_javaFileStorage.Jfs.UploadFile(_path, _memoryStream.ToArray(), _useFileTransaction);
				}
				catch (Java.Lang.Exception e)
				{
					throw _javaFileStorage.LogAndConvertJavaException(e);
				}
			}
		}

		public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
		{
			return UrlUtil.StripExtension(
					_jfs.GetFilename(IocToPath(ioc)));
		}

		public bool RequiresCredentials(IOConnectionInfo ioc)
		{
			return false;
		}

		public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
		{
			try
			{
				Jfs.CreateFolder(IocToPath(ioc), newDirName);
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

		public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
		{
			try
			{
				IList<JavaFileStorageFileEntry> entries = Jfs.ListFiles(IocToPath(ioc));

				return entries.Select(ConvertToFileDescription);

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

		private FileDescription ConvertToFileDescription(JavaFileStorageFileEntry e)
		{
			return new FileDescription
				{
					CanRead = e.CanRead,
					CanWrite = e.CanWrite,
					DisplayName = e.DisplayName,
					IsDirectory = e.IsDirectory,
					LastModified = JavaTimeToCSharp(e.LastModifiedTime),
					Path = e.Path,
					SizeInBytes = e.SizeInBytes
				};
		}

		public FileDescription GetFileDescription(IOConnectionInfo ioc)
		{
			Kp2aLog.Log("GetFileDescription "+ioc.Path);
			try
			{
				return ConvertToFileDescription(Jfs.GetFileEntry(IocToPath(ioc)));
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

		public bool RequiresSetup(IOConnectionInfo ioConnection)
		{
			return _jfs.RequiresSetup(IocToPath(ioConnection));
		}

		public void StartSelectFile(IFileStorageSetupInitiatorActivity activity, bool isForSave, int requestCode, string protocolId)
		{
			Kp2aLog.Log("StartSelectFile " + protocolId);
			_jfs.StartSelectFile((IJavaFileStorageFileStorageSetupInitiatorActivity) activity, isForSave, requestCode);
		}

		public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode, Boolean alwaysReturnSuccess)
		{
			try
			{
				_jfs.PrepareFileUsage((IJavaFileStorageFileStorageSetupInitiatorActivity) activity, IocToPath(ioc), requestCode,
				                      alwaysReturnSuccess);
			}
			catch (Exception e)
			{
				throw LogAndConvertJavaException(e);
			}
		}

		public void PrepareFileUsage(Context ctx, IOConnectionInfo ioc)
		{
			try
			{
				_jfs.PrepareFileUsage(ctx, IocToPath(ioc));
			}
			catch (Exception e)
			{
				throw LogAndConvertJavaException(e);
			}
			
		}

		public bool IsPermanentLocation(IOConnectionInfo ioc)
		{
			return true;
		}

		public bool IsReadOnly(IOConnectionInfo ioc, OptionalOut<UiStringKey> reason = null)
		{
			return false; //TODO implement. note, however, that we MAY return false even if it's read-only
		}

		public void OnCreate(IFileStorageSetupActivity activity, Bundle savedInstanceState)
		{
			_jfs.OnCreate(((IJavaFileStorageFileStorageSetupActivity)activity), savedInstanceState);
		}

		public void OnResume(IFileStorageSetupActivity activity)
		{
			Kp2aLog.Log("JFS/OnResume Ioc.Path=" +activity.Ioc.Path+". Path="+((IJavaFileStorageFileStorageSetupActivity)activity).Path);
			_jfs.OnResume(((IJavaFileStorageFileStorageSetupActivity) activity));
		}

		public void OnStart(IFileStorageSetupActivity activity)
		{
			_jfs.OnStart(((IJavaFileStorageFileStorageSetupActivity) activity));
		}

		public void OnActivityResult(IFileStorageSetupActivity activity, int requestCode, int resultCode, Intent data)
		{
			_jfs.OnActivityResult(((IJavaFileStorageFileStorageSetupActivity) activity), requestCode, resultCode, data);
		}

		public string GetDisplayName(IOConnectionInfo ioc)
		{
			return _jfs.GetDisplayName(ioc.Path);
		}

		public string CreateFilePath(string parent, string newFilename)
		{
			return _jfs.CreateFilePath(parent, newFilename);
		}

		public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
		{
			return IoUtil.GetParentPath(ioc);
		}

		public IOConnectionInfo GetFilePath(IOConnectionInfo folderPath, string filename)
		{
			try
			{
				return IOConnectionInfo.FromPath(
				ListContents(folderPath).Where(desc => { return desc.DisplayName == filename; })
					.Single()
					.Path);
			}
			catch (Exception e)
			{
				throw new Exception("Error finding " + filename + " in " + folderPath.GetDisplayName(), e);
			}
			
		}

		private DateTime JavaTimeToCSharp(long javatime)
		{
			return new DateTime(1970, 1, 1).AddMilliseconds(javatime);

		}

		public virtual string IocToPath(IOConnectionInfo ioc)
		{
			return ioc.Path;
		}

		public void OnRequestPermissionsResult(IFileStorageSetupActivity fileStorageSetupActivity, int requestCode,
			string[] permissions, Permission[] grantResults)
		{
			_jfs.OnRequestPermissionsResult(((IJavaFileStorageFileStorageSetupActivity) fileStorageSetupActivity), requestCode,
				permissions, grantResults.Select(p => (int)p).ToArray());
		}
	}
#endif
}