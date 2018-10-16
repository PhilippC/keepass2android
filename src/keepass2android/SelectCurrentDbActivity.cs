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
using keepass2android.Io;
using keepass2android.Utils;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using Object = Java.Lang.Object;

namespace keepass2android
{
    [Activity(Label = AppNames.AppName, MainLauncher = false, Theme = "@style/MyTheme_Blue", LaunchMode = LaunchMode.SingleInstance)]
    public class SelectCurrentDbActivity : AppCompatActivity
    {
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
            StartFileSelect();
        }

        private void OnItemSelected(Database selectedDatabase)
        {
            App.Kp2a.CurrentDb = selectedDatabase;
            AppTask.LaunchFirstGroupActivity(this);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.open_db_selection);

            var toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.mytoolbar);

            SetSupportActionBar(toolbar);

            SupportActionBar.Title = GetString(Resource.String.select_database);

            if ((AppTask == null) && (Intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory)))
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
            
        }


        IOConnectionInfo LoadIoc(string defaultFileName)
        {
            return App.Kp2a.FileDbHelper.CursorToIoc(App.Kp2a.FileDbHelper.FetchFileByName(defaultFileName));
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (!IsFinishing && !LaunchingPasswordActivity)
            {
                if (App.Kp2a.OpenDatabases.Any() == false)
                {
                    StartFileSelect();
                    return;
                }

                if (_loadAnotherDatabase)
                {
                    StartFileSelect();
                    _loadAnotherDatabase = false;
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
            LaunchingPasswordActivity = false;
            base.OnPause();
        }

        private void StartFileSelect()
        {
            Intent intent = new Intent(this, typeof(FileSelectActivity));
            AppTask.ToIntent(intent);
            intent.AddFlags(ActivityFlags.ForwardResult);
            StartActivity(intent);
        }

        internal AppTask AppTask;
        private bool _loadAnotherDatabase;
        private OpenDatabaseAdapter _adapter;

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
                    _loadAnotherDatabase = true;
                    break;
            }
        
        }

        private void LaunchPasswordActivityForReload(IOConnectionInfo ioc, CompositeKey compositeKey)
        {
            LaunchingPasswordActivity = true;
            PasswordActivity.Launch(this, ioc, AppTask, compositeKey);
        }

        public bool LaunchingPasswordActivity { get; set; }
    }
}