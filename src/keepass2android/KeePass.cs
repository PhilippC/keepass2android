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
/**
 * General documentation
 * 
 * Activity stack and activity results
 * ===================================
 * 
 * Keepass2Android comprises quite a number of different activities and entry points: The app can be started 
 * using the launcher icon (-> Activity "Keepass"), or by sending a URL (-> FileSelect), opening a .kdb(x)-file (->Password),
 * swiping a YubikeyNEO (NfcOtpActivity).
 * While the database is closed, there is only one activity on the stack: Keepass -> FileSelect <-> Password.
 * After opening an database (in Password), Password is always the root of the stack (exception: after creating a database, 
 * FileSelect is the root without Password being open). 
 * Another exception: QueryCredentialsActivity is root of the stack if an external app is querying credentials.
 * QueryCredentialsActivity checks the plugin access permissions, then launches FileSelectActivity (which starts
 * the normal stack.)
 * 
 * Some possible stacks:
 * Password -> Group ( -> Group (subgroups) ... ) -> EntryView -> EntryEdit
 *                         (AdvancedSearch Menu)  -> Search -> SearchResults -> EntryView -> EntryEdit
 *                         (SearchWidget)         -> SearchResults -> EntryView -> EntryEdit
 * Password -> ShareUrlResults -> EntryView
 * FileSelect -> Group (after Create DB)
 * 
 * 
 * In each of these activities, an AppTask may be present and must be passed to started activities and ActivityResults
 * must be returned. Therefore, if any Activity calls { StartActivity(newActivity);Finish(); }, it must specify FLAG_ACTIVITY_FORWARD_RESULT.
 * 
 * Further sub-activities may be opened (e.g. Settings -> ExportDb, ...), but these are not necesarrily
 * part of the AppTask. Then, neither the task has to be passed nor must the sub-activity return an ActivityResult.
 * 
 * Activities with AppTasks should check if they get a new AppTask in OnActivityResult.
 * 
 * Note: Chrome fires the ActionSend (Share URL) intent with NEW_TASK (i.e. KP2A appears in a separate task, either a new one,
 * or, if it was running before, in the KP2A task), whereas Firefox doesn't specify that flag and KP2A appears "inside" Firefox.
 * This means that the AppTask must be cleared for use in Chrome after finding an entry or pressing back button in ShareUrlResults.
 * This would not be necessary for Firefox where the (Android) Task of standalone KP2A is not affected by the search.
 */

namespace keepass2android
{
	/// <summary>
	/// Launcher activity of Keepass2Android. This activity usually forwards to FileSelect but may show the revision dialog after installation or updates.
	/// </summary>
	[Activity(Label = AppNames.AppName, MainLauncher = false, Theme = "@style/MyTheme_ActionBar")]
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
		private ActivityDesign _design;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			_design.ApplyTheme();
			//see comment to this in PasswordActivity.
			//Note that this activity is affected even though it's finished when the app is closed because it
			//seems that the "app launch intent" is re-delivered, so this might end up here.
			if ((_appTask == null) && (Intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory)))
			{
				_appTask = new NullTask();
			}
			else
			{
				_appTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);
			}


			Kp2aLog.Log("KeePass.OnCreate");
		}

		protected override void OnResume()
		{
			base.OnResume();
			Kp2aLog.Log("KeePass.OnResume");
			_design.ReapplyTheme();

		}


		protected override void OnStart()
		{
			base.OnStart();
			Kp2aLog.Log("KeePass.OnStart");

			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
			/*if (GetCurrentRuntimeValue().StartsWith("ART"))
			{
				
				if (!prefs.GetBoolean("ART_WARNING", false))
				{

					ISharedPreferencesEditor edit = prefs.Edit();
					edit.PutBoolean("ART_WARNING", true);
					EditorCompat.Apply(edit);

					new AlertDialog.Builder(this)
					.SetTitle("Warning")
					.SetMessage(
						//"It looks like you are running ART (Android Runtime). Please note: At the time of this app's release, Google says ART is experimental. And indeed, the early releases of ART (e.g. in Android 4.4, 4.4.1 and 4.4.2) contain a bug which causes crashes in Mono for Android apps including Keepass2Android. This bug was fixed after the 4.4.2 release so if you have a later Android release, you might be able to use this app. If not, please switch to Dalvik. Please do not downrate Keepass2Android for this problem, it's not our bug :-). Thanks! See our website (keepass2android.codeplex.com) for more information on this issue.")
						"It looks like you are running ART (Android Runtime). Please note: At the time of this app's release, this app does not run completely stable with ART. I am waiting for more fixes regarding ART from the makers of Mono for Android. As ART is still not yet meant for every-day use, please do not down rate KP2A for this. Thanks.")
					.SetPositiveButton("OK", (sender, args) => LaunchNextActivity())
					.Create()
					.Show();

					return;
				}
				
			}*/

			
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
#if DEBUG
		    showChangeLog = false;
#endif

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
		public static string StartWithTask = "keepass2android.ACTION_START_WITH_TASK";

		public KeePass()
		{
			_design = new ActivityDesign(this);
		}

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
			intent.AddFlags(ActivityFlags.ForwardResult);
			StartActivity(intent);
			Finish();
			
		}
		

		protected override void OnDestroy() {
			Kp2aLog.Log("KeePass.OnDestroy"+IsFinishing.ToString());
			base.OnDestroy();
		}


	}
}


