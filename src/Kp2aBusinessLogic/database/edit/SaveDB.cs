/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System;
using System.IO;
using System.Security.Cryptography;
using Android.Content;
using Android.OS;
using Java.Lang;
using KeePassLib;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using keepass2android.Io;
using Debug = System.Diagnostics.Debug;
using Exception = System.Exception;

namespace keepass2android
{

	public class SaveDb : RunnableOnFinish {
		private readonly IKp2aApp _app;
		private readonly bool _dontSave;

		/// <summary>
		/// stream for reading the data from the original file. If this is set to a non-null value, we know we need to sync
		/// </summary>
		private readonly Stream _streamForOrigFile;
		private readonly Context _ctx;
		private Thread _workerThread;

		public SaveDb(Context ctx, IKp2aApp app, OnFinish finish, bool dontSave)
			: base(finish)
		{
			_ctx = ctx;
			_app = app;
			_dontSave = dontSave;
		}
		
		/// <summary>
		/// Constructor for sync
		/// </summary>
		/// <param name="ctx"></param>
		/// <param name="app"></param>
		/// <param name="finish"></param>
		/// <param name="dontSave"></param>
		/// <param name="streamForOrigFile">Stream for reading the data from the (changed) original location</param>
		public SaveDb(Context ctx, IKp2aApp app, OnFinish finish, bool dontSave, Stream streamForOrigFile)
			: base(finish)
		{
			_ctx = ctx;
			_app = app;
			_dontSave = dontSave;
			_streamForOrigFile = streamForOrigFile;
		}

		public SaveDb(Context ctx, IKp2aApp app, OnFinish finish)
			: base(finish)
		{
			_ctx = ctx;
			_app = app;
			_dontSave = false;
		}
		
		
		public override void Run ()
		{

			if (!_dontSave)
			{
				try
				{
					if (_app.GetDb().CanWrite == false)
					{
						//this should only happen if there is a problem in the UI so that the user sees an edit interface.
						Finish(false,"Cannot save changes. File is read-only!");
						return;
					}

					StatusLogger.UpdateMessage(UiStringKey.saving_database);
					IOConnectionInfo ioc = _app.GetDb().Ioc;
					IFileStorage fileStorage = _app.GetFileStorage(ioc);

					if (_streamForOrigFile == null)
					{
						if ((!_app.GetBooleanPreference(PreferenceKey.CheckForFileChangesOnSave))
							|| (_app.GetDb().KpDatabase.HashOfFileOnDisk == null)) //first time saving
						{
							PerformSaveWithoutCheck(fileStorage, ioc);
							Finish(true);
							return;
						}	
					}
					

					if (
						(_streamForOrigFile != null)
						|| fileStorage.CheckForFileChangeFast(ioc, _app.GetDb().LastFileVersion)  //first try to use the fast change detection
						|| (FileHashChanged(ioc, _app.GetDb().KpDatabase.HashOfFileOnDisk) == FileHashChange.Changed) //if that fails, hash the file and compare:
						)
					{

						//ask user...
						_app.AskYesNoCancel(UiStringKey.TitleSyncQuestion, UiStringKey.MessageSyncQuestion, 
							UiStringKey.YesSynchronize,
							UiStringKey.NoOverwrite,
							//yes = sync
							(sender, args) =>
							{
								Action runHandler = () =>
									{
										//note: when synced, the file might be downloaded once again from the server. Caching the data
										//in the hashing function would solve this but increases complexity. I currently assume the files are 
										//small.
										MergeIn(fileStorage, ioc);
										PerformSaveWithoutCheck(fileStorage, ioc);
										Finish(true);
									};
								RunInWorkerThread(runHandler);
							},
							//no = overwrite
							(sender, args) =>
							{
								RunInWorkerThread(() =>
									{
										PerformSaveWithoutCheck(fileStorage, ioc);
										Finish(true);
									});
							},
							//cancel 
							(sender, args) =>
							{
								RunInWorkerThread(() => Finish(false));
							},
							_ctx
							);
					}
					else
					{
						PerformSaveWithoutCheck(fileStorage, ioc);
						Finish(true);
					}

				}
				catch (Exception e)
				{
					/* TODO KPDesktop:
					 * catch(Exception exSave)
			{
				MessageService.ShowSaveWarning(pd.IOConnectionInfo, exSave, true);
				bSuccess = false;
			}
*/
					Kp2aLog.Log("Error while saving: " + e.ToString());
					Finish(false, e.Message);
					return;
				}
			}
			else
			{
				Finish(true);
			}
			
		}

