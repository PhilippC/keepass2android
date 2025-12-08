using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Google.Android.Material.Dialog;
using keepass2android.database.edit;
using KeePassLib;
using KeePassLib.Serialization;

namespace keepass2android
{
    [Activity(Label = "@string/keeshare_title", MainLauncher = false, Theme = "@style/Kp2aTheme_BlueNoActionBar", LaunchMode = LaunchMode.SingleInstance, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden, Exported = true)]
    [IntentFilter(new[] { "kp2a.action.ConfigureKeeShareActivity" }, Categories = new[] { Intent.CategoryDefault })]
    public class ConfigureKeeShareActivity : LockCloseActivity
    {
        private KeeShareAdapter _adapter;
        private const int ReqCodeSelectFile = 1;
        private const string PendingConfigItemUuidKey = "PendingConfigItemUuid";
        private KeeShareItem _pendingConfigItem;

        public class KeeShareAdapter : BaseAdapter
        {
            private readonly ConfigureKeeShareActivity _context;
            internal List<KeeShareItem> _displayedItems;

            public KeeShareAdapter(ConfigureKeeShareActivity context)
            {
                _context = context;
                Update();
            }

            public override Java.Lang.Object GetItem(int position)
            {
                return position;
            }

            public override long GetItemId(int position)
            {
                return position;
            }

            private LayoutInflater _inflater;

            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                if (_inflater == null)
                    _inflater = (LayoutInflater)_context.GetSystemService(Context.LayoutInflaterService);

                View view;

                if (convertView == null)
                {
                    view = _inflater.Inflate(Resource.Layout.keeshare_config_row, parent, false);

                    view.FindViewById<Button>(Resource.Id.keeshare_configure_path).Click += (sender, args) =>
                    {
                        View sendingView = (View)sender;
                        _context.OnConfigurePath(_displayedItems[GetClickedPos(sendingView)]);
                    };

                    view.FindViewById<Button>(Resource.Id.keeshare_clear_path).Click += (sender, args) =>
                    {
                        View sendingView = (View)sender;
                        _context.OnClearPath(_displayedItems[GetClickedPos(sendingView)]);
                    };

                    view.FindViewById<Button>(Resource.Id.keeshare_sync_now).Click += (sender, args) =>
                    {
                        View sendingView = (View)sender;
                        _context.OnSyncNow(_displayedItems[GetClickedPos(sendingView)]);
                    };
                }
                else
                {
                    view = convertView;
                }

                var item = _displayedItems[position];
                var group = item.Group;

                string deviceId = KeeAutoExecExt.ThisDeviceId;
                string effectivePath = KeeShare.GetEffectiveFilePath(group);
                bool hasDevicePath = KeeShare.HasDeviceFilePath(group);

                view.FindViewById<TextView>(Resource.Id.keeshare_group_name).Text = group.Name;

                string typeText = item.Type;
                if (string.IsNullOrEmpty(typeText)) typeText = "Unknown";
                view.FindViewById<TextView>(Resource.Id.keeshare_type).Text = 
                    _context.GetString(Resource.String.keeshare_type) + ": " + typeText;

                string originalPath = item.OriginalPath;
                view.FindViewById<TextView>(Resource.Id.keeshare_original_path).Text = 
                    _context.GetString(Resource.String.keeshare_original_path) + ": " + 
                    (string.IsNullOrEmpty(originalPath) ? _context.GetString(Resource.String.not_set) : originalPath);

                string statusText;
                if (hasDevicePath)
                {
                    statusText = _context.GetString(Resource.String.keeshare_path_configured) + ": " + effectivePath;
                }
                else if (!string.IsNullOrEmpty(effectivePath))
                {
                    statusText = _context.GetString(Resource.String.keeshare_using_original_path);
                }
                else
                {
                    statusText = _context.GetString(Resource.String.keeshare_path_not_configured);
                }
                view.FindViewById<TextView>(Resource.Id.keeshare_device_status).Text = statusText;

                view.FindViewById<Button>(Resource.Id.keeshare_clear_path).Visibility = 
                    hasDevicePath ? ViewStates.Visible : ViewStates.Gone;

                view.FindViewById<Button>(Resource.Id.keeshare_sync_now).Visibility = 
                    !string.IsNullOrEmpty(effectivePath) ? ViewStates.Visible : ViewStates.Gone;

                Database db = App.Kp2a.TryFindDatabaseForElement(group);
                var iv = view.FindViewById<ImageView>(Resource.Id.keeshare_icon);
                if (db != null)
                {
                    db.DrawableFactory.AssignDrawableTo(iv, _context, db.KpDatabase, group.IconId, group.CustomIconUuid, false);
                }

