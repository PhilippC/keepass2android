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
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Text;
using Android.Views;
using Android.Widget;
using Android.Preferences;
using Android.Text.Method;
using System.Globalization;
using System.IO;
using System.Net;
using Android.Content.PM;
using Android.Webkit;
using Android.Graphics;
using keepass2android.EntryActivityClasses;
using KeePassLib;
using KeePassLib.Security;
using KeePassLib.Utility;
using Keepass2android.Pluginsdk;
using keepass2android.Io;
using KeePass.DataExchange;
using KeePass.Util.Spr;
using KeePassLib.Interfaces;
using KeePassLib.Serialization;
using PluginTOTP;
using File = Java.IO.File;
using Uri = Android.Net.Uri;
using keepass2android.fileselect;
using Boolean = Java.Lang.Boolean;

namespace keepass2android
{
    public class ExportBinaryProcessManager : FileSaveProcessManager
    {
        private readonly string _binaryToSave;

        public ExportBinaryProcessManager(int requestCode, Activity activity, string key) : base(requestCode, activity)
        {
            _binaryToSave = key;
        }

        public ExportBinaryProcessManager(int requestCode, EntryActivity activity, Bundle savedInstanceState) : base(requestCode, activity)
        {
            _binaryToSave = savedInstanceState.GetString("BinaryToSave", null);
        }

        protected override void SaveFile(IOConnectionInfo ioc)
        {
            var task = new EntryActivity.WriteBinaryTask(_activity, App.Kp2a, new ActionOnFinish(_activity, (success, message, activity) =>
                {
                    if (!success)
                        Toast.MakeText(activity, message, ToastLength.Long).Show();
                }
            ), ((EntryActivity)_activity).Entry.Binaries.Get(_binaryToSave), ioc);
            ProgressTask pt = new ProgressTask(App.Kp2a, _activity, task);
            pt.Run();

        }

        public override void OnSaveInstanceState(Bundle outState)
        {
            outState.PutString("BinaryToSave", _binaryToSave);
        }
        

    }


	[Activity (Label = "@string/app_name", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden,
        Theme = "@style/MyTheme_ActionBar")]
	public class EntryActivity : LockCloseActivity 
	{
		public const String KeyEntry = "entry";
        public const String KeyRefreshPos = "refresh_pos";
        public const String KeyEntryHistoryIndex = "entry_history_index";
		public const String KeyActivateKeyboard = "activate_keyboard";
		public const String KeyGroupFullPath = "groupfullpath_key";

	    public const int requestCodeBinaryFilename = 42376;
        public const int requestCodeSelFileStorageForWriteAttachment = 42377;
	    


        public static void Launch(Activity act, PwEntry pw, int pos, AppTask appTask, ActivityFlags? flags = null, int historyIndex=-1)
		{
			Intent i = new Intent(act, typeof(EntryActivity));

            var db = App.Kp2a.FindDatabaseForElement(pw);
			i.PutExtra(KeyEntry, new ElementAndDatabaseId(db, pw).FullId);
			i.PutExtra(KeyRefreshPos, pos);
            i.PutExtra(KeyEntryHistoryIndex, historyIndex);

		    if (App.Kp2a.CurrentDb != db)
		    {
		        App.Kp2a.CurrentDb = db;
		    }

			if (flags != null)
				i.SetFlags((ActivityFlags) flags);

			appTask.ToIntent(i);
			if (flags != null && (((ActivityFlags) flags) | ActivityFlags.ForwardResult) == ActivityFlags.ForwardResult)
				act.StartActivity(i);
			else
				act.StartActivityForResult(i, 0);
		}

		public EntryActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		public EntryActivity()
		{
			_activityDesign = new ActivityDesign(this);
		}

		//this is the entry we display. Note that it might be an element from a History list in case _historyIndex >= 0
	    public PwEntry Entry;
		//if _historyIndex >=0, _historyParentEntry stores the PwEntry which contains the history entry "Entry"
        private PwEntry _historyParentEntry;

		private PasswordFont _passwordFont = new PasswordFont();

		internal Dictionary<TextView /*the "ProtectedField" of the ProtectedTextviewGroup*/, bool> _showPassword = new Dictionary<TextView, bool>();
		private int _pos;

        private AppTask _appTask;
        private AppTask AppTask
        {
            get { return _appTask; }
            set
            {
                _appTask = value;
                Kp2aLog.LogTask(value, MyDebugName);
            }
        }

		struct ProtectedTextviewGroup
	    {
	        public TextView ProtectedField;
	        public TextView VisibleProtectedField;
        }

		private List<ProtectedTextviewGroup> _protectedTextViews;
		private IMenu _menu;

		private readonly Dictionary<string, List<IPopupMenuItem>> _popupMenuItems =
			new Dictionary<string, List<IPopupMenuItem>>();

		private readonly Dictionary<string, IStringView> _stringViews = new Dictionary<string, IStringView>();
		private readonly List<PluginMenuOption> _pendingMenuOptions = new List<PluginMenuOption>();
		
		//make sure _timer doesn't go out of scope:
		private Timer _timer;
		private PluginActionReceiver _pluginActionReceiver;
		private PluginFieldReceiver _pluginFieldReceiver;
		private ActivityDesign _activityDesign;
	    


	    protected void SetEntryView()
		{
			SetContentView(Resource.Layout.entry_view);
		}

		protected void SetupEditButtons() {
			View edit =  FindViewById(Resource.Id.entry_edit);
			if (App.Kp2a.CurrentDb.CanWrite && _historyIndex < 0)
			{
				edit.Visibility = ViewStates.Visible;
				edit.Click += (sender, e) =>
				{
					EntryEditActivity.Launch(this, Entry, AppTask);
				};	
			}
			else
			{
				edit.Visibility = ViewStates.Gone;
			}
			
		}

		
		private class PluginActionReceiver : BroadcastReceiver
		{
			private readonly EntryActivity _activity;

			public PluginActionReceiver(EntryActivity activity)
			{
				_activity = activity;
			}

