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
using Android.Content.PM;
using Android.Webkit;
using Android.Graphics;
using Java.IO;
using KeePassLib;
using KeePassLib.Security;
using KeePassLib.Utility;
using Keepass2android.Pluginsdk;
using PluginHostTest;
using keepass2android.Io;
using Uri = Android.Net.Uri;

namespace keepass2android
{

	[Activity (Label = "@string/app_name", ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.KeyboardHidden, Theme="@style/NoTitleBar")]			
	public class EntryActivity : LockCloseActivity 
	{
		public const String KeyEntry = "entry";
		public const String KeyRefreshPos = "refresh_pos";
		public const String KeyCloseAfterCreate = "close_after_create";

		public static void Launch(Activity act, PwEntry pw, int pos, AppTask appTask, ActivityFlags? flags = null)
		{
			Intent i = new Intent(act, typeof(EntryActivity));

			i.PutExtra(KeyEntry, pw.Uuid.ToHexString());
			i.PutExtra(KeyRefreshPos, pos);

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
			
		}

		protected PwEntry Entry;

		private static Typeface _passwordFont;

		internal bool _showPassword;
		private int _pos;

		AppTask _appTask;
		private List<TextView> _protectedTextViews;
		private IMenu _menu;

		private readonly Dictionary<string, List<IPopupMenuItem>> _popupMenuItems =
			new Dictionary<string, List<IPopupMenuItem>>();

		private readonly Dictionary<string, IStringView> _stringViews = new Dictionary<string, IStringView>();
		private readonly List<PluginMenuOption> _pendingMenuOptions = new List<PluginMenuOption>();
		
		//make sure _timer doesn't go out of scope:
		private Timer _timer;


		protected void SetEntryView()
		{
			SetContentView(Resource.Layout.entry_view);
		}

