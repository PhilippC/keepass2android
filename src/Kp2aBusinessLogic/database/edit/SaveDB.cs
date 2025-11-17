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
using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Java.Lang;
using KeePassLib;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using keepass2android.Io;
using Debug = System.Diagnostics.Debug;
using Exception = System.Exception;
using KeePass.Util;
using Thread = System.Threading.Thread;

namespace keepass2android
{

  /// <summary>
  /// Save the database. If the file has changed, ask the user if he wants to overwrite or sync.
  /// </summary>

  public class SaveDb : OperationWithFinishHandler
  {
    private readonly IKp2aApp _app;
    private readonly Database _db;
    private readonly bool _dontSave;
    private readonly IDatabaseModificationWatcher _modificationWatcher;
    private bool requiresSubsequentSync = false; //if true, we need to sync the file after saving.

    public bool DoNotSetStatusLoggerMessage = false;

    /// <summary>
    /// stream for reading the data from the original file. If this is set to a non-null value, we know we need to sync
    /// </summary>
    private readonly Stream _streamForOrigFile;

    private Java.Lang.Thread _workerThread;

    public SaveDb(IKp2aApp app, Database db, OnOperationFinishedHandler operationFinishedHandler, bool dontSave, IDatabaseModificationWatcher modificationWatcher)
        : base(app, operationFinishedHandler)
    {
      _db = db;
      _app = app;
      _dontSave = dontSave;
      _modificationWatcher = modificationWatcher ?? new NullDatabaseModificationWatcher();
    }

    /// <summary>
    /// Constructor for sync
    /// </summary>
    /// <param name="app"></param>
    /// <param name="operationFinishedHandler"></param>
    /// <param name="dontSave"></param>
    /// <param name="streamForOrigFile">Stream for reading the data from the (changed) original location</param>
    public SaveDb(IKp2aApp app, OnOperationFinishedHandler operationFinishedHandler, Database db, bool dontSave, Stream streamForOrigFile, IDatabaseModificationWatcher modificationWatcher = null)
        : base(app, operationFinishedHandler)
    {
      _modificationWatcher = modificationWatcher ?? new NullDatabaseModificationWatcher();
      _db = db;
      _app = app;
      _dontSave = dontSave;
      _streamForOrigFile = streamForOrigFile;
      SyncInBackground = _app.SyncInBackgroundPreference;

    }

    public SaveDb(IKp2aApp app, Database db, OnOperationFinishedHandler operationFinishedHandler, IDatabaseModificationWatcher modificationWatcher = null)
        : base(app, operationFinishedHandler)
    {

      _modificationWatcher = modificationWatcher ?? new NullDatabaseModificationWatcher();
      _app = app;
      _db = db;
      _dontSave = false;
      SyncInBackground = _app.SyncInBackgroundPreference;
    }

    public bool ShowDatabaseIocInStatus { get; set; }

