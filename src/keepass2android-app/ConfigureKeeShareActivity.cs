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
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Google.Android.Material.FloatingActionButton;
using keepass2android.KeeShare;
using KeePassLib;

namespace keepass2android
{
    /// <summary>
    /// Dashboard activity showing all groups configured for KeeShare synchronization.
    /// Similar to ConfigureChildDatabasesActivity but for KeeShare.
    /// </summary>
    [Activity(Label = "@string/keeshare_title", MainLauncher = false, 
        Theme = "@style/Kp2aTheme_BlueNoActionBar", 
        LaunchMode = LaunchMode.SingleInstance, 
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
    [IntentFilter(new[] { "kp2a.action.ConfigureKeeShareActivity" }, Categories = new[] { Intent.CategoryDefault })]
    public class ConfigureKeeShareActivity : LockCloseActivity
    {
        private KeeShareAdapter _adapter;
        private ListView _listView;
        
        public const int RequestCodeEditKeeShare = 100;

        /// <summary>
        /// Adapter for displaying KeeShare-enabled groups
        /// </summary>
        public class KeeShareAdapter : BaseAdapter
        {
            private readonly ConfigureKeeShareActivity _context;
            internal List<KeeShareGroupInfo> _keeShareGroups;

            public class KeeShareGroupInfo
            {
                public PwGroup Group { get; set; }
                public KeeShareSettings.Reference Reference { get; set; }
                public KeeShareMode Mode { get; set; }
                public string LastSyncStatus { get; set; }
            }

            public KeeShareAdapter(ConfigureKeeShareActivity context)
            {
                _context = context;
                Update();
            }

            public void Update()
            {
                _keeShareGroups = new List<KeeShareGroupInfo>();
                
                if (App.Kp2a.CurrentDb?.Root == null) return;
                
                var allGroups = App.Kp2a.CurrentDb.Root.GetGroups(true);
                allGroups.Add(App.Kp2a.CurrentDb.Root);
                
                foreach (var group in allGroups)
                {
                    var reference = KeeShareSettings.GetReference(group);
                    if (reference != null)
                    {
                        var mode = KeeShareMode.Import;
                        if (reference.IsExporting)
                            mode = reference.IsImporting ? KeeShareMode.Synchronize : KeeShareMode.Export;
                        
                        _keeShareGroups.Add(new KeeShareGroupInfo
                        {
                            Group = group,
                            Reference = reference,
                            Mode = mode,
                            LastSyncStatus = GetLastSyncStatus(reference)
                        });
                    }
                }
                
                _keeShareGroups = _keeShareGroups.OrderBy(g => g.Group.Name).ToList();
            }

            private string GetLastSyncStatus(KeeShareSettings.Reference reference)
            {
                // TODO: Track last sync time in settings
                return reference.Path != null ? "Configured" : "Not configured";
            }

            public override int Count => _keeShareGroups.Count;

            public override Java.Lang.Object GetItem(int position)
            {
                return position;
            }

            public override long GetItemId(int position)
            {
                return position;
            }

            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                var inflater = _context.LayoutInflater;
                
                View view = convertView ?? inflater.Inflate(Resource.Layout.keeshare_config_row, parent, false);
                
                var info = _keeShareGroups[position];
                
                // Group name
                var titleView = view.FindViewById<TextView>(Resource.Id.keeshare_group_name);
                if (titleView != null)
                    titleView.Text = info.Group.Name;
                
                // Mode
                var modeView = view.FindViewById<TextView>(Resource.Id.keeshare_mode);
                if (modeView != null)
                {
                    string modeText = info.Mode switch
                    {
                        KeeShareMode.Import => _context.GetString(Resource.String.keeshare_mode_import),
                        KeeShareMode.Export => _context.GetString(Resource.String.keeshare_mode_export),
                        KeeShareMode.Synchronize => _context.GetString(Resource.String.keeshare_mode_sync),
                        _ => "Unknown"
                    };
                    modeView.Text = modeText;
                }
                
                // Path
                var pathView = view.FindViewById<TextView>(Resource.Id.keeshare_path);
                if (pathView != null)
                    pathView.Text = info.Reference?.Path ?? "No path configured";
                
                // Status
                var statusView = view.FindViewById<TextView>(Resource.Id.keeshare_status);
                if (statusView != null)
                    statusView.Text = info.LastSyncStatus;
                
                // Icon
                var iconView = view.FindViewById<ImageView>(Resource.Id.keeshare_icon);
                if (iconView != null)
                {
                    var db = App.Kp2a.CurrentDb;
                    db.DrawableFactory.AssignDrawableTo(iconView, _context, db.KpDatabase, 
                        info.Group.IconId, info.Group.CustomIconUuid, false);
                }
                
                // Edit button
                var editButton = view.FindViewById<Button>(Resource.Id.keeshare_edit);
                if (editButton != null)
                {
                    editButton.Tag = position;
                    editButton.Click -= OnEditClick;
                    editButton.Click += OnEditClick;
                }
                
                // Sync now button  
                var syncButton = view.FindViewById<Button>(Resource.Id.keeshare_sync_now);
                if (syncButton != null)
                {
                    syncButton.Tag = position;
                    syncButton.Click -= OnSyncClick;
                    syncButton.Click += OnSyncClick;
                }
                
                // Remove button
                var removeButton = view.FindViewById<Button>(Resource.Id.keeshare_remove);
                if (removeButton != null)
                {
                    removeButton.Tag = position;
                    removeButton.Click -= OnRemoveClick;
                    removeButton.Click += OnRemoveClick;
                }
                
                view.Tag = position;
                
                return view;
            }

            private void OnEditClick(object sender, EventArgs e)
            {
                var button = (View)sender;
                int pos = (int)button.Tag;
                _context.OnEditKeeShare(_keeShareGroups[pos]);
            }

            private void OnSyncClick(object sender, EventArgs e)
            {
                var button = (View)sender;
                int pos = (int)button.Tag;
                _context.OnSyncNow(_keeShareGroups[pos]);
            }

            private void OnRemoveClick(object sender, EventArgs e)
            {
                var button = (View)sender;
                int pos = (int)button.Tag;
                _context.OnRemoveKeeShare(_keeShareGroups[pos]);
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            SetContentView(Resource.Layout.config_keeshare);
            
            _adapter = new KeeShareAdapter(this);
            _listView = FindViewById<ListView>(Android.Resource.Id.List);
            _listView.Adapter = _adapter;
            
            // Setup toolbar
            var toolbar = FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.mytoolbar);
            if (toolbar != null)
                SetSupportActionBar(toolbar);
            
            // FAB for adding new KeeShare configuration
            var fab = FindViewById<FloatingActionButton>(Resource.Id.fab_add_keeshare);
            if (fab != null)
            {
                fab.Click += (s, e) => ShowAddKeeShareDialog();
            }
            
            // Empty state message
            UpdateEmptyState();
        }

        protected override void OnResume()
        {
            base.OnResume();
            _adapter?.Update();
            _adapter?.NotifyDataSetChanged();
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            var emptyView = FindViewById<TextView>(Resource.Id.keeshare_empty_text);
            if (emptyView != null)
            {
                emptyView.Visibility = (_adapter?.Count ?? 0) == 0 ? ViewStates.Visible : ViewStates.Gone;
            }
            if (_listView != null)
            {
                _listView.Visibility = (_adapter?.Count ?? 0) > 0 ? ViewStates.Visible : ViewStates.Gone;
            }
        }

        private void ShowAddKeeShareDialog()
        {
            // Show dialog to select a group to configure for KeeShare
            var groups = new List<PwGroup>();
            var allGroups = App.Kp2a.CurrentDb.Root.GetGroups(true);
            
            // Filter out groups that already have KeeShare configured
            var existingUuids = _adapter._keeShareGroups.Select(g => g.Group.Uuid).ToHashSet();
            
            foreach (var group in allGroups)
            {
                if (!existingUuids.Contains(group.Uuid) && group.ParentGroup != null)
                {
                    groups.Add(group);
                }
            }
            
            if (groups.Count == 0)
            {
                Toast.MakeText(this, Resource.String.keeshare_no_groups_available, ToastLength.Short).Show();
                return;
            }
            
            var groupNames = groups.Select(g => g.Name).ToArray();
            
            new Google.Android.Material.Dialog.MaterialAlertDialogBuilder(this)
                .SetTitle(Resource.String.keeshare_select_group)
                .SetItems(groupNames, (s, args) =>
                {
                    var selectedGroup = groups[args.Which];
                    LaunchEditKeeShare(selectedGroup, null);
                })
                .Show();
        }

        private void OnEditKeeShare(KeeShareAdapter.KeeShareGroupInfo info)
        {
            LaunchEditKeeShare(info.Group, info.Reference);
        }

        private void LaunchEditKeeShare(PwGroup group, KeeShareSettings.Reference existingRef)
        {
            var intent = new Intent(this, typeof(EditKeeShareActivity));
            intent.PutExtra(EditKeeShareActivity.ExtraGroupUuid, group.Uuid.ToHexString());
            if (existingRef != null)
            {
                intent.PutExtra(EditKeeShareActivity.ExtraPath, existingRef.Path);
                intent.PutExtra(EditKeeShareActivity.ExtraPassword, existingRef.Password);
                intent.PutExtra(EditKeeShareActivity.ExtraIsImporting, existingRef.IsImporting);
                intent.PutExtra(EditKeeShareActivity.ExtraIsExporting, existingRef.IsExporting);
            }
            StartActivityForResult(intent, RequestCodeEditKeeShare);
        }

        private void OnSyncNow(KeeShareAdapter.KeeShareGroupInfo info)
        {
            // Trigger sync for this specific group
            Toast.MakeText(this, $"Syncing {info.Group.Name}...", ToastLength.Short).Show();
            
            // Run sync in background
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var results = KeeShareImporter.CheckAndImport(App.Kp2a.CurrentDb, App.Kp2a, null);
                    
                    RunOnUiThread(() =>
                    {
                        var groupResult = results.FirstOrDefault(r => r.SharePath == info.Reference?.Path);
                        if (groupResult != null)
                        {
                            string message = groupResult.IsSuccess
                                ? $"Synced {groupResult.EntriesImported} entries"
                                : $"Sync failed: {groupResult.Message}";
                            Toast.MakeText(this, message, ToastLength.Long).Show();
                        }
                        else
                        {
                            Toast.MakeText(this, "Sync completed", ToastLength.Short).Show();
                        }
                        
                        _adapter.Update();
                        _adapter.NotifyDataSetChanged();
                    });
                }
                catch (Exception ex)
                {
                    RunOnUiThread(() =>
                    {
                        Toast.MakeText(this, $"Sync error: {ex.Message}", ToastLength.Long).Show();
                    });
                }
            });
        }

        private void OnRemoveKeeShare(KeeShareAdapter.KeeShareGroupInfo info)
        {
            new Google.Android.Material.Dialog.MaterialAlertDialogBuilder(this)
                .SetTitle(Resource.String.keeshare_remove_title)
                .SetMessage(string.Format(GetString(Resource.String.keeshare_remove_message), info.Group.Name))
                .SetPositiveButton(Android.Resource.String.Ok, (s, args) =>
                {
                    // Remove KeeShare settings from group
                    KeeShareSettings.RemoveReference(info.Group);
                    
                    // Save database
                    var saveTask = new database.edit.SaveDb(App.Kp2a, App.Kp2a.CurrentDb, 
                        new ActionOnFinish(this, (success, message, activity) =>
                        {
                            if (activity is ConfigureKeeShareActivity configActivity)
                            {
                                configActivity._adapter.Update();
                                configActivity._adapter.NotifyDataSetChanged();
                                configActivity.UpdateEmptyState();
                            }
                        }));
                    new BlockingOperationStarter(App.Kp2a, saveTask).Run();
                })
                .SetNegativeButton(Android.Resource.String.Cancel, (s, args) => { })
                .Show();
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            
            if (requestCode == RequestCodeEditKeeShare && resultCode == Result.Ok)
            {
                _adapter?.Update();
                _adapter?.NotifyDataSetChanged();
                UpdateEmptyState();
            }
        }
    }
}
