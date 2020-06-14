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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;
using Android.Preferences;
using KeePassLib.Interfaces;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using keepass2android.Io;
using keepass2android.database.edit;
using keepass2android.view;
using Android.Graphics.Drawables;
using Android.Provider;
using Android.Support.V4.View;
using Android.Views.Autofill;
using CursorAdapter = Android.Support.V4.Widget.CursorAdapter;
using Object = Java.Lang.Object;

namespace keepass2android
{

    public abstract class GroupBaseActivity : LockCloseActivity
    {
        public const String KeyEntry = "entry";
        public const String KeyMode = "mode";

        public const int RequestCodeActivateRealSearch = 12366;

        static readonly Dictionary<int /*resource id*/, int /*prio*/> bottomBarElementsPriority = new Dictionary<int, int>()
        {
            { Resource.Id.cancel_insert_element, 20 },
            { Resource.Id.insert_element, 20 },
            //only use the same id if elements can be shown simultaneously!
            { Resource.Id.dbreadonly_infotext, 14 },
            { Resource.Id.child_db_infotext, 13 },
            { Resource.Id.fingerprint_infotext, 12 },
            { Resource.Id.autofill_infotext, 11 },
            { Resource.Id.notification_info_android8_infotext, 10 },
            { Resource.Id.infotext, 9 },
            { Resource.Id.select_other_entry, 20},
            { Resource.Id.add_url_entry, 20},
        };

        private readonly HashSet<int /*resource id*/> showableBottomBarElements = new HashSet<int>();

        private ActivityDesign _design;

        public virtual void LaunchActivityForEntry(PwEntry pwEntry, int pos)
        {
            EntryActivity.Launch(this, pwEntry, pos, AppTask);
        }

        protected GroupBaseActivity()
        {
            _design = new ActivityDesign(this);
        }

        protected GroupBaseActivity(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {

        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            AppTask.ToBundle(outState);
        }

        public virtual void SetupNormalButtons()
        {
            SetNormalButtonVisibility(AddGroupEnabled, AddEntryEnabled);
        }


        protected virtual bool AddGroupEnabled
        {
            get { return false; }
        }
        protected virtual bool AddEntryEnabled
        {
            get { return false; }
        }

        public void SetNormalButtonVisibility(bool showAddGroup, bool showAddEntry)
        {
            if (FindViewById(Resource.Id.fabCancelAddNew) != null)
            {
                FindViewById(Resource.Id.fabCancelAddNew).Visibility = ViewStates.Gone;
                FindViewById(Resource.Id.fabAddNewGroup).Visibility = ViewStates.Gone;
                FindViewById(Resource.Id.fabAddNewEntry).Visibility = ViewStates.Gone;

                FindViewById(Resource.Id.fabAddNew).Visibility = (showAddGroup || showAddEntry) ? ViewStates.Visible : ViewStates.Gone;
            }

            UpdateBottomBarElementVisibility(Resource.Id.insert_element, false);
            UpdateBottomBarElementVisibility(Resource.Id.cancel_insert_element, false);

        }

        void UpdateBottomBarVisibility()
        {
            var bottomBar = FindViewById<RelativeLayout>(Resource.Id.bottom_bar);
            //check for null because the "empty" layouts may not have all views
            int highestPrio = -1;
            HashSet<int> highestPrioElements = new HashSet<int>();
            if (bottomBar != null)
            {
                for (int i = 0; i < bottomBar.ChildCount; i++)
                {
                    int id = bottomBar.GetChildAt(i).Id;
                    if (!showableBottomBarElements.Contains(id))
                        continue;
                    int myPrio = bottomBarElementsPriority[id];

                    if (!highestPrioElements.Any() || highestPrio < myPrio)
                    {
                        highestPrioElements.Clear();
                        highestPrio = myPrio;
                    }
                    if (highestPrio == myPrio)
                    {
                        highestPrioElements.Add(id);
                    }
                }

                bottomBar.Visibility = highestPrioElements.Any() ? ViewStates.Visible : ViewStates.Gone;

                for (int i = 0; i < bottomBar.ChildCount; i++)
                {
                    int id = bottomBar.GetChildAt(i).Id;
                    bottomBar.GetChildAt(i).Visibility = highestPrioElements.Contains(id) ? ViewStates.Visible : ViewStates.Gone;
                }

                if (FindViewById(Resource.Id.divider2) != null)
                    FindViewById(Resource.Id.divider2).Visibility = highestPrioElements.Any() ? ViewStates.Visible : ViewStates.Gone;
            }
        }


        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (AppTask.TryGetFromActivityResult(data, ref AppTask))
            {
                //make sure the app task is passed to the calling activity
                AppTask.SetActivityResult(this, KeePass.ExitNormal);

            }

            if (RequestCodeActivateRealSearch == requestCode)
            {
                hasCalledOtherActivity = true;
                SetSearchItemVisibility();
                ActivateSearchView();
            }

            if ((GroupEditActivity.RequestCodeGroupEdit == requestCode) && (resultCode == Result.Ok))
            {
                String groupName = data.Extras.GetString(GroupEditActivity.KeyName);
                int groupIconId = data.Extras.GetInt(GroupEditActivity.KeyIconId);
                PwUuid groupCustomIconId =
                    new PwUuid(MemUtil.HexStringToByteArray(data.Extras.GetString(GroupEditActivity.KeyCustomIconId)));
                String strGroupUuid = data.Extras.GetString(GroupEditActivity.KeyGroupUuid);
                GroupBaseActivity act = this;
                Handler handler = new Handler();
                RunnableOnFinish task;
                if (strGroupUuid == null)
                {
                    task = AddGroup.GetInstance(this, App.Kp2a, groupName, groupIconId, groupCustomIconId, Group, new RefreshTask(handler, this), false);
                }
                else
                {
                    PwUuid groupUuid = new PwUuid(MemUtil.HexStringToByteArray(strGroupUuid));
                    task = new EditGroup(this, App.Kp2a, groupName, (PwIcon)groupIconId, groupCustomIconId, App.Kp2a.FindGroup(groupUuid),
                                         new RefreshTask(handler, this));
                }
                ProgressTask pt = new ProgressTask(App.Kp2a, act, task);
                pt.Run();
            }

            if (resultCode == KeePass.ExitCloseAfterTaskComplete)
            {
                AppTask.SetActivityResult(this, KeePass.ExitCloseAfterTaskComplete);
                Finish();
            }

            if (resultCode == KeePass.ExitLoadAnotherDb)
            {
                AppTask.SetActivityResult(this, KeePass.ExitLoadAnotherDb);
                Finish();
            }

            if (resultCode == KeePass.ExitReloadDb)
            {
                AppTask.SetActivityResult(this, KeePass.ExitReloadDb);
                Finish();
            }

        }

