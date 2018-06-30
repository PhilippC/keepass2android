using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Android.App;
using Android.Content;
using KeePassLib.Serialization;
using keepass2android.Io;

namespace keepass2android
{
	public class SynchronizeCachedDatabase: RunnableOnFinish 
	{
		private readonly Activity _context;
		private readonly IKp2aApp _app;
		private SaveDb _saveDb;

		public SynchronizeCachedDatabase(Activity context, IKp2aApp app, OnFinish finish)
			: base(context, finish)
		{
			_context = context;
			_app = app;
		}

		public override void Run()
		{
			try
			{
				IOConnectionInfo ioc = _app.GetDb().Ioc;
				IFileStorage fileStorage = _app.GetFileStorage(ioc);
				if (!(fileStorage is CachingFileStorage))
				{
					throw new Exception("Cannot sync a non-cached database!");
				}
				StatusLogger.UpdateMessage(UiStringKey.SynchronizingCachedDatabase);
				CachingFileStorage cachingFileStorage = (CachingFileStorage) fileStorage;

				//download file from remote location and calculate hash:
				StatusLogger.UpdateSubMessage(_app.GetResourceString(UiStringKey.DownloadingRemoteFile));
				string hash;
				
				MemoryStream remoteData;
				try
				{
					remoteData = cachingFileStorage.GetRemoteDataAndHash(ioc, out hash);
				}
				catch (FileNotFoundException)
				{
					StatusLogger.UpdateSubMessage(_app.GetResourceString(UiStringKey.RestoringRemoteFile));
					cachingFileStorage.UpdateRemoteFile(ioc, _app.GetBooleanPreference(PreferenceKey.UseFileTransactions));
					Finish(true, _app.GetResourceString(UiStringKey.SynchronizedDatabaseSuccessfully));
					return;
				}

				//check if remote file was modified:
				if (cachingFileStorage.GetBaseVersionHash(ioc) != hash)
				{
					//remote file is modified
					if (cachingFileStorage.HasLocalChanges(ioc))
					{
						//conflict! need to merge
						_saveDb = new SaveDb(_context, _app, new ActionOnFinish(ActiveActivity, (success, result, activity) =>
							{
								if (!success)
								{
									Finish(false, result);
								}
								else
								{
									Finish(true, _app.GetResourceString(UiStringKey.SynchronizedDatabaseSuccessfully));
								}
								_saveDb = null;
							}), false, remoteData);
						_saveDb.Run();

						_app.GetDb().MarkAllGroupsAsDirty();
					}
					else
					{
						//only the remote file was modified -> reload database.
						//note: it's best to lock the database and do a complete reload here (also better for UI consistency in case something goes wrong etc.)
						_app.TriggerReload(_context);
						Finish(true);
					}
				}
				else
				{
					//remote file is unmodified
					if (cachingFileStorage.HasLocalChanges(ioc))
					{
						//but we have local changes -> upload:
						StatusLogger.UpdateSubMessage(_app.GetResourceString(UiStringKey.UploadingFile));
						cachingFileStorage.UpdateRemoteFile(ioc, _app.GetBooleanPreference(PreferenceKey.UseFileTransactions));
						StatusLogger.UpdateSubMessage("");
						Finish(true, _app.GetResourceString(UiStringKey.SynchronizedDatabaseSuccessfully));
					}
					else
					{
						//files are in sync: just set the result
						Finish(true, _app.GetResourceString(UiStringKey.FilesInSync));
					}
				}
			}
			catch (Exception e)
			{
				Finish(false, e.Message);
			}
			
		}

		public void JoinWorkerThread()
		{
			if (_saveDb != null)
				_saveDb.JoinWorkerThread();
		}
	}
}
