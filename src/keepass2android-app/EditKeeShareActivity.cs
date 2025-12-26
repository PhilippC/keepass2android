// This file is part of Keepass2Android, Copyright 2025.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using keepass2android.KeeShare;
using KeePassLib;
using KeePassLib.Utility;

namespace keepass2android
{
    /// <summary>
    /// Activity for configuring KeeShare settings on a specific group.
    /// Allows setting mode (Import/Export/Sync), file path, and password.
    /// </summary>
    [Activity(Label = "@string/keeshare_edit_title", MainLauncher = false,
        Theme = "@style/Kp2aTheme_BlueNoActionBar",
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
    public class EditKeeShareActivity : LockCloseActivity
    {
        public const string ExtraGroupUuid = "group_uuid";
        public const string ExtraPath = "share_path";
        public const string ExtraPassword = "share_password";
        public const string ExtraIsImporting = "is_importing";
        public const string ExtraIsExporting = "is_exporting";
        
        private const int RequestCodeSelectFile = 200;
        
        private PwGroup _group;
        private Spinner _modeSpinner;
        private EditText _pathEditText;
        private EditText _passwordEditText;
        private Button _browseButton;
        private Button _saveButton;
        private Button _cancelButton;
        private TextView _groupNameText;
        private TextView _fingerprintText;
        private View _fingerprintSection;
        
        private KeeShareMode _selectedMode = KeeShareMode.Import;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            SetContentView(Resource.Layout.edit_keeshare);
            
            // Setup toolbar
            var toolbar = FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.mytoolbar);
            if (toolbar != null)
                SetSupportActionBar(toolbar);
            
            // Get the group
            string groupUuidHex = Intent.GetStringExtra(ExtraGroupUuid);
            if (string.IsNullOrEmpty(groupUuidHex))
            {
                Toast.MakeText(this, "No group specified", ToastLength.Short).Show();
                Finish();
                return;
            }
            
            var groupUuid = new PwUuid(MemUtil.HexStringToByteArray(groupUuidHex));
            _group = App.Kp2a.CurrentDb.KpDatabase.RootGroup.FindGroup(groupUuid, true);
            
            if (_group == null)
            {
                Toast.MakeText(this, "Group not found", ToastLength.Short).Show();
                Finish();
                return;
            }
            
            InitializeViews();
            LoadExistingSettings();
        }

        private void InitializeViews()
        {
            _groupNameText = FindViewById<TextView>(Resource.Id.keeshare_group_name);
            _modeSpinner = FindViewById<Spinner>(Resource.Id.keeshare_mode_spinner);
            _pathEditText = FindViewById<EditText>(Resource.Id.keeshare_path_edit);
            _passwordEditText = FindViewById<EditText>(Resource.Id.keeshare_password_edit);
            _browseButton = FindViewById<Button>(Resource.Id.keeshare_browse_button);
            _saveButton = FindViewById<Button>(Resource.Id.keeshare_save_button);
            _cancelButton = FindViewById<Button>(Resource.Id.keeshare_cancel_button);
            _fingerprintSection = FindViewById<View>(Resource.Id.keeshare_fingerprint_section);
            _fingerprintText = FindViewById<TextView>(Resource.Id.keeshare_fingerprint_text);
            
            // Set group name
            if (_groupNameText != null)
                _groupNameText.Text = _group.Name;
            
            // Setup mode spinner
            if (_modeSpinner != null)
            {
                var modes = new string[]
                {
                    GetString(Resource.String.keeshare_mode_import),
                    GetString(Resource.String.keeshare_mode_export),
                    GetString(Resource.String.keeshare_mode_sync)
                };
                
                var adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, modes);
                adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
                _modeSpinner.Adapter = adapter;
                
                _modeSpinner.ItemSelected += (s, e) =>
                {
                    _selectedMode = (KeeShareMode)e.Position;
                    UpdateFingerprintVisibility();
                };
            }
            
            // Browse button
            if (_browseButton != null)
            {
                _browseButton.Click += (s, e) => StartFilePicker();
            }
            
            // Save button
            if (_saveButton != null)
            {
                _saveButton.Click += (s, e) => SaveSettings();
            }
            
            // Cancel button
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) =>
                {
                    SetResult(Result.Canceled);
                    Finish();
                };
            }
            
            // Initially hide fingerprint section (only shown for signed containers)
            if (_fingerprintSection != null)
                _fingerprintSection.Visibility = ViewStates.Gone;
        }

        private void LoadExistingSettings()
        {
            string path = Intent.GetStringExtra(ExtraPath);
            string password = Intent.GetStringExtra(ExtraPassword);
            bool isImporting = Intent.GetBooleanExtra(ExtraIsImporting, true);
            bool isExporting = Intent.GetBooleanExtra(ExtraIsExporting, false);
            
            if (!string.IsNullOrEmpty(path) && _pathEditText != null)
                _pathEditText.Text = path;
            
            if (!string.IsNullOrEmpty(password) && _passwordEditText != null)
                _passwordEditText.Text = password;
            
            // Determine mode
            if (isImporting && isExporting)
                _selectedMode = KeeShareMode.Synchronize;
            else if (isExporting)
                _selectedMode = KeeShareMode.Export;
            else
                _selectedMode = KeeShareMode.Import;
            
            if (_modeSpinner != null)
                _modeSpinner.SetSelection((int)_selectedMode);
        }

        private void UpdateFingerprintVisibility()
        {
            // Fingerprint section is shown when path ends with .share (signed container)
            if (_fingerprintSection == null) return;
            
            string path = _pathEditText?.Text ?? "";
            bool isSignedContainer = path.EndsWith(".share", StringComparison.OrdinalIgnoreCase);
            
            _fingerprintSection.Visibility = isSignedContainer ? ViewStates.Visible : ViewStates.Gone;
        }

        private void StartFilePicker()
        {
            // Use Android file picker
            Intent intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");
            intent.PutExtra(Intent.ExtraMimeTypes, new string[] { 
                "application/octet-stream", 
                "application/x-keepass2",
                "*/*" 
            });
            
            try
            {
                StartActivityForResult(Intent.CreateChooser(intent, GetString(Resource.String.keeshare_select_file)), RequestCodeSelectFile);
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, $"Cannot open file picker: {ex.Message}", ToastLength.Long).Show();
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            
            if (requestCode == RequestCodeSelectFile && resultCode == Result.Ok && data?.Data != null)
            {
                var uri = data.Data;
                
                // Take persistent permission
                try
                {
                    ContentResolver.TakePersistableUriPermission(uri, 
                        ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
                }
                catch { }
                
                if (_pathEditText != null)
                    _pathEditText.Text = uri.ToString();
                
                UpdateFingerprintVisibility();
            }
        }

        private void SaveSettings()
        {
            string path = _pathEditText?.Text?.Trim();
            string password = _passwordEditText?.Text;
            
            if (string.IsNullOrEmpty(path))
            {
                Toast.MakeText(this, Resource.String.keeshare_path_required, ToastLength.Short).Show();
                return;
            }
            
            // Create KeeShare reference
            var reference = new KeeShareSettings.Reference
            {
                Path = path,
                Password = password,
                IsImporting = _selectedMode == KeeShareMode.Import || _selectedMode == KeeShareMode.Synchronize,
                IsExporting = _selectedMode == KeeShareMode.Export || _selectedMode == KeeShareMode.Synchronize
            };
            
            // Save to group
            KeeShareSettings.SetReference(_group, reference);
            
            // Save database
            var saveTask = new database.edit.SaveDb(App.Kp2a, App.Kp2a.CurrentDb,
                new ActionOnFinish(this, (success, message, activity) =>
                {
                    if (success)
                    {
                        Toast.MakeText(activity, Resource.String.keeshare_saved, ToastLength.Short).Show();
                        activity.SetResult(Result.Ok);
                        activity.Finish();
                    }
                    else
                    {
                        Toast.MakeText(activity, $"Save failed: {message}", ToastLength.Long).Show();
                    }
                }));
            
            new BlockingOperationStarter(App.Kp2a, saveTask).Run();
        }
    }
}
