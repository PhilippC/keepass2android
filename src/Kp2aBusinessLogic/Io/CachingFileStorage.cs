using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using KeePassLib.Cryptography;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace keepass2android.Io
{
	/// <summary>
	/// Interface for classes which can handle certain Cache events on a higher level (e.g. by user interaction)
	/// </summary>
	public interface ICacheSupervisor
	{
		/// <summary>
		/// called when a save operation only updated the cache but not the remote file
		/// </summary>
		/// <param name="ioc">The file which we tried to write</param>
		/// <param name="ex">The exception why the remote file couldn't be updated</param>
		void CouldntSaveToRemote(IOConnectionInfo ioc, Exception ex);

		/// <summary>
		/// Called when only the local file could be opened during an open operation.
		/// </summary>
		void CouldntOpenFromRemote(IOConnectionInfo ioc, Exception ex);

		/// <summary>
		/// Called when the local file either didn't exist or was unmodified, so the remote file
		/// was loaded and the cache was updated during the load operation.
		/// </summary>
		void UpdatedCachedFileOnLoad(IOConnectionInfo ioc);

		/// <summary>
		/// Called when the remote file either didn't exist or was unmodified, so the local file
		/// was loaded and the remote file was updated during the load operation.
		/// </summary>
		void UpdatedRemoteFileOnLoad(IOConnectionInfo ioc);

		/// <summary>
		/// Called to notify the supervisor that the file described by ioc is opened from the cache because there's a conflict
		/// with local and remote changes
		/// </summary>
		/// <param name="ioc"></param>
		void NotifyOpenFromLocalDueToConflict(IOConnectionInfo ioc);

		/// <summary>
		/// Called when the load operation was performed and the remote file was identical with the local file
		/// </summary>
		void LoadedFromRemoteInSync(IOConnectionInfo ioc);
	}


	/// <summary>
	/// Implements the IFileStorage interface as a proxy: A base storage is used as a remote storage. Local files are used to cache the
	/// files on remote.
	/// </summary>
	public class CachingFileStorage : IFileStorage, IOfflineSwitchable, IPermissionRequestingFileStorage
	{
		
		protected readonly OfflineSwitchableFileStorage _cachedStorage;
		private readonly ICacheSupervisor _cacheSupervisor;
		private readonly string _legacyCacheDir;
	    private readonly string _cacheDir;

        public CachingFileStorage(IFileStorage cachedStorage, Context cacheDirContext, ICacheSupervisor cacheSupervisor)
		{
			_cachedStorage = new OfflineSwitchableFileStorage(cachedStorage);
			_cacheSupervisor = cacheSupervisor;
			_legacyCacheDir = cacheDirContext.CacheDir.Path + Java.IO.File.Separator + "OfflineCache" + Java.IO.File.Separator;
			if (!Directory.Exists(_legacyCacheDir))
				Directory.CreateDirectory(_legacyCacheDir);

		    _cacheDir = IoUtil.GetInternalDirectory(cacheDirContext).Path + Java.IO.File.Separator + "OfflineCache" + Java.IO.File.Separator;
		    if (!Directory.Exists(_cacheDir))
		        Directory.CreateDirectory(_cacheDir);

        }

		public void ClearCache()
		{
			IoUtil.DeleteDir(new Java.IO.File(_legacyCacheDir), true);
		    IoUtil.DeleteDir(new Java.IO.File(_cacheDir), true);
        }

		public IEnumerable<string> SupportedProtocols { get { return _cachedStorage.SupportedProtocols; } }

	    public bool UserShouldBackup
	    {
	        get { return _cachedStorage.UserShouldBackup; }
	    }

	    public void DeleteFile(IOConnectionInfo ioc)
		{
			if (IsCached(ioc))
			{
				File.Delete(CachedFilePath(ioc));
				File.Delete(VersionFilePath(ioc));
				File.Delete(BaseVersionFilePath(ioc));
			}
			
			_cachedStorage.Delete(ioc);
		}

		private string CachedFilePath(IOConnectionInfo ioc)
		{
			SHA256Managed sha256 = new SHA256Managed();
			string iocAsHexString = MemUtil.ByteArrayToHexString(sha256.ComputeHash(Encoding.Unicode.GetBytes(ioc.Path.ToCharArray())))+".cache";
		    if (File.Exists(_legacyCacheDir + iocAsHexString))
		        return _legacyCacheDir + iocAsHexString;

		    return _cacheDir + iocAsHexString;

		}

		public bool IsCached(IOConnectionInfo ioc)
		{
			bool result = File.Exists(CachedFilePath(ioc))
				&& File.Exists(VersionFilePath(ioc))
				&& File.Exists(BaseVersionFilePath(ioc));

			Kp2aLog.Log(ioc.GetDisplayName() + " isCached = " + result);

            return result;
        }

		public void Delete(IOConnectionInfo ioc)
		{
			_cachedStorage.Delete(ioc);
		}

		public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
		{
			//see comment in GetCurrentFileVersionFast
			return false;
		}

		public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
		{
			//fast file version checking is not supported by CachingFileStorage:
			//it's hard to return good versions in cases that the base source is offline
			//or after modifying the cache.
			//It's probably not relevant because fast file version checking is meant for local storage
			//which is not cached.
			return String.Empty;
		}

		private string VersionFilePath(IOConnectionInfo ioc)
		{
			return CachedFilePath(ioc)+".version";
		}

		private string BaseVersionFilePath(IOConnectionInfo ioc)
		{
			return CachedFilePath(ioc) + ".baseversion";
		}

		public Stream OpenFileForRead(IOConnectionInfo ioc)
		{
			string cachedFilePath = CachedFilePath(ioc);
			try
			{
				if (!IsCached(ioc)
				    || GetLocalVersionHash(ioc) == GetBaseVersionHash(ioc))
				{
					Kp2aLog.Log("CFS: OpenWhenNoLocalChanges");
					return OpenFileForReadWhenNoLocalChanges(ioc, cachedFilePath);
				}
				else
				{
					Kp2aLog.Log("CFS: OpenWhenLocalChanges");
					return OpenFileForReadWhenLocalChanges(ioc, cachedFilePath);
				}
			}
			catch (Exception ex)
			{
				if (!IsCached(ioc))
					throw;

#if DEBUG
                Kp2aLog.Log("couldn't open from remote " + ioc.Path);
#endif
				Kp2aLog.Log(ex.ToString());

				_cacheSupervisor.CouldntOpenFromRemote(ioc, ex);
				return File.OpenRead(cachedFilePath);
			}
		}

		private Stream OpenFileForReadWhenLocalChanges(IOConnectionInfo ioc, string cachedFilePath)
		{
			//file is cached but has local modifications
			//try to upload the changes if remote file doesn't have changes as well:
			var hash = CalculateHash(ioc);

			if (File.ReadAllText(BaseVersionFilePath(ioc)) == hash)
			{
				Kp2aLog.Log("CFS: No changes in remote");
				//no changes in remote file -> upload
				using (Stream localData = File.OpenRead(CachedFilePath(ioc)))
				{
					if (TryUpdateRemoteFile(localData, ioc, true, hash))
					{
						_cacheSupervisor.UpdatedRemoteFileOnLoad(ioc);
						Kp2aLog.Log("CFS: Updated remote file");
					}
					return File.OpenRead(cachedFilePath);
				}
			}
			else
			{
				Kp2aLog.Log("CFS: Files in conflict");
				//conflict: both files changed.
				return OpenFileForReadWithConflict(ioc, cachedFilePath);
			}
			
		}

		protected virtual Stream OpenFileForReadWithConflict(IOConnectionInfo ioc, string cachedFilePath)
		{
			//signal that we're loading from local
			_cacheSupervisor.NotifyOpenFromLocalDueToConflict(ioc);
			return File.OpenRead(cachedFilePath);
		}

		public MemoryStream GetRemoteDataAndHash(IOConnectionInfo ioc, out string hash)
		{
			MemoryStream remoteData = new MemoryStream();
			
			using (var remoteStream =_cachedStorage.OpenFileForRead(ioc))
			{
				//note: directly copying to remoteData and hashing causes NullReferenceExceptions in FTP and with Digest auth
				// -> use the temp data approach:
				MemoryStream tempData = new MemoryStream();
				remoteStream.CopyTo(tempData);
				tempData.Position = 0;
				HashingStreamEx hashingRemoteStream = new HashingStreamEx(tempData, false, new SHA256Managed());
				
				hashingRemoteStream.CopyTo(remoteData);
				hashingRemoteStream.Close();
				hash = MemUtil.ByteArrayToHexString(hashingRemoteStream.Hash);
			}
			remoteData.Position = 0;
			return remoteData;
		}

		private string CalculateHash(IOConnectionInfo ioc)
		{
			string hash;
			GetRemoteDataAndHash(ioc, out hash);
			return hash;
		}

		private Stream OpenFileForReadWhenNoLocalChanges(IOConnectionInfo ioc, string cachedFilePath)
		{

			//remember current hash
			string previousHash = null;
			string baseVersionFilePath = BaseVersionFilePath(ioc);
			if (File.Exists(baseVersionFilePath))
			{
				Kp2aLog.Log("CFS: hashing cached version");
				previousHash = File.ReadAllText(baseVersionFilePath);
			}

			//copy to cache: 
			var fileHash = UpdateCacheFromRemote(ioc, cachedFilePath);

			//notify supervisor what we did:
			if (previousHash != fileHash)
			{
				Kp2aLog.Log("CFS: Updated Cache");
				_cacheSupervisor.UpdatedCachedFileOnLoad(ioc);
			}
			else
			{
				Kp2aLog.Log("CFS: Files in Sync");
				_cacheSupervisor.LoadedFromRemoteInSync(ioc);
			}

			return File.OpenRead(cachedFilePath);	

			
		}

		/// <summary>
		/// copies the file in ioc to the local cache. Updates the cache version files and returns the new file hash.
		/// </summary>
		protected string UpdateCacheFromRemote(IOConnectionInfo ioc, string cachedFilePath)
		{
			//note: we might use the file version to check if it's already in the cache and if copying is required. 
			//However, this is safer.
			string fileHash;
			
			//open stream:
			using (Stream remoteFile = _cachedStorage.OpenFileForRead(ioc))
			{

				using (HashingStreamEx cachedFile = new HashingStreamEx(File.Create(cachedFilePath), true, new SHA256Managed()))
				{
					remoteFile.CopyTo(cachedFile);
					cachedFile.Close();
					fileHash = MemUtil.ByteArrayToHexString(cachedFile.Hash);
				}
			}

			//save hash in cache files:
			File.WriteAllText(VersionFilePath(ioc), fileHash);
			File.WriteAllText(BaseVersionFilePath(ioc), fileHash);
			return fileHash;
		}

		private bool TryUpdateRemoteFile(Stream cachedData, IOConnectionInfo ioc, bool useFileTransaction, string hash)
		{
			try
			{
				UpdateRemoteFile(cachedData, ioc, useFileTransaction, hash);
				return true;
			}
			catch (Exception e)
			{
				Kp2aLog.Log("couldn't save to remote " + ioc.Path);
				Kp2aLog.Log(e.ToString());
				//notify the supervisor so it might display a warning or schedule a retry
				_cacheSupervisor.CouldntSaveToRemote(ioc, e);
				return false;
			}
		}

		protected void UpdateRemoteFile(Stream cachedData, IOConnectionInfo ioc, bool useFileTransaction, string hash)
		{
			//try to write to remote:
			using (
				IWriteTransaction remoteTrans = _cachedStorage.OpenWriteTransaction(ioc, useFileTransaction))
			{
				Stream remoteStream = remoteTrans.OpenFile();
				cachedData.CopyTo(remoteStream);
				remoteStream.Close();
				remoteTrans.CommitWrite();
			}
			//success. Update base-version of cache:
			File.WriteAllText(BaseVersionFilePath(ioc), hash);
			File.WriteAllText(VersionFilePath(ioc), hash);
		}

		public void UpdateRemoteFile(IOConnectionInfo ioc, bool useFileTransaction)
		{
			using (Stream cachedData = File.OpenRead(CachedFilePath(ioc)))
			{
				UpdateRemoteFile(cachedData, ioc, useFileTransaction, GetLocalVersionHash(ioc));	
			}
			
		}

		

		private class CachedWriteTransaction: IWriteTransaction
		{
			

			private readonly IOConnectionInfo _ioc;
			private readonly bool _useFileTransaction;
			private readonly CachingFileStorage _cachingFileStorage;
			private MemoryStream _memoryStream;
			private bool _committed;

			public CachedWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction, CachingFileStorage cachingFileStorage)
			{
				_ioc = ioc;
				_useFileTransaction = useFileTransaction;
				_cachingFileStorage = cachingFileStorage;
			}

			public void Dispose()
			{
				if (!_committed)
				{
					try
					{
						_memoryStream.Dispose();
					}
					catch (ObjectDisposedException e)
					{
						Kp2aLog.Log("Ignoring exception in Dispose: "+e);
					}
					
				}
					
			}

			public Stream OpenFile()
			{
				_memoryStream = new MemoryStream();
				return _memoryStream;
			}

			public void CommitWrite()
			{	
				_committed = true;
			    _memoryStream.Close();

			    //write file to cache:
			    //(note: this might overwrite local changes. It's assumed that a sync operation or check was performed before

			    byte[] output = _memoryStream.ToArray();

			    string hash;
			    using (var hashingStream = new HashingStreamEx(File.Create(_cachingFileStorage.CachedFilePath(_ioc)), true, new SHA256Managed()))
			    {
			        hashingStream.Write(output, 0, output.Length);

			        hashingStream.Close();
			        hash = MemUtil.ByteArrayToHexString(hashingStream.Hash);
			    }

			    File.WriteAllText(_cachingFileStorage.VersionFilePath(_ioc), hash);
                //create another memory stream which is open for reading again
                MemoryStream openMemStream = new MemoryStream(output);
                //update file on remote. This might overwrite changes there as well, see above.
                if (_cachingFileStorage.IsCached(_ioc))
			    {
			        //if the file already is in the cache, it's ok if writing to remote fails.
			        _cachingFileStorage.TryUpdateRemoteFile(openMemStream, _ioc, _useFileTransaction, hash);
			    }
			    else
			    {
			        //if not, we don't accept a failure (e.g. invalid credentials would always remain a problem)
			        _cachingFileStorage.UpdateRemoteFile(openMemStream, _ioc, _useFileTransaction, hash);
			    }
			    
                openMemStream.Dispose();
			}

        }


		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			//create a transaction which writes to memory stream
			//on close: write to cache. If possible, write to online 
			//update versions
			return new CachedWriteTransaction(ioc, useFileTransaction, this);
		}

		public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
		{
			return _cachedStorage.GetFilenameWithoutPathAndExt(ioc);
		}

	    public string GetFileExtension(IOConnectionInfo ioc)
	    {
	        return _cachedStorage.GetFileExtension(ioc);
	    }

	    public bool RequiresCredentials(IOConnectionInfo ioc)
		{
			return _cachedStorage.RequiresCredentials(ioc);
		}

		public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
		{
			_cachedStorage.CreateDirectory(ioc, newDirName);
		}

		public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
		{
			return _cachedStorage.ListContents(ioc);
		}

		public FileDescription GetFileDescription(IOConnectionInfo ioc)
		{
			return _cachedStorage.GetFileDescription(ioc);
		}

		public bool RequiresSetup(IOConnectionInfo ioConnection)
		{
			return _cachedStorage.RequiresSetup(ioConnection);
		}

		public string IocToPath(IOConnectionInfo ioc)
		{
			return _cachedStorage.IocToPath(ioc);
		}

		public void StartSelectFile(IFileStorageSetupInitiatorActivity activity, bool isForSave, int requestCode, string protocolId)
		{
			_cachedStorage.StartSelectFile(activity, isForSave, requestCode, protocolId);
		}

		public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode, bool alwaysReturnSuccess)
		{
			//we try to prepare the file usage by the underlying file storage but if the ioc is cached, set the flag to ignore errors 
			_cachedStorage.PrepareFileUsage(activity, ioc, requestCode, alwaysReturnSuccess || IsCached(ioc));
		}

		public void PrepareFileUsage(Context ctx, IOConnectionInfo ioc)
		{
			_cachedStorage.PrepareFileUsage(ctx, ioc);
		}

		public void OnCreate(IFileStorageSetupActivity activity, Bundle savedInstanceState)
		{
			_cachedStorage.OnCreate(activity, savedInstanceState);
		}

		public void OnResume(IFileStorageSetupActivity activity)
		{
			_cachedStorage.OnResume(activity);
		}

		public void OnStart(IFileStorageSetupActivity activity)
		{
			_cachedStorage.OnStart(activity);
		}

		public void OnActivityResult(IFileStorageSetupActivity activity, int requestCode, int resultCode, Intent data)
		{
			_cachedStorage.OnActivityResult(activity, requestCode, resultCode, data);
		}

		public string GetDisplayName(IOConnectionInfo ioc)
		{
			return _cachedStorage.GetDisplayName(ioc);
		}

		public string CreateFilePath(string parent, string newFilename)
		{
			return _cachedStorage.CreateFilePath(parent, newFilename);
		}

		public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
		{
			return _cachedStorage.GetParentPath(ioc);
		}

		public IOConnectionInfo GetFilePath(IOConnectionInfo folderPath, string filename)
		{
			try
			{
				IOConnectionInfo res = _cachedStorage.GetFilePath(folderPath, filename);
				//some file storage implementations require accessing the network to determine the file path (e.g. because
				//they might contain file ids). In this case, we need to cache the result to enable cached access to such files
				StoreFilePath(folderPath, filename, res);
				return res;
			}
			catch (Exception)
			{
				IOConnectionInfo res;
				if (!TryGetCachedFilePath(folderPath, filename, out res)) throw;
				return res;
			}
			
		}

		public bool IsPermanentLocation(IOConnectionInfo ioc)
		{
			//even though the cache would be permanent, it's not a good idea to cache a temporary file, so return false in that case:
			return _cachedStorage.IsPermanentLocation(ioc);
		}

		public bool IsReadOnly(IOConnectionInfo ioc, OptionalOut<UiStringKey> reason = null)
		{
			//even though the cache can always be written, the changes made in the cache could not be transferred to the cached file
			//so we better treat the cache as read-only as well.
			return _cachedStorage.IsReadOnly(ioc, reason);
		}

		private void StoreFilePath(IOConnectionInfo folderPath, string filename, IOConnectionInfo res)
		{
			File.WriteAllText(CachedFilePath(GetPseudoIoc(folderPath, filename)) + ".filepath", res.Path);
		}

		private IOConnectionInfo GetPseudoIoc(IOConnectionInfo folderPath, string filename)
		{
			IOConnectionInfo res = folderPath.CloneDeep();
			if (!res.Path.EndsWith("/"))
				res.Path += "/";
			res.Path += filename;
			return res;
		}

		private bool TryGetCachedFilePath(IOConnectionInfo folderPath, string filename, out IOConnectionInfo res)
		{
			res = folderPath.CloneDeep();
			string filePathCache = CachedFilePath(GetPseudoIoc(folderPath, filename)) + ".filepath";
			if (!File.Exists(filePathCache))
				return false;
			res.Path = File.ReadAllText(filePathCache);
			return true;
		}


		public string GetBaseVersionHash(IOConnectionInfo ioc)
		{
			string hash = File.ReadAllText(BaseVersionFilePath(ioc));
            Kp2aLog.Log(ioc.GetDisplayName() + " baseVersionHash = " + hash);
			return hash;
        }
		public string GetLocalVersionHash(IOConnectionInfo ioc)
		{
			string hash = File.ReadAllText(VersionFilePath(ioc));
            Kp2aLog.Log(ioc.GetDisplayName() + " localVersionHash = " + hash);
			return hash;
		}
		public bool HasLocalChanges(IOConnectionInfo ioc)
		{
			return IsCached(ioc)
			       && GetLocalVersionHash(ioc) != GetBaseVersionHash(ioc);
		}

		public Stream OpenRemoteForReadIfAvailable(IOConnectionInfo ioc)
		{
			try
			{
				return _cachedStorage.OpenFileForRead(ioc);
			}
			catch (Exception)
			{
				return File.OpenRead(CachedFilePath(ioc));
			}
		}

		public bool IsOffline
		{
			get { return _cachedStorage.IsOffline; }
			set { _cachedStorage.IsOffline = value; }
		}

		public void OnRequestPermissionsResult(IFileStorageSetupActivity fileStorageSetupActivity, int requestCode,
			string[] permissions, Permission[] grantResults)
		{
			_cachedStorage.OnRequestPermissionsResult(fileStorageSetupActivity, requestCode, permissions, grantResults);
		}
	}
}
