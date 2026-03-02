// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using keepass2android.Io;
using KeePassLib.Serialization;
using System;
using AndroidX.Preference;

namespace keepass2android
{
  public class SyncUtil
  {
    private LifecycleAwareActivity _activity;

    public SyncUtil(LifecycleAwareActivity activity)
    {
      _activity = activity;
    }

    public class SyncOtpAuxFile : OperationWithFinishHandler
    {
      private readonly IOConnectionInfo _ioc;

      public SyncOtpAuxFile(Activity activity, IOConnectionInfo ioc)
          : base(App.Kp2a, null)
      {
        _ioc = ioc;
      }

      public override void Run()
      {
        StatusLogger.UpdateMessage(UiStringKey.SynchronizingOtpAuxFile);
        try
        {
          //simply open the file. The file storage does a complete sync.
          using (App.Kp2a.GetOtpAuxFileStorage(_ioc).OpenFileForRead(_ioc))
          {
          }

          Finish(true);
        }
        catch (Exception e)
        {

          Finish(false, Util.GetErrorMessage(e));
        }


      }

    }


    /// <summary>
    /// Starts the sync process for the database 
    /// </summary>
    /// <param name="database">database to sync</param>
    /// <param name="forceSynchronization">If true, sync is always started. If false, we respect the background sync options from preferences.</param>
    public void StartSynchronizeDatabase(Database database, bool forceSynchronization)
    {
      if (!forceSynchronization)
      {
        ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(App.Context);
        if (prefs != null &&
            prefs.GetBoolean(App.Context.GetString(Resource.String.SyncOfflineCacheInBackground_key), true))
        {
          // background sync is turned on

          if (prefs.GetBoolean("BackgroundSyncWifiOnly", false) &&
              !NetworkUtils.IsAllowedNetwork(App.Context, SSIDManagerActivity.LoadSSIDS(App.Context)))
          {
            // user only wants to sync when in (specific) wifi but is not

            //remember that we should sync as soon as possible (e.g. when wifi is back)
            database.SynchronizationPending = true;

            Kp2aLog.Log("Not synchronizing because of current network connectivity.");
            return;
          }
        }

      }

      var ioc = database.Ioc;

      var filestorage = App.Kp2a.GetFileStorage(ioc);


      OperationWithFinishHandler task;
      OnOperationFinishedHandler onOperationFinishedHandler = new ActionInContextInstanceOnOperationFinished(_activity.ContextInstanceId, App.Kp2a, (success, message, context) =>
      {
        App.Kp2a.UiThreadHandler.Post(() =>
              {
                if (!String.IsNullOrEmpty(message))
                  App.Kp2a.ShowMessage(context, message, success ? MessageSeverity.Info : MessageSeverity.Error);

                // Tell the adapter to refresh it's list
                BaseAdapter adapter = (context as GroupBaseActivity)?.ListAdapter;

                adapter?.NotifyDataSetChanged();
              });

        if (database?.OtpAuxFileIoc != null)
        {
          var task2 = new SyncOtpAuxFile(_activity, database.OtpAuxFileIoc);

          OperationRunner.Instance.Run(App.Kp2a, task2);
        }

      });

      if (filestorage is CachingFileStorage)
      {

        task = new SynchronizeCachedDatabase(App.Kp2a, database, onOperationFinishedHandler, new BackgroundDatabaseModificationLocker(App.Kp2a));
      }
      else
      {
        task = new CheckDatabaseForChanges(App.Kp2a, onOperationFinishedHandler);
      }

      OperationRunner.Instance.Run(App.Kp2a, task);

    }

    public void TryStartPendingSyncs()
    {
      var prefs = PreferenceManager.GetDefaultSharedPreferences(_activity);
      string periodic_sync_default = _activity.GetString(Resource.String.pref_periodic_background_sync_interval_default);
      foreach (var db in App.Kp2a.OpenDatabases)
      {
        if (db.SynchronizationRunning)
        {
          continue;
        }

        if (db.SynchronizationPending || (prefs.GetBoolean("pref_enable_periodic_background_sync", false) &&
                                          db.LastSyncTime != DateTime.MaxValue &&
                                          db.LastSyncTime.AddMinutes(ParseOrDefault(
                                              prefs.GetString("pref_periodic_background_sync_interval", periodic_sync_default),
                                              int.Parse(periodic_sync_default))) < DateTime.Now))
        {
          Kp2aLog.Log("Triggering sync for " + db.Ioc.GetDisplayName());
          StartSynchronizeDatabase(db, false);
        }
      }
    }

    private int ParseOrDefault(string stringToParse, int def)
    {
      int result = def;
      int.TryParse(stringToParse, out result);
      return result;
    }
  }
}