                view.Tag = position.ToString();

                return view;
            }

            private static int GetClickedPos(View sendingView)
            {
                View viewWithTag = sendingView;
                while (viewWithTag.Tag == null)
                    viewWithTag = (View)viewWithTag.Parent;
                int clickedPos = int.Parse((string)viewWithTag.Tag);
                return clickedPos;
            }

            public override int Count => _displayedItems.Count;

            public void Update()
            {
                _displayedItems = KeeShare.GetKeeShareItems(App.Kp2a.CurrentDb?.KpDatabase)
                    .Where(item => App.Kp2a.TryFindDatabaseForElement(item.Group) != null)
                    .OrderBy(item => item.Group.Name)
                    .ToList();
            }
        }

        private void OnConfigurePath(KeeShareItem item)
        {
            _pendingConfigItem = item;
            
            Intent intent = new Intent(this, typeof(FileSelectActivity));
            intent.PutExtra(FileSelectActivity.NoForwardToPasswordActivity, true);
            intent.PutExtra("MakeCurrent", false);
            StartActivityForResult(intent, ReqCodeSelectFile);
        }

        private void OnClearPath(KeeShareItem item)
        {
            new MaterialAlertDialogBuilder(this)
                .SetTitle(Resource.String.keeshare_clear_path_title)
                .SetMessage(Resource.String.keeshare_clear_path_message)
                .SetPositiveButton(Resource.String.yes, (sender, args) =>
                {
                    KeeShare.SetDeviceFilePath(item.Group, null);
                    Save(item);
                })
                .SetNegativeButton(Resource.String.no, (sender, args) => { })
                .Show();
        }

        private void OnSyncNow(KeeShareItem item)
        {
            var syncOp = new KeeShareCheckOperation(App.Kp2a, new ActionOnOperationFinished(App.Kp2a,
                (success, message, importantMessage, exception, context) =>
                {
                    if (success)
                    {
                        App.Kp2a.ShowMessage(this, GetString(Resource.String.keeshare_sync_complete), MessageSeverity.Info);
                    }
                    else
                    {
                        App.Kp2a.ShowMessage(this, message ?? GetString(Resource.String.keeshare_sync_failed), MessageSeverity.Error);
                    }
                    Update();
                }));
            
            BlockingOperationStarter pt = new BlockingOperationStarter(App.Kp2a, syncOp);
            pt.Run();
        }

        private void Update()
        {
            _adapter.Update();
            _adapter.NotifyDataSetChanged();
        }

        private void Save(KeeShareItem item)
        {
            var saveTask = new SaveDb(App.Kp2a, App.Kp2a.FindDatabaseForElement(item.Group), 
                new ActionInContextInstanceOnOperationFinished(ContextInstanceId, App.Kp2a, 
                    (success, message, importantMessage, exception, context) => (context as ConfigureKeeShareActivity)?.Update()));

            BlockingOperationStarter pt = new BlockingOperationStarter(App.Kp2a, saveTask);
            pt.Run();
        }

        protected override void OnResume()
        {
            base.OnResume();
            _adapter?.Update();
            _adapter?.NotifyDataSetChanged();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.config_keeshare);

            _adapter = new KeeShareAdapter(this);
            var listView = FindViewById<ListView>(Android.Resource.Id.List);
            listView.Adapter = _adapter;

            SetSupportActionBar(FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.mytoolbar));

            if (savedInstanceState != null)
            {
                string uuidString = savedInstanceState.GetString(PendingConfigItemUuidKey);
                if (!string.IsNullOrEmpty(uuidString))
                {
                    var uuid = new PwUuid(Convert.FromBase64String(uuidString));
                    _pendingConfigItem = _adapter._displayedItems.FirstOrDefault(i => i.Group.Uuid.Equals(uuid));
                }
            }
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            if (_pendingConfigItem != null)
            {
                outState.PutString(PendingConfigItemUuidKey, Convert.ToBase64String(_pendingConfigItem.Group.Uuid.UuidBytes));
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == ReqCodeSelectFile && _pendingConfigItem != null)
            {
                if (resultCode == Result.Ok)
                {
                    string iocString = data?.GetStringExtra("ioc");
                    if (!string.IsNullOrEmpty(iocString))
                    {
                        IOConnectionInfo ioc = IOConnectionInfo.UnserializeFromString(iocString);
                        KeeShare.SetDeviceFilePath(_pendingConfigItem.Group, ioc.Path);
                        Save(_pendingConfigItem);
                    }
                }
                _pendingConfigItem = null;
            }
        }
    }
}