		private void RunInWorkerThread(Action runHandler)
		{
			try
			{
				_workerThread = new Thread(() =>
					{
						try
						{
							runHandler();
						}
						catch (Exception e)
						{
							Kp2aLog.Log("Error in worker thread of SaveDb: " + e);
							Finish(false, e.Message);
						}
						
					});
				_workerThread.Start();
			}
			catch (Exception e)
			{
				Kp2aLog.Log("Error starting worker thread of SaveDb: "+e);
				Finish(false, e.Message);
			}
			
		}

		public void JoinWorkerThread()
		{
			if (_workerThread != null)
				_workerThread.Join();
		}

		private void MergeIn(IFileStorage fileStorage, IOConnectionInfo ioc)
		{
			StatusLogger.UpdateSubMessage(_app.GetResourceString(UiStringKey.SynchronizingDatabase));

			PwDatabase pwImp = new PwDatabase();
			PwDatabase pwDatabase = _app.GetDb().KpDatabase;
			pwImp.New(new IOConnectionInfo(), pwDatabase.MasterKey);
			pwImp.MemoryProtection = pwDatabase.MemoryProtection.CloneDeep();
			pwImp.MasterKey = pwDatabase.MasterKey;
			var stream = GetStreamForBaseFile(fileStorage, ioc);

			_app.GetDb().DatabaseFormat.PopulateDatabaseFromStream(pwImp, stream, null);
			

			pwDatabase.MergeIn(pwImp, PwMergeMethod.Synchronize, null); 

		}

		private Stream GetStreamForBaseFile(IFileStorage fileStorage, IOConnectionInfo ioc)
		{
			//if we have the original file already available: use it
			if (_streamForOrigFile != null)
				return _streamForOrigFile;

			//if the file storage caches, it might return the local data in case of a conflict. This would result in data loss
			// so we need to ensure we get the data from remote (only if the remote file is available. if not, we won't overwrite anything)
			CachingFileStorage cachingFileStorage = fileStorage as CachingFileStorage;
			if (cachingFileStorage != null)
			{
				return cachingFileStorage.OpenRemoteForReadIfAvailable(ioc);
			}
			return fileStorage.OpenFileForRead(ioc);
		}

		private void PerformSaveWithoutCheck(IFileStorage fileStorage, IOConnectionInfo ioc)
		{
			StatusLogger.UpdateSubMessage("");
			_app.GetDb().SaveData(_ctx);
			_app.GetDb().LastFileVersion = fileStorage.GetCurrentFileVersionFast(ioc);
		}

		public byte[] HashOriginalFile(IOConnectionInfo iocFile)
		{
			if (iocFile == null) { Debug.Assert(false); return null; } // Assert only

			Stream sIn;
			try
			{
				IFileStorage fileStorage = _app.GetFileStorage(iocFile);
				CachingFileStorage cachingFileStorage = fileStorage as CachingFileStorage;
				if (cachingFileStorage != null)
				{
					string hash;
					cachingFileStorage.GetRemoteDataAndHash(iocFile, out hash);
					return MemUtil.HexStringToByteArray(hash);
				}
				else
				{
					sIn = fileStorage.OpenFileForRead(iocFile);	
				}
				
				if (sIn == null) throw new FileNotFoundException();
			}
			catch (Exception) { return null; }

			byte[] pbHash;
			try
			{
				SHA256Managed sha256 = new SHA256Managed();
				pbHash = sha256.ComputeHash(sIn);
			}
			catch (Exception) { Debug.Assert(false); sIn.Close(); return null; }

			sIn.Close();
			return pbHash;
		}

		enum FileHashChange
		{
			Equal,
			Changed,
			FileNotAvailable
		}

		private FileHashChange FileHashChanged(IOConnectionInfo ioc, byte[] hashOfFileOnDisk)
		{
			StatusLogger.UpdateSubMessage(_app.GetResourceString(UiStringKey.CheckingTargetFileForChanges));
			byte[] fileHash = HashOriginalFile(ioc);
			if (fileHash == null)
				return FileHashChange.FileNotAvailable;
			return MemUtil.ArraysEqual(fileHash, hashOfFileOnDisk) ? FileHashChange.Equal : FileHashChange.Changed;
		}

		
	}

}

