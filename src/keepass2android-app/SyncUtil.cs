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
        /// Starts the sync process for the database identified by its IOConnectionInfo ioc
        /// </summary>
        /// <param name="ioc">database file ioc</param>
        /// <param name="forceSynchronization">If true, sync is always started. If false, we respect the background sync options from preferences.</param>
        public void StartSynchronizeDatabase(IOConnectionInfo ioc, bool forceSynchronization)
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

                        //TODO add to list of pending syncs such that we sync when network is back?

                        Kp2aLog.Log("Not synchronizing because of current network connectivity.");
                        return;
                    }
                }

            }

            var filestorage = App.Kp2a.GetFileStorage(ioc);
            var databaseForIoc = App.Kp2a.GetDatabase(ioc);

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

                if (databaseForIoc?.OtpAuxFileIoc != null)
                {
                    var task2 = new SyncOtpAuxFile(_activity, databaseForIoc.OtpAuxFileIoc);

                    OperationRunner.Instance.Run(App.Kp2a, task2);
                }
               
            });

            if (filestorage is CachingFileStorage)
            {

                task = new SynchronizeCachedDatabase(App.Kp2a, databaseForIoc, onOperationFinishedHandler, new BackgroundDatabaseModificationLocker(App.Kp2a));
            }
            else
            {
                task = new CheckDatabaseForChanges( App.Kp2a, onOperationFinishedHandler);
            }

            OperationRunner.Instance.Run(App.Kp2a, task);

        }
    }
}