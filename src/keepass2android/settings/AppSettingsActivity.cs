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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Preferences;
using KeePassLib.Cryptography.Cipher;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", Theme="@style/NoTitleBar")]			
	public class AppSettingsActivity : LockingClosePreferenceActivity {
		public static bool KEYFILE_DEFAULT = false;
		
		public static void Launch(Context ctx) {
			Intent i = new Intent(ctx, typeof(AppSettingsActivity));
			
			ctx.StartActivity(i);
		}
		
		protected override void OnCreate(Bundle savedInstanceState) {
			base.OnCreate(savedInstanceState);
			
			AddPreferencesFromResource(Resource.Xml.preferences);
			
			Preference keyFile = FindPreference(GetString(Resource.String.keyfile_key));
			keyFile.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) => 
			{
				bool value = (bool) e.NewValue;
				
				if ( ! value ) {
					FileDbHelper helper = App.fileDbHelper;
					
					helper.deleteAllKeys();
				}
					
				return;
			};
			
			Database db = App.getDB();
			if ( db.Open ) {
				Preference rounds = FindPreference(GetString(Resource.String.rounds_key));
				rounds.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) => 
				{
					setRounds(App.getDB(), e.Preference);
					return;
				};

				Preference defaultUser = FindPreference(GetString(Resource.String.default_username_key));
				((EditTextPreference)defaultUser).EditText.Text = db.pm.DefaultUserName;
				((EditTextPreference)defaultUser).Text = db.pm.DefaultUserName;
				defaultUser.PreferenceChange += (object sender, Preference.PreferenceChangeEventArgs e) => 
				{
					DateTime previousUsernameChanged = db.pm.DefaultUserNameChanged;
					String previousUsername = db.pm.DefaultUserName;
					db.pm.DefaultUserName = e.NewValue.ToString();
				
					Handler handler = new Handler();

					SaveDB save = new SaveDB(this, App.getDB(), new ActionOnFinish( (success, message) => 
					                                                         {
						if (!success)
						{
							db.pm.DefaultUserName = previousUsername;
							db.pm.DefaultUserNameChanged = previousUsernameChanged;
							Toast.MakeText(this, message, ToastLength.Long).Show();
						}
					}));
					ProgressTask pt = new ProgressTask(this, save, Resource.String.saving_database);
					pt.run();
				};
				
				setRounds(db, rounds);
				
				Preference algorithm = FindPreference(GetString(Resource.String.algorithm_key));
				setAlgorithm(db, algorithm);
				
			} else {
				Preference dbSettings = FindPreference(GetString(Resource.String.db_key));
				dbSettings.Enabled = false;
			}

		}
		
		protected override void OnStop() {
			
			base.OnStop();
		}
		
		private void setRounds(Database db, Preference rounds) {
			rounds.Summary = db.pm.KeyEncryptionRounds.ToString();
		}
		
		private void setAlgorithm(Database db, Preference algorithm) {

			algorithm.Summary = CipherPool.GlobalPool.GetCipher(db.pm.DataCipherUuid).DisplayName;
		}
		

	}

}

