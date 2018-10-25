using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Support.V7.App;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.IO;
using Java.Net;
using keepass2android.Io;
using keepass2android.Utils;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using Object = Java.Lang.Object;

namespace keepass2android
{
    [Activity(Label = AppNames.AppName, MainLauncher = false, Theme = "@style/MyTheme_Blue", LaunchMode = LaunchMode.SingleInstance)] //caution, see manifest file
    public class SelectCurrentDbActivity : AppCompatActivity
    {
        private int ReqCodeOpenNewDb = 1;
        

        public class OpenDatabaseAdapter : BaseAdapter
        {

            private readonly SelectCurrentDbActivity _context;
            internal List<Database> _displayedDatabases;

            public OpenDatabaseAdapter(SelectCurrentDbActivity context)
            {
                _context = context;
                Update();

            }

            public override Object GetItem(int position)
            {
                return position;
            }

            public override long GetItemId(int position)
            {
                return position;
            }



            public static float convertDpToPixel(float dp, Context context)
            {
                return Util.convertDpToPixel(dp, context);
            }


            public override View GetView(int position, View convertView, ViewGroup parent)
            {

                Button btn;

                if (convertView == null)
                {
                    // if it's not recycled, initialize some attributes

                    btn = new Button(_context);
                    btn.LayoutParameters = new GridView.LayoutParams((int) convertDpToPixel(140, _context),
                        (int) convertDpToPixel(150, _context));
                    btn.SetBackgroundResource(Resource.Drawable.storagetype_button_bg);
                    btn.SetPadding((int) convertDpToPixel(4, _context),
                        (int) convertDpToPixel(20, _context),
                        (int) convertDpToPixel(4, _context),
                        (int) convertDpToPixel(4, _context));
                    btn.SetTextSize(ComplexUnitType.Sp, 11);
                    btn.SetTextColor(new Color(115, 115, 115));
                    btn.SetSingleLine(false);
                    btn.Gravity = GravityFlags.Center;
                    btn.Click += (sender, args) =>
                    {
                        int pos;
                        int.TryParse(((Button) sender).Tag.ToString(), out pos);
                        if (pos < _displayedDatabases.Count)
                            _context.OnItemSelected(_displayedDatabases[pos]);
                        else
                        {
                            _context.OnOpenOther();
                        }
                    };
                }
                else
                {
                    btn = (Button) convertView;
                }

                btn.Tag = position.ToString();

                string displayName;
                Drawable drawable;
                if (position < _displayedDatabases.Count)
                {
                    var db = _displayedDatabases[position];
                    drawable = App.Kp2a.GetResourceDrawable("ic_storage_" + Util.GetProtocolId(db.Ioc));
                    displayName = db.KpDatabase.Name;
                    displayName += "\n" + App.Kp2a.GetFileStorage(db.Ioc).GetDisplayName(db.Ioc);
                }
                else
                {
                    displayName = _context.GetString(Resource.String.start_open_file);
                    drawable = App.Kp2a.GetResourceDrawable("ic_nav_changedb");
                }

                var str = new SpannableString(displayName);

                btn.TextFormatted = str;
                //var drawable = ContextCompat.GetDrawable(context, Resource.Drawable.Icon);
                btn.SetCompoundDrawablesWithIntrinsicBounds(null, drawable, null, null);

                return btn;
            }

            public override int Count
            {
                get { return _displayedDatabases.Count+1; }
            }

            public void Update()
            {
                _displayedDatabases = App.Kp2a.OpenDatabases.ToList();
            }
        }

        private void OnOpenOther()
        {
            StartFileSelect(true);
        }

        private void OnItemSelected(Database selectedDatabase)
        {
            App.Kp2a.CurrentDb = selectedDatabase;
            LaunchingOther = true;
            AppTask.LaunchFirstGroupActivity(this);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.open_db_selection);

            var toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.mytoolbar);

            SetSupportActionBar(toolbar);

            SupportActionBar.Title = GetString(Resource.String.select_database);


