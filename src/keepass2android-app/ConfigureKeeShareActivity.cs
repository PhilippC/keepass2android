using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Google.Android.Material.Dialog;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.TextField;
using keepass2android.database.edit;
using KeePassLib;
using KeePassLib.Serialization;

namespace keepass2android
{
    [Activity(Label = "@string/keeshare_title", MainLauncher = false, Theme = "@style/Kp2aTheme_BlueNoActionBar", LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden, Exported = true)]
    [IntentFilter(new[] { "kp2a.action.ConfigureKeeShareActivity" }, Categories = new[] { Intent.CategoryDefault })]
    public class ConfigureKeeShareActivity : LockCloseActivity
    {
        private KeeShareAdapter _adapter;
        private const int ReqCodeSelectFile = 1;
        private const int ReqCodeSelectFileForNewConfig = 2;
        private const string PendingConfigItemUuidKey = "PendingConfigItemUuid";
        private const string PendingNewConfigGroupUuidKey = "PendingNewConfigGroupUuid";
        private KeeShareItem _pendingConfigItem;
        private PwGroup _pendingNewConfigGroup;
        private AlertDialog _addDialog;
        private TextInputEditText _dialogFilePathEdit;
        private string _pendingNewConfigType;
        private string _pendingNewConfigPassword;
        private string _pendingNewGroupName; // For creating a new group in OnActivityResult
        private bool _pendingCreateNewGroup; // Flag to indicate if we should create a new group

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
                return _displayedItems[position];
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

