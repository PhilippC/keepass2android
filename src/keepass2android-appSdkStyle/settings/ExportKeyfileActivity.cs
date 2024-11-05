using System;
using Android.App;
using Android.Content;
using Android.Widget;
using keepass2android.Io;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace keepass2android
{
    [Activity]
    public class ExportKeyfileActivity : LockCloseActivity
    {

        public class ExportKeyfile : RunnableOnFinish
        {
            private readonly IKp2aApp _app;
            private IOConnectionInfo _targetIoc;

            public ExportKeyfile(Activity activity, IKp2aApp app, OnFinish onFinish, IOConnectionInfo targetIoc) : base(
                activity, onFinish)
            {
                _app = app;
                _targetIoc = targetIoc;
            }

            public override void Run()
            {
                StatusLogger.UpdateMessage(UiStringKey.exporting_database);

                try
                {
                    var fileStorage = _app.GetFileStorage(_targetIoc);
                    if (fileStorage is IOfflineSwitchable)
                    {
                        ((IOfflineSwitchable) fileStorage).IsOffline = false;
                    }

                    CompositeKey masterKey = App.Kp2a.CurrentDb.KpDatabase.MasterKey;
                    var sourceIoc = ((KcpKeyFile) masterKey.GetUserKey(typeof(KcpKeyFile))).Ioc;

                    IoUtil.Copy(_targetIoc, sourceIoc, App.Kp2a);

                    if (fileStorage is IOfflineSwitchable)
                    {
                        ((IOfflineSwitchable) fileStorage).IsOffline = App.Kp2a.OfflineMode;
                    }

                    Finish(true);


                }
                catch (Exception ex)
                {
                    Finish(false, ex.Message);
                }


            }
        }


        public class ExportKeyfileProcessManager : FileSaveProcessManager
        {
            public ExportKeyfileProcessManager(int requestCode, Activity activity) : base(requestCode, activity)
            {

            }

            protected override void SaveFile(IOConnectionInfo ioc)
            {
                var exportKeyfile = new ExportKeyfile(_activity, App.Kp2a, new ActionOnFinish(_activity,
                    (success, message, activity) =>
                    {
                        if (!success)
                            Toast.MakeText(activity, message, ToastLength.Long).Show();
                        else
                            Toast.MakeText(activity, _activity.GetString(Resource.String.export_keyfile_successful),
                                ToastLength.Long).Show();
                        activity.Finish();
                    }
                ), ioc);
                ProgressTask pt = new ProgressTask(App.Kp2a, _activity, exportKeyfile);
                pt.Run();

            }
        }

        private ExportKeyfileProcessManager _exportKeyfileProcessManager;


        protected override void OnCreate(Android.OS.Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            _exportKeyfileProcessManager = new ExportKeyfileProcessManager(0, this);
            _exportKeyfileProcessManager.StartProcess();

        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (_exportKeyfileProcessManager?.OnActivityResult(requestCode, resultCode, data) == true)
                return;

            Finish();

        }


    }
}