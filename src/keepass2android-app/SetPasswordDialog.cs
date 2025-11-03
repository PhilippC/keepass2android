/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */
using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Widget;
using keepass2android;

namespace keepass2android
{

    public class SetPasswordDialog : CancelDialog
    {


        internal String Keyfile;

        public SetPasswordDialog(Activity activity) : base(activity)
        {

        }



        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.set_password);

            SetTitle(Resource.String.password_title);

            // Ok button
            Button okButton = (Button)FindViewById(Resource.Id.ok);
            okButton.Click += (sender, e) =>
            {
                TextView passView = (TextView)FindViewById(Resource.Id.pass_password);
                String pass = passView.Text;
                TextView passConfView = (TextView)FindViewById(Resource.Id.pass_conf_password);
                String confpass = passConfView.Text;

                // Verify that passwords match
                if (!pass.Equals(confpass))
                {
                    // Passwords do not match
                    App.Kp2a.ShowMessage(Context, Resource.String.error_pass_match, MessageSeverity.Error);
                    return;
                }

                TextView keyfileView = (TextView)FindViewById(Resource.Id.pass_keyfile);
                String keyfile = keyfileView.Text;
                Keyfile = keyfile;

                // Verify that a password or keyfile is set
                if (pass.Length == 0 && keyfile.Length == 0)
                {
                    App.Kp2a.ShowMessage(Context, Resource.String.error_nopass, MessageSeverity.Error);
                    return;

                }

                SetPassword sp = new SetPassword(App.Kp2a, pass, keyfile, new AfterSave(_activity, this, null, new Handler()));
                BlockingOperationStarter pt = new BlockingOperationStarter(App.Kp2a, sp);
                pt.Run();
            };



            // Cancel button
            Button cancelButton = (Button)FindViewById(Resource.Id.cancel);
            cancelButton.Click += (sender, e) =>
            {
                Cancel();
            };
        }



        class AfterSave : OnOperationFinishedHandler
        {
            private readonly FileOnFinish _operationFinishedHandler;

            readonly SetPasswordDialog _dlg;

            public AfterSave(Activity activity, SetPasswordDialog dlg, FileOnFinish operationFinishedHandler, Handler handler) : base(App.Kp2a, operationFinishedHandler, handler)
            {
                _operationFinishedHandler = operationFinishedHandler;
                _dlg = dlg;
            }


            public override void Run()
            {
                if (Success)
                {
                    if (_operationFinishedHandler != null)
                    {
                        _operationFinishedHandler.Filename = _dlg.Keyfile;
                    }
                    FingerprintUnlockMode um;
                    Enum.TryParse(PreferenceManager.GetDefaultSharedPreferences(_dlg.Context).GetString(App.Kp2a.CurrentDb.CurrentFingerprintModePrefKey, ""), out um);

                    if (um == FingerprintUnlockMode.FullUnlock)
                    {
                        ISharedPreferencesEditor edit = PreferenceManager.GetDefaultSharedPreferences(_dlg.Context).Edit();
                        edit.PutString(App.Kp2a.CurrentDb.CurrentFingerprintPrefKey, "");
                        edit.PutString(App.Kp2a.CurrentDb.CurrentFingerprintModePrefKey, FingerprintUnlockMode.Disabled.ToString());
                        edit.Commit();

                        App.Kp2a.ShowMessage(_dlg.Context, Resource.String.fingerprint_reenable, MessageSeverity.Warning);
                        _dlg.Context.StartActivity(typeof(BiometricSetupActivity));
                    }

                    _dlg.Dismiss();
                }
                else
                {
                    DisplayMessage(_dlg.Context);
                }

                base.Run();
            }

        }

    }

}

