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

        public class SyncOtpAuxFile : RunnableOnFinish
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

                    Finish(false, e.Message);
                }


            }

        }


        public void SynchronizeDatabase(Action runAfterSuccess)
        {
            var filestorage = App.Kp2a.GetFileStorage(App.Kp2a.CurrentDb.Ioc);
            RunnableOnFinish task;
            OnFinish onFinish = new ActionOnFinish(_activity, (success, message, activity) =>
            {
                if (!String.IsNullOrEmpty(message))
                    Toast.MakeText(activity, message, ToastLength.Long).Show();

                // Tell the adapter to refresh it's list
                BaseAdapter adapter = (activity as GroupBaseActivity)?.ListAdapter;
                adapter?.NotifyDataSetChanged();

                if (App.Kp2a.CurrentDb?.OtpAuxFileIoc != null)
                {
                    var task2 = new SyncOtpAuxFile(_activity, App.Kp2a.CurrentDb.OtpAuxFileIoc);
                    task2.OnFinishToRun = new ActionOnFinish(_activity, (b, s, activeActivity) =>
                    {
                        runAfterSuccess();
                    });
                    new ProgressTask(App.Kp2a, activity, task2).Run(true);
                }
                else
                {
                    runAfterSuccess();
                }
            });

            if (filestorage is CachingFileStorage)
            {

                task = new SynchronizeCachedDatabase(_activity, App.Kp2a, onFinish);
            }
            else
            {

                task = new CheckDatabaseForChanges(_activity, App.Kp2a, onFinish);
            }




            var progressTask = new ProgressTask(App.Kp2a, _activity, task);
            progressTask.Run();

        }
    }
}