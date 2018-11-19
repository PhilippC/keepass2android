using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Support.V7.App;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using keepass2android.database.edit;
using KeePass.Util.Spr;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using AlertDialog = Android.App.AlertDialog;
using Object = Java.Lang.Object;

namespace keepass2android
{
    [Activity(Label = "@string/child_dbs_title", MainLauncher = false, Theme = "@style/MyTheme_Blue", LaunchMode = LaunchMode.SingleInstance)]
    [IntentFilter(new[] { "kp2a.action.ConfigureChildDatabasesActivity" }, Categories = new[] { Intent.CategoryDefault })]
    public class ConfigureChildDatabasesActivity : LockCloseActivity
    {
        private ChildDatabasesAdapter _adapter;

        public class ChildDatabasesAdapter : BaseAdapter
        {

            private readonly ConfigureChildDatabasesActivity _context;
            internal List<AutoExecItem> _displayedChildDatabases;

            public ChildDatabasesAdapter(ConfigureChildDatabasesActivity context)
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

            

            private LayoutInflater cursorInflater;

            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                if (cursorInflater == null)
                    cursorInflater = (LayoutInflater)_context.GetSystemService(Context.LayoutInflaterService);

                View view;

                if (convertView == null)
                {
                    // if it's not recycled, initialize some attributes

                    view = cursorInflater.Inflate(Resource.Layout.child_db_config_row, parent, false);


                    view.FindViewById<Button>(Resource.Id.child_db_enable_on_this_device).Click += (sender, args) =>
                    {
                        View sending_view = (View) sender;
                        _context.OnEnable(_displayedChildDatabases[GetClickedPos(sending_view)]);
                    };

                    view.FindViewById<Button>(Resource.Id.child_db_disable_on_this_device).Click += (sender, args) =>
                    {
                        View sending_view = (View)sender;
                        _context.OnDisable(_displayedChildDatabases[GetClickedPos(sending_view)]);
                    };


                    view.FindViewById<Button>(Resource.Id.child_db_edit).Click += (sender, args) =>
                    {
                        View sending_view = (View)sender;
                        _context.OnEdit(_displayedChildDatabases[GetClickedPos(sending_view)]);
                    };


                    view.FindViewById<Button>(Resource.Id.child_db_open).Click += (sender, args) =>
                    {
                        View sending_view = (View)sender;
                        _context.OnOpen(_displayedChildDatabases[GetClickedPos(sending_view)]);
                    };


                    view.FindViewById<Button>(Resource.Id.child_db_enable_a_copy_for_this_device).Click += (sender, args) =>
                    {
                        View sending_view = (View)sender;
                        _context.OnEnableCopy(_displayedChildDatabases[GetClickedPos(sending_view)]);
                    };
                }
                else
                {
                    view = convertView;
                }

                
                var iv = view.FindViewById<ImageView>(Resource.Id.child_db_icon);
                var autoExecItem = _displayedChildDatabases[position];
                var pw = autoExecItem.Entry;

                SprContext ctx = new SprContext(pw, App.Kp2a.FindDatabaseForElement(pw).KpDatabase, SprCompileFlags.All);

                string deviceId = KeeAutoExecExt.ThisDeviceId;

                view.FindViewById<TextView>(Resource.Id.child_db_title).Text =
                    SprEngine.Compile(pw.Strings.GetSafe(PwDefs.TitleField).ReadString(), ctx);

                view.FindViewById<TextView>(Resource.Id.child_db_url).Text =
                    _context.GetString(Resource.String.entry_url) + ": " + SprEngine.Compile(pw.Strings.GetSafe(PwDefs.UrlField).ReadString(),ctx);

                bool deviceEnabledExplict;
                bool deviceEnabled = KeeAutoExecExt.IsDeviceEnabled(autoExecItem, deviceId, out deviceEnabledExplict);
                deviceEnabled &= deviceEnabledExplict;


