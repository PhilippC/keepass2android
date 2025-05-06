using System;
using Android.App;
using Android.Widget;
using keepass2android.Io;
using KeePassLib.Serialization;

namespace keepass2android
{
    public class SyncUtil
    {
        private Activity _activity;

        public SyncUtil(Activity activity)
        {
            _activity = activity;
        }

        public class SyncOtpAuxFile : OperationWithFinishHandler
        {
            private readonly IOConnectionInfo _ioc;

            public SyncOtpAuxFile(Activity activity, IOConnectionInfo ioc)
                : base(activity, null)
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


        public void StartSynchronizeDatabase()
        {
            var filestorage = App.Kp2a.GetFileStorage(App.Kp2a.CurrentDb.Ioc);
            OperationWithFinishHandler task;
            OnOperationFinishedHandler onOperationFinishedHandler = new ActionOnOperationFinished(_activity, (success, message, activity) =>
            {
                if (!String.IsNullOrEmpty(message))
                    App.Kp2a.ShowMessage(activity, message,  MessageSeverity.Error);

                // Tell the adapter to refresh it's list
                BaseAdapter adapter = (activity as GroupBaseActivity)?.ListAdapter;
                adapter?.NotifyDataSetChanged();

                if (App.Kp2a.CurrentDb?.OtpAuxFileIoc != null)
                {
                    var task2 = new SyncOtpAuxFile(_activity, App.Kp2a.CurrentDb.OtpAuxFileIoc);
                    
                    //TODO new BackgroundOperationRunner(App.Kp2a, activity, task2).Run(true);
                }
               
            });

            if (filestorage is CachingFileStorage)
            {

                task = new SynchronizeCachedDatabase(_activity, App.Kp2a, onOperationFinishedHandler);
            }
            else
            {
                //TODO do we want this to run in the background?
                task = new CheckDatabaseForChanges(_activity, App.Kp2a, onOperationFinishedHandler);
            }

            //TODO var backgroundTaskRunner = new BackgroundOperationRunner(App.Kp2a, _activity, task);
            //TODO backgroundTaskRunner.Run();

        }
    }
}