    public override void Run()
    {

      if (!_dontSave)
      {
        try
        {
          if (_db.CanWrite == false)
          {
            //this should only happen if there is a problem in the UI so that the user sees an edit interface.
            Finish(false, "Cannot save changes. File is read-only!");
            return;
          }

          string message = _app.GetResourceString(UiStringKey.saving_database);

          if (ShowDatabaseIocInStatus)
            message += " (" + _app.GetFileStorage(_db.Ioc).GetDisplayName(_db.Ioc) + ")";

          if (!DoNotSetStatusLoggerMessage)
          {
            StatusLogger.UpdateMessage(message);
          }

          IOConnectionInfo ioc = _db.Ioc;
          IFileStorage fileStorage = _app.GetFileStorage(ioc);

          if (SyncInBackground && fileStorage is IOfflineSwitchable offlineSwitchable)
          {
            offlineSwitchable.IsOffline = true;
            //no warning. We'll trigger a sync later.
            offlineSwitchable.TriggerWarningWhenFallingBackToCache = false;
            requiresSubsequentSync = true;

          }


          if (_streamForOrigFile == null)
          {
            if ((!_app.GetBooleanPreference(PreferenceKey.CheckForFileChangesOnSave))
                || (_db.KpDatabase.HashOfFileOnDisk == null)) //first time saving
            {
              PerformSaveWithoutCheck(fileStorage, ioc);
              FinishWithSuccess();
              return;
            }
          }

          bool hasStreamForOrigFile = (_streamForOrigFile != null);
          bool hasChangeFast = hasStreamForOrigFile ||
                               fileStorage.CheckForFileChangeFast(ioc, _db.LastFileVersion);  //first try to use the fast change detection;
          bool hasHashChanged = !requiresSubsequentSync && (
              hasChangeFast ||
                                (FileHashChanged(ioc, _db.KpDatabase.HashOfFileOnDisk) ==
                                 FileHashChange.Changed)); //if that fails, hash the file and compare:

          if (hasHashChanged)
          {
            Kp2aLog.Log("Conflict. " + hasStreamForOrigFile + " " + hasChangeFast + " " + hasHashChanged);

            bool alwaysMerge = (PreferenceManager.GetDefaultSharedPreferences(Application.Context)
                .GetBoolean("AlwaysMergeOnConflict", false));

            if (alwaysMerge)
            {
              MergeAndFinish(fileStorage, ioc);
            }
            else
            {


              //ask user...
              _app.AskYesNoCancel(UiStringKey.TitleSyncQuestion, UiStringKey.MessageSyncQuestion,
                  UiStringKey.YesSynchronize,
                  UiStringKey.NoOverwrite,
                  //yes = sync
                  (sender, args) =>
                  {
                    Action runHandler = () => { MergeAndFinish(fileStorage, ioc); };
                    RunInWorkerThread(runHandler);
                  },
                  //no = overwrite
                  (sender, args) =>
                  {
                    RunInWorkerThread(() =>
                                      {
                                        PerformSaveWithoutCheck(fileStorage, ioc);
                                        FinishWithSuccess();
                                      });
                  },
                  //cancel 
                  (sender, args) =>
                  {
                    RunInWorkerThread(() => Finish(false));
                  }
                  );
            }

          }
          else
          {
            PerformSaveWithoutCheck(fileStorage, ioc);
            FinishWithSuccess();
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
          Kp2aLog.LogUnexpectedError(e);
          Finish(false, ExceptionUtil.GetErrorMessage(e));
          return;
        }
      }
      else
      {
        FinishWithSuccess();
      }

    }

    public bool SyncInBackground { get; set; }

    private void FinishWithSuccess()
    {
      if (requiresSubsequentSync)
      {
        var syncTask = new SynchronizeCachedDatabase(_app, _db, new ActionOnOperationFinished(_app,
            (success, message, context) =>
            {
              if (!System.String.IsNullOrEmpty(message))
                _app.ShowMessage(context, message, success ? MessageSeverity.Info : MessageSeverity.Error);

            }), new BackgroundDatabaseModificationLocker(_app)
        );
        OperationRunner.Instance.Run(_app, syncTask);
      }
      else
      {
        _db.LastSyncTime = DateTime.Now;

      }
      Finish(true);
    }

    private void MergeAndFinish(IFileStorage fileStorage, IOConnectionInfo ioc)
    {
      //note: when synced, the file might be downloaded once again from the server. Caching the data
      //in the hashing function would solve this but increases complexity. I currently assume the files are 
      //small.

      try
      {
        _modificationWatcher.BeforeModifyDatabases();
      }
      catch (Java.Lang.InterruptedException)
      {
        // leave without Finish()
        return;
      }


      try
      {
        MergeIn(fileStorage, ioc);
      }
      finally
      {
        _modificationWatcher.AfterModifyDatabases();

      }

      PerformSaveWithoutCheck(fileStorage, ioc);
      new Handler(Looper.MainLooper).Post(() =>
      {
        _db.UpdateGlobals();
      });

      FinishWithSuccess();
    }


    private void RunInWorkerThread(Action runHandler)
    {
      try
      {
        _workerThread = new Java.Lang.Thread(() =>
            {
              try
              {
                runHandler();
              }
              catch (Exception e)
              {
                Kp2aLog.LogUnexpectedError(e);
                Kp2aLog.Log("Error in worker thread of SaveDb: " + ExceptionUtil.GetErrorMessage(e));
                Finish(false, ExceptionUtil.GetErrorMessage(e));
              }

            });
        _workerThread.Start();
      }
      catch (Exception e)
      {
        Kp2aLog.LogUnexpectedError(e);
        Kp2aLog.Log("Error starting worker thread of SaveDb: " + e);
        Finish(false, ExceptionUtil.GetErrorMessage(e));
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
      PwDatabase pwDatabase = _db.KpDatabase;
      pwImp.New(new IOConnectionInfo(), pwDatabase.MasterKey, _app.GetFileStorage(ioc).GetFilenameWithoutPathAndExt(ioc));
      pwImp.MemoryProtection = pwDatabase.MemoryProtection.CloneDeep();
      pwImp.MasterKey = pwDatabase.MasterKey;
      var stream = GetStreamForBaseFile(fileStorage, ioc);

      _db.DatabaseFormat.PopulateDatabaseFromStream(pwImp, stream, null);


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
      _db.SaveData(fileStorage);
      _db.LastFileVersion = fileStorage.GetCurrentFileVersionFast(ioc);
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