		protected void SetupEditButtons() {
			View edit =  FindViewById(Resource.Id.entry_edit);
			if (App.Kp2a.GetDb().CanWrite)
			{
				edit.Visibility = ViewStates.Visible;
				edit.Click += (sender, e) =>
				{
					EntryEditActivity.Launch(this, Entry, _appTask);
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
			App.Kp2a.GetDb().LastOpenedEntry.OutputStrings.Set(key, new ProtectedString(isProtected, value));
			Intent updateKeyboardIntent = new Intent(this, typeof(CopyToClipboardService));
			Intent.SetAction(Intents.UpdateKeyboard);
			updateKeyboardIntent.PutExtra(KeyEntry, Entry.Uuid.ToHexString());
			StartService(updateKeyboardIntent);

			//notify plugins
			NotifyPluginsOnModification(Strings.PrefixString+key);
		}

		private void AddPluginAction(string pluginPackage, string fieldId, string popupItemId, string displayText, int iconId, Bundle bundleExtra)
		{
			if (fieldId != null)
			{
				try
				{
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
					Kp2aLog.Log(e.ToString());
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
				PluginHost.AddEntryToIntent(i, App.Kp2a.GetDb().LastOpenedEntry);

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

			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);

			long usageCount = prefs.GetLong(GetString(Resource.String.UsageCount_key), 0);

			ISharedPreferencesEditor edit = prefs.Edit();
			edit.PutLong(GetString(Resource.String.UsageCount_key), usageCount + 1);
			edit.Commit();

			_showPassword =
				!prefs.GetBoolean(GetString(Resource.String.maskpass_key), Resources.GetBoolean(Resource.Boolean.maskpass_default));

			base.OnCreate(savedInstanceState);
			RequestWindowFeature(WindowFeatures.IndeterminateProgress);

			new ActivityDesign(this).ApplyTheme();

			SetEntryView();

			Database db = App.Kp2a.GetDb();
			// Likely the app has been killed exit the activity 
			if (!db.Loaded || (App.Kp2a.QuickLocked))
			{
				Finish();
				return;
			}

			SetResult(KeePass.ExitNormal);

			Intent i = Intent;
			PwUuid uuid = new PwUuid(MemUtil.HexStringToByteArray(i.GetStringExtra(KeyEntry)));
			_pos = i.GetIntExtra(KeyRefreshPos, -1);

			_appTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);

			Entry = db.Entries[uuid];

			// Refresh Menu contents in case onCreateMenuOptions was called before Entry was set
			ActivityCompat.InvalidateOptionsMenu(this);

			// Update last access time.
			Entry.Touch(false);

			if (PwDefs.IsTanEntry(Entry) && prefs.GetBoolean(GetString(Resource.String.TanExpiresOnUse_key), Resources.GetBoolean(Resource.Boolean.TanExpiresOnUse_default)) && ((Entry.Expires == false) || Entry.ExpiryTime > DateTime.Now))
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

			App.Kp2a.GetDb().LastOpenedEntry = new PwEntryOutput(Entry, App.Kp2a.GetDb().KpDatabase);

			RegisterReceiver(new PluginActionReceiver(this), new IntentFilter(Strings.ActionAddEntryAction));
			RegisterReceiver(new PluginFieldReceiver(this), new IntentFilter(Strings.ActionSetEntryField));

			new Thread(NotifyPluginsOnOpen).Start();

			//the rest of the things to do depends on the current app task:
			_appTask.CompleteOnCreateEntryActivity(this);
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

		

		internal void StartNotificationsService(bool closeAfterCreate)
		{
			Intent showNotIntent = new Intent(this, typeof (CopyToClipboardService));
			showNotIntent.SetAction(Intents.ShowNotification);
			showNotIntent.PutExtra(KeyEntry, Entry.Uuid.ToHexString());

			showNotIntent.PutExtra(KeyCloseAfterCreate, closeAfterCreate);

			StartService(showNotIntent);
		}


		private String getDateTime(DateTime dt)
		{
			return dt.ToString("g", CultureInfo.CurrentUICulture);
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
			foreach (var pair in Entry.Strings.Where(pair => !PwDefs.IsStandardField(pair.Key)).OrderBy(pair => pair.Key))
			{
				var stringView = CreateExtraSection(pair.Key, pair.Value.ReadString(), pair.Value.IsProtected);
				extraGroup.AddView(stringView.View);
			}

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
			if (value != null)
				valueView.Text = value;
			SetPasswordTypeface(valueView);
			if (isProtected)
			{
				RegisterProtectedTextView(valueView);
				valueView.TransformationMethod = PasswordTransformationMethod.Instance;
			}

			layout.AddView(valueViewContainer);
			var stringView = new ExtraStringView(layout, valueView, keyView);

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
			string binaryDirectory = prefs.GetString(GetString(Resource.String.BinaryDirectory_key), GetString(Resource.String.BinaryDirectory_default));
			if (writeToCacheDirectory)
				binaryDirectory = CacheDir.Path + File.Separator + AttachmentContentProvider.AttachmentCacheSubDir;

			string filepart = key;
			if (writeToCacheDirectory)
				filepart = filepart.Replace(" ", "");
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
			Uri fileUri = Uri.FromFile(targetFile);

			byte[] pbData = pb.ReadData();
			try
			{
				System.IO.File.WriteAllBytes(filename, pbData);
			}
			catch (Exception exWrite)
			{
				Toast.MakeText(this, GetString(Resource.String.SaveAttachment_Failed, new Java.Lang.Object[] { filename })
					+ exWrite.Message, ToastLength.Long).Show();
				return null;
			}
			finally
			{
				MemUtil.ZeroByteArray(pbData);
			}
			Toast.MakeText(this, GetString(Resource.String.SaveAttachment_doneMessage, new Java.Lang.Object[] { filename }), ToastLength.Short).Show();
			if (writeToCacheDirectory)
			{
				return Uri.Parse("content://" + AttachmentContentProvider.Authority + "/"
											  + filename);
			}
			return fileUri;
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



		private void RegisterProtectedTextView(TextView protectedTextView)
		{
			_protectedTextViews.Add(protectedTextView);
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
				type = mime.GetMimeTypeFromExtension(extension);
			}
			return type;
		}

		public override void OnBackPressed()
		{
			base.OnBackPressed();
			OverridePendingTransition(Resource.Animation.anim_enter_back, Resource.Animation.anim_leave_back);
		}

		protected void FillData()
		{
			_protectedTextViews = new List<TextView>();
			ImageView iv = (ImageView) FindViewById(Resource.Id.entry_icon);
			if (iv != null)
			{
				iv.SetImageDrawable(Resources.GetDrawable(Resource.Drawable.ic00));
			}



			ActionBar.Title = Entry.Strings.ReadSafe(PwDefs.TitleField);
			ActionBar.SetDisplayHomeAsUpEnabled(true);

			PopulateStandardText(Resource.Id.entry_user_name, Resource.Id.entryfield_container_username, PwDefs.UserNameField);
			PopulateStandardText(Resource.Id.entry_url, Resource.Id.entryfield_container_url, PwDefs.UrlField);
			PopulateStandardText(Resource.Id.entry_password, Resource.Id.entryfield_container_password, PwDefs.PasswordField);
			RegisterProtectedTextView(FindViewById<TextView>(Resource.Id.entry_password));
			SetPasswordTypeface(FindViewById<TextView>(Resource.Id.entry_password));

			RegisterTextPopup(FindViewById<RelativeLayout>(Resource.Id.username_container),
			                  FindViewById(Resource.Id.username_vdots), PwDefs.UserNameField);
			RegisterTextPopup(FindViewById<RelativeLayout>(Resource.Id.url_container),
			                  FindViewById(Resource.Id.url_vdots), PwDefs.UrlField)
				.Add(new GotoUrlMenuItem(this));
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
			PopulateText(Resource.Id.entry_tags, Resource.Id.entryfield_container_tags, concatTags(Entry.Tags));
			PopulateText(Resource.Id.entry_override_url, Resource.Id.entryfield_container_overrideurl, Entry.OverrideUrl);

			PopulateExtraStrings();

			PopulateBinaries();

			SetPasswordStyle();
		}

		

		protected override void OnDestroy()
		{
			NotifyPluginsOnClose();
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
			popupItems.Add(new CopyToClipboardPopupMenuIcon(this, _stringViews[fieldKey]));
			if (isProtected)
				popupItems.Add(new ToggleVisibilityPopupMenuItem(this));
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
			if (_passwordFont == null)
				_passwordFont = Typeface.CreateFromAsset(Assets, "DejaVuSansMono.ttf");
			textView.Typeface = _passwordFont;
		}

		private void PopulateText(int viewId, int containerViewId, String text)
		{
			View container = FindViewById(containerViewId);
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

		private void PopulateStandardText(int viewId, int containerViewId, String key)
		{
			PopulateText(viewId, containerViewId, Entry.Strings.ReadSafe(key));
			_stringViews.Add(key, new StandardStringView(viewId, containerViewId, this));
		}

		private void RequiresRefresh()
		{
			Intent ret = new Intent();
			ret.PutExtra(KeyRefreshPos, _pos);
			_appTask.ToIntent(ret);
			SetResult(KeePass.ExitRefresh, ret);
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
			base.OnActivityResult(requestCode, resultCode, data);
			if (AppTask.TryGetFromActivityResult(data, ref _appTask))
			{
				//make sure app task is passed to calling activity.
				//the result code might be modified later.
				Intent retData = new Intent();
				_appTask.ToIntent(retData);
				SetResult(KeePass.ExitNormal, retData);	
			}

		
			

			if ( resultCode == KeePass.ExitRefresh || resultCode == KeePass.ExitRefreshTitle ) {
				if ( resultCode == KeePass.ExitRefreshTitle ) {
					RequiresRefresh ();
				}
				Recreate();
			}
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

			IMenuItem gotoUrl = menu.FindItem(Resource.Id.menu_goto_url);
			//Disabled IMenuItem copyUser = menu.FindItem(Resource.Id.menu_copy_user);
			//Disabled IMenuItem copyPass = menu.FindItem(Resource.Id.menu_copy_pass);

			// In API >= 11 onCreateOptionsMenu may be called before onCreate completes
			// so _entry may not be set
			if (Entry == null)
			{
				gotoUrl.SetVisible(false);
				//Disabled copyUser.SetVisible(false);
				//Disabled copyPass.SetVisible(false);
			}
			else
			{
				String url = Entry.Strings.ReadSafe(PwDefs.UrlField);
				if (String.IsNullOrEmpty(url))
				{
					// disable button if url is not available
					gotoUrl.SetVisible(false);
				}
				if (String.IsNullOrEmpty(Entry.Strings.ReadSafe(PwDefs.UserNameField)))
				{
					// disable button if username is not available
					//Disabled copyUser.SetVisible(false);
				}
				if (String.IsNullOrEmpty(Entry.Strings.ReadSafe(PwDefs.PasswordField)))
				{
					// disable button if password is not available
					//Disabled copyPass.SetVisible(false);
				}
			}
			return true;
		}

		private void UpdateTogglePasswordMenu()
		{
			IMenuItem togglePassword = _menu.FindItem(Resource.Id.menu_toggle_pass);
			if (_showPassword)
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
			foreach (TextView password in _protectedTextViews)
			{

				if (_showPassword)
				{
					password.TransformationMethod = null;
				}
				else
				{
					password.TransformationMethod = PasswordTransformationMethod.Instance;
				}
			}
		}

		protected override void OnResume()
		{
			ClearCache();
			base.OnResume();
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
					try
					{
						Util.GotoDonateUrl(this);
					}
					catch (ActivityNotFoundException)
					{
						Toast.MakeText(this, Resource.String.error_failed_to_launch_link, ToastLength.Long).Show();
						return false;
					}

					return true;
				case Resource.Id.menu_toggle_pass:
					if (_showPassword)
					{
						item.SetTitle(Resource.String.show_password);
						_showPassword = false;
					}
					else
					{
						item.SetTitle(Resource.String.menu_hide_password);
						_showPassword = true;
					}
					SetPasswordStyle();

					return true;

				case Resource.Id.menu_goto_url:
					return GotoUrl();
			/* TODO: required?
			case Resource.Id.menu_copy_user:
				timeoutCopyToClipboard(_entry.Strings.ReadSafe (PwDefs.UserNameField));
				return true;
				
			case Resource.Id.menu_copy_pass:
				timeoutCopyToClipboard(_entry.Strings.ReadSafe (PwDefs.UserNameField));
				return true;
				*/
				case Resource.Id.menu_rate:
					try
					{
						Util.GotoMarket(this);
					}
					catch (ActivityNotFoundException)
					{
						Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
					}
					return true;
				case Resource.Id.menu_suggest_improvements:
					try
					{
						Util.GotoUrl(this, Resource.String.SuggestionsURL);
					}
					catch (ActivityNotFoundException)
					{
						Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
					}
					return true;
				case Resource.Id.menu_lock:
					return true;
				case Resource.Id.menu_translate:
					try
					{
						Util.GotoUrl(this, Resource.String.TranslationURL);
					}
					catch (ActivityNotFoundException)
					{
						Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
					}
					return true;
				case Android.Resource.Id.Home:
					//Currently the action bar only displays the home button when we come from a previous activity.
					//So we can simply Finish. See this page for information on how to do this in more general (future?) cases:
					//http://developer.android.com/training/implementing-navigation/ancestral.html
					Finish();
					OverridePendingTransition(Resource.Animation.anim_enter_back, Resource.Animation.anim_leave_back);

					return true;
			}


			return base.OnOptionsItemSelected(item);
		}

		
		
		internal void AddUrlToEntry(string url, Action finishAction)
		{
			PwEntry initialEntry = Entry.CloneDeep();

			PwEntry newEntry = Entry;
			newEntry.History = newEntry.History.CloneDeep();
			newEntry.CreateBackup(null);

			newEntry.Touch(true, false); // Touch *after* backup

			//if there is no URL in the entry, set that field. If it's already in use, use an additional (not existing) field
			if (String.IsNullOrEmpty(newEntry.Strings.ReadSafe(PwDefs.UrlField)))
			{
				newEntry.Strings.Set(PwDefs.UrlField, new ProtectedString(false, url));
			}
			else
			{
				int c = 1;
				while (newEntry.Strings.Get("KP2A_URL_" + c) != null)
				{
					c++;
				}

				newEntry.Strings.Set("KP2A_URL_" + c, new ProtectedString(false, url));
			}

			//save the entry:

			ActionOnFinish closeOrShowError = new ActionOnFinish((success, message) =>
			{
				OnFinish.DisplayMessage(this, message);
				finishAction();
			});


			RunnableOnFinish runnable = new UpdateEntry(this, App.Kp2a, initialEntry, newEntry, closeOrShowError);

			ProgressTask pt = new ProgressTask(App.Kp2a, this, runnable);
			pt.Run();

		}	
		public void ToggleVisibility()
		{
			_showPassword = !_showPassword;
			SetPasswordStyle();
			UpdateTogglePasswordMenu();
		}


		public bool GotoUrl()
		{
			string url = _stringViews[PwDefs.UrlField].Text;
			if (url == null) return false;

			// Default http:// if no protocol specified
			if (!url.Contains("://"))
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
			PluginHost.AddEntryToIntent(intent, App.Kp2a.GetDb().LastOpenedEntry);
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
	}
}