        private ISharedPreferences _prefs;

        protected PwGroup Group;

        internal AppTask AppTask;

        private String strCachedGroupUuid = null;
        private IMenuItem _offlineItem;
        private IMenuItem _onlineItem;
        private IMenuItem _syncItem;
        private Android.Support.V7.Widget.SearchView searchView;


        public String UuidGroup
        {
            get
            {
                if (strCachedGroupUuid == null)
                {
                    strCachedGroupUuid = MemUtil.ByteArrayToHexString(Group.Uuid.UuidBytes);
                }
                return strCachedGroupUuid;
            }
        }

        private bool hasCalledOtherActivity = false;
        private IMenuItem searchItem;
        private IMenuItem searchItemDummy;

        protected override void OnResume()
        {
            base.OnResume();
            _design.ReapplyTheme();
            AppTask.StartInGroupActivity(this);
            AppTask.SetupGroupBaseActivityButtons(this);

            UpdateDbReadOnlyInfo();
            UpdateChildDbInfo();
            UpdateFingerprintInfo();
            UpdateAutofillInfo();
            UpdateAndroid8NotificationInfo();
            UpdateInfotexts();

            RefreshIfDirty();

            SetSearchItemVisibility();
        }

        private void UpdateInfotexts()
        {

            string lastInfoText;
            if (IsTimeForInfotext(out lastInfoText) && (FindViewById<TextView>(Resource.Id.info_head) != null))
            {

                FingerprintUnlockMode um;
                Enum.TryParse(_prefs.GetString(Database.GetFingerprintModePrefKey(App.Kp2a.CurrentDb.Ioc), ""), out um);
                bool isFingerprintEnabled = (um == FingerprintUnlockMode.FullUnlock);

                string masterKeyKey = "MasterKey" + isFingerprintEnabled;
                string emergencyKey = "Emergency";
                string backupKey = "Backup";

                List<string> applicableInfoTextKeys = new List<string> { masterKeyKey };

                if (App.Kp2a.GetFileStorage(App.Kp2a.CurrentDb.Ioc).UserShouldBackup)
                {
                    applicableInfoTextKeys.Add(backupKey);
                }
                if (App.Kp2a.CurrentDb.EntriesById.Count > 15)
                {
                    applicableInfoTextKeys.Add(emergencyKey);
                }

                List<string> enabledInfoTextKeys = new List<string>();
                foreach (string key in applicableInfoTextKeys)
                {
                    if (!InfoTextWasDisabled(key))
                        enabledInfoTextKeys.Add(key);
                }

                if (enabledInfoTextKeys.Any())
                {
                    string infoTextKey = "", infoHead = "", infoMain = "", infoNote = "";

                    if (enabledInfoTextKeys.Count > 1)
                    {
                        foreach (string key in enabledInfoTextKeys)
                            if (key == lastInfoText)
                            {
                                enabledInfoTextKeys.Remove(key);
                                break;
                            }
                        infoTextKey = enabledInfoTextKeys[new Random().Next(enabledInfoTextKeys.Count)];
                    }

                    if (infoTextKey == masterKeyKey)
                    {
                        infoHead = GetString(Resource.String.masterkey_infotext_head);
                        infoMain = GetString(Resource.String.masterkey_infotext_main);
                        if (isFingerprintEnabled)
                            infoNote = GetString(Resource.String.masterkey_infotext_fingerprint_note);
                    }
                    else if (infoTextKey == emergencyKey)
                    {
                        infoHead = GetString(Resource.String.emergency_infotext_head);
                        infoMain = GetString(Resource.String.emergency_infotext_main);
                    }
                    else if (infoTextKey == backupKey)
                    {
                        infoHead = GetString(Resource.String.backup_infotext_head);
                        infoMain = GetString(Resource.String.backup_infotext_main);
                        infoNote = GetString(Resource.String.backup_infotext_note, GetString(Resource.String.menu_app_settings), GetString(Resource.String.menu_db_settings), GetString(Resource.String.export_prefs));
                    }



                    FindViewById<TextView>(Resource.Id.info_head).Text = infoHead;
                    FindViewById<TextView>(Resource.Id.info_main).Text = infoMain;
                    var additionalInfoText = FindViewById<TextView>(Resource.Id.info_additional);
                    additionalInfoText.Text = infoNote;
                    additionalInfoText.Visibility = string.IsNullOrEmpty(infoNote) ? ViewStates.Gone : ViewStates.Visible;

                    if (infoTextKey != "")
                    {

                        RegisterInfoTextDisplay(infoTextKey);
                        FindViewById(Resource.Id.info_ok).Click += (sender, args) =>
                        {
                            UpdateBottomBarElementVisibility(Resource.Id.infotext, false);
                        };
                        FindViewById(Resource.Id.info_dont_show_again).Click += (sender, args) =>
                        {
                            UpdateBottomBarElementVisibility(Resource.Id.infotext, false);
                            DisableInfoTextDisplay(infoTextKey);
                        };

                        UpdateBottomBarElementVisibility(Resource.Id.infotext, true);
                    }

                }



            }
        }

        protected override void OnStop()
        {
            base.OnStop();
            hasCalledOtherActivity = false;
        }

        private void UpdateAndroid8NotificationInfo(bool hideForever = false)
        {
            const string prefsKey = "DidShowAndroid8NotificationInfo";
            
            bool canShowNotificationInfo = (Build.VERSION.SdkInt >= BuildVersionCodes.O) && (!_prefs.GetBoolean(prefsKey, false));
            if ((canShowNotificationInfo) && hideForever)
            {
                _prefs.Edit().PutBoolean(prefsKey, true).Commit();
                canShowNotificationInfo = false;
            }
            if (canShowNotificationInfo)
            {
                RegisterInfoTextDisplay("Android8Notification"); //this ensures that we don't show the general info texts too soon
            }
            UpdateBottomBarElementVisibility(Resource.Id.notification_info_android8_infotext, canShowNotificationInfo);
            

        }

        public override bool OnSearchRequested()
        {
            Intent i = new Intent(this, typeof(SearchActivity));
            AppTask.ToIntent(i);
            StartActivityForResult(i, 0);
            return true;
        }

        public void RefreshIfDirty()
        {
            if (App.Kp2a.DirtyGroups.Contains(Group))
            {
                App.Kp2a.DirtyGroups.Remove(Group);
                ListAdapter.NotifyDataSetChanged();

            }
        }

        public BaseAdapter ListAdapter
        {
            get { return (BaseAdapter)FragmentManager.FindFragmentById<GroupListFragment>(Resource.Id.list_fragment).ListAdapter; }
        }

