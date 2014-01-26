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

using Android.App;
using Android.Content;
using Android.Widget;
using Android.OS;
using Android.Preferences;
using Android.Content.PM;
using Android.Text;
using Android.Text.Method;
using Java.Lang;
using Java.Lang.Reflect;
using KeePassLib.Serialization;
using Exception = System.Exception;
using String = System.String;

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
		public const Result ResultOkPasswordGenerator = Result.FirstUser + 9;

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

			if (GetCurrentRuntimeValue().StartsWith("ART"))
			{
				new AlertDialog.Builder(this)
					.SetTitle("Warning")
					.SetMessage(
						"It looks like you are running ART (Android Runtime). Please note: At the time of this app's release, Google says ART is experimental. And indeed, the early releases of ART (e.g. in Android 4.4, 4.4.1 and 4.4.2) contain a bug which causes crashes in Mono for Android apps including Keepass2Android. This bug was fixed after the 4.4.2 release so if you have a later Android release, you might be able to use this app. If not, please switch to Dalvik. Please do not downrate Keepass2Android for this problem, it's not our bug :-). Thanks! See our website (keepass2android.codeplex.com) for more information on this issue.")
					.SetPositiveButton("OK", (sender, args) => LaunchNextActivity())
					.Create()
					.Show();
				return;
			}

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

			}
			catch (PackageManager.NameNotFoundException)
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





		private static String SELECT_RUNTIME_PROPERTY = "persist.sys.dalvik.vm.lib";
		private static String LIB_DALVIK = "libdvm.so";
		private static String LIB_ART = "libart.so";
		private static String LIB_ART_D = "libartd.so";

		private String GetCurrentRuntimeValue()
		{
			try
			{
				Class systemProperties = Class.ForName("android.os.SystemProperties");
				try
				{
					Method get = systemProperties.GetMethod("get",
					                                        Class.FromType(typeof (Java.Lang.String)),
					                                        Class.FromType(typeof (Java.Lang.String)));
					if (get == null)
					{
						return "WTF?!";
					}
					try
					{
						String value = (String) get.Invoke(
							systemProperties, SELECT_RUNTIME_PROPERTY,
							/* Assuming default is */"Dalvik");
						if (LIB_DALVIK.Equals(value))
						{
							return "Dalvik";
						}
						else if (LIB_ART.Equals(value))
						{
							return "ART";
						}
						else if (LIB_ART_D.Equals(value))
						{
							return "ART debug build";
						}

						return value;
					}
					catch (IllegalAccessException e)
					{
						return "IllegalAccessException";
					}
					catch (IllegalArgumentException e)
					{
						return "IllegalArgumentException";
					}
					catch (InvocationTargetException e)
					{
						return "InvocationTargetException";
					}
				}
				catch (NoSuchMethodException e)
				{
					return "SystemProperties.get(String key, String def) method is not found";
				}
			}
			catch (ClassNotFoundException e)
			{
				return "SystemProperties class is not found";
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


