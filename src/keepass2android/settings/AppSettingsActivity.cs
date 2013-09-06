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
using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Widget;
using keepass2android.Io;

namespace keepass2android
{
	/// <summary>
	/// Activity to configure the application, without database settings. Does not require an unlocked database, or close when the database is locked
	/// </summary>
	[Activity (Label = "@string/app_name", Theme="@style/NoTitleBar")]			
	public class AppSettingsActivity : LockingPreferenceActivity
	{
		public static void Launch(Context ctx)
		{
			ctx.StartActivity(new Intent(ctx, typeof(AppSettingsActivity)));
		}

		protected override void OnCreate(Bundle savedInstanceState) 
		{
			base.OnCreate(savedInstanceState);
			
			AddPreferencesFromResource(Resource.Xml.preferences);
			
			FindPreference(GetString(Resource.String.keyfile_key)).PreferenceChange += OnRememberKeyFileHistoryChanged;
			FindPreference(GetString(Resource.String.ShowUnlockedNotification_key)).PreferenceChange += OnShowUnlockedNotificationChanged;;
			
			Preference cachingPreference = FindPreference(GetString(Resource.String.UseOfflineCache_key));
			cachingPreference.PreferenceChange += OnUseOfflineCacheChanged;

#if NoNet
			try
			{
				((PreferenceScreen) FindPreference(GetString(Resource.String.FileHandling_prefs_key))).RemovePreference(cachingPreference);
			}
			catch (Exception ex)
			{
				Kp2aLog.Log(ex.ToString());	
			}
#endif


			FindPreference(GetString(Resource.String.db_key)).Enabled = false;
		}

		private void OnUseOfflineCacheChanged(object sender, Preference.PreferenceChangeEventArgs e)
		{
			//ensure the user gets a matching database
			if (App.Kp2a.GetDb().Loaded && !App.Kp2a.GetDb().Ioc.IsLocalFile())
				App.Kp2a.LockDatabase(false);

			if (!(bool)e.NewValue)
			{
				AlertDialog.Builder builder = new AlertDialog.Builder(this);
				builder.SetTitle(GetString(Resource.String.ClearOfflineCache_title));

				builder.SetMessage(GetString(Resource.String.ClearOfflineCache_question));

				builder.SetPositiveButton(App.Kp2a.GetResourceString(UiStringKey.yes), (o, args) =>
					 {
						 try
						 {
							 App.Kp2a.ClearOfflineCache();
						 }
						 catch (Exception ex)
						 {
							 Kp2aLog.Log(ex.ToString());
							 Toast.MakeText(Application.Context, ex.Message, ToastLength.Long).Show();
						 }
					 }
					);

				builder.SetNegativeButton(App.Kp2a.GetResourceString(UiStringKey.no), (o, args) =>
				{
				}
				);

				Dialog dialog = builder.Create();
				dialog.Show();

				
			}
		}

		internal static void OnRememberKeyFileHistoryChanged(object sender, Preference.PreferenceChangeEventArgs eventArgs)
		{
			if (!(bool)eventArgs.NewValue)
			{
				App.Kp2a.FileDbHelper.DeleteAllKeys();
			}
		}

		internal static void OnShowUnlockedNotificationChanged(object sender, Preference.PreferenceChangeEventArgs eventArgs)
		{
			var ctx = ((Preference)sender).Context;
			ctx.StartService(new Intent(ctx, typeof(OngoingNotificationsService)));
		}
	}

}