            //only load the AppTask if this is the "first" OnCreate (not because of kill/resume, i.e. savedInstanceState==null)
            // and if the activity is not launched from history (i.e. recent tasks) because this would mean that
            // the Activity was closed already (user cancelling the task or task complete) but is restarted due recent tasks.
            // Don't re-start the task (especially bad if tak was complete already)
            if (Intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory))
            {
                AppTask = new NullTask();
            }
            else
            {
                AppTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);
            }

            _adapter = new OpenDatabaseAdapter(this);
            var gridView = FindViewById<GridView>(Resource.Id.gridview);
            gridView.ItemClick += (sender, args) => OnItemSelected(_adapter._displayedDatabases[args.Position]);
            gridView.Adapter = _adapter;

            if (!string.IsNullOrEmpty(Intent.GetStringExtra(Util.KeyFilename)))
            {
                //forward to password activity
                Intent i = new Intent(this, typeof(PasswordActivity));
                IOConnectionInfo ioc = new IOConnectionInfo();
                Util.SetIoConnectionFromIntent(ioc, Intent);
                Util.PutIoConnectionToIntent(ioc, i);
                i.PutExtra(PasswordActivity.KeyKeyfile, i.GetStringExtra(PasswordActivity.KeyKeyfile));
                i.PutExtra(PasswordActivity.KeyPassword, i.GetStringExtra(PasswordActivity.KeyPassword));
                LaunchingOther = true;
                StartActivityForResult(i, ReqCodeOpenNewDb);
            }
            else
            {
                if (Intent.Action == Intent.ActionView)
                {
                    GetIocFromViewIntent(Intent);
                }
                else if (Intent.Action == Intent.ActionSend)
                {
                    AppTask = new SearchUrlTask { UrlToSearchFor = Intent.GetStringExtra(Intent.ExtraText) };
                }
            }

            _intentReceiver = new MyBroadcastReceiver(this);
            IntentFilter filter = new IntentFilter();
            filter.AddAction(Intents.DatabaseLocked);
            RegisterReceiver(_intentReceiver, filter);

        }

        private bool GetIocFromViewIntent(Intent intent)
        {
            IOConnectionInfo ioc = new IOConnectionInfo();

            //started from "view" intent (e.g. from file browser)
            ioc.Path = intent.DataString;

            if (ioc.Path.StartsWith("file://"))
            {
                ioc.Path = URLDecoder.Decode(ioc.Path.Substring(7));

                if (ioc.Path.Length == 0)
                {
                    // No file name
                    Toast.MakeText(this, Resource.String.FileNotFound, ToastLength.Long).Show();
                    return false;
                }

                File dbFile = new File(ioc.Path);
                if (!dbFile.Exists())
                {
                    // File does not exist
                    Toast.MakeText(this, Resource.String.FileNotFound, ToastLength.Long).Show();
                    return false;
                }
            }
            else
            {
                if (!ioc.Path.StartsWith("content://"))
                {
                    Toast.MakeText(this, Resource.String.error_can_not_handle_uri, ToastLength.Long).Show();
                    return false;
                }
                IoUtil.TryTakePersistablePermissions(this.ContentResolver, intent.Data);


            }

            {
                Intent launchIntent = new Intent(this, typeof(PasswordActivity));
                Util.PutIoConnectionToIntent(ioc, launchIntent);
                LaunchingOther = true;
                StartActivityForResult(launchIntent, ReqCodeOpenNewDb);
            }


            return true;
        }
        
        protected override void OnResume()
        {
            base.OnResume();
            if (!IsFinishing && !LaunchingOther)
            {
                if (App.Kp2a.OpenDatabases.Any() == false)
                {
                    StartFileSelect();
                    return;
                }

                //database loaded
                if (App.Kp2a.QuickLocked)
                {
                    var i = new Intent(this, typeof(QuickUnlock));
                    Util.PutIoConnectionToIntent(App.Kp2a.GetDbForQuickUnlock().Ioc, i);
                    Kp2aLog.Log("Starting QuickUnlock");
                    StartActivityForResult(i, 0);
                    return;
                }

                //database(s) unlocked
                if (App.Kp2a.OpenDatabases.Count() == 1)
                {
                    LaunchingOther = true;
                    AppTask.LaunchFirstGroupActivity(this);
                    return;
                }

                //more than one database open or user requested to load another db. Don't launch another activity.
                _adapter.Update();
                _adapter.NotifyDataSetChanged();


            }
            base.OnResume();
        }

        protected override void OnPause()
        {
            LaunchingOther = false;
            base.OnPause();
        }

        private void StartFileSelect(bool noForwardToPassword = false)
        {
            Intent intent = new Intent(this, typeof(FileSelectActivity));
            AppTask.ToIntent(intent);
            intent.PutExtra(FileSelectActivity.NoForwardToPasswordActivity, noForwardToPassword);
            LaunchingOther = true;
            StartActivityForResult(intent, ReqCodeOpenNewDb);
        }

        internal AppTask AppTask;
        private bool _loadAnotherDatabase;
        private OpenDatabaseAdapter _adapter;
        private MyBroadcastReceiver _intentReceiver;

        public override void OnBackPressed()
        {
            base.OnBackPressed();
            if (PreferenceManager.GetDefaultSharedPreferences(this)
                .GetBoolean(GetString(Resource.String.LockWhenNavigateBack_key), false))
            {
                App.Kp2a.Lock();
            }
            //by leaving the app with the back button, the user probably wants to cancel the task
            //The activity might be resumed (through Android's recent tasks list), then use a NullTask:
            AppTask = new NullTask();
            Finish();
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            Kp2aLog.Log("StackBaseActivity.OnActivityResult " + resultCode + "/" + requestCode);

            AppTask.TryGetFromActivityResult(data, ref AppTask);

            if (requestCode == ReqCodeOpenNewDb)
            {
                switch ((int)resultCode)
                {
                    case (int)Result.Ok:

                        string iocString = data?.GetStringExtra("ioc");
                        IOConnectionInfo ioc = IOConnectionInfo.UnserializeFromString(iocString);
                        if (App.Kp2a.TrySelectCurrentDb(ioc))
                        {
                            LaunchingOther = true;
                            AppTask.CanActivateSearchViewOnStart = true;
                            AppTask.LaunchFirstGroupActivity(this);
                        }


                        break;
                    case PasswordActivity.ResultSelectOtherFile:
                        StartFileSelect(true);
                        break;
                    case (int)Result.Canceled:
                        if (App.Kp2a.OpenDatabases.Any() == false)
                        {
                            //don't open fileselect/password activity again
                            OnBackPressed();
                        }
                        break;
                    default:
                        break;
                }

                return;
            }

            switch (resultCode)
            {
                case KeePass.ExitNormal: // Returned to this screen using the Back key
                    if (App.Kp2a.OpenDatabases.Count() == 1)
                    {
                        OnBackPressed(); 
                    }
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

                    if (App.Kp2a.CurrentDb!= null)
                    {
                        //remember the composite key for reloading:
                        var compositeKey = App.Kp2a.CurrentDb.KpDatabase.MasterKey;
                        var ioc = App.Kp2a.CurrentDb.Ioc;

                        //lock the database:
                        App.Kp2a.CloseDatabase(App.Kp2a.CurrentDb);

                        LaunchPasswordActivityForReload(ioc, compositeKey);
                    }

                    break;
                case KeePass.ExitLoadAnotherDb:
                    StartFileSelect(true);
                    break;
            }
        
        }

        private void LaunchPasswordActivityForReload(IOConnectionInfo ioc, CompositeKey compositeKey)
        {
            LaunchingOther = true;
            PasswordActivity.Launch(this, ioc, compositeKey, new ActivityLaunchModeRequestCode(ReqCodeOpenNewDb));
        }

        public bool LaunchingOther { get; set; }


        private class MyBroadcastReceiver : BroadcastReceiver
        {
            readonly SelectCurrentDbActivity _activity;
            public MyBroadcastReceiver(SelectCurrentDbActivity activity)
            {
                _activity = activity;
            }

            public override void OnReceive(Context context, Intent intent)
            {
                switch (intent.Action)
                {
                    case Intents.DatabaseLocked:
                        _activity.OnLockDatabase();
                        break;
                    case Intent.ActionScreenOff:
                        App.Kp2a.OnScreenOff();
                        break;
                }
            }
        }

        private void OnLockDatabase()
        {
            //app tasks are assumed to be finished/cancelled when the database is locked
            AppTask = new NullTask();
        }
    }
}