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

using System;
using Android.App;
using Android.Content;
using Android.Widget;
using keepass2android.Io;
using keepass2android;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace keepass2android
{
    [Activity(Theme = "@style/Kp2aTheme_ActionBar")]
    public class ExportKeyfileActivity : LockCloseActivity
    {

        public class ExportKeyfile : OperationWithFinishHandler
        {
            private readonly IKp2aApp _app;
            private IOConnectionInfo _targetIoc;

            public ExportKeyfile(IKp2aApp app, OnOperationFinishedHandler onOperationFinishedHandler, IOConnectionInfo targetIoc) : base(
                App.Kp2a, onOperationFinishedHandler)
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
                        ((IOfflineSwitchable)fileStorage).IsOffline = false;
                    }

                    CompositeKey masterKey = App.Kp2a.CurrentDb.KpDatabase.MasterKey;
                    var sourceIoc = ((KcpKeyFile)masterKey.GetUserKey(typeof(KcpKeyFile))).Ioc;

                    IoUtil.Copy(_targetIoc, sourceIoc, App.Kp2a);

                    if (fileStorage is IOfflineSwitchable)
                    {
                        ((IOfflineSwitchable)fileStorage).IsOffline = App.Kp2a.OfflineMode;
                    }

                    Finish(true);


                }
                catch (Exception ex)
                {
                    Finish(false, Util.GetErrorMessage(ex));
                }


            }
        }


        public class ExportKeyfileProcessManager : FileSaveProcessManager
        {
            public ExportKeyfileProcessManager(int requestCode, LifecycleAwareActivity activity) : base(requestCode, activity)
            {

            }

            protected override void SaveFile(IOConnectionInfo ioc)
            {
                var exportKeyfile = new ExportKeyfile(App.Kp2a, new ActionInContextInstanceOnOperationFinished(_activity.ContextInstanceId, App.Kp2a,
                    (success, message, context) =>
                    {
                        if (!success)
                            App.Kp2a.ShowMessage(context, message, MessageSeverity.Error);
                        else
                            App.Kp2a.ShowMessage(context, _activity.GetString(Resource.String.export_keyfile_successful),
                                 MessageSeverity.Info);
                        (context as Activity)?.Finish();
                    }
                ), ioc);
                BlockingOperationStarter pt = new BlockingOperationStarter(App.Kp2a, exportKeyfile);
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