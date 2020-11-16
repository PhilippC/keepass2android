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

namespace keepass2android
{
	
	public class SetPasswordDialog : CancelDialog 
	{
	    

	    internal String Keyfile;
		
		public SetPasswordDialog(Activity activity):base(activity)
		{
		    
		}
		
		
		
		protected override void OnCreate(Bundle savedInstanceState) 
		{
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.set_password);
			
			SetTitle(Resource.String.password_title);
			
			// Ok button
			Button okButton = (Button) FindViewById(Resource.Id.ok);
			okButton.Click += (sender, e) => 
			{
				TextView passView = (TextView) FindViewById(Resource.Id.pass_password);
				String pass = passView.Text;
				TextView passConfView = (TextView) FindViewById(Resource.Id.pass_conf_password);
				String confpass = passConfView.Text;
				
				// Verify that passwords match
				if ( ! pass.Equals(confpass) ) {
					// Passwords do not match
					Toast.MakeText(Context, Resource.String.error_pass_match, ToastLength.Long).Show();
					return;
				}
				
				TextView keyfileView = (TextView) FindViewById(Resource.Id.pass_keyfile);
				String keyfile = keyfileView.Text;
				Keyfile = keyfile;
				
				// Verify that a password or keyfile is set
				if ( pass.Length == 0 && keyfile.Length == 0 ) {
					Toast.MakeText(Context, Resource.String.error_nopass, ToastLength.Long).Show();
					return;
					
				}
				
				SetPassword sp = new SetPassword(_activity, App.Kp2a, pass, keyfile, new AfterSave(_activity, this, null, new Handler()));
				ProgressTask pt = new ProgressTask(App.Kp2a, _activity, sp);
				pt.Run();
			};
				

			
			// Cancel button
			Button cancelButton = (Button) FindViewById(Resource.Id.cancel);
			cancelButton.Click += (sender,e) => {
				Cancel();
			}; 
		}


		
		class AfterSave : OnFinish {
			private readonly FileOnFinish _finish;

			readonly SetPasswordDialog _dlg;
			
			public AfterSave(Activity activity, SetPasswordDialog dlg, FileOnFinish finish, Handler handler): base(activity, finish, handler) {
				_finish = finish;
				_dlg = dlg;
			}
			
			
			public override void Run() {
				if ( Success ) {
					if ( _finish != null ) {
						_finish.Filename = _dlg.Keyfile;
					}
					FingerprintUnlockMode um;
					Enum.TryParse(PreferenceManager.GetDefaultSharedPreferences(_dlg.Context).GetString(App.Kp2a.CurrentDb.CurrentFingerprintModePrefKey, ""), out um);

					if (um == FingerprintUnlockMode.FullUnlock)
					{
						ISharedPreferencesEditor edit = PreferenceManager.GetDefaultSharedPreferences(_dlg.Context).Edit();
						edit.PutString(App.Kp2a.CurrentDb.CurrentFingerprintPrefKey, "");
						edit.PutString(App.Kp2a.CurrentDb.CurrentFingerprintModePrefKey, FingerprintUnlockMode.Disabled.ToString());
						edit.Commit();

						Toast.MakeText(_dlg.Context, Resource.String.fingerprint_reenable, ToastLength.Long).Show();
						_dlg.Context.StartActivity(typeof(BiometricSetupActivity));
					}

					_dlg.Dismiss();
				} else {
					DisplayMessage(_dlg.Context);
				}
				
				base.Run();
			}
			
		}
		
	}

}