        public virtual bool IsSearchResult
        {
            get { return false; }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            _design.ApplyTheme();
            base.OnCreate(savedInstanceState);

            Android.Util.Log.Debug("KP2A", "Creating GBA");

            AppTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);

            // Likely the app has been killed exit the activity 
            if (App.Kp2a.CurrentDb== null)
            {
                Finish();
                return;
            }

            _prefs = PreferenceManager.GetDefaultSharedPreferences(this);


            SetContentView(ContentResourceId);
            

            if (FindViewById(Resource.Id.enable_autofill) != null)
            {
                FindViewById(Resource.Id.enable_autofill).Click += (sender, args) =>
                {
                    var intent = new Intent(Settings.ActionRequestSetAutofillService);
                    intent.SetData(Android.Net.Uri.Parse("package:" + PackageName));
		            try
		            {
		                StartActivity(intent);
		            }
		            catch (ActivityNotFoundException e)
		            {
                        //this exception was reported by many Huawei users
		                Kp2aLog.LogUnexpectedError(e);
		                new AlertDialog.Builder(this)
		                    .SetTitle(Resource.String.autofill_enable)
		                    .SetMessage(Resource.String.autofill_enable_failed)
		                    .SetPositiveButton(Resource.String.ok, (o, eventArgs) => { })
		                    .Show();
		                const string autofillservicewasenabled = "AutofillServiceWasEnabled";
		                _prefs.Edit().PutBoolean(autofillservicewasenabled, true).Commit();
		                UpdateBottomBarElementVisibility(Resource.Id.autofill_infotext, false);
                    }
                };
            }

            if (FindViewById(Resource.Id.info_dont_show_fingerprint_again) != null)
            {
                FindViewById(Resource.Id.info_dont_show_fingerprint_again).Click += (sender, args) =>
                {
                    _prefs.Edit().PutBoolean(fingerprintinfohidden_prefskey, true).Commit();
                    UpdateFingerprintInfo();
                };
            }


            if (FindViewById(Resource.Id.hide_fingerprint_info) != null)
            {
                FindViewById(Resource.Id.hide_fingerprint_info).Click += (sender, args) =>
                {
                    _prefs.Edit().PutBoolean(fingerprintinfohidden_prefskey + App.Kp2a.CurrentDb.CurrentFingerprintPrefKey, true).Commit(); 
                    UpdateFingerprintInfo();
                };
            }

            if (FindViewById(Resource.Id.enable_fingerprint) != null)
            {
                FindViewById(Resource.Id.enable_fingerprint).Click += (sender, args) =>
                {
                    StartActivity(typeof(BiometricSetupActivity));
                };
            }

            if (FindViewById(Resource.Id.info_dont_show_autofill_again) != null)
            {
                FindViewById(Resource.Id.info_dont_show_autofill_again).Click += (sender, args) =>
                {
                    _prefs.Edit().PutBoolean(autofillservicewasenabled_prefskey, true).Commit();
                    UpdateAutofillInfo();
                };
            }

            if (FindViewById(Resource.Id.configure_child_db) != null)
            {
                FindViewById(Resource.Id.configure_child_db).Click += (sender, args) =>
                {
                    StartActivity(typeof(ConfigureChildDatabasesActivity));
                };
            }

            if (FindViewById(Resource.Id.info_dont_show_child_db_again) != null)
            {
                FindViewById(Resource.Id.info_dont_show_child_db_again).Click += (sender, args) =>
                {
                    _prefs.Edit().PutBoolean(childdb_ignore_prefskey + App.Kp2a.CurrentDb.CurrentFingerprintPrefKey, true).Commit();
                    UpdateChildDbInfo();
                };
            }


            if (FindViewById(Resource.Id.info_dont_show_dbreadonly_again) != null)
            {
                FindViewById(Resource.Id.info_dont_show_dbreadonly_again).Click += (sender, args) =>
                {
                    _prefs.Edit().PutBoolean(dbreadonly_ignore_prefskey + App.Kp2a.CurrentDb.CurrentFingerprintPrefKey, true).Commit();
                    UpdateDbReadOnlyInfo();
                };
            }

            if (FindViewById(Resource.Id.fabCancelAddNew) != null)
            {
                FindViewById(Resource.Id.fabAddNew).Click += (sender, args) =>
                {
                    FindViewById(Resource.Id.fabCancelAddNew).Visibility = ViewStates.Visible;
                    FindViewById(Resource.Id.fabAddNewGroup).Visibility = AddGroupEnabled ? ViewStates.Visible : ViewStates.Gone;
                    FindViewById(Resource.Id.fabAddNewEntry).Visibility = AddEntryEnabled ? ViewStates.Visible : ViewStates.Gone;
                    FindViewById(Resource.Id.fabAddNew).Visibility = ViewStates.Gone;
                };

                FindViewById(Resource.Id.fabCancelAddNew).Click += (sender, args) =>
                {
                    FindViewById(Resource.Id.fabCancelAddNew).Visibility = ViewStates.Gone;
                    FindViewById(Resource.Id.fabAddNewGroup).Visibility = ViewStates.Gone;
                    FindViewById(Resource.Id.fabAddNewEntry).Visibility = ViewStates.Gone;
                    FindViewById(Resource.Id.fabAddNew).Visibility = ViewStates.Visible;
                };


            }


            if (FindViewById(Resource.Id.cancel_insert_element) != null)
            {
                FindViewById(Resource.Id.cancel_insert_element).Click += (sender, args) => StopMovingElements();
                FindViewById(Resource.Id.insert_element).Click += (sender, args) => InsertElements();
                Util.MoveBottomBarButtons(Resource.Id.cancel_insert_element, Resource.Id.insert_element, Resource.Id.bottom_bar, this);
            }

            if (FindViewById(Resource.Id.show_autofill_info) != null)
            {
                FindViewById(Resource.Id.show_autofill_info).Click += (sender, args) => Util.GotoUrl(this, "https://philippc.github.io/keepass2android/OreoAutoFill.html");
                Util.MoveBottomBarButtons(Resource.Id.show_autofill_info, Resource.Id.enable_autofill, Resource.Id.autofill_buttons, this);
            }

            if (FindViewById(Resource.Id.configure_notification_channels) != null)
            {
                FindViewById(Resource.Id.configure_notification_channels).Click += (sender, args) =>
                {
                    Intent intent = new Intent(Settings.ActionChannelNotificationSettings);
                    intent.PutExtra(Settings.ExtraChannelId, App.NotificationChannelIdQuicklocked);
                    intent.PutExtra(Settings.ExtraAppPackage, PackageName);
                    try
                    {
                        StartActivity(intent);
                    }
                    catch (Exception e)
                    {
                        new AlertDialog.Builder(this)
                            .SetTitle("Unexpected error")
                            .SetMessage(
                                "Opening the settings failed. Please report this to crocoapps@gmail.com including information about your device vendor and OS. Please try to configure the notifications by long pressing a KP2A notification. Details: " + e.ToString())
                            .Show();
                    }
                    UpdateAndroid8NotificationInfo(true);
                };
                FindViewById(Resource.Id.ignore_notification_channel).Click += (sender, args) =>
                {
                    UpdateAndroid8NotificationInfo(true);
                };

            }