                if (!autoExecItem.Enabled)
                {
                    view.FindViewById<TextView>(Resource.Id.child_db_enabled_here).Text =
                        _context.GetString(Resource.String.plugin_disabled);
                }
                else
                {
                    view.FindViewById<TextView>(Resource.Id.child_db_enabled_here).Text =
                        _context.GetString(Resource.String.child_db_enabled_on_this_device) + ": " +
                        (!deviceEnabledExplict ? 
                            _context.GetString(Resource.String.unspecified)
                            : 
                            ((autoExecItem.Enabled && deviceEnabled)
                                ? _context.GetString(Resource.String.yes)
                                : _context.GetString(Resource.String.no)));
                }

                view.FindViewById(Resource.Id.child_db_enable_on_this_device).Visibility = !deviceEnabled && autoExecItem.Enabled ? ViewStates.Visible : ViewStates.Gone;
                view.FindViewById(Resource.Id.child_db_disable_on_this_device).Visibility = (deviceEnabled || !deviceEnabledExplict) && autoExecItem.Enabled ? ViewStates.Visible : ViewStates.Gone;
                view.FindViewById(Resource.Id.child_db_enable_a_copy_for_this_device_container).Visibility = !deviceEnabled && autoExecItem.Enabled ? ViewStates.Visible : ViewStates.Gone;
                view.FindViewById(Resource.Id.child_db_edit).Visibility = deviceEnabledExplict || !autoExecItem.Enabled  ? ViewStates.Visible : ViewStates.Gone;
                IOConnectionInfo ioc;
                if ((KeeAutoExecExt.TryGetDatabaseIoc(autoExecItem, out ioc)) && App.Kp2a.TryGetDatabase(ioc) == null)
                {
                    view.FindViewById(Resource.Id.child_db_open).Visibility = ViewStates.Visible;
                }
                else view.FindViewById(Resource.Id.child_db_open).Visibility = ViewStates.Gone;


                Database db = App.Kp2a.FindDatabaseForElement(pw);

                bool isExpired = pw.Expires && pw.ExpiryTime < DateTime.Now;
                if (isExpired)
                {
                    db.DrawableFactory.AssignDrawableTo(iv, _context, db.KpDatabase, PwIcon.Expired, PwUuid.Zero, false);
                }
                else
                {
                    db.DrawableFactory.AssignDrawableTo(iv, _context, db.KpDatabase, pw.IconId, pw.CustomIconUuid, false);
                }



                view.Tag = position.ToString();
                
                return view;
            }

            private static int GetClickedPos(View sending_view)
            {
                View viewWithTag = sending_view;
                while (viewWithTag.Tag == null)
                    viewWithTag = (View) viewWithTag.Parent;
                int clicked_pos = int.Parse((string) viewWithTag.Tag);
                return clicked_pos;
            }

            public override int Count
            {
                get { return _displayedChildDatabases.Count; }
            }

            public void Update()
            {

                _displayedChildDatabases = KeeAutoExecExt.GetAutoExecItems(App.Kp2a.CurrentDb.KpDatabase)
                    .Where(e => App.Kp2a.TryFindDatabaseForElement(e.Entry) != null) //Update() can be called while we're adding entries to the database. They may be part of the group but without saving complete
                    .OrderBy(e => SprEngine.Compile(e.Entry.Strings.ReadSafe(PwDefs.TitleField),new SprContext(e.Entry, App.Kp2a.FindDatabaseForElement(e.Entry).KpDatabase, SprCompileFlags.All)))
                    .ThenByDescending(e => e.Entry.LastModificationTime)
                  .ToList();
            }
        }

        private void OnOpen(AutoExecItem item)
        {
            KeeAutoExecExt.AutoOpenEntry(this, item, true);

        }

