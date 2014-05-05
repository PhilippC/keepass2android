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
using Keepass2android.Pluginsdk;
using PluginHostTest;
using Uri = Android.Net.Uri;

namespace keepass2android
{

	[Activity(Label = "@string/app_name", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
		Theme = "@style/NoTitleBar")]
	public class EntryActivity : Activity
	{
		public const String KeyEntry = "entry";
		public const String KeyRefreshPos = "refresh_pos";
		public const String KeyCloseAfterCreate = "close_after_create";

		protected PwEntry Entry = new PwEntry(true, true);

		private static Typeface _passwordFont;

		internal bool _showPassword;
		private int _pos;

		private List<TextView> _protectedTextViews;

		private readonly Dictionary<string, List<IPopupMenuItem>> _popupMenuItems =
			new Dictionary<string, List<IPopupMenuItem>>();

		private readonly Dictionary<string, IStringView> _stringViews = new Dictionary<string, IStringView>();
		private readonly List<PluginMenuOption> _pendingMenuOptions = new List<PluginMenuOption>();
		private IMenu _menu;

		protected void SetEntryView()
		{
			SetContentView(Resource.Layout.entry_view);
		}

		protected void SetupEditButtons()
		{
			View edit = FindViewById(Resource.Id.entry_edit);
			if (true)
			{
				edit.Visibility = ViewStates.Visible;
				edit.Click += (sender, e) =>
					{

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

		}

		private void AddPluginAction(string pluginPackage, string fieldId, string displayText, int iconId, Bundle bundleExtra)
		{
			if (fieldId != null)
			{
				_popupMenuItems[fieldId].Add(new PluginPopupMenuItem(this, pluginPackage, fieldId, displayText, iconId, bundleExtra));
			}
			else
			{
				//we need to add an option to the  menu.
				//As it is not sure that OnCreateOptionsMenu was called yet, we cannot access _menu without a check:

				Intent i = new Intent(Strings.ActionEntryActionSelected);
				i.SetPackage(pluginPackage);
				i.PutExtra(Strings.ExtraActionData, bundleExtra);
				i.PutExtra(Strings.ExtraSender, PackageName);

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
						//						Util.GotoDonateUrl(this);
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
					string url = _stringViews[PwDefs.UrlField].Text;
					if (url == null) return false;

					// Default http:// if no protocol specified
					if (!url.Contains("://"))
					{
						url = "http://" + url;
					}

					try
					{

					}
					catch (ActivityNotFoundException)
					{
						Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
					}
					return true;
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
					}
					catch (ActivityNotFoundException)
					{
						Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
					}
					return true;
				case Resource.Id.menu_suggest_improvements:
					try
					{
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

					return true;
			}


			return base.OnOptionsItemSelected(item);
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

			Entry.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "philipp "));
			Entry.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, "password value"));
			Entry.Strings.Set(PwDefs.UrlField, new ProtectedString(false, "https://www.google.com"));
			Entry.Strings.Set("field header", new ProtectedString(true, "protected field value"));
			Entry.Strings.Set("public field header", new ProtectedString(false, "public field value"));

			base.OnCreate(savedInstanceState);
			SetEntryView();

			FillData();

			SetupEditButtons();

			RegisterReceiver(new PluginActionReceiver(this), new IntentFilter(Strings.ActionAddEntryAction));
			RegisterReceiver(new PluginFieldReceiver(this), new IntentFilter(Strings.ActionSetEntryField));

			new Thread(NotifyPluginsOnOpen).Start();
		}

		private void NotifyPluginsOnOpen()
		{
			App.Kp2A.GetDb().SetEntry(Entry);

			Intent i = new Intent(Strings.ActionOpenEntry);
			i.PutExtra(Strings.ExtraSender, PackageName);
			PluginHost.AddEntryToIntent(i, Entry);

			foreach (var plugin in new PluginDatabase(this).GetPluginsWithAcceptedScope(Strings.ScopeCurrentEntry))
			{
				i.SetPackage(plugin);
				SendBroadcast(i);
			}
		}

		public void CompleteOnCreate()
		{

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


		private void RegisterProtectedTextView(TextView protectedTextView)
		{
			_protectedTextViews.Add(protectedTextView);
		}


		private void PopulateBinaries()
		{
			ViewGroup binariesGroup = (ViewGroup) FindViewById(Resource.Id.binaries);
			foreach (KeyValuePair<string, string> pair in new Dictionary<string, string>() {{"abc", ""}, {"test.png", "uia"}})
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
			FindViewById(Resource.Id.entry_binaries_label).Visibility = true ? ViewStates.Visible : ViewStates.Gone;
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
		}

		protected void FillData()
		{
			_protectedTextViews = new List<TextView>();
			ImageView iv = (ImageView) FindViewById(Resource.Id.entry_icon);
			if (iv != null)
			{
				iv.SetImageDrawable(Resources.GetDrawable(Resource.Drawable.ic00));
			}



			ActionBar.Title = "Entry title";
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

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);
			if (resultCode == /*TODO*/ 0)
			{
				if (resultCode == /*TODO*/ 0)
				{
					RequiresRefresh();
				}
				Recreate();
			}
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

		private void ShowPopup(int resAnchor, string popupKey)
		{
			ShowPopup(FindViewById(resAnchor), popupKey);
		}

		private void SetPasswordTypeface(TextView textView)
		{

		}

		private void PopulateText(int viewId, int containerViewId, int resId)
		{
			View header = FindViewById(containerViewId);
			TextView tv = (TextView) FindViewById(viewId);

			header.Visibility = tv.Visibility = ViewStates.Visible;
			tv.SetText(resId);
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

			base.OnResume();
		}

		/// <summary>
		/// brings up a dialog asking the user whether he wants to add the given URL to the entry for automatic finding
		/// </summary>
		public void AskAddUrlThenCompleteCreate(string url)
		{
			AlertDialog.Builder builder = new AlertDialog.Builder(this);
			builder.SetTitle(GetString(Resource.String.AddUrlToEntryDialog_title));

			builder.SetMessage(GetString(Resource.String.AddUrlToEntryDialog_text, new Java.Lang.Object[] {url}));

			builder.SetPositiveButton(GetString(Resource.String.yes), (dlgSender, dlgEvt) =>
				{

				});

			builder.SetNegativeButton(GetString(Resource.String.no), (dlgSender, dlgEvt) =>
				{
					CompleteOnCreate();
				});

			Dialog dialog = builder.Create();
			dialog.Show();

		}

		public void ToggleVisibility()
		{
			_showPassword = !_showPassword;
			SetPasswordStyle();
			UpdateTogglePasswordMenu();
		}

		public Android.Net.Uri WriteBinaryToFile(string key, bool writeToCacheDirectory)
		{
			return Android.Net.Uri.Empty;
			//TODO
		}

		private void UpdateTogglePasswordMenu()
		{
			//todo use real method
		}

		public void GotoUrl()
		{
			//TODO
			
		}

		public void OpenBinaryFile(Uri newUri)
		{
			Toast.MakeText(this, "opening file TODO", ToastLength.Short).Show();
		}
	}
}