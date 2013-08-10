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

			FindPreference(GetString(Resource.String.db_key)).Enabled = false;
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

