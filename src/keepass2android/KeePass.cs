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
using Android.Widget;
using Android.OS;
using Android.Preferences;
using Android.Content.PM;
using Android.Text;
using Android.Text.Method;
using KeePassLib.Serialization;

namespace keepass2android
{
	/// <summary>
	/// Launcher activity of Keepass2Android. This activity usually forwards to FileSelect but may show the revision dialog after installation or updates.
	/// </summary>
	[Activity (Label = AppNames.AppName, MainLauncher = true, Theme="@style/Base")]
	public class KeePass : LifecycleDebugActivity
	{
		public const Result ExitNormal = Result.FirstUser;
		public const Result ExitLock = Result.FirstUser+1;
		public const Result ExitRefresh = Result.FirstUser+2;
		public const Result ExitRefreshTitle = Result.FirstUser+3;
		public const Result ExitCloseAfterTaskComplete = Result.FirstUser+4;
		public const Result TaskComplete = Result.FirstUser + 5;
		public const Result ExitReloadDb = Result.FirstUser+6;
		public const Result ExitClose = Result.FirstUser + 7;
		public const Result ExitFileStorageSelectionOk = Result.FirstUser + 8;

		AppTask _appTask;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			_appTask = AppTask.GetTaskInOnCreate(bundle, Intent);
			Kp2aLog.Log("KeePass.OnCreate");
		}

		protected override void OnResume()
		{
			base.OnResume();
			Kp2aLog.Log("KeePass.OnResume");
		}
		

		protected override void OnStart()
		{
			base.OnStart();
			Kp2aLog.Log("KeePass.OnStart");

			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);

			bool showChangeLog = false;
			try
			{
				PackageInfo packageInfo = PackageManager.GetPackageInfo(PackageName, 0);
				int lastInfoVersionCode = prefs.GetInt(GetString(Resource.String.LastInfoVersionCode_key), 0);
				if (packageInfo.VersionCode > lastInfoVersionCode)
				{
					showChangeLog = true;

					ISharedPreferencesEditor edit = prefs.Edit();
					edit.PutInt(GetString(Resource.String.LastInfoVersionCode_key), packageInfo.VersionCode);
					EditorCompat.Apply(edit);
				}
				
			} catch (PackageManager.NameNotFoundException)
			{

			}

			if (showChangeLog)
			{
				ChangeLog.ShowChangeLog(this, LaunchNextActivity);


			}
			else
			{
				LaunchNextActivity();
			}





		}

		
		IOConnectionInfo LoadIoc(string defaultFileName)
		{
			return App.Kp2a.FileDbHelper.CursorToIoc(App.Kp2a.FileDbHelper.FetchFileByName(defaultFileName));
		}

		private void LaunchNextActivity() {

			

			if (!App.Kp2a.GetDb().Loaded)
			{
				// Load default database
				ISharedPreferences prefs = Android.Preferences.PreferenceManager.GetDefaultSharedPreferences(this);
				String defaultFileName = prefs.GetString(PasswordActivity.KeyDefaultFilename, "");

				if (defaultFileName.Length > 0)
				{
					try
					{
						PasswordActivity.Launch(this, LoadIoc(defaultFileName), _appTask);
						Finish();
						return;
					}
					catch (Exception e)
					{
						Toast.MakeText(this, e.Message, ToastLength.Long);
						// Ignore exception
					}
				}
			}
			else
			{
				PasswordActivity.Launch(this, App.Kp2a.GetDb().Ioc, _appTask);
				Finish();
				return;
			}

			Intent intent = new Intent(this, typeof(FileSelectActivity));
			_appTask.ToIntent(intent);


			StartActivityForResult(intent, 0);
			Finish();
			
		}
		

		protected override void OnDestroy() {
			Kp2aLog.Log("KeePass.OnDestroy"+IsFinishing.ToString());
			base.OnDestroy();
		}


		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
			base.OnActivityResult(requestCode, resultCode, data);
			
			Finish();
		}
	}
}