                    view.FindViewById<Button>(Resource.Id.keeshare_edit_settings).Click += (sender, args) =>
                    {
                        View sendingView = (View)sender;
                        _context.OnEditSettings(_displayedItems[GetClickedPos(sendingView)]);
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

                // Show password status
                string password = item.Password;
                var passwordStatusView = view.FindViewById<TextView>(Resource.Id.keeshare_password_status);
                if (string.IsNullOrEmpty(password))
                {
                    passwordStatusView.Text = _context.GetString(Resource.String.keeshare_password_not_set);
                    passwordStatusView.SetTextColor(Android.Graphics.Color.ParseColor("#CC6600")); // Orange warning
                }
                else
                {
                    passwordStatusView.Text = _context.GetString(Resource.String.keeshare_password_set);
                    passwordStatusView.SetTextColor(Android.Graphics.Color.ParseColor("#008800")); // Green ok
                }

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
                while (viewWithTag?.Tag == null)
                {
                    if (viewWithTag?.Parent == null)
                        throw new InvalidOperationException("Could not find position tag in view hierarchy");
                    viewWithTag = (View)viewWithTag.Parent;
                }
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
                    var activity = context as ConfigureKeeShareActivity ?? this;
                    if (success)
                    {
                        App.Kp2a.ShowMessage(activity, activity.GetString(Resource.String.keeshare_sync_complete), MessageSeverity.Info);
                    }
                    else
                    {
                        // Provide more helpful error messages
                        string errorMessage = message ?? activity.GetString(Resource.String.keeshare_sync_failed);
                        if (errorMessage.Contains("master key") || errorMessage.Contains("InvalidCompositeKeyException"))
                        {
                            errorMessage = activity.GetString(Resource.String.keeshare_wrong_password);
                        }
                        App.Kp2a.ShowMessage(activity, errorMessage, MessageSeverity.Error);
                    }
                    // Update must be called on UI thread
                    activity?.RunOnUiThread(() => activity.Update());
                }));

            BlockingOperationStarter pt = new BlockingOperationStarter(App.Kp2a, syncOp);
            pt.Run();
        }

        private void OnEditSettings(KeeShareItem item)
        {
            ShowEditKeeShareDialog(item);
        }

        private void ShowEditKeeShareDialog(KeeShareItem item)
        {
            var group = item.Group;
            var inflater = LayoutInflater.From(this);
            var dialogView = inflater.Inflate(Resource.Layout.dialog_edit_keeshare, null);

            // Set up share type radio buttons
            var typeRadioGroup = dialogView.FindViewById<RadioGroup>(Resource.Id.radio_group_type);
            string currentType = item.Type;
            if (currentType == "Import")
                dialogView.FindViewById<RadioButton>(Resource.Id.radio_import).Checked = true;
            else if (currentType == "Synchronize")
                dialogView.FindViewById<RadioButton>(Resource.Id.radio_synchronize).Checked = true;
            else if (currentType == "Export")
                dialogView.FindViewById<RadioButton>(Resource.Id.radio_export).Checked = true;

            // Set up password field
            var passwordEdit = dialogView.FindViewById<TextInputEditText>(Resource.Id.edit_password);
            string currentPassword = item.Password;
            if (!string.IsNullOrEmpty(currentPassword))
            {
                passwordEdit.Text = currentPassword;
            }

            // Show current path
            var pathText = dialogView.FindViewById<TextView>(Resource.Id.text_current_path);
            string effectivePath = KeeShare.GetEffectiveFilePath(group);
            pathText.Text = !string.IsNullOrEmpty(effectivePath) ? effectivePath : GetString(Resource.String.not_set);

            var builder = new MaterialAlertDialogBuilder(this)
                .SetTitle(Resource.String.keeshare_edit_title)
                .SetView(dialogView)
                .SetPositiveButton(Android.Resource.String.Ok, (sender, args) =>
                {
                    // Get new values
                    int selectedTypeId = typeRadioGroup.CheckedRadioButtonId;
                    string newType;
                    if (selectedTypeId == Resource.Id.radio_import)
                        newType = "Import";
                    else if (selectedTypeId == Resource.Id.radio_synchronize)
                        newType = "Synchronize";
                    else
                        newType = "Export";

                    string newPassword = passwordEdit?.Text?.ToString();

                    // Update the group's KeeShare settings
                    KeeShare.UpdateKeeShareConfig(group, newType, null, newPassword);

                    // Save and update
                    var saveItem = new KeeShareItem(group, item.Database);
                    Save(saveItem);
                })
                .SetNegativeButton(Android.Resource.String.Cancel, (sender, args) => { });

            builder.Show();
        }

        private void Update()
        {
            _adapter.Update();
            _adapter.NotifyDataSetChanged();
            UpdateEmptyView();
        }

        private void UpdateEmptyView()
        {
            var emptyText = FindViewById<TextView>(Resource.Id.empty_text);
            var listView = FindViewById<ListView>(Android.Resource.Id.List);

            if (emptyText != null && listView != null)
            {
                if (_adapter.Count == 0)
                {
                    emptyText.Visibility = ViewStates.Visible;
                    listView.Visibility = ViewStates.Gone;
                }
                else
                {
                    emptyText.Visibility = ViewStates.Gone;
                    listView.Visibility = ViewStates.Visible;
                }
            }
        }

        private void Save(KeeShareItem item)
        {
            var saveTask = new SaveDb(App.Kp2a, App.Kp2a.FindDatabaseForElement(item.Group),
                new ActionInContextInstanceOnOperationFinished(ContextInstanceId, App.Kp2a,
                    (success, message, importantMessage, exception, context) => (context as ConfigureKeeShareActivity)?.Update()));

            BlockingOperationStarter pt = new BlockingOperationStarter(App.Kp2a, saveTask);
            pt.Run();
        }

        private void SaveGroup(PwGroup group)
        {
            var db = App.Kp2a.FindDatabaseForElement(group);
            if (db == null) return;
            SaveDatabase(db);
        }

        // Save using a specific database instance (useful for newly created groups
        // that FindDatabaseForElement might not recognize yet)
        private void SaveDatabase(Database db)
        {
            var saveTask = new SaveDb(App.Kp2a, db,
                new ActionInContextInstanceOnOperationFinished(ContextInstanceId, App.Kp2a,
                    (success, message, importantMessage, exception, context) =>
                    {
                        var activity = context as ConfigureKeeShareActivity;
                        if (activity != null)
                        {
                            // Ensure Elements collection is updated before UI refresh.
                            // SaveDb posts UpdateGlobals() asynchronously, but we need it
                            // to complete before Update() so new groups are visible.
                            db.UpdateGlobals();
                            activity.Update();
                            if (success)
                            {
                                App.Kp2a.ShowMessage(activity, activity.GetString(Resource.String.keeshare_added), MessageSeverity.Info);
                            }
                        }
                    }));

            BlockingOperationStarter pt = new BlockingOperationStarter(App.Kp2a, saveTask);
            pt.Run();
        }

        protected override void OnResume()
        {
            base.OnResume();
            _adapter?.Update();
            _adapter?.NotifyDataSetChanged();
            UpdateEmptyView();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.config_keeshare);

            _adapter = new KeeShareAdapter(this);
            var listView = FindViewById<ListView>(Android.Resource.Id.List);
            listView.Adapter = _adapter;

            SetSupportActionBar(FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.mytoolbar));

            // Set up FAB for adding new KeeShare configuration
            var fab = FindViewById<FloatingActionButton>(Resource.Id.fab_add_keeshare);
            if (fab != null)
            {
                fab.Click += (sender, args) => ShowAddKeeShareDialog();
            }

            UpdateEmptyView();

            if (savedInstanceState != null)
            {
                string uuidString = savedInstanceState.GetString(PendingConfigItemUuidKey);
                if (!string.IsNullOrEmpty(uuidString))
                {
                    try
                    {
                        var uuid = new PwUuid(Convert.FromBase64String(uuidString));
                        _pendingConfigItem = _adapter._displayedItems.FirstOrDefault(i => i.Group.Uuid.Equals(uuid));
                    }
                    catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
                    {
                        Log.Error("ConfigureKeeShareActivity", "Failed to reconstruct PwUuid from saved state", ex);
                        _pendingConfigItem = null;
                    }
                }

                string newConfigUuidString = savedInstanceState.GetString(PendingNewConfigGroupUuidKey);
                if (!string.IsNullOrEmpty(newConfigUuidString))
                {
                    try
                    {
                        var uuid = new PwUuid(Convert.FromBase64String(newConfigUuidString));
                        _pendingNewConfigGroup = App.Kp2a.CurrentDb?.KpDatabase?.RootGroup?.FindGroup(uuid, true);
                    }
                    catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
                    {
                        Log.Error("ConfigureKeeShareActivity", "Failed to reconstruct new config group UUID from saved state", ex);
                        _pendingNewConfigGroup = null;
                    }
                }

                _pendingNewConfigType = savedInstanceState.GetString("PendingNewConfigType");
                _pendingNewConfigPassword = savedInstanceState.GetString("PendingNewConfigPassword");

                // Restore deferred group creation flags
                _pendingCreateNewGroup = savedInstanceState.GetBoolean("PendingCreateNewGroup", false);
                _pendingNewGroupName = savedInstanceState.GetString("PendingNewGroupName");
            }
        }

        private void ShowAddKeeShareDialog()
        {
            var db = App.Kp2a.CurrentDb;
            if (db?.KpDatabase?.RootGroup == null)
            {
                App.Kp2a.ShowMessage(this, GetString(Resource.String.error_group_not_found), MessageSeverity.Error);
                return;
            }

            var inflater = LayoutInflater.From(this);
            var dialogView = inflater.Inflate(Resource.Layout.dialog_add_keeshare, null);

            // Set up group spinner
            var groupSpinner = dialogView.FindViewById<Spinner>(Resource.Id.spinner_group);
            var newGroupContainer = dialogView.FindViewById<LinearLayout>(Resource.Id.new_group_container);
            var newGroupNameEdit = dialogView.FindViewById<TextInputEditText>(Resource.Id.edit_new_group_name);

            // Get all groups (excluding ones that already have KeeShare configured)
            var existingKeeShareGroups = new HashSet<PwUuid>(
                KeeShare.GetKeeShareItems(db.KpDatabase).Select(i => i.Group.Uuid));

            var availableGroups = new List<PwGroup>();
            CollectGroups(db.KpDatabase.RootGroup, availableGroups, existingKeeShareGroups);

            var groupNames = new List<string> { GetString(Resource.String.keeshare_create_new_group) };
            groupNames.AddRange(availableGroups.Select(g => GetGroupPath(g)));

            var spinnerAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, groupNames);
            spinnerAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            groupSpinner.Adapter = spinnerAdapter;

            groupSpinner.ItemSelected += (sender, args) =>
            {
                if (args.Position == 0)
                {
                    // "Create new group" selected
                    newGroupContainer.Visibility = ViewStates.Visible;
                }
                else
                {
                    newGroupContainer.Visibility = ViewStates.Gone;
                }
            };

            // Set up file path and browse button
            _dialogFilePathEdit = dialogView.FindViewById<TextInputEditText>(Resource.Id.edit_filepath);
            var browseButton = dialogView.FindViewById<Button>(Resource.Id.btn_browse);
            browseButton.Click += (sender, args) =>
            {
                // Store the current dialog state
                var typeRadioGroup = dialogView.FindViewById<RadioGroup>(Resource.Id.radio_group_type);
                int selectedTypeId = typeRadioGroup.CheckedRadioButtonId;
                if (selectedTypeId == Resource.Id.radio_import)
                    _pendingNewConfigType = "Import";
                else if (selectedTypeId == Resource.Id.radio_synchronize)
                    _pendingNewConfigType = "Synchronize";
                else
                    _pendingNewConfigType = "Export";

                var passwordEdit = dialogView.FindViewById<TextInputEditText>(Resource.Id.edit_password);
                _pendingNewConfigPassword = passwordEdit?.Text?.ToString();

                // Store group selection info - defer actual group creation to OnActivityResult
                // to avoid issues with activity recreation and database state
                int groupPosition = groupSpinner.SelectedItemPosition;
                if (groupPosition == 0)
                {
                    // Will create new group in OnActivityResult
                    _pendingCreateNewGroup = true;
                    _pendingNewGroupName = newGroupNameEdit?.Text?.ToString();
                    if (string.IsNullOrWhiteSpace(_pendingNewGroupName))
                    {
                        _pendingNewGroupName = "KeeShare Import";
                    }
                    _pendingNewConfigGroup = null;
                }
                else
                {
                    // Use existing group
                    _pendingCreateNewGroup = false;
                    _pendingNewGroupName = null;
                    _pendingNewConfigGroup = availableGroups[groupPosition - 1];
                }

                // Launch direct file picker using Android Storage Access Framework
                // This avoids the complex FileSelectActivity flow that can launch PasswordActivity
                Kp2aLog.Log("ConfigureKeeShare: Browse button tapped, launching ACTION_OPEN_DOCUMENT");
                Intent intent = new Intent(Intent.ActionOpenDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType("*/*");
                string[] mimeTypes = { "application/octet-stream", "application/x-keepass2" };
                intent.PutExtra(Intent.ExtraMimeTypes, mimeTypes);
                StartActivityForResult(intent, ReqCodeSelectFileForNewConfig);
                Kp2aLog.Log($"ConfigureKeeShare: Started file picker with requestCode={ReqCodeSelectFileForNewConfig}");

                _addDialog?.Dismiss();
            };

            var builder = new MaterialAlertDialogBuilder(this)
                .SetTitle(Resource.String.keeshare_add_title)
                .SetView(dialogView)
                .SetPositiveButton(Android.Resource.String.Ok, (EventHandler<DialogClickEventArgs>)null)
                .SetNegativeButton(Android.Resource.String.Cancel, (sender, args) => { });

            _addDialog = builder.Create();
            _addDialog.Show();

            // Override positive button to validate before dismissing
            _addDialog.GetButton((int)DialogButtonType.Positive).Click += (sender, args) =>
            {
                var typeRadioGroup = dialogView.FindViewById<RadioGroup>(Resource.Id.radio_group_type);
                var filePathEdit = dialogView.FindViewById<TextInputEditText>(Resource.Id.edit_filepath);
                var passwordEdit = dialogView.FindViewById<TextInputEditText>(Resource.Id.edit_password);

                string filePath = filePathEdit?.Text?.ToString();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    App.Kp2a.ShowMessage(this, GetString(Resource.String.keeshare_filepath_required), MessageSeverity.Warning);
                    return;
                }

                int selectedTypeId = typeRadioGroup.CheckedRadioButtonId;
                string type;
                if (selectedTypeId == Resource.Id.radio_import)
                    type = "Import";
                else if (selectedTypeId == Resource.Id.radio_synchronize)
                    type = "Synchronize";
                else
                    type = "Export";

                string password = passwordEdit?.Text?.ToString();

                // Determine the group
                int groupPosition = groupSpinner.SelectedItemPosition;
                PwGroup targetGroup;
                if (groupPosition == 0)
                {
                    // Create new group
                    string newGroupName = newGroupNameEdit?.Text?.ToString();
                    if (string.IsNullOrWhiteSpace(newGroupName))
                    {
                        newGroupName = "KeeShare Import";
                    }
                    targetGroup = new PwGroup(true, true, newGroupName, PwIcon.Folder);
                    db.KpDatabase.RootGroup.AddGroup(targetGroup, true);
                }
                else
                {
                    targetGroup = availableGroups[groupPosition - 1];
                }

                // Enable KeeShare on the group
                KeeShare.EnableKeeShare(targetGroup, type, filePath, password);

                // Save and update
                SaveGroup(targetGroup);
                _addDialog.Dismiss();
            };
        }

        private void CollectGroups(PwGroup group, List<PwGroup> result, HashSet<PwUuid> excludeUuids)
        {
            if (group == null) return;

            if (!excludeUuids.Contains(group.Uuid))
            {
                result.Add(group);
            }

            foreach (var child in group.Groups)
            {
                CollectGroups(child, result, excludeUuids);
            }
        }

        private string GetGroupPath(PwGroup group)
        {
            if (group == null) return "";

            var parts = new List<string>();
            var current = group;
            while (current != null)
            {
                parts.Insert(0, current.Name);
                current = current.ParentGroup;
            }
            return string.Join(" / ", parts);
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            if (_pendingConfigItem != null)
            {
                outState.PutString(PendingConfigItemUuidKey, Convert.ToBase64String(_pendingConfigItem.Group.Uuid.UuidBytes));
            }
            if (_pendingNewConfigGroup != null)
            {
                outState.PutString(PendingNewConfigGroupUuidKey, Convert.ToBase64String(_pendingNewConfigGroup.Uuid.UuidBytes));
            }
            if (_pendingNewConfigType != null)
            {
                outState.PutString("PendingNewConfigType", _pendingNewConfigType);
            }
            if (_pendingNewConfigPassword != null)
            {
                outState.PutString("PendingNewConfigPassword", _pendingNewConfigPassword);
            }
            // Save flags for deferred group creation
            outState.PutBoolean("PendingCreateNewGroup", _pendingCreateNewGroup);
            if (_pendingNewGroupName != null)
            {
                outState.PutString("PendingNewGroupName", _pendingNewGroupName);
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
                        string serializedIoc = IOConnectionInfo.SerializeToString(ioc);
                        KeeShare.SetDeviceFilePath(_pendingConfigItem.Group, serializedIoc);
                        Save(_pendingConfigItem);
                    }
                }
                _pendingConfigItem = null;
            }
            else if (requestCode == ReqCodeSelectFileForNewConfig && (_pendingNewConfigGroup != null || _pendingCreateNewGroup))
            {
                Kp2aLog.Log($"ConfigureKeeShare: OnActivityResult for new config, resultCode={resultCode}, hasData={data?.Data != null}, createNewGroup={_pendingCreateNewGroup}");
                if (resultCode == Result.Ok && data?.Data != null)
                {
                    var db = App.Kp2a.CurrentDb;
                    if (db?.KpDatabase?.RootGroup == null)
                    {
                        Kp2aLog.Log("ConfigureKeeShare: Database not available in OnActivityResult");
                        App.Kp2a.ShowMessage(this, GetString(Resource.String.error_group_not_found), MessageSeverity.Error);
                        return;
                    }

                    // Handle direct file picker result (Android Storage Access Framework)
                    Android.Net.Uri uri = data.Data;
                    Kp2aLog.Log($"ConfigureKeeShare: Selected file URI: {uri}");

                    // Take persistent permission for this URI
                    try
                    {
                        ContentResolver.TakePersistableUriPermission(uri,
                            ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
                    }
                    catch (Exception ex)
                    {
                        Kp2aLog.Log($"ConfigureKeeShare: Could not take persistent permission: {ex.Message}");
                    }

                    // Create IOConnectionInfo from the URI
                    IOConnectionInfo ioc = IOConnectionInfo.FromPath(uri.ToString());
                    string serializedIoc = IOConnectionInfo.SerializeToString(ioc);

                    // Determine the target group - create new group if needed
                    PwGroup targetGroup;
                    if (_pendingCreateNewGroup)
                    {
                        // Create new group now (deferred from Browse button click)
                        string groupName = _pendingNewGroupName ?? "KeeShare Import";
                        targetGroup = new PwGroup(true, true, groupName, PwIcon.Folder);
                        db.KpDatabase.RootGroup.AddGroup(targetGroup, true);
                        Kp2aLog.Log($"ConfigureKeeShare: Created new group '{groupName}'");
                    }
                    else
                    {
                        targetGroup = _pendingNewConfigGroup;
                    }

                    // Enable KeeShare on the group with the selected file
                    // Store the serialized IOC as the device-specific path
                    KeeShare.EnableKeeShare(targetGroup, _pendingNewConfigType ?? "Import", null, _pendingNewConfigPassword);
                    KeeShare.SetDeviceFilePath(targetGroup, serializedIoc);

                    // Save using the database directly (don't use SaveGroup because
                    // FindDatabaseForElement may not recognize newly created groups)
                    SaveDatabase(db);
                    Update(); // Refresh the list to show the new KeeShare group
                }
                else
                {
                    Kp2aLog.Log($"ConfigureKeeShare: File selection cancelled or no data");
                }
                // Reset all pending state
                _pendingNewConfigGroup = null;
                _pendingNewConfigType = null;
                _pendingNewConfigPassword = null;
                _pendingCreateNewGroup = false;
                _pendingNewGroupName = null;
            }
        }
    }
}
