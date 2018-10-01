using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using keepass2android.Io;
using keepass2android.Utils;
using KeeChallenge;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace keepass2android
{
    [Activity(Label = AppNames.AppName, MainLauncher = false, Theme = "@style/MyTheme_Blue", LaunchMode = LaunchMode.SingleInstance)]
    public class StackBaseActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            if ((AppTask == null) && (Intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory)))
            {
                AppTask = new NullTask();
            }
            else
            {
                AppTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);
            }
        }


        IOConnectionInfo LoadIoc(string defaultFileName)
        {
            return App.Kp2a.FileDbHelper.CursorToIoc(App.Kp2a.FileDbHelper.FetchFileByName(defaultFileName));
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (!IsFinishing)
            {
                if (App.Kp2a.GetDb() == null)
                {
                    // Load default database
                    ISharedPreferences prefs = Android.Preferences.PreferenceManager.GetDefaultSharedPreferences(this);
                    String defaultFileName = prefs.GetString(PasswordActivity.KeyDefaultFilename, "");

                    if (defaultFileName.Length > 0)
                    {
                        try
                        {
                            PasswordActivity.Launch(this, LoadIoc(defaultFileName), AppTask);
                            return;
                        }
                        catch (Exception e)
                        {
                            Toast.MakeText(this, e.Message, ToastLength.Long);
                            // Ignore exception
                        }
                    }

                    Intent intent = new Intent(this, typeof(FileSelectActivity));
                    AppTask.ToIntent(intent);
                    intent.AddFlags(ActivityFlags.ForwardResult);
                    StartActivity(intent);
                    return;
                }


                //database loaded
                if (App.Kp2a.QuickLocked)
                {
                    var i = new Intent(this, typeof(QuickUnlock));
                    Util.PutIoConnectionToIntent(App.Kp2a.GetDb().Ioc, i);
                    Kp2aLog.Log("Starting QuickUnlock");
                    StartActivityForResult(i, 0);
                }
                else
                {
                    AppTask.LaunchFirstGroupActivity(this);
                }
                
            }
            base.OnResume();
        }

        internal AppTask AppTask;

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            Kp2aLog.Log("StackBaseActivity.OnActivityResult " + resultCode + "/" + requestCode);

            AppTask.TryGetFromActivityResult(data, ref AppTask);

            switch (resultCode)
            {
                case KeePass.ExitNormal: // Returned to this screen using the Back key
                    if (PreferenceManager.GetDefaultSharedPreferences(this)
                        .GetBoolean(GetString(Resource.String.LockWhenNavigateBack_key), false))
                    {
                        App.Kp2a.LockDatabase();
                    }
                    //by leaving the app with the back button, the user probably wants to cancel the task
                    //The activity might be resumed (through Android's recent tasks list), then use a NullTask:
                    AppTask = new NullTask();
                    Finish();
                    break;
                case KeePass.ExitLock:
                    // The database has already been locked, and the quick unlock screen will be shown if appropriate
                    break;
                case KeePass.ExitCloseAfterTaskComplete:
                    // Do not lock the database
                    SetResult(KeePass.ExitCloseAfterTaskComplete);
                    Finish();
                    break;
                case KeePass.ExitClose:
                    SetResult(KeePass.ExitClose);
                    Finish();
                    break;
                case KeePass.ExitReloadDb:

                    if (App.Kp2a.GetDb() != null)
                    {
                        //remember the composite key for reloading:
                        var compositeKey = App.Kp2a.GetDb().KpDatabase.MasterKey;
                        var ioc = App.Kp2a.GetDb().Ioc;

                        //lock the database:
                        App.Kp2a.LockDatabase(false);

                        LaunchPasswordActivityForReload(ioc, compositeKey);
                    }

                    break;
            }
        
        }

        private void LaunchPasswordActivityForReload(IOConnectionInfo ioc, CompositeKey compositeKey)
        {
            PasswordActivity.Launch(this, ioc, AppTask, compositeKey);
        }
    }
}