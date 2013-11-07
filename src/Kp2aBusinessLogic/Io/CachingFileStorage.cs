using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Android.Content;
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
	public class CachingFileStorage: IFileStorage
	{
		private readonly IFileStorage _cachedStorage;
		private readonly ICacheSupervisor _cacheSupervisor;
		private readonly string _streamCacheDir;

		public CachingFileStorage(IFileStorage cachedStorage, string cacheDir, ICacheSupervisor cacheSupervisor)
		{
			_cachedStorage = cachedStorage;
			_cacheSupervisor = cacheSupervisor;
			_streamCacheDir = cacheDir + Java.IO.File.Separator + "OfflineCache" + Java.IO.File.Separator;
			if (!Directory.Exists(_streamCacheDir))
				Directory.CreateDirectory(_streamCacheDir);
			
		}

		public void ClearCache()
		{
			IoUtil.DeleteDir(new Java.IO.File(_streamCacheDir), true);
		}

		public IEnumerable<string> SupportedProtocols { get { return _cachedStorage.SupportedProtocols; } }

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
			return _streamCacheDir + iocAsHexString;
		}

		private bool IsCached(IOConnectionInfo ioc)
		{
			return File.Exists(CachedFilePath(ioc))
				&& File.Exists(VersionFilePath(ioc))
				&& File.Exists(BaseVersionFilePath(ioc));
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
					return OpenFileForReadWhenNoLocalChanges(ioc, cachedFilePath);
				}
				else
				{
					return OpenFileForReadWhenLocalChanges(ioc, cachedFilePath);
				}
			}
			catch (Exception ex)
			{
				if (!IsCached(ioc))
					throw;

				Kp2aLog.Log("couldn't open from remote " + ioc.Path);
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
				//no changes in remote file -> upload
				using (Stream localData = File.OpenRead(CachedFilePath(ioc)))
				{
					if (TryUpdateRemoteFile(localData, ioc, true, hash))
						_cacheSupervisor.UpdatedRemoteFileOnLoad(ioc);
				}
			}
			else
			{
				//conflict: both files changed.
				//signal that we're loading from local
				_cacheSupervisor.NotifyOpenFromLocalDueToConflict(ioc);
			}
			return File.OpenRead(cachedFilePath);
		}

		public MemoryStream GetRemoteDataAndHash(IOConnectionInfo ioc, out string hash)
		{
			MemoryStream remoteData = new MemoryStream();
			using (
				HashingStreamEx hashingRemoteStream = new HashingStreamEx(_cachedStorage.OpenFileForRead(ioc), false,
																		  new SHA256Managed()))
			{
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
			//open stream:
			using (Stream file = _cachedStorage.OpenFileForRead(ioc))
			{
				//copy to cache: 
				//note: we might use the file version to check if it's already in the cache and if copying is required. 
				//However, this is safer.
				string fileHash;
				using (HashingStreamEx cachedFile = new HashingStreamEx(File.Create(cachedFilePath), true, new SHA256Managed()))
				{
					file.CopyTo(cachedFile);
					cachedFile.Close();
					fileHash = MemUtil.ByteArrayToHexString(cachedFile.Hash);
				}

				//remember current hash
				string previousHash = null;
				string baseVersionFilePath = BaseVersionFilePath(ioc);
				if (File.Exists(baseVersionFilePath))
					previousHash = File.ReadAllText(baseVersionFilePath);

				//save hash in cache files:
				File.WriteAllText(VersionFilePath(ioc), fileHash);
				File.WriteAllText(baseVersionFilePath, fileHash);

				//notify supervisor what we did:
				if (previousHash != fileHash)
					_cacheSupervisor.UpdatedCachedFileOnLoad(ioc);
				else
					_cacheSupervisor.LoadedFromRemoteInSync(ioc);

				return File.OpenRead(cachedFilePath);	
			}
			
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

		private void UpdateRemoteFile(Stream cachedData, IOConnectionInfo ioc, bool useFileTransaction, string hash)
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
			private class CachedWriteMemoryStream : MemoryStream
			{
				private readonly IOConnectionInfo ioc;
				private readonly CachingFileStorage _cachingFileStorage;
				private readonly bool _useFileTransaction;
				private bool _closed;

				public CachedWriteMemoryStream(IOConnectionInfo ioc, CachingFileStorage cachingFileStorage, bool useFileTransaction)
				{
					this.ioc = ioc;
					_cachingFileStorage = cachingFileStorage;
					_useFileTransaction = useFileTransaction;
				}


				public override void Close()
				{
					if (_closed) return;

					//write file to cache:
					//(note: this might overwrite local changes. It's assumed that a sync operation or check was performed before
					string hash;
					using (var hashingStream = new HashingStreamEx(File.Create(_cachingFileStorage.CachedFilePath(ioc)), true, new SHA256Managed()))
					{
						Position = 0;
						CopyTo(hashingStream);

						hashingStream.Close();
						hash = MemUtil.ByteArrayToHexString(hashingStream.Hash);
					}

					File.WriteAllText(_cachingFileStorage.VersionFilePath(ioc), hash);
					//update file on remote. This might overwrite changes there as well, see above.
					Position = 0;
					if (_cachingFileStorage.IsCached(ioc))
					{
						//if the file already is in the cache, it's ok if writing to remote fails.
						_cachingFileStorage.TryUpdateRemoteFile(this, ioc, _useFileTransaction, hash);
					}
					else
					{
						//if not, we don't accept a failure (e.g. invalid credentials would always remain a problem)
						_cachingFileStorage.UpdateRemoteFile(this, ioc, _useFileTransaction, hash);
					}

					base.Close();

					_closed = true;
				}

			}

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
				_memoryStream = new CachedWriteMemoryStream(_ioc, _cachingFileStorage, _useFileTransaction);
				return _memoryStream;
			}

			public void CommitWrite()
			{	
				//the transaction is committed in the stream's Close
				_committed = true;
			}

		}


		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			//create a transaction which writes to memory stream
			//on close: write to cache. If possible, write to online 
			//update versions
			return new CachedWriteTransaction(ioc, useFileTransaction, this);
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
				UrlUtil.GetFileName(ioc.Path));
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


		public string GetBaseVersionHash(IOConnectionInfo ioc)
		{
			return File.ReadAllText(BaseVersionFilePath(ioc));
		}
		public string GetLocalVersionHash(IOConnectionInfo ioc)
		{
			return File.ReadAllText(VersionFilePath(ioc));
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
	}
}