            SetResult(KeePass.ExitNormal);

            



        }
        
        private bool IsTimeForInfotext(out string lastInfoText)
        {
            DateTime lastDisplayTime = new DateTime(_prefs.GetLong("LastInfoTextTime", 0));
            lastInfoText = _prefs.GetString("LastInfoTextKey", "");
#if DEBUG
            return DateTime.UtcNow - lastDisplayTime > TimeSpan.FromSeconds(10);
#else
            return DateTime.UtcNow - lastDisplayTime > TimeSpan.FromDays(3);
#endif
        }

        private void DisableInfoTextDisplay(string infoTextKey)
        {
            _prefs
                .Edit()
                .PutBoolean("InfoTextDisabled_" + infoTextKey, true)
                .Commit();

        }

        private void RegisterInfoTextDisplay(string infoTextKey)
        {
            _prefs
                .Edit()
                .PutLong("LastInfoTextTime", DateTime.UtcNow.Ticks)
                .PutString("LastInfoTextKey", infoTextKey)
                .Commit();

        }

        private bool InfoTextWasDisabled(string infoTextKey)
        {
            return _prefs.GetBoolean("InfoTextDisabled_" + infoTextKey, false);
        }

        const string dbreadonly_ignore_prefskey = "dbreadonly_ignore_prefskey";
        const string childdb_ignore_prefskey = "childdb_ignore_prefskey";
        const string autofillservicewasenabled_prefskey = "AutofillServiceWasEnabled";
        const string fingerprintinfohidden_prefskey = "fingerprintinfohidden_prefskey";

        private void UpdateAutofillInfo()
        {
            bool canShowAutofillInfo = false;

            if (!((Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.O) ||
                  !((AutofillManager)GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager))))
                      .IsAutofillSupported))
            {
                if (!((AutofillManager)GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager))))
                    .HasEnabledAutofillServices)
                {
                    if (!_prefs.GetBoolean(autofillservicewasenabled_prefskey, false))
                        canShowAutofillInfo = true;
                }
                else
                {
                    _prefs.Edit().PutBoolean(autofillservicewasenabled_prefskey, true).Commit();

                }
            }
            if (canShowAutofillInfo)
            {
                RegisterInfoTextDisplay("AutofillSuggestion"); //this ensures that we don't show the general info texts too soon
                
            }
            UpdateBottomBarElementVisibility(Resource.Id.autofill_infotext, canShowAutofillInfo);
        }

        private void UpdateFingerprintInfo()
        {
            bool canShowFingerprintInfo = false;

            bool disabledForDatabase = _prefs.GetBoolean(fingerprintinfohidden_prefskey + App.Kp2a.CurrentDb.CurrentFingerprintPrefKey, false);
            bool disabledForAll = _prefs.GetBoolean(fingerprintinfohidden_prefskey, false);
            if (!disabledForAll && !disabledForDatabase && !App.Kp2a.IsChildDatabase(App.Kp2a.CurrentDb))
            {

                BiometricModule biometricModule = new BiometricModule(this);
                if (biometricModule.IsAvailable)
                {
                    FingerprintUnlockMode um;
                    Enum.TryParse(_prefs.GetString(Database.GetFingerprintModePrefKey(App.Kp2a.CurrentDb.Ioc), ""), out um);
                    canShowFingerprintInfo = um == FingerprintUnlockMode.Disabled;
                }
            }


            if (canShowFingerprintInfo)
            {
                RegisterInfoTextDisplay("FingerprintSuggestion"); //this ensures that we don't show the general info texts too soon

            }
            UpdateBottomBarElementVisibility(Resource.Id.fingerprint_infotext, canShowFingerprintInfo);
        }

        private void UpdateChildDbInfo()
        {
            bool canShow = Group == App.Kp2a.CurrentDb.Root
                && KeeAutoExecExt.GetAutoExecItems(App.Kp2a.CurrentDb.KpDatabase).Any(item =>
            {
                bool isexplicit;
                KeeAutoExecExt.IsDeviceEnabled(item, KeeAutoExecExt.ThisDeviceId, out isexplicit);
                return !isexplicit;
            });

            bool disabledForDatabase = _prefs.GetBoolean(childdb_ignore_prefskey+ App.Kp2a.CurrentDb.CurrentFingerprintPrefKey, false);

            if (canShow && !disabledForDatabase)
            {
                RegisterInfoTextDisplay("ChildDb"); //this ensures that we don't show the general info texts too soon

            }
            UpdateBottomBarElementVisibility(Resource.Id.child_db_infotext, canShow && !disabledForDatabase);
        }

        private void UpdateDbReadOnlyInfo()
        {
            bool disabledForDatabase = _prefs.GetBoolean(dbreadonly_ignore_prefskey + App.Kp2a.CurrentDb.CurrentFingerprintPrefKey, false);

            bool canShow = false;

            if (!disabledForDatabase)
            {
                var ioc = App.Kp2a.CurrentDb.Ioc;
                OptionalOut<UiStringKey> reason = new OptionalOut<UiStringKey>();

                if (App.Kp2a.GetFileStorage(ioc).IsReadOnly(ioc, reason))
                {
                    canShow = true;
                    RegisterInfoTextDisplay(
                        "DbReadOnly"); //this ensures that we don't show the general info texts too soon

                    FindViewById<TextView>(Resource.Id.dbreadonly_infotext_text).Text =
                    (GetString(Resource.String.FileReadOnlyMessagePre) + " " +
                     App.Kp2a.GetResourceString(reason.Result));
                }
            }
            UpdateBottomBarElementVisibility(Resource.Id.dbreadonly_infotext, canShow);
        }


        protected void UpdateBottomBarElementVisibility(int resourceId, bool canShow)
        {
            if (canShow)
                showableBottomBarElements.Add(resourceId);
            else
                showableBottomBarElements.Remove(resourceId);
            UpdateBottomBarVisibility();
        }

        protected virtual int ContentResourceId
        {
            get { return Resource.Layout.group; }
        }

        private void InsertElements()
        {
            MoveElementsTask moveElementsTask = (MoveElementsTask)AppTask;
            IEnumerable<IStructureItem> elementsToMove = moveElementsTask.Uuids.Select(uuid => App.Kp2a.FindStructureItem(uuid));



            var moveElement = new MoveElements(elementsToMove.ToList(), Group, this, App.Kp2a, new ActionOnFinish(this,
                (success, message, activity) =>
                {
                    ((GroupBaseActivity)activity)?.StopMovingElements();
                    if (!String.IsNullOrEmpty(message))
                        Toast.MakeText(activity, message, ToastLength.Long).Show();
                }));
            var progressTask = new ProgressTask(App.Kp2a, this, moveElement);
            progressTask.Run();

        }



        protected void SetGroupTitle()
        {
            String name = Group.Name;
            String titleText;
            bool clickable = (Group != null) && (Group.IsVirtual == false) && ((Group.ParentGroup != null) || App.Kp2a.OpenDatabases.Count() > 1);
            if (!String.IsNullOrEmpty(name))
            {
                titleText = name;
            }
            else
            {
                titleText = GetText(Resource.String.root);
            }

            SupportActionBar.Title = titleText;
            if (clickable)
            {
                SupportActionBar.SetHomeButtonEnabled(true);
                SupportActionBar.SetDisplayHomeAsUpEnabled(true);
                SupportActionBar.SetDisplayShowHomeEnabled(true);
            }

        }


        protected void SetGroupIcon()
        {
            if (Group != null)
            {
                SupportActionBar.SetDisplayShowHomeEnabled(true);
            }
        }

        class SuggestionListener : Java.Lang.Object, SearchView.IOnSuggestionListener, Android.Support.V7.Widget.SearchView.IOnSuggestionListener
        {
            private readonly CursorAdapter _suggestionsAdapter;
            private readonly GroupBaseActivity _activity;
            private readonly IMenuItem _searchItem;


            public SuggestionListener(Android.Support.V4.Widget.CursorAdapter suggestionsAdapter, GroupBaseActivity activity, IMenuItem searchItem)
            {
                _suggestionsAdapter = suggestionsAdapter;
                _activity = activity;
                _searchItem = searchItem;
            }

            public bool OnSuggestionClick(int position)
            {
                var cursor = _suggestionsAdapter.Cursor;
                cursor.MoveToPosition(position);
                
                ElementAndDatabaseId fullId = new ElementAndDatabaseId(cursor.GetString(cursor.GetColumnIndexOrThrow(SearchManager.SuggestColumnIntentDataId)));
                var entryId = fullId.ElementId;
                EntryActivity.Launch(_activity, App.Kp2a.GetDatabase(fullId.DatabaseId).EntriesById[entryId], -1, _activity.AppTask);
                return true;
            }

            public bool OnSuggestionSelect(int position)
            {
                return false;
            }
        }

        class OnQueryTextListener : Java.Lang.Object, Android.Support.V7.Widget.SearchView.IOnQueryTextListener
        {
            private readonly GroupBaseActivity _activity;

            public OnQueryTextListener(GroupBaseActivity activity)
            {
                _activity = activity;
            }

            public bool OnQueryTextChange(string newText)
            {
                return false;
            }

            public bool OnQueryTextSubmit(string query)
            {
                if (String.IsNullOrEmpty(query))
                    return false; //let the default happen

                Intent searchIntent = new Intent(_activity, typeof(search.SearchResults));
                searchIntent.SetAction(Intent.ActionSearch); //currently not necessary to set because SearchResults doesn't care, but let's be as close to the default as possible
                searchIntent.PutExtra(SearchManager.Query, query);
                //forward appTask:
                _activity.AppTask.ToIntent(searchIntent);

                _activity.StartActivityForResult(searchIntent, 0);

                return true;
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {

            MenuInflater inflater = MenuInflater;
            inflater.Inflate(Resource.Menu.group, menu);
            var searchManager = (SearchManager)GetSystemService(Context.SearchService);

            /*This is the start of a pretty hacky workaround to avoid a crash on Samsung devices with Android 9.
             * The crash stacktrace is pretty unspecific (see https://stackoverflow.com/questions/54530604/app-crash-but-no-app-specific-code-in-stack-trace)
             * It points to InputMethodService.java which seems to be modified by Samsung. Hard to tell what's going on.
             * The problem only occurs, if our own keyboard is activated.
             * Users found that the crash does not appear if another activity was launched and closed before activating search view.
             * That's what we do as a workaround: We display another search menu option in case a crash would occur. When that search option is clicked,
             * we launch an activity which immediately finished. In the activity result, we can activate the search view safely.
             * If anybody reading this has a better idea, please let me know :-)
             */
            searchItem = menu.FindItem(Resource.Id.menu_search);
            searchItemDummy = menu.FindItem(Resource.Id.menu_search_dummy);
            SetSearchItemVisibility();


            var view = searchItem.ActionView;

            searchView = view.JavaCast<Android.Support.V7.Widget.SearchView>();

            searchView.SetSearchableInfo(searchManager.GetSearchableInfo(ComponentName));
            searchView.SetOnSuggestionListener(new SuggestionListener(searchView.SuggestionsAdapter, this, searchItem));
            searchView.SetOnQueryTextListener(new OnQueryTextListener(this));
            
            if (_prefs.GetBoolean("ActivateSearchView", false) && AppTask.CanActivateSearchViewOnStart)
            {

                //need to use PostDelayed, otherwise the menu_lock item completely disappears
                searchView.PostDelayed(ActivateSearchView, 500);
            }

            ActionBar.LayoutParams lparams = new ActionBar.LayoutParams(ActionBar.LayoutParams.MatchParent,
                ActionBar.LayoutParams.MatchParent);
            searchView.LayoutParameters = lparams;


            _syncItem = menu.FindItem(Resource.Id.menu_sync);


            _offlineItem = menu.FindItem(Resource.Id.menu_work_offline);
            _onlineItem = menu.FindItem(Resource.Id.menu_work_online);

            UpdateOfflineModeMenu();


            return base.OnCreateOptionsMenu(menu);

        }

        private void SetSearchItemVisibility()
        {
            if ((searchItem == null) || (searchItemDummy == null))
                return;
            if (Build.Manufacturer.ToLowerInvariant() == "samsung" && ((int) Build.VERSION.SdkInt >= 28) && (IsKp2aKeyboardActive()) && !hasCalledOtherActivity)
            {
                searchItem.SetVisible(false);
                searchItemDummy.SetVisible(true);
            }
            else
            {
                searchItem.SetVisible(true);
                searchItemDummy.SetVisible(false);
            }
        }

        private string Kp2aInputMethodName
        {
            get { return PackageName + "/keepass2android.softkeyboard.KP2AKeyboard"; }
        }
        private bool IsKp2aKeyboardActive()
        {
            string currentIme = Android.Provider.Settings.Secure.GetString(
                ContentResolver,
                Android.Provider.Settings.Secure.DefaultInputMethod);
            
            return Kp2aInputMethodName == currentIme;

        }

        private void ActivateSearchView()
        {
            
                searchView.Iconified = false;
                AppTask.CanActivateSearchViewOnStart = false;
            
        }

        private void UpdateOfflineModeMenu()
        {
            try
            {
                if (_syncItem != null)
                {
                    if (((App.Kp2a.OpenDatabases.Count() == 1)  || (EntriesBelongToCurrentDatabaseOnly))
                        && App.Kp2a.CurrentDb.Ioc.IsLocalFile())
                        _syncItem.SetVisible(false);
                    else
                        _syncItem.SetVisible(!App.Kp2a.OfflineMode);
                }

                if (((App.Kp2a.OpenDatabases.Count() == 1) || (EntriesBelongToCurrentDatabaseOnly))
                    &&  (App.Kp2a.GetFileStorage(App.Kp2a.CurrentDb.Ioc) is IOfflineSwitchable))
                {
                    _offlineItem?.SetVisible(App.Kp2a.OfflineMode == false);
                    _onlineItem?.SetVisible(App.Kp2a.OfflineMode);
                }
                else
                {
                    _offlineItem?.SetVisible(false);
                    _onlineItem?.SetVisible(false);
                }
            }
            catch (Exception e)
            {
                Kp2aLog.LogUnexpectedError(new Exception("Cannot UpdateOfflineModeMenu " + (App.Kp2a == null) + " " + ((App.Kp2a == null) || (App.Kp2a.CurrentDb== null)) + " " + (((App.Kp2a == null) || (App.Kp2a.CurrentDb== null) || (App.Kp2a.CurrentDb.Ioc == null)) + " " + (_syncItem != null) + " " + (_offlineItem != null) + " " + (_onlineItem != null))));
            }

        }

        public abstract bool EntriesBelongToCurrentDatabaseOnly { get; }

        public abstract ElementAndDatabaseId FullGroupId { get; }


        public override bool OnPrepareOptionsMenu(IMenu menu)
        {
            if (!base.OnPrepareOptionsMenu(menu))
            {
                return false;
            }

            Util.PrepareDonateOptionMenu(menu, this);


            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.menu_donate:
                    return Util.GotoDonateUrl(this);
                case Resource.Id.menu_lock:
                    App.Kp2a.Lock();
                    return true;
                case Resource.Id.menu_search_dummy:
                    StartActivityForResult(typeof(CloseImmediatelyActivity), RequestCodeActivateRealSearch);
                    OverridePendingTransition(0, 0);
                    hasCalledOtherActivity = true;
                    //TODO transition?

                    return true;
                case Resource.Id.menu_search:
                case Resource.Id.menu_search_advanced:
                    OnSearchRequested();
                    return true;

                case Resource.Id.menu_app_settings:
                    DatabaseSettingsActivity.Launch(this);
                    return true;

                case Resource.Id.menu_sync:
                    Synchronize();
                    return true;

                case Resource.Id.menu_work_offline:
                    App.Kp2a.OfflineMode = App.Kp2a.OfflineModePreference = true;
                    UpdateOfflineModeMenu();
                    return true;

                case Resource.Id.menu_work_online:
                    App.Kp2a.OfflineMode = App.Kp2a.OfflineModePreference = false;
                    UpdateOfflineModeMenu();
                    Synchronize();
                    return true;
                case Resource.Id.menu_open_other_db:
                    AppTask.SetActivityResult(this, KeePass.ExitLoadAnotherDb);
                    Finish();
                    return true;
                case Resource.Id.menu_sort:
                    ChangeSort();
                    return true;
                case Android.Resource.Id.Home:
                    //Currently the action bar only displays the home button when we come from a previous activity.
                    //So we can simply Finish. See this page for information on how to do this in more general (future?) cases:
                    //http://developer.android.com/training/implementing-navigation/ancestral.html
                    AppTask.SetActivityResult(this, KeePass.ExitNormal);
                    Finish();
                    //OverridePendingTransition(Resource.Animation.anim_enter_back, Resource.Animation.anim_leave_back);

                    return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        public class SyncOtpAuxFile : RunnableOnFinish
        {
            private readonly IOConnectionInfo _ioc;

            public SyncOtpAuxFile(Activity activity, IOConnectionInfo ioc)
                : base(activity,null)
            {
                _ioc = ioc;
            }

            public override void Run()
            {
                StatusLogger.UpdateMessage(UiStringKey.SynchronizingOtpAuxFile);
                try
                {
                    //simply open the file. The file storage does a complete sync.
                    using (App.Kp2a.GetOtpAuxFileStorage(_ioc).OpenFileForRead(_ioc))
                    {
                    }

                    Finish(true);
                }
                catch (Exception e)
                {

                    Finish(false, e.Message);
                }


            }

        }

        private void Synchronize()
        {
            var filestorage = App.Kp2a.GetFileStorage(App.Kp2a.CurrentDb.Ioc);
            RunnableOnFinish task;
            OnFinish onFinish = new ActionOnFinish(this, (success, message, activity) =>
            {
                if (!String.IsNullOrEmpty(message))
                    Toast.MakeText(activity, message, ToastLength.Long).Show();

                // Tell the adapter to refresh it's list
                BaseAdapter adapter = (BaseAdapter)((GroupBaseActivity)activity)?.ListAdapter;
                adapter?.NotifyDataSetChanged();

                if (App.Kp2a.CurrentDb.OtpAuxFileIoc != null)
                {
                    var task2 = new SyncOtpAuxFile(this, App.Kp2a.CurrentDb.OtpAuxFileIoc);
                    new ProgressTask(App.Kp2a, activity, task2).Run(true);
                }
            });

            if (filestorage is CachingFileStorage)
            {

                task = new SynchronizeCachedDatabase(this, App.Kp2a, onFinish);
            }
            else
            {

                task = new CheckDatabaseForChanges(this, App.Kp2a, onFinish);
            }




            var progressTask = new ProgressTask(App.Kp2a, this, task);
            progressTask.Run();

        }

        public override void OnBackPressed()
        {
            AppTask.SetActivityResult(this, KeePass.ExitNormal);
            base.OnBackPressed();
        }

        private void ChangeSort()
        {
            var sortOrderManager = new GroupViewSortOrderManager(this);
            IEnumerable<string> sortOptions = sortOrderManager.SortOrders.Select(
                o => GetString(o.ResourceId)
                );

            int selectedBefore = sortOrderManager.GetCurrentSortOrderIndex();

            new AlertDialog.Builder(this)
                .SetSingleChoiceItems(sortOptions.ToArray(), selectedBefore, (sender, args) =>
                    {
                        int selectedAfter = args.Which;

                        sortOrderManager.SetNewSortOrder(selectedAfter);
                        // Refresh menu titles
                        ActivityCompat.InvalidateOptionsMenu(this);

                        // Mark all groups as dirty now to refresh them on load
                        
                        App.Kp2a.MarkAllGroupsAsDirty();
                        // We'll manually refresh this group so we can remove it
                        App.Kp2a.DirtyGroups.Remove(Group);

                        // Tell the adapter to refresh it's list

                        BaseAdapter adapter = (BaseAdapter)ListAdapter;
                        adapter.NotifyDataSetChanged();


                    })
                    .SetPositiveButton(Android.Resource.String.Ok, (sender, args) => ((Dialog)sender).Dismiss())
                    .Show();




        }

        public class RefreshTask : OnFinish
        {
            public RefreshTask(Handler handler, GroupBaseActivity act)
                : base(act, handler)
            {
            }

            public override void Run()
            {
                if (Success)
                {
                    ((GroupBaseActivity)ActiveActivity)?.RefreshIfDirty();
                }
                else
                {
                    DisplayMessage(ActiveActivity);
                }
            }
        }
        public class AfterDeleteGroup : OnFinish
        {
            public AfterDeleteGroup(Handler handler, GroupBaseActivity act)
                : base(act, handler)
            {
            }


            public override void Run()
            {
                if (Success)
                {
                    ((GroupBaseActivity)ActiveActivity)?.RefreshIfDirty();
                }
                else
                {
                    Handler.Post(() =>
                    {
                        Toast.MakeText(ActiveActivity ?? Application.Context, "Unrecoverable error: " + Message, ToastLength.Long).Show();
                    });

                    App.Kp2a.Lock(false);
                }
            }

        }

        public bool IsBeingMoved(PwUuid uuid)
        {
            MoveElementsTask moveElementsTask = AppTask as MoveElementsTask;
            if (moveElementsTask != null)
            {
                if (moveElementsTask.Uuids.Any(uuidMoved => uuidMoved.Equals(uuid)))
                    return true;
            }
            return false;
        }

        public void StartTask(AppTask task)
        {
            AppTask = task;
            task.StartInGroupActivity(this);
        }


        public void StartMovingElements()
        {

            ShowInsertElementsButtons();
            BaseAdapter adapter = (BaseAdapter)ListAdapter;
            adapter.NotifyDataSetChanged();
        }

        public void ShowInsertElementsButtons()
        {
            FindViewById(Resource.Id.fabCancelAddNew).Visibility = ViewStates.Gone;
            FindViewById(Resource.Id.fabAddNewGroup).Visibility = ViewStates.Gone;
            FindViewById(Resource.Id.fabAddNewEntry).Visibility = ViewStates.Gone;
            FindViewById(Resource.Id.fabAddNew).Visibility = ViewStates.Gone;

            UpdateBottomBarElementVisibility(Resource.Id.insert_element, true);
            UpdateBottomBarElementVisibility(Resource.Id.cancel_insert_element, true);

        }

        public void StopMovingElements()
        {
            try
            {
                MoveElementsTask moveElementsTask = (MoveElementsTask)AppTask;
                foreach (var uuid in moveElementsTask.Uuids)
                {
                    IStructureItem elementToMove = App.Kp2a.FindStructureItem(uuid);
                    if (elementToMove.ParentGroup != Group)
                        App.Kp2a.DirtyGroups.Add(elementToMove.ParentGroup);
                }
            }
            catch (Exception e)
            {
                //don't crash if adding to dirty fails but log the exception:
                Kp2aLog.LogUnexpectedError(e);
            }

            AppTask = new NullTask();
            AppTask.SetupGroupBaseActivityButtons(this);
            BaseAdapter adapter = (BaseAdapter)ListAdapter;
            adapter.NotifyDataSetChanged();
        }


        public void EditGroup(PwGroup pwGroup)
        {
            GroupEditActivity.Launch(this, pwGroup.ParentGroup, pwGroup);
        }
    }

    public class GroupListFragment : ListFragment, AbsListView.IMultiChoiceModeListener
    {
        private ActionMode _mode;
        private int _statusBarColor;

        public override void OnActivityCreated(Bundle savedInstanceState)
        {
            base.OnActivityCreated(savedInstanceState);

            ListView.SetMultiChoiceModeListener(this);
            if (App.Kp2a.OpenDatabases.Any(db => db.CanWrite))
            {
                ListView.ChoiceMode = ChoiceMode.MultipleModal;

                ListView.ItemLongClick += delegate(object sender, AdapterView.ItemLongClickEventArgs args)
                {
                    ListView.SetItemChecked(args.Position, true);
                };

            }
            else
            {
                ListView.ChoiceMode = ChoiceMode.None;
            }

            ListView.ItemClick += (sender, args) => ((GroupListItemView)args.View).OnClick();

            StyleListView();

        }

        protected void StyleListView()
        {
            ListView lv = ListView;
            lv.ScrollBarStyle = ScrollbarStyles.InsideInset;
            lv.TextFilterEnabled = true;

            lv.Divider = null;
        }

        public bool OnActionItemClicked(ActionMode mode, IMenuItem item)
        {
            var listView = FragmentManager.FindFragmentById<GroupListFragment>(Resource.Id.list_fragment).ListView;
            var checkedItemPositions = listView.CheckedItemPositions;

            List<IStructureItem> checkedItems = new List<IStructureItem>();
            for (int i = 0; i < checkedItemPositions.Size(); i++)
            {
                if (checkedItemPositions.ValueAt(i))
                {
                    checkedItems.Add(((PwGroupListAdapter)ListAdapter).GetItemAtPosition(checkedItemPositions.KeyAt(i)));
                }
            }

            //shouldn't happen, just in case...
            if (!checkedItems.Any())
            {
                return false;
            }
            Handler handler = new Handler();
            switch (item.ItemId)
            {

                case Resource.Id.menu_delete:
                    DeleteMultipleItems((GroupBaseActivity)Activity, checkedItems, new GroupBaseActivity.RefreshTask(handler, ((GroupBaseActivity)Activity)), App.Kp2a);
                    break;
                case Resource.Id.menu_move:
                    var navMove = new NavigateToFolderAndLaunchMoveElementTask(App.Kp2a.CurrentDb, checkedItems.First().ParentGroup, checkedItems.Select(i => i.Uuid).ToList(), ((GroupBaseActivity)Activity).IsSearchResult);
                    ((GroupBaseActivity)Activity).StartTask(navMove);
                    break;
                case Resource.Id.menu_copy:

                    var copyTask = new CopyEntry((GroupBaseActivity)Activity, App.Kp2a, (PwEntry)checkedItems.First(),
                        new GroupBaseActivity.RefreshTask(handler, ((GroupBaseActivity)Activity)), App.Kp2a.CurrentDb);

                    ProgressTask pt = new ProgressTask(App.Kp2a, Activity, copyTask);
                    pt.Run();
                    break;

                case Resource.Id.menu_navigate:
                    NavigateToFolder navNavigate = new NavigateToFolder(App.Kp2a.CurrentDb, checkedItems.First().ParentGroup, true);
                    ((GroupBaseActivity)Activity).StartTask(navNavigate);
                    break;
                case Resource.Id.menu_edit:
                    GroupEditActivity.Launch(Activity, checkedItems.First().ParentGroup, (PwGroup)checkedItems.First());
                    break;
                default:
                    return false;


            }
            listView.ClearChoices();
            ((BaseAdapter)ListAdapter).NotifyDataSetChanged();
            if (_mode != null)
                mode.Finish();

            return true;
        }

        public bool OnCreateActionMode(ActionMode mode, IMenu menu)
        {
            MenuInflater inflater = Activity.MenuInflater;
            inflater.Inflate(Resource.Menu.group_entriesselected, menu);
            //mode.Title = "Select Items";
            Android.Util.Log.Debug("KP2A", "Create action mode" + mode);
            ((PwGroupListAdapter)ListView.Adapter).InActionMode = true;
            ((PwGroupListAdapter)ListView.Adapter).NotifyDataSetChanged();
            _mode = mode;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                _statusBarColor = Activity.Window.StatusBarColor;
                Activity.Window.SetStatusBarColor(Activity.Resources.GetColor(Resource.Color.appAccentColorDark));
            }
            return true;
        }

        public void OnDestroyActionMode(ActionMode mode)
        {
            Android.Util.Log.Debug("KP2A", "Destroy action mode" + mode);
            ((PwGroupListAdapter)ListView.Adapter).InActionMode = false;
            ((PwGroupListAdapter)ListView.Adapter).NotifyDataSetChanged();
            _mode = null;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                Activity.Window.SetStatusBarColor(new Android.Graphics.Color(_statusBarColor));
            }
        }

        public bool OnPrepareActionMode(ActionMode mode, IMenu menu)
        {
            Android.Util.Log.Debug("KP2A", "Prepare action mode" + mode);
            ((PwGroupListAdapter)ListView.Adapter).InActionMode = mode != null;
            ((PwGroupListAdapter)ListView.Adapter).NotifyDataSetChanged();
            UpdateMenuItemVisibilities(mode);
            return true;
        }

        public void OnItemCheckedStateChanged(ActionMode mode, int position, long id, bool @checked)
        {
            UpdateMenuItemVisibilities(mode);
        }

        private void UpdateMenuItemVisibilities(ActionMode mode)
        {
            var menuItem = mode.Menu.FindItem(Resource.Id.menu_edit);
            if (menuItem != null)
            {
                menuItem.SetVisible(IsOnlyOneGroupChecked());
            }

            menuItem = mode.Menu.FindItem(Resource.Id.menu_navigate);
            if (menuItem != null)
            {
                menuItem.SetVisible(((GroupBaseActivity) Activity).IsSearchResult && IsOnlyOneItemChecked());
            }

            menuItem = mode.Menu.FindItem(Resource.Id.menu_copy);
            if (menuItem != null)
            {
                menuItem.SetVisible(IsOnlyOneEntryChecked());
            }
        }

        private bool IsOnlyOneGroupChecked()
        {
            var checkedItems = ListView.CheckedItemPositions;
            bool hadCheckedGroup = false;
            if (checkedItems != null)
            {
                for (int i = 0; i < checkedItems.Size(); i++)
                {
                    if (checkedItems.ValueAt(i))
                    {
                        if (hadCheckedGroup)
                        {
                            return false;
                        }

                        if (((PwGroupListAdapter)ListAdapter).IsGroupAtPosition(checkedItems.KeyAt(i)))
                        {
                            hadCheckedGroup = true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            return hadCheckedGroup;
        }

        private bool IsOnlyOneItemChecked()
        {
            var checkedItems = ListView.CheckedItemPositions;
            bool hadCheckedItem = false;
            if (checkedItems != null)
            {
                for (int i = 0; i < checkedItems.Size(); i++)
                {
                    if (checkedItems.ValueAt(i))
                    {
                        if (hadCheckedItem)
                        {
                            return false;
                        }

                        hadCheckedItem = true;
                    }
                }
            }
            return hadCheckedItem;
        }

        private bool IsOnlyOneEntryChecked()
        {
            return IsOnlyOneItemChecked() && !IsOnlyOneGroupChecked();
        }


        public void  DeleteMultipleItems(GroupBaseActivity activity, List<IStructureItem> checkedItems, OnFinish onFinish, Kp2aApp app)
        {
            if (checkedItems.Any() == false)
                return;
            //sort checkedItems by database
            List<KeyValuePair<Database, List<IStructureItem>>> itemsForDatabases =
                new List<KeyValuePair<Database, List<IStructureItem>>>();
            foreach (var item in checkedItems)
            {
                var db = app.FindDatabaseForElement(item);
                if (db != null)
                {
                    bool foundDatabase = false;
                    foreach (var listEntry in itemsForDatabases)
                    {
                        if (listEntry.Key == db)
                        {
                            foundDatabase = true;
                            listEntry.Value.Add(item);
                            break;
                        }
                    }
                    if (!foundDatabase)
                    {
                        itemsForDatabases.Add(new KeyValuePair<Database, List<IStructureItem>>(db, new List<IStructureItem> { item }));
                    }
                }

            }

            int dbIndex = 0;
            Action<bool, string, Activity> action = null;
            action = (success, message, activeActivity) =>
            {
                if (success)
                {
                    dbIndex++;
                    if (dbIndex == itemsForDatabases.Count)
                    {
                        onFinish.SetResult(true);
                        onFinish.Run();
                        return;
                    }
                    new DeleteMultipleItemsFromOneDatabase(activity, itemsForDatabases[dbIndex].Key,
                        itemsForDatabases[dbIndex].Value, new ActionOnFinish(activeActivity, (b, s, activity1) => action(b, s, activity1)), app)
                        .Start();
                }
                else
                {
                    onFinish.SetResult(false, message, true, null);
                }
            };

            new DeleteMultipleItemsFromOneDatabase(activity, itemsForDatabases[dbIndex].Key,
                itemsForDatabases[dbIndex].Value, new ActionOnFinish(activity, (b, s, activity1) => action(b, s, activity1)), app)
                .Start();
        }

    }
}