			public override void OnReceive(Context context, Intent intent)
			{
				var pluginPackage = intent.GetStringExtra(Strings.ExtraSender);
				if (new PluginDatabase(context).IsValidAccessToken(pluginPackage,
				                                                   intent.GetStringExtra(Strings.ExtraAccessToken),
				                                                   Strings.ScopeCurrentEntry))
				{
					if (intent.GetStringExtra(Strings.ExtraEntryId) != _activity.Entry.Uuid.ToHexString())
					{
						Kp2aLog.Log("received action for wrong entry " + intent.GetStringExtra(Strings.ExtraEntryId));
						return;
					}
					_activity.AddPluginAction(pluginPackage,
					                          intent.GetStringExtra(Strings.ExtraFieldId),
											  intent.GetStringExtra(Strings.ExtraActionId),
					                          intent.GetStringExtra(Strings.ExtraActionDisplayText),
					                          intent.GetIntExtra(Strings.ExtraActionIconResId, -1),
					                          intent.GetBundleExtra(Strings.ExtraActionData));
				}
				else
				{
					Kp2aLog.Log("received invalid request. Plugin not authorized.");
				}
			}
		}

		private class PluginFieldReceiver : BroadcastReceiver
		{
			private readonly EntryActivity _activity;

			public PluginFieldReceiver(EntryActivity activity)
			{
				_activity = activity;
			}

			public override void OnReceive(Context context, Intent intent)
			{
				if (intent.GetStringExtra(Strings.ExtraEntryId) != _activity.Entry.Uuid.ToHexString())
				{
					Kp2aLog.Log("received field for wrong entry " + intent.GetStringExtra(Strings.ExtraEntryId));
					return;
				}
				if (!new PluginDatabase(context).IsValidAccessToken(intent.GetStringExtra(Strings.ExtraSender),
				                                                    intent.GetStringExtra(Strings.ExtraAccessToken),
				                                                    Strings.ScopeCurrentEntry))
				{
					Kp2aLog.Log("received field with invalid access token from " + intent.GetStringExtra(Strings.ExtraSender));
					return;
				}
				string key = intent.GetStringExtra(Strings.ExtraFieldId);
				string value = intent.GetStringExtra(Strings.ExtraFieldValue);
				bool isProtected = intent.GetBooleanExtra(Strings.ExtraFieldProtected, false);
				_activity.SetPluginField(key, value, isProtected);
			}
		}

		private void SetPluginField(string key, string value, bool isProtected)
		{
			//update or add the string view:
			IStringView existingField;
			if (_stringViews.TryGetValue(key, out existingField))
			{
				existingField.Text = value;
			}
			else
			{
				ViewGroup extraGroup = (ViewGroup) FindViewById(Resource.Id.extra_strings);
				var view = CreateExtraSection(key, value, isProtected);
				extraGroup.AddView(view.View);
			}

			//update the Entry output in the App database and notify the CopyToClipboard service

		    if (App.Kp2a.LastOpenedEntry != null)
		    {
		        App.Kp2a.LastOpenedEntry.OutputStrings.Set(key, new ProtectedString(isProtected, value));
		        Intent updateKeyboardIntent = new Intent(this, typeof(CopyToClipboardService));
		        updateKeyboardIntent.SetAction(Intents.UpdateKeyboard);
		        updateKeyboardIntent.PutExtra(KeyEntry, new ElementAndDatabaseId(App.Kp2a.CurrentDb, Entry).FullId);
		        StartService(updateKeyboardIntent);

		        //notify plugins
		        NotifyPluginsOnModification(Strings.PrefixString + key);
		    }
		}

		private void AddPluginAction(string pluginPackage, string fieldId, string popupItemId, string displayText, int iconId, Bundle bundleExtra)
		{
			if (fieldId != null)
			{
				try
				{
					if (!_popupMenuItems.ContainsKey(fieldId))
					{
						Kp2aLog.Log("Did not find field with key " + fieldId);
						return;
					}
					//create a new popup item for the plugin action:
					var newPopup = new PluginPopupMenuItem(this, pluginPackage, fieldId, popupItemId, displayText, iconId, bundleExtra);
					//see if we already have a popup item for this field with the same item id
					var popupsForField = _popupMenuItems[fieldId];
					var popupItemPos = popupsForField.FindIndex(0,
															item =>
															(item is PluginPopupMenuItem) &&
															((PluginPopupMenuItem)item).PopupItemId == popupItemId);

					//replace existing or add
					if (popupItemPos >= 0)
					{
						popupsForField[popupItemPos] = newPopup;
					}
					else
					{
						popupsForField.Add(newPopup);
					}
				}
				catch (Exception e)
				{
					Kp2aLog.LogUnexpectedError(e);
				}
				
			}
			else
			{
				//we need to add an option to the  menu.
				//As it is not sure that OnCreateOptionsMenu was called yet, we cannot access _menu without a check:

				Intent i = new Intent(Strings.ActionEntryActionSelected);
				i.SetPackage(pluginPackage);
				i.PutExtra(Strings.ExtraActionData, bundleExtra);
				i.PutExtra(Strings.ExtraSender, PackageName);
				PluginHost.AddEntryToIntent(i, App.Kp2a.LastOpenedEntry);

				var menuOption = new PluginMenuOption()
					{
						DisplayText = displayText,
						Icon = PackageManager.GetResourcesForApplication(pluginPackage).GetDrawable(iconId),
						Intent = i
					};

				if (_menu != null)
				{
					AddMenuOption(menuOption);
				}
				else
				{
					lock (_pendingMenuOptions)
					{
						_pendingMenuOptions.Add(menuOption);
					}

				}


			}
		}

		private void AddMenuOption(PluginMenuOption menuOption)
		{
			var menuItem = _menu.Add(menuOption.DisplayText);
			menuItem.SetIcon(menuOption.Icon);
			menuItem.SetIntent(menuOption.Intent);
		}

		


		protected override void OnCreate(Bundle savedInstanceState)
		{
		    if (savedInstanceState != null)
		    {
		        _exportBinaryProcessManager =
		            new ExportBinaryProcessManager(requestCodeSelFileStorageForWriteAttachment, this, savedInstanceState);

		    }

		    ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);

			long usageCount = prefs.GetLong(GetString(Resource.String.UsageCount_key), 0);

			ISharedPreferencesEditor edit = prefs.Edit();
			edit.PutLong(GetString(Resource.String.UsageCount_key), usageCount + 1);
			edit.Commit();

			_showPasswordDefault =
				!prefs.GetBoolean(GetString(Resource.String.maskpass_key), Resources.GetBoolean(Resource.Boolean.maskpass_default));
            _showTotpDefault =
                !prefs.GetBoolean(GetString(Resource.String.masktotp_key), Resources.GetBoolean(Resource.Boolean.masktotp_default));

			RequestWindowFeature(WindowFeatures.IndeterminateProgress);
			
			_activityDesign.ApplyTheme(); 
			base.OnCreate(savedInstanceState);
			

			

			SetEntryView();

