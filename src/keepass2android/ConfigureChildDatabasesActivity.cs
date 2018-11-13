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
using KeePassLib.Security;
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

                view.FindViewById<Button>(Resource.Id.child_db_enable_on_this_device).Visibility = !deviceEnabled && autoExecItem.Enabled ? ViewStates.Gone : ViewStates.Visible;
                view.FindViewById<Button>(Resource.Id.child_db_disable_on_this_device).Visibility = deviceEnabled && autoExecItem.Enabled ? ViewStates.Visible : ViewStates.Gone;
                view.FindViewById<Button>(Resource.Id.child_db_enable_a_copy_for_this_device_container).Visibility = !deviceEnabled && autoExecItem.Enabled ? ViewStates.Visible : ViewStates.Gone;
                

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
                _displayedChildDatabases = App.Kp2a.OpenDatabases.SelectMany(db => KeeAutoExecExt.GetAutoExecItems(db.KpDatabase)
                    .OrderBy(e => SprEngine.Compile(e.Entry.Strings.ReadSafe(PwDefs.TitleField),new SprContext(e.Entry, App.Kp2a.FindDatabaseForElement(e.Entry).KpDatabase, SprCompileFlags.All)))
                    .ThenByDescending(e => e.Entry.LastModificationTime))
                  .ToList();
            }
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
            EntryActivity.Launch(this,item.Entry,0,new NullTask());
        }

        private void OnDisable(AutoExecItem item)
        {
            KeeAutoExecExt.SetDeviceEnabled(item,KeeAutoExecExt.ThisDeviceId, false);
        }

        private void OnEnable(AutoExecItem item)
        {
            KeeAutoExecExt.SetDeviceEnabled(item, KeeAutoExecExt.ThisDeviceId, true);
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
        }
    }
}