        private void OnEnableCopy(AutoExecItem item)
        {
            //disable this device for the cloned entry
            KeeAutoExecExt.SetDeviceEnabled(item, KeeAutoExecExt.ThisDeviceId, false);
            //remember the original device settings
            ProtectedString ifDeviceOrig = item.Entry.Strings.GetSafe(KeeAutoExecExt._ifDevice);
            //reset device settings so only the current device is enabled 
            item.Entry.Strings.Set(KeeAutoExecExt._ifDevice,new ProtectedString(false,""));
            KeeAutoExecExt.SetDeviceEnabled(item, KeeAutoExecExt.ThisDeviceId, true); 
            //now clone
            var newEntry = item.Entry.CloneDeep();
            //reset device settings
            item.Entry.Strings.Set(KeeAutoExecExt._ifDevice, ifDeviceOrig);
            newEntry.SetUuid(new PwUuid(true), true); // Create new UUID
            string strTitle = newEntry.Strings.ReadSafe(PwDefs.TitleField);
            newEntry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, strTitle + " (" + Android.OS.Build.Model + ")"));
            var addTask = new AddEntry(this, App.Kp2a, newEntry,item.Entry.ParentGroup,new ActionOnFinish(this, (success, message, activity) => ((ConfigureChildDatabasesActivity)activity).Update()));

            ProgressTask pt = new ProgressTask(App.Kp2a, this, addTask);
            pt.Run();

        }

        private void Update()
        {
            _adapter.Update();
            _adapter.NotifyDataSetChanged();
        }

        private void OnEdit(AutoExecItem item)
        {
            EntryEditActivity.Launch(this,item.Entry,new NullTask());
        }

        private void OnDisable(AutoExecItem item)
        {
            KeeAutoExecExt.SetDeviceEnabled(item,KeeAutoExecExt.ThisDeviceId, false);
            Save(item);
        }

        private void OnEnable(AutoExecItem item)
        {
            KeeAutoExecExt.SetDeviceEnabled(item, KeeAutoExecExt.ThisDeviceId, true);
            Save(item);
        }

        private void Save(AutoExecItem item)
        {
            var addTask = new SaveDb(this, App.Kp2a, App.Kp2a.FindDatabaseForElement(item.Entry), new ActionOnFinish(this, (success, message, activity) => ((ConfigureChildDatabasesActivity)activity).Update()));

            ProgressTask pt = new ProgressTask(App.Kp2a, this, addTask);
            pt.Run();
        }

        protected override void OnResume()
        {
            base.OnResume();
            _adapter.Update();
            _adapter.NotifyDataSetChanged();
        }


        protected override void OnCreate(Bundle savedInstanceState)
        {

            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.config_child_db);

            _adapter = new ChildDatabasesAdapter(this);
            var listView = FindViewById<ListView>(Android.Resource.Id.List);
            listView.Adapter = _adapter;

            SetSupportActionBar(FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.mytoolbar));

            FindViewById<Button>(Resource.Id.add_child_db_button).Click += (sender, args) =>
            {
                AlertDialog.Builder builder = new AlertDialog.Builder(this);
                builder.SetTitle(Resource.String.add_child_db);

                List<string> items = new List<string>();
                Dictionary<int, Database> indexToDb = new Dictionary<int, Database>();

                foreach (var db in App.Kp2a.OpenDatabases)
                {
                    if (db != App.Kp2a.CurrentDb)
                    {
                        indexToDb[items.Count] = db;
                        items.Add(App.Kp2a.GetFileStorage(db.Ioc).GetDisplayName(db.Ioc));
                    }
                }
                indexToDb[items.Count] = null;
                items.Add(GetString(Resource.String.open_other_db));

                builder.SetItems(items.ToArray(), (o, eventArgs) =>
                {
                    Database db;
                    if (!indexToDb.TryGetValue(eventArgs.Which, out db) || (db == null))
                    {
                        StartFileSelect();
                    }
                    else
                    {
                        AddAutoOpenEntryForDatabase(db);
                    }

                });
                

                
                AlertDialog dialog = builder.Create();
                dialog.Show();
            };
        }

        private void AddAutoOpenEntryForDatabase(Database db)
        {
            PwGroup autoOpenGroup = null;
            var rootGroup = App.Kp2a.CurrentDb.KpDatabase.RootGroup;
            foreach (PwGroup pgSub in rootGroup.Groups)
            {
                if (pgSub.Name == "AutoOpen")
                {
                    autoOpenGroup = pgSub;
                    break;

                }
                    
            }
            if (autoOpenGroup == null)
            {
                AddGroup addGroupTask = AddGroup.GetInstance(this, App.Kp2a, "AutoOpen", 1, null, rootGroup, null, true);
                addGroupTask.Run();
                autoOpenGroup = addGroupTask.Group;
            }

            PwEntry newEntry = new PwEntry(true, true);
            newEntry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, App.Kp2a.GetFileStorage(db.Ioc).GetDisplayName(db.Ioc)));
            newEntry.Strings.Set(PwDefs.UrlField, new ProtectedString(false, TryMakeRelativePath(App.Kp2a.CurrentDb, db.Ioc)));
            var password = db.KpDatabase.MasterKey.GetUserKey<KcpPassword>();
            newEntry.Strings.Set(PwDefs.PasswordField, password == null ? new ProtectedString(true, "") : password.Password);

            var keyfile = db.KpDatabase.MasterKey.GetUserKey<KcpKeyFile>();
            if ((keyfile != null) && (keyfile.Ioc != null))
            {
                newEntry.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, TryMakeRelativePath(App.Kp2a.CurrentDb, keyfile.Ioc)));
            }

            newEntry.Strings.Set(KeeAutoExecExt._ifDevice,
                new ProtectedString(false,
                    KeeAutoExecExt.BuildIfDevice(new Dictionary<string, bool>()
                    {
                        {KeeAutoExecExt.ThisDeviceId, true}
                    })));

            var addTask = new AddEntry(this, App.Kp2a, newEntry, autoOpenGroup, new ActionOnFinish(this, (success, message, activity) => (activity as ConfigureChildDatabasesActivity)?.Update()));

            ProgressTask pt = new ProgressTask(App.Kp2a, this, addTask);
            pt.Run();
        }

        private string TryMakeRelativePath(Database db, IOConnectionInfo ioc)
        {
            try
            {
                var fsDb = App.Kp2a.GetFileStorage(db.Ioc);
                var dbParent = fsDb.GetParentPath(db.Ioc).Path + "/";
                if (ioc.Path.StartsWith(dbParent))
                {
                    return "{DB_DIR}{ENV_DIRSEP}" + ioc.Path.Substring(dbParent.Length);
                }

            }
            catch (NoFileStorageFoundException)
            {
            }
            return ioc.Path;

        }

        private void StartFileSelect(bool noForwardToPassword = false)
        {
            Intent intent = new Intent(this, typeof(FileSelectActivity));
            intent.PutExtra(FileSelectActivity.NoForwardToPasswordActivity, noForwardToPassword);
            intent.PutExtra("MakeCurrent", false);
            StartActivityForResult(intent, ReqCodeOpenNewDb);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == ReqCodeOpenNewDb)
            {
                switch ((int)resultCode)
                {
                    case (int)Result.Ok:

                        string iocString = data?.GetStringExtra("ioc");
                        IOConnectionInfo ioc = IOConnectionInfo.UnserializeFromString(iocString);
                        var db = App.Kp2a.TryGetDatabase(ioc);
                        if (db != null)
                            AddAutoOpenEntryForDatabase(db);

                        break;
                    case PasswordActivity.ResultSelectOtherFile:
                        StartFileSelect(true);
                        break;
                    default:
                        break;
                }

                return;
            }
        }

        public const int ReqCodeOpenNewDb = 1;



    }
}