			Database db = App.Kp2a.CurrentDb;
			// Likely the app has been killed exit the activity 
			if (db == null || (App.Kp2a.QuickLocked))
			{
				Finish();
				return;
			}

			SetResult(KeePass.ExitNormal);

			Intent i = Intent;
            ElementAndDatabaseId dbAndElementId = new ElementAndDatabaseId(i.GetStringExtra(KeyEntry));
			PwUuid uuid = new PwUuid(MemUtil.HexStringToByteArray(dbAndElementId.ElementIdString));
			_pos = i.GetIntExtra(KeyRefreshPos, -1);
            _historyIndex = i.GetIntExtra(KeyEntryHistoryIndex, -1);

			AppTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);

			Entry = db.EntriesById[uuid];

            if (_historyIndex >= 0 && _historyIndex < Entry.History.UCount)
            {
                _historyParentEntry = Entry;
                Entry = Entry.History.Skip(_historyIndex).First();
                FindViewById<Button>(Resource.Id.btn_restore_history).Click += (sender, args) =>
                {
                    RestoreFromHistory();
                    SaveHistoryChangeAndFinish();
                };
                FindViewById<Button>(Resource.Id.btn_remove_history).Click += (sender, args) =>
                {
                    RemoveFromHistory();
                    SaveHistoryChangeAndFinish();
				};


            }
            else
            {
                // Update last access time.
                Entry.Touch(false);
                FindViewById<Button>(Resource.Id.btn_restore_history).Visibility = ViewStates.Gone;
                FindViewById<Button>(Resource.Id.btn_remove_history).Visibility = ViewStates.Gone;
			}

            // Refresh Menu contents in case onCreateMenuOptions was called before Entry was set
			ActivityCompat.InvalidateOptionsMenu(this);

			

			if (PwDefs.IsTanEntry(Entry) 
                && prefs.GetBoolean(GetString(Resource.String.TanExpiresOnUse_key), Resources.GetBoolean(Resource.Boolean.TanExpiresOnUse_default)) 
                && ((Entry.Expires == false) || Entry.ExpiryTime > DateTime.Now))
			{
				PwEntry backupEntry = Entry.CloneDeep();
				Entry.ExpiryTime = DateTime.Now;
				Entry.Expires = true;
				Entry.Touch(true);
				RequiresRefresh();
				UpdateEntry update = new UpdateEntry(this, App.Kp2a, backupEntry, Entry, null);
				ProgressTask pt = new ProgressTask(App.Kp2a, this, update);
				pt.Run();
			}
			FillData();

			SetupEditButtons();
			
			App.Kp2a.LastOpenedEntry = new PwEntryOutput(Entry, App.Kp2a.CurrentDb);

			_pluginActionReceiver = new PluginActionReceiver(this);
			RegisterReceiver(_pluginActionReceiver, new IntentFilter(Strings.ActionAddEntryAction));
			_pluginFieldReceiver = new PluginFieldReceiver(this);
			RegisterReceiver(_pluginFieldReceiver, new IntentFilter(Strings.ActionSetEntryField));

			new Thread(NotifyPluginsOnOpen).Start();

			//the rest of the things to do depends on the current app task:
			AppTask.CompleteOnCreateEntryActivity(this);
		}

        private void RemoveFromHistory()
        {
            _historyParentEntry.History.RemoveAt((uint)_historyIndex);
            _historyParentEntry.Touch(true, false);
		}

        private void RestoreFromHistory()
        {
            var db = App.Kp2a.FindDatabaseForElement(_historyParentEntry);
			_historyParentEntry.RestoreFromBackup((uint)_historyIndex, db.KpDatabase);
            _historyParentEntry.Touch(true, false);
		}

        private void SaveHistoryChangeAndFinish()
        {
            PwGroup parent = _historyParentEntry.ParentGroup;
            if (parent != null)
            {
                // Mark parent group dirty (title might have changed etc.)
                App.Kp2a.DirtyGroups.Add(parent);
            }

			var saveTask = new SaveDb(this, App.Kp2a, App.Kp2a.FindDatabaseForElement(Entry), new ActionOnFinish(this, (success, message, activity) =>
            {
                activity.SetResult(KeePass.ExitRefresh);
                activity.Finish();
            }));

            ProgressTask pt = new ProgressTask(App.Kp2a, this, saveTask);
            pt.Run();
		}

        private void NotifyPluginsOnOpen()
		{
			Intent i = new Intent(Strings.ActionOpenEntry);
			i.PutExtra(Strings.ExtraSender, PackageName);
			AddEntryToIntent(i);

			foreach (var plugin in new PluginDatabase(this).GetPluginsWithAcceptedScope(Strings.ScopeCurrentEntry))
			{
				i.SetPackage(plugin);
				SendBroadcast(i);
			}

			new Kp2aTotp().OnOpenEntry();

		}
		private void NotifyPluginsOnModification(string fieldId)
		{
			Intent i = new Intent(Strings.ActionEntryOutputModified);
			i.PutExtra(Strings.ExtraSender, PackageName);
			i.PutExtra(Strings.ExtraFieldId, fieldId);
			AddEntryToIntent(i);


			foreach (var plugin in new PluginDatabase(this).GetPluginsWithAcceptedScope(Strings.ScopeCurrentEntry))
			{
				i.SetPackage(plugin);
				SendBroadcast(i);
			}
		}


        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            if (permissions.Length == 1 && permissions.First() == Android.Manifest.Permission.PostNotifications &&
                grantResults.First() == Permission.Granted)
            {
                StartNotificationsServiceAfterPermissionsCheck(requestCode == 1 /*requestCode is used to transfer this flag*/);
            }

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
        internal void StartNotificationsService(bool activateKeyboard)
        {
            if (PreferenceManager.GetDefaultSharedPreferences(this).GetBoolean(
                    GetString(Resource.String.CopyToClipboardNotification_key),
                    Resources.GetBoolean(Resource.Boolean.CopyToClipboardNotification_default)) == false
                && PreferenceManager.GetDefaultSharedPreferences(this).GetBoolean(
                    GetString(Resource.String.UseKp2aKeyboard_key),
                    Resources.GetBoolean(Resource.Boolean.UseKp2aKeyboard_default)) == false)
            {
				//notifications are disabled
                return;
            }

            if ((int)Build.VERSION.SdkInt < 33 || CheckSelfPermission(Android.Manifest.Permission.PostNotifications) ==
                Permission.Granted)
			{
                StartNotificationsServiceAfterPermissionsCheck(activateKeyboard);
                return;
            }

            //user has not yet granted Android 13's POST_NOTIFICATONS permission for the app.

			//check if we should ask them to grant:
            if (!ShouldShowRequestPermissionRationale(Android.Manifest.Permission.PostNotifications) //this menthod returns false if we haven't asked yet or if the user has denied permission too often
                && PreferenceManager.GetDefaultSharedPreferences(this).GetBoolean("RequestedPostNotificationsPermission", false))//use a preference to tell the difference between "haven't asked yet" and "have asked too often"
            {
				//user has denied permission before. Do not show the dialog. User must give permission in the Android App settings.
                return;
            }

            new AlertDialog.Builder(this)
                .SetTitle(Resource.String.post_notifications_dialog_title)
                .SetMessage(Resource.String.post_notifications_dialog_message)
                .SetNegativeButton(Resource.String.post_notifications_dialog_disable, (sender, args) =>
                {
					//disable this dialog for the future by disabling the notification preferences
                    var edit= PreferenceManager.GetDefaultSharedPreferences(this).Edit();
                    edit.PutBoolean(GetString(Resource.String.CopyToClipboardNotification_key), false);
                    edit.PutBoolean(GetString(Resource.String.UseKp2aKeyboard_key), false);
                    edit.Commit();
                })
                .SetPositiveButton(Resource.String.post_notifications_dialog_allow, (sender, args) =>
                {

                    //remember that we did ask for permission at least once:
                    var edit = PreferenceManager.GetDefaultSharedPreferences(this).Edit();
                    edit.PutBoolean("RequestedPostNotificationsPermission", true);
                    edit.Commit();

                    //request permission. user must grant, we'll show notifications in the OnRequestPermissionResults() callback
                    Android.Support.V4.App.ActivityCompat.RequestPermissions(this, new[] { Android.Manifest.Permission.PostNotifications }, activateKeyboard ? 1 : 0 /*use requestCode to transfer the flag*/);


                })
                .SetNeutralButton(Resource.String.post_notifications_dialog_notnow, (sender, args) => {  })
                .Show();


        }

        private void StartNotificationsServiceAfterPermissionsCheck(bool activateKeyboard)
        {
            Intent showNotIntent = new Intent(this, typeof(CopyToClipboardService));
            showNotIntent.SetAction(Intents.ShowNotification);
            showNotIntent.PutExtra(KeyEntry, new ElementAndDatabaseId(App.Kp2a.CurrentDb, Entry).FullId);
            AppTask.PopulatePasswordAccessServiceIntent(showNotIntent);
            showNotIntent.PutExtra(KeyActivateKeyboard, activateKeyboard);

            StartService(showNotIntent);
        }


        private String getDateTime(DateTime dt)
		{
			return dt.ToLocalTime().ToString("g", CultureInfo.CurrentUICulture);
		}

		private String concatTags(List<string> tags)
		{
			StringBuilder sb = new StringBuilder();
			foreach (string tag in tags)
			{
				sb.Append(tag);
				sb.Append(", ");
			}
			if (tags.Count > 0)
				sb.Remove(sb.Length - 2, 2);
			return sb.ToString();
		}

		private void PopulateExtraStrings()
		{
			ViewGroup extraGroup = (ViewGroup) FindViewById(Resource.Id.extra_strings);
		    bool hasExtras = false;
			EditModeBase editMode = new DefaultEdit();
			if (KpEntryTemplatedEdit.IsTemplated(App.Kp2a.CurrentDb, this.Entry))
				editMode = new KpEntryTemplatedEdit(App.Kp2a.CurrentDb, this.Entry);
			foreach (var key in  editMode.SortExtraFieldKeys(Entry.Strings.GetKeys().Where(key=> !PwDefs.IsStandardField(key))))
			{
				if (editMode.IsVisible(key))
				{
					hasExtras = true;
					var value = Entry.Strings.Get(key);
					var stringView = CreateExtraSection(key, value.ReadString(), value.IsProtected);
					extraGroup.AddView(stringView.View);
				}
			}
            FindViewById(Resource.Id.extra_strings_container).Visibility = hasExtras ? ViewStates.Visible : ViewStates.Gone;
		}

		private ExtraStringView CreateExtraSection(string key, string value, bool isProtected)
		{
			LinearLayout layout = new LinearLayout(this, null) {Orientation = Orientation.Vertical};
			LinearLayout.LayoutParams layoutParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.FillParent,
			                                                                       ViewGroup.LayoutParams.WrapContent);

			layout.LayoutParameters = layoutParams;
			View viewInflated = LayoutInflater.Inflate(Resource.Layout.entry_extrastring_title, null);
			TextView keyView = viewInflated.FindViewById<TextView>(Resource.Id.entry_title);
			if (key != null)
				keyView.Text = key;

			layout.AddView(viewInflated);
			RelativeLayout valueViewContainer =
				(RelativeLayout) LayoutInflater.Inflate(Resource.Layout.entry_extrastring_value, null);
			var valueView = valueViewContainer.FindViewById<TextView>(Resource.Id.entry_extra);
		    var valueViewVisible = valueViewContainer.FindViewById<TextView>(Resource.Id.entry_extra_visible);
		    if (value != null)
		    {
		        valueView.Text = value;
                valueViewVisible.Text = value;

            }
		    SetPasswordTypeface(valueViewVisible);
		    if (isProtected)
		    {
		        RegisterProtectedTextView(key, valueView, valueViewVisible);
                
		    }
		    else
		    {
		        valueView.Visibility = ViewStates.Gone;
		    }

			layout.AddView(valueViewContainer);
			var stringView = new ExtraStringView(layout, valueView, valueViewVisible, keyView);

			_stringViews.Add(key, stringView);
			RegisterTextPopup(valueViewContainer, valueViewContainer.FindViewById(Resource.Id.extra_vdots), key, isProtected);

			return stringView;

		}



		private List<IPopupMenuItem> RegisterPopup(string popupKey, View clickView, View anchorView)
		{
			clickView.Click += (sender, args) =>
				{
					ShowPopup(anchorView, popupKey);
				};
			_popupMenuItems[popupKey] = new List<IPopupMenuItem>();
			return _popupMenuItems[popupKey];
		}

	    internal Uri WriteBinaryToFile(string key, bool writeToCacheDirectory)
	    {
	        ProtectedBinary pb = Entry.Binaries.Get(key);
	        System.Diagnostics.Debug.Assert(pb != null);
	        if (pb == null)
	            throw new ArgumentException();


	        ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
	        
	        if (writeToCacheDirectory)
	        {
	            string binaryDirectory = CacheDir.Path + File.Separator + AttachmentContentProvider.AttachmentCacheSubDir;

	            string filepart = key;
	            Java.Lang.String javaFilename = new Java.Lang.String(filepart);
	            filepart = javaFilename.ReplaceAll("[^a-zA-Z0-9.-]", "_");

	            var targetFile = new File(binaryDirectory, filepart);

	            File parent = targetFile.ParentFile;

	            if (parent == null || (parent.Exists() && !parent.IsDirectory))
	            {
	                Toast.MakeText(this,
	                    Resource.String.error_invalid_path,
	                    ToastLength.Long).Show();
	                return null;
	            }

	            if (!parent.Exists())
	            {
	                // Create parent directory
	                if (!parent.Mkdirs())
	                {
	                    Toast.MakeText(this,
	                        Resource.String.error_could_not_create_parent,
	                        ToastLength.Long).Show();
	                    return null;

	                }
	            }
	            string filename = targetFile.AbsolutePath;

	            byte[] pbData = pb.ReadData();
	            try
	            {
	                System.IO.File.WriteAllBytes(filename, pbData);
	            }
	            catch (Exception exWrite)
	            {
	                Toast.MakeText(this,
	                    GetString(Resource.String.SaveAttachment_Failed, new Java.Lang.Object[] {filename})
	                    + exWrite.Message, ToastLength.Long).Show();
	                return null;
	            }
	            finally
	            {
	                MemUtil.ZeroByteArray(pbData);
	            }
	            Toast.MakeText(this,
	                GetString(Resource.String.SaveAttachment_doneMessage, new Java.Lang.Object[] {filename}),
	                ToastLength.Short).Show();
	            return Uri.Parse("content://" + AttachmentContentProvider.Authority + "/"
	                             + filename);
	        }
	        else
	        {
	            _exportBinaryProcessManager =
	                new ExportBinaryProcessManager(requestCodeSelFileStorageForWriteAttachment, this, key);
                _exportBinaryProcessManager.StartProcess();
	            return null;
	        }
	    
    	    
		}

		internal void OpenBinaryFile(Android.Net.Uri uri)
		{


			String theMimeType = GetMimeType(uri.Path);
			if (theMimeType != null)
			{

				Intent theIntent = new Intent(Intent.ActionView);
				theIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ExcludeFromRecents);
				theIntent.SetDataAndType(uri, theMimeType);
				try
				{
					StartActivity(theIntent);
				}
				catch (ActivityNotFoundException)
				{
					//ignore
					Toast.MakeText(this, "Couldn't open file", ToastLength.Short).Show();
				}
			}

		}



		private void RegisterProtectedTextView(string fieldKey, TextView protectedTextView, TextView visibleTextView)
		{
            if (!_showPassword.ContainsKey(protectedTextView))
            {
                _showPassword[protectedTextView] = fieldKey == UpdateTotpTimerTask.TotpKey ? _showTotpDefault : _showPasswordDefault;
            }
		    var protectedTextviewGroup = new ProtectedTextviewGroup { ProtectedField = protectedTextView, VisibleProtectedField = visibleTextView};
		    _protectedTextViews.Add(protectedTextviewGroup);
            SetPasswordStyle(protectedTextviewGroup);
		}


		private void PopulateBinaries()
		{
			ViewGroup binariesGroup = (ViewGroup) FindViewById(Resource.Id.binaries);
			foreach (KeyValuePair<string, ProtectedBinary> pair in Entry.Binaries)
			{
				String key = pair.Key;


				RelativeLayout valueViewContainer =
					(RelativeLayout) LayoutInflater.Inflate(Resource.Layout.entry_extrastring_value, null);
				var valueView = valueViewContainer.FindViewById<TextView>(Resource.Id.entry_extra);
				if (key != null)
					valueView.Text = key;

				string popupKey = Strings.PrefixBinary + key;

				var itemList = RegisterPopup(popupKey, valueViewContainer, valueViewContainer.FindViewById(Resource.Id.extra_vdots));
				itemList.Add(new WriteBinaryToFilePopupItem(key, this));
				itemList.Add(new OpenBinaryPopupItem(key, this));
				itemList.Add(new ViewImagePopupItem(key, this));




				binariesGroup.AddView(valueViewContainer);
				/*
				Button binaryButton = new Button(this);
				RelativeLayout.LayoutParams layoutParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
				binaryButton.Text = key;
				binaryButton.SetCompoundDrawablesWithIntrinsicBounds( Resources.GetDrawable(Android.Resource.Drawable.IcMenuSave),null, null, null);
				binaryButton.Click += (sender, e) => 
				{
					Button btnSender = (Button)(sender);

					AlertDialog.Builder builder = new AlertDialog.Builder(this);
					builder.SetTitle(GetString(Resource.String.SaveAttachmentDialog_title));
					
					builder.SetMessage(GetString(Resource.String.SaveAttachmentDialog_text));
					
					builder.SetPositiveButton(GetString(Resource.String.SaveAttachmentDialog_save), (dlgSender, dlgEvt) => 
					                                                                                                                    {
							
						});
					
					builder.SetNegativeButton(GetString(Resource.String.SaveAttachmentDialog_open), (dlgSender, dlgEvt) => 
					                                                                                                                   {
							
						});

					Dialog dialog = builder.Create();
					dialog.Show();


				};
				binariesGroup.AddView(binaryButton,layoutParams);
				*/

			}
			FindViewById(Resource.Id.entry_binaries_label).Visibility = Entry.Binaries.Any() ? ViewStates.Visible : ViewStates.Gone;
		}

		// url = file path or whatever suitable URL you want.
		public static String GetMimeType(String url)
		{
			String type = null;
			String extension = MimeTypeMap.GetFileExtensionFromUrl(url);
			if (extension != null)
			{
				MimeTypeMap mime = MimeTypeMap.Singleton;
				type = mime.GetMimeTypeFromExtension(extension.ToLowerInvariant());
			}
			return type;
		}

		public override void OnBackPressed()
		{
			base.OnBackPressed();
			//OverridePendingTransition(Resource.Animation.anim_enter_back, Resource.Animation.anim_leave_back);
		}

		protected void FillData()
		{
			_protectedTextViews = new List<ProtectedTextviewGroup>();
			ImageView iv = (ImageView) FindViewById(Resource.Id.icon);
			if (iv != null)
			{
				iv.SetImageDrawable(Resources.GetDrawable(Resource.Drawable.ic00));
			}



            SupportActionBar.Title = Entry.Strings.ReadSafe(PwDefs.TitleField);
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetHomeButtonEnabled(true);

			PopulateGroupText (Resource.Id.entry_group_name, Resource.Id.entryfield_group_container, KeyGroupFullPath);

			PopulateStandardText(Resource.Id.entry_user_name, Resource.Id.entryfield_container_username, PwDefs.UserNameField);
			PopulateStandardText(Resource.Id.entry_url, Resource.Id.entryfield_container_url, PwDefs.UrlField);
			PopulateStandardText(new List<int> { Resource.Id.entry_password, Resource.Id.entry_password_visible}, Resource.Id.entryfield_container_password, PwDefs.PasswordField);
		    
            RegisterProtectedTextView(PwDefs.PasswordField, FindViewById<TextView>(Resource.Id.entry_password), FindViewById<TextView>(Resource.Id.entry_password_visible));

			RegisterTextPopup(FindViewById<RelativeLayout> (Resource.Id.groupname_container),
				              FindViewById (Resource.Id.entry_group_name), KeyGroupFullPath);

			RegisterTextPopup(FindViewById<RelativeLayout>(Resource.Id.username_container),
			                  FindViewById(Resource.Id.username_vdots), PwDefs.UserNameField);

			RegisterTextPopup(FindViewById<RelativeLayout>(Resource.Id.url_container),
			                  FindViewById(Resource.Id.url_vdots), PwDefs.UrlField)
				.Add(new GotoUrlMenuItem(this, PwDefs.UrlField));
			RegisterTextPopup(FindViewById<RelativeLayout>(Resource.Id.password_container),
			                  FindViewById(Resource.Id.password_vdots), PwDefs.PasswordField);


			PopulateText(Resource.Id.entry_created, Resource.Id.entryfield_container_created, getDateTime(Entry.CreationTime));
			PopulateText(Resource.Id.entry_modified, Resource.Id.entryfield_container_modified, getDateTime(Entry.LastModificationTime));

			if (Entry.Expires)
			{
				PopulateText(Resource.Id.entry_expires, Resource.Id.entryfield_container_expires, getDateTime(Entry.ExpiryTime));

			}
			else
			{
				PopulateText(Resource.Id.entry_expires, Resource.Id.entryfield_container_expires, null);
			}
			PopulateStandardText(Resource.Id.entry_comment, Resource.Id.entryfield_container_comment, PwDefs.NotesField);
			RegisterTextPopup(FindViewById<RelativeLayout>(Resource.Id.comment_container),
							  FindViewById(Resource.Id.comment_vdots), PwDefs.NotesField);
                              
			PopulateText(Resource.Id.entry_tags, Resource.Id.entryfield_container_tags, concatTags(Entry.Tags));
			PopulateText(Resource.Id.entry_override_url, Resource.Id.entryfield_container_overrideurl, Entry.OverrideUrl);

			PopulateExtraStrings();

			PopulateBinaries();

            PopulatePreviousVersions();

			SetPasswordStyle();
		}

        private void PopulatePreviousVersions()
        {
			ViewGroup historyGroup = (ViewGroup)FindViewById(Resource.Id.previous_versions);
            int index = 0;
			foreach (var previousVersion in Entry.History)
			{
				


                Button btn = new Button(this);
                btn.Text = getDateTime(previousVersion.LastModificationTime);

				//copy variable from outer scope for capturing it below.
                var index1 = index;
                btn.Click += (sender, args) =>
                {
                    EntryActivity.Launch(this, this.Entry, this._pos, this.AppTask, null, index1);
                };

				historyGroup.AddView(btn);

                index++;


            }
			FindViewById(Resource.Id.entry_history_container).Visibility = Entry.History.Any() ? ViewStates.Visible : ViewStates.Gone;
		}


        protected override void OnDestroy()
		{
			NotifyPluginsOnClose();
			if (_pluginActionReceiver != null)
				UnregisterReceiver(_pluginActionReceiver);
			if (_pluginFieldReceiver != null)
				UnregisterReceiver(_pluginFieldReceiver);
			base.OnDestroy();
		}

		private void NotifyPluginsOnClose()
		{
			Intent i = new Intent(Strings.ActionCloseEntryView);
			i.PutExtra(Strings.ExtraSender, PackageName);
			foreach (var plugin in new PluginDatabase(this).GetPluginsWithAcceptedScope(Strings.ScopeCurrentEntry))
			{
				i.SetPackage(plugin);
				SendBroadcast(i);
			}
		}
		private List<IPopupMenuItem> RegisterTextPopup(View container, View anchor, string fieldKey)
		{
			return RegisterTextPopup(container, anchor, fieldKey, Entry.Strings.GetSafe(fieldKey).IsProtected);
		}

		private List<IPopupMenuItem> RegisterTextPopup(View container, View anchor, string fieldKey, bool isProtected)
		{
			string popupKey = Strings.PrefixString + fieldKey;
			var popupItems = RegisterPopup(
				popupKey,
				container,
				anchor);
			popupItems.Add(new CopyToClipboardPopupMenuIcon(this, _stringViews[fieldKey], isProtected));
            if (isProtected)
            {
                var valueView = container.FindViewById<TextView>(fieldKey == PwDefs.PasswordField ? Resource.Id.entry_password : Resource.Id.entry_extra);
				popupItems.Add(new ToggleVisibilityPopupMenuItem(this, valueView));
            }

            if (fieldKey != PwDefs.UrlField //url already has a go-to-url menu
                && (_stringViews[fieldKey].Text.StartsWith(KeePass.AndroidAppScheme)
                    || _stringViews[fieldKey].Text.StartsWith("http://")
                    || _stringViews[fieldKey].Text.StartsWith("https://")))
            {
                popupItems.Add(new GotoUrlMenuItem(this, fieldKey));
			}
			return popupItems;
		}



		private void ShowPopup(View anchor, string popupKey)
		{
			//PopupMenu popupMenu = new PopupMenu(this, FindViewById(Resource.Id.entry_user_name));
			PopupMenu popupMenu = new PopupMenu(this, anchor);

			AccessManager.PreparePopup(popupMenu);
			int itemId = 0;
			foreach (IPopupMenuItem popupItem in _popupMenuItems[popupKey])
			{
				popupMenu.Menu.Add(0, itemId, 0, popupItem.Text)
				         .SetIcon(popupItem.Icon);
				itemId++;
			}

			popupMenu.MenuItemClick += delegate(object sender, PopupMenu.MenuItemClickEventArgs args)
				{
					_popupMenuItems[popupKey][args.Item.ItemId].HandleClick();
				};
			popupMenu.Show();
		}

		
		private void SetPasswordTypeface(TextView textView)
		{
			_passwordFont.ApplyTo(textView);	
		}

	    private void PopulateText(int viewId, int containerViewId, String text)
	    {
	        PopulateText(new List<int> {viewId}, containerViewId, text);
	    }


        private void PopulateText(List<int> viewIds, int containerViewId, String text)
		{
			View container = FindViewById(containerViewId);
		    foreach (int viewId in viewIds)
		    {
		        TextView tv = (TextView) FindViewById(viewId);
		        if (String.IsNullOrEmpty(text))
		        {
		            container.Visibility = tv.Visibility = ViewStates.Gone;
		        }
		        else
		        {
		            container.Visibility = tv.Visibility = ViewStates.Visible;
		            tv.Text = text;

		        }
		    }
		}

	    private void PopulateStandardText(int viewId, int containerViewId, String key)
	    {
	        PopulateStandardText(new List<int> {viewId}, containerViewId, key);
	    }


        private void PopulateStandardText(List<int> viewIds, int containerViewId, String key)
		{
			String value = Entry.Strings.ReadSafe(key);
			value = SprEngine.Compile(value, new SprContext(Entry, App.Kp2a.CurrentDb.KpDatabase, SprCompileFlags.All));
			PopulateText(viewIds, containerViewId, value);
			_stringViews.Add(key, new StandardStringView(viewIds, containerViewId, this));
		}

		private void PopulateGroupText(int viewId, int containerViewId, String key)
		{
			string groupName = null;
			if (PreferenceManager.GetDefaultSharedPreferences(this).GetBoolean(
				"ShowGroupInEntry", false))
			{
				groupName = Entry.ParentGroup.GetFullPath();
			}
			PopulateText(viewId, containerViewId, groupName);
			_stringViews.Add (key, new StandardStringView (new List<int>{viewId}, containerViewId, this));
		}

		private void RequiresRefresh()
		{
			Intent ret = new Intent();
			ret.PutExtra(KeyRefreshPos, _pos);
			AppTask.ToIntent(ret);
			SetResult(KeePass.ExitRefresh, ret);
		}
        
        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
			base.OnActivityResult(requestCode, resultCode, data);

            if (_exportBinaryProcessManager?.OnActivityResult(requestCode, resultCode, data) == true)
            {
                return;
            }

            AppTask appTask = null;
            if (AppTask.TryGetFromActivityResult(data, ref appTask))
            {
                
				//make sure app task is passed to calling activity.
				//the result code might be modified later.
				Intent retData = new Intent();
                AppTask = appTask;
				appTask.ToIntent(retData);
				SetResult(KeePass.ExitNormal, retData);	
			}

		
			

			if ( resultCode == KeePass.ExitRefresh || resultCode == KeePass.ExitRefreshTitle ) {
				if ( resultCode == KeePass.ExitRefreshTitle ) {
					RequiresRefresh ();
				}
				Recreate();
			}
		}


	    public class WriteBinaryTask : RunnableOnFinish
	    {
	        private readonly IKp2aApp _app;
	        private readonly ProtectedBinary _data;
	        private IOConnectionInfo _targetIoc;

	        public WriteBinaryTask(Activity activity, IKp2aApp app, OnFinish onFinish, ProtectedBinary data, IOConnectionInfo targetIoc) : base(activity, onFinish)
	        {
	            _app = app;
	            _data = data;
	            _targetIoc = targetIoc;
	        }

	        public override void Run()
	        {
	            try
	            {
	                var fileStorage = _app.GetFileStorage(_targetIoc);
	                if (fileStorage is IOfflineSwitchable)
	                {
	                    ((IOfflineSwitchable)fileStorage).IsOffline = false;
	                }
	                using (var writeTransaction = fileStorage.OpenWriteTransaction(_targetIoc, _app.GetBooleanPreference(PreferenceKey.UseFileTransactions)))
	                {
	                    Stream sOut = writeTransaction.OpenFile();

	                    byte[] byteArray = _data.ReadData();
	                    sOut.Write(byteArray, 0, byteArray.Length);

                        sOut.Close();

	                    writeTransaction.CommitWrite();

	                }
	                if (fileStorage is IOfflineSwitchable)
	                {
	                    ((IOfflineSwitchable)fileStorage).IsOffline = App.Kp2a.OfflineMode;
	                }

	                Finish(true);


	            }
	            catch (Exception ex)
	            {
	                Finish(false, ex.Message);
	            }


	        }
	    }
        
	    private ExportBinaryProcessManager _exportBinaryProcessManager;
        private bool _showPasswordDefault;
        private bool _showTotpDefault;
        private int _historyIndex;

        protected override void OnSaveInstanceState(Bundle outState)
	    {
	        
	        
	        _exportBinaryProcessManager?.OnSaveInstanceState(outState);
	        
	        base.OnSaveInstanceState(outState);
	    }

	    public override bool OnCreateOptionsMenu(IMenu menu)
		{
			_menu = menu;
			base.OnCreateOptionsMenu(menu);

			MenuInflater inflater = MenuInflater;
			inflater.Inflate(Resource.Menu.entry, menu);

			lock (_pendingMenuOptions)
			{
				foreach (var option in _pendingMenuOptions)
					AddMenuOption(option);
				_pendingMenuOptions.Clear();
			}


			UpdateTogglePasswordMenu();

			return true;
		}

		public override bool OnPrepareOptionsMenu(IMenu menu)
		{
			Util.PrepareDonateOptionMenu(menu, this);
			return base.OnPrepareOptionsMenu(menu);
		}

        

		

		private void UpdateTogglePasswordMenu()
		{
			IMenuItem togglePassword = _menu.FindItem(Resource.Id.menu_toggle_pass);
			if (_showPassword.Values.All(x => x))
			{
				togglePassword.SetTitle(Resource.String.menu_hide_password);
			}
			else
			{
				togglePassword.SetTitle(Resource.String.show_password);
			}
		}

		private void SetPasswordStyle()
		{
			foreach (ProtectedTextviewGroup group in _protectedTextViews)
            {
                SetPasswordStyle(group);
            }
        }

        private void SetPasswordStyle(ProtectedTextviewGroup group)
        {
            bool showPassword = _showPassword.GetValueOrDefault(group.ProtectedField, _showPasswordDefault);
            group.VisibleProtectedField.Visibility = showPassword ? ViewStates.Visible : ViewStates.Gone;
            group.ProtectedField.Visibility = !showPassword ? ViewStates.Visible : ViewStates.Gone;

            SetPasswordTypeface(group.VisibleProtectedField);

            group.ProtectedField.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
        }

        protected override void OnResume()
		{
			ClearCache();
			base.OnResume();
			_activityDesign.ReapplyTheme();
		}

		public void ClearCache()
		{
			try
			{
				File dir = new File(CacheDir.Path + File.Separator + AttachmentContentProvider.AttachmentCacheSubDir);
				if (dir.IsDirectory)
				{
					IoUtil.DeleteDir(dir);
				}
			}
			catch (Exception)
			{

			}
		}

		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			//check if this is a plugin action
			if ((item.Intent != null) && (item.Intent.Action == Strings.ActionEntryActionSelected))
			{
				//yes. let the plugin handle the click:
				SendBroadcast(item.Intent);
				return true;
			}

			switch (item.ItemId)
			{
				case Resource.Id.menu_donate:
					return Util.GotoDonateUrl(this);
                case Resource.Id.menu_move:
					var navMove = new NavigateToFolderAndLaunchMoveElementTask(App.Kp2a.CurrentDb, Entry.ParentGroup, new List<PwUuid>() {Entry.Uuid}, false);
                    AppTask = navMove;
					navMove.SetActivityResult(this, Result.Ok);
                    Finish();
                    return true;
				case Resource.Id.menu_delete:
                    DeleteEntry task = new DeleteEntry(this, App.Kp2a, Entry,
                        new ActionOnFinish(this, (success, message, activity) => { if (success) { RequiresRefresh(); Finish();}}));
                    task.Start();
                    break;
                case Resource.Id.menu_toggle_pass:
					if (_showPassword.Values.All(x => x))
					{
						item.SetTitle(Resource.String.show_password);
						foreach (var k in _showPassword.Keys.ToList())
						    _showPassword[k] = false;
					}
					else
					{
						item.SetTitle(Resource.String.menu_hide_password);
                        foreach (var k in _showPassword.Keys.ToList())
                            _showPassword[k] = true;
					}
					SetPasswordStyle();

					return true;

				case Resource.Id.menu_lock:
					App.Kp2a.Lock();
					return true;
				case Android.Resource.Id.Home:
					//Currently the action bar only displays the home button when we come from a previous activity.
					//So we can simply Finish. See this page for information on how to do this in more general (future?) cases:
					//http://developer.android.com/training/implementing-navigation/ancestral.html
					Finish();
					//OverridePendingTransition(Resource.Animation.anim_enter_back, Resource.Animation.anim_leave_back);

					return true;
			}


			return base.OnOptionsItemSelected(item);
		}

		
		
		internal void AddUrlToEntry(string url, Action<EntryActivity> finishAction)
		{
			PwEntry initialEntry = Entry.CloneDeep();

			PwEntry newEntry = Entry;
			newEntry.History = newEntry.History.CloneDeep();
			newEntry.CreateBackup(null);

			newEntry.Touch(true, false); // Touch *after* backup

			//if there is no URL in the entry, set that field. If it's already in use, use an additional (not existing) field
			if (!url.StartsWith(KeePass.AndroidAppScheme) && String.IsNullOrEmpty(newEntry.Strings.ReadSafe(PwDefs.UrlField)))
			{
				newEntry.Strings.Set(PwDefs.UrlField, new ProtectedString(false, url));
			}
			else
            {
                Util.SetNextFreeUrlField(newEntry, url);

                
			}

			//save the entry:

			ActionOnFinish closeOrShowError = new ActionOnFinish(this, (success, message, activity) =>
			{
				OnFinish.DisplayMessage(this, message, true);
			    finishAction((EntryActivity)activity);
			});


			RunnableOnFinish runnable = new UpdateEntry(this, App.Kp2a, initialEntry, newEntry, closeOrShowError);

			ProgressTask pt = new ProgressTask(App.Kp2a, this, runnable);
			pt.Run();

		}

        public bool GetVisibilityForProtectedView(TextView protectedView)
        {
            if (protectedView == null)
            {
                return _showPasswordDefault;
            }
            if (_showPassword.ContainsKey(protectedView) == false)
            {
                _showPassword[protectedView] = _showPasswordDefault;
            }

            return _showPassword[protectedView];
        }

		public void ToggleVisibility(TextView valueView)
		{
            
            _showPassword[valueView] = !GetVisibilityForProtectedView(valueView);
			SetPasswordStyle();
			UpdateTogglePasswordMenu();
		}


		public bool GotoUrl(string urlFieldKey)
        {
            string url = _stringViews[urlFieldKey].Text;
			if (url == null) return false;

			// Default http:// if no protocol specified
			if ((!url.Contains(":") || (url.StartsWith("www."))))
			{
				url = "http://" + url;
			}

			try
			{
				Util.GotoUrl(this, url);
			}
			catch (ActivityNotFoundException)
			{
				Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
			}
			return true;
		}

		public void AddEntryToIntent(Intent intent)
		{
			PluginHost.AddEntryToIntent(intent, App.Kp2a.LastOpenedEntry);
		}

		public void CloseAfterTaskComplete()
		{
			//before closing, wait a little to get plugin updates
			int numPlugins = new PluginDatabase(this).GetPluginsWithAcceptedScope(Strings.ScopeCurrentEntry).Count();
			var timeToWait = TimeSpan.FromMilliseconds(500*numPlugins);
			SetProgressBarIndeterminateVisibility(true);
			_timer = new Timer(obj =>
				{
					RunOnUiThread(() =>
						{
							//task is completed -> return NullTask
							Intent resIntent = new Intent();
							new NullTask().ToIntent(resIntent);
							SetResult(KeePass.ExitCloseAfterTaskComplete, resIntent);
							//close activity:
							Finish();
						}
						);
				},
				null, timeToWait, TimeSpan.FromMilliseconds(-1));
		}

		public void ShowAttachedImage(string key)
		{
			ProtectedBinary pb = Entry.Binaries.Get(key);
			System.Diagnostics.Debug.Assert(pb != null);
			if (pb == null)
				throw new ArgumentException();
			byte[] pbData = pb.ReadData();		

			Intent imageViewerIntent = new Intent(this, typeof(ImageViewActivity));
			imageViewerIntent.PutExtra("EntryId", new ElementAndDatabaseId(App.Kp2a.CurrentDb, Entry).FullId);
			imageViewerIntent.PutExtra("EntryKey", key);
			StartActivity(imageViewerIntent);
		}
	}
}