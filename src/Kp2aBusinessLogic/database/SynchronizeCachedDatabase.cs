using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using KeePassLib.Serialization;
using keepass2android.Io;
using KeePass.Util;
using Group.Pals.Android.Lib.UI.Filechooser.Utils;
using KeePassLib;

namespace keepass2android
{
	public class SynchronizeCachedDatabase: OperationWithFinishHandler 
	{
		private readonly IKp2aApp _app;
        private IDatabaseModificationWatcher _modificationWatcher;
        private readonly Database _database;


        public SynchronizeCachedDatabase(IKp2aApp app, Database database, OnOperationFinishedHandler operationFinishedHandler, IDatabaseModificationWatcher modificationWatcher)
			: base(app, operationFinishedHandler)
        {
            _app = app;
            _database = database;
            _modificationWatcher = modificationWatcher;
        }

		public override void Run()
		{
            try
            {
                IOConnectionInfo ioc = _database.Ioc;
                IFileStorage fileStorage = _app.GetFileStorage(ioc);
                if (!(fileStorage is CachingFileStorage))
                {
                    throw new Exception("Cannot sync a non-cached database!");
                }

                StatusLogger.UpdateMessage(UiStringKey.SynchronizingCachedDatabase);
                CachingFileStorage cachingFileStorage = (CachingFileStorage)fileStorage;

                //download file from remote location and calculate hash:
                StatusLogger.UpdateSubMessage(_app.GetResourceString(UiStringKey.DownloadingRemoteFile));
                string hash;

                MemoryStream remoteData;
                try
                {
                    remoteData = cachingFileStorage.GetRemoteDataAndHash(ioc, out hash);
                    Kp2aLog.Log("Checking for file change. Current hash = " + hash);
                }
                catch (FileNotFoundException)
                {
                    StatusLogger.UpdateSubMessage(_app.GetResourceString(UiStringKey.RestoringRemoteFile));
                    cachingFileStorage.UpdateRemoteFile(ioc,
                        _app.GetBooleanPreference(PreferenceKey.UseFileTransactions));
                    Finish(true, _app.GetResourceString(UiStringKey.SynchronizedDatabaseSuccessfully));
                    Kp2aLog.Log("Checking for file change: file not found");
                    return;
                }

                //check if remote file was modified:
                var baseVersionHash = cachingFileStorage.GetBaseVersionHash(ioc);
                Kp2aLog.Log("Checking for file change. baseVersionHash = " + baseVersionHash);
                if (baseVersionHash != hash)
                {
                    //remote file is modified
                    if (cachingFileStorage.HasLocalChanges(ioc))
                    {
                        //conflict! need to merge
                        var _saveDb = new SaveDb(_app, new ActionOnOperationFinished(_app,
                            (success, result, activity) =>
                            {
                                if (!success)
                                {
                                    Finish(false, result);
                                }
                                else
                                {
                                    Finish(true, _app.GetResourceString(UiStringKey.SynchronizedDatabaseSuccessfully));
                                }
                            }), _database, false, remoteData, _modificationWatcher);
                        _saveDb.SetStatusLogger(StatusLogger);
                        _saveDb.DoNotSetStatusLoggerMessage = true; //Keep "sync db" as main message
                        _saveDb.SyncInBackground = false;
                        _saveDb.Run();

                        _database.UpdateGlobals();

                        _app.MarkAllGroupsAsDirty();
                    }
                    else
                    {
                        //only the remote file was modified -> reload database.
                        var onFinished = new ActionOnOperationFinished(_app, (success, result, activity) =>
                        {
                            if (!success)
                            {
                                Finish(false, result);
                            }
                            else
                            {
                                new Handler(Looper.MainLooper).Post(() =>
                                {
                                    _database.UpdateGlobals();

                                    _app.MarkAllGroupsAsDirty();
                                    Finish(true, _app.GetResourceString(UiStringKey.SynchronizedDatabaseSuccessfully));
                                });

                            }
                        });
                        var _loadDb = new LoadDb(_app, ioc, Task.FromResult(remoteData),
                            _database.KpDatabase.MasterKey, null, onFinished, true, false, _modificationWatcher);
                        _loadDb.SetStatusLogger(StatusLogger);
                        _loadDb.DoNotSetStatusLoggerMessage = true; //Keep "sync db" as main message
                        _loadDb.Run();

                    }
                }
                else
                {
                    //remote file is unmodified
                    if (cachingFileStorage.HasLocalChanges(ioc))
                    {
                        //but we have local changes -> upload:
                        StatusLogger.UpdateSubMessage(_app.GetResourceString(UiStringKey.UploadingFile));
                        cachingFileStorage.UpdateRemoteFile(ioc,
                            _app.GetBooleanPreference(PreferenceKey.UseFileTransactions));
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
            catch (Java.Lang.InterruptedException e)
            {
                Kp2aLog.LogUnexpectedError(e);
                //no Finish()
            }
            catch (Java.IO.InterruptedIOException e)
            {
                Kp2aLog.LogUnexpectedError(e);
                //no Finish()
            }
			catch (Exception e)
			{
                Kp2aLog.LogUnexpectedError(e);
				Finish(false, ExceptionUtil.GetErrorMessage(e));
			}
			
		}

	}
}
