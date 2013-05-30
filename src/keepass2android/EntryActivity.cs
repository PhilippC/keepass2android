/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
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
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;
using Android.Text.Format;
using KeePassLib.Utility;
using Java.Util;
using Android.Preferences;
using Android.Text.Method;
using Android.Util;
using System.Globalization;
using Android.Content.PM;
using KeePassLib.Security;
using keepass2android.view;
using Android.Webkit;
using Android.Graphics;
using Java.IO;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.KeyboardHidden, Theme="@style/NoTitleBar")]			
	public class EntryActivity : LockCloseActivity {
		public const String KEY_ENTRY = "entry";
		public const String KEY_REFRESH_POS = "refresh_pos";
		public const String KEY_CLOSE_AFTER_CREATE = "close_after_create";


		public static void Launch(Activity act, PwEntry pw, int pos, AppTask appTask) {
			Intent i;
			
			i = new Intent(act, typeof(EntryActivity));
			
			
			i.PutExtra(KEY_ENTRY, pw.Uuid.ToHexString());
			i.PutExtra(KEY_REFRESH_POS, pos);

			appTask.ToIntent(i);
			
			act.StartActivityForResult(i,0);
		}

		protected PwEntry mEntry;
	
		private bool mShowPassword;
		private int mPos;

		AppTask mAppTask;
		

		protected void setEntryView() {
			SetContentView(Resource.Layout.entry_view);
		}
		
		protected void setupEditButtons() {
			View edit =  FindViewById(Resource.Id.entry_edit);
			edit.Click += (sender, e) => {
					EntryEditActivity.Launch(this, mEntry);
			};
		}
		
		protected override void OnCreate(Bundle savedInstanceState)
		{
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);

			long usageCount = prefs.GetLong(GetString(Resource.String.UsageCount_key), 0);

			ISharedPreferencesEditor edit = prefs.Edit();
			edit.PutLong(GetString(Resource.String.UsageCount_key), usageCount+1);
			EditorCompat.apply(edit);

			mShowPassword = ! prefs.GetBoolean(GetString(Resource.String.maskpass_key), Resources.GetBoolean(Resource.Boolean.maskpass_default));
			
			base.OnCreate(savedInstanceState);
			setEntryView();
			
			Context appCtx = ApplicationContext;


			Database db = App.getDB();
			// Likely the app has been killed exit the activity 
			if (! db.Loaded)
			{
				Finish();
				return;
			}
			
			SetResult(KeePass.EXIT_NORMAL);
			
			Intent i = Intent;
			PwUuid uuid = new PwUuid(MemUtil.HexStringToByteArray(i.GetStringExtra(KEY_ENTRY)));
			mPos = i.GetIntExtra(KEY_REFRESH_POS, -1);

			mAppTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);

			bool closeAfterCreate = mAppTask.CloseEntryActivityAfterCreate;

			mEntry = db.entries [uuid];

			// Refresh Menu contents in case onCreateMenuOptions was called before mEntry was set
			ActivityCompat.invalidateOptionsMenu(this);
			
			// Update last access time.
			mEntry.Touch(false);
			
			if (PwDefs.IsTanEntry(mEntry) && prefs.GetBoolean(GetString(Resource.String.TanExpiresOnUse_key), Resources.GetBoolean(Resource.Boolean.TanExpiresOnUse_default)) && ((mEntry.Expires == false) || mEntry.ExpiryTime > DateTime.Now))
			{
				PwEntry backupEntry = mEntry.CloneDeep();
				mEntry.ExpiryTime = DateTime.Now;
				mEntry.Expires = true;
				mEntry.Touch(true);
				requiresRefresh();
				Handler handler = new Handler();
				UpdateEntry update = new UpdateEntry(this, App.getDB(), backupEntry, mEntry, new AfterSave(handler));
				ProgressTask pt = new ProgressTask(this, update, Resource.String.saving_database);
				pt.run();
			}
			fillData(false);
			
			setupEditButtons();

			Intent showNotIntent = new Intent(this, typeof(CopyToClipboardService));
			Intent.SetAction(Intents.SHOW_NOTIFICATION);
			showNotIntent.PutExtra(KEY_ENTRY, mEntry.Uuid.ToHexString());
			showNotIntent.PutExtra(KEY_CLOSE_AFTER_CREATE, closeAfterCreate);

			StartService(showNotIntent);

			Android.Util.Log.Debug("DEBUG", "Requesting copy to clipboard for Uuid=" + mEntry.Uuid.ToHexString());

			/*foreach (PwUuid key in App.getDB().entries.Keys)
			{
				Android.Util.Log.Debug("DEBUG",key.ToHexString() + " -> " + App.getDB().entries[key].Uuid.ToHexString());
			}*/

			if (closeAfterCreate)
			{
				Finish();
			}
		}

		private class AfterSave : OnFinish {
			
			public AfterSave(Handler handler):base(handler) {
				
			}
			
			public override void run() {

				base.run();
			}
			
		};
		
		

		private String getDateTime(System.DateTime dt) {
			return dt.ToString ("g", CultureInfo.CurrentUICulture);
		}

		String concatTags(List<string> tags)
		{
			StringBuilder sb = new StringBuilder();
			foreach (string tag in tags)
			{
				sb.Append(tag);
				sb.Append(", ");
			}
			if (tags.Count > 0)
				sb.Remove(sb.Length-2,2);
			return sb.ToString();
		}

		void populateExtraStrings(bool trimList)
		{
			ViewGroup extraGroup = (ViewGroup)FindViewById(Resource.Id.extra_strings);
			if (trimList)
			{
				extraGroup.RemoveAllViews();
			}
			bool hasExtraFields = false;
			foreach (KeyValuePair<string, ProtectedString> pair in mEntry.Strings)
			{
				String key = pair.Key;
				if (!PwDefs.IsStandardField(key))
				{
					//View view = new EntrySection(this, null, key, pair.Value.ReadString());
					View view = CreateEditSection(key, pair.Value.ReadString());
					extraGroup.AddView(view);
					hasExtraFields = true;
				}
			}
			FindViewById(Resource.Id.entry_extra_strings_label).Visibility = hasExtraFields ? ViewStates.Visible : ViewStates.Gone;
		}

		View CreateEditSection(string key, string value)
		{
			LinearLayout layout = new LinearLayout(this, null);
			layout.Orientation = Orientation.Vertical;
			LinearLayout.LayoutParams layoutParams = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.FillParent, LinearLayout.LayoutParams.WrapContent);
			layoutParams.SetMargins(10,0,0,0);
			layout.LayoutParameters = layoutParams;
			View viewInflated = LayoutInflater.Inflate(Resource.Layout.entry_extrastring_title,null);
			TextView keyView = (TextView)viewInflated;
			if (key != null)
				keyView.Text = key;

			layout.AddView(keyView);
			TextView valueView = (TextView)LayoutInflater.Inflate(Resource.Layout.entry_extrastring_value, null);
			if (value != null)
				valueView.Text = value;
			valueView.Typeface = Typeface.Monospace;

			if ((int)Android.OS.Build.VERSION.SdkInt >= 11)
				valueView.SetTextIsSelectable(true);
			layout.AddView(valueView);
			return layout;
		}
		
		Android.Net.Uri writeBinaryToFile(string key, bool writeToCacheDirectory)
		{
			ProtectedBinary pb = mEntry.Binaries.Get(key);
			System.Diagnostics.Debug.Assert(pb != null);
			if (pb == null)
				throw new ArgumentException();

			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
			string binaryDirectory = prefs.GetString(GetString(Resource.String.BinaryDirectory_key), GetString(Resource.String.BinaryDirectory_default));
			if (writeToCacheDirectory)
				binaryDirectory = CacheDir.Path;
		
			var targetFile = new Java.IO.File(binaryDirectory, key);

			Java.IO.File parent = targetFile.ParentFile;
			
			if (parent == null || (parent.Exists() && ! parent.IsDirectory))
			{
				Toast.MakeText(this,
				               Resource.String.error_invalid_path,
				               ToastLength.Long).Show();
				return null;
			}
			
			if (! parent.Exists())
			{
				// Create parent directory
				if (! parent.Mkdirs())
				{
					Toast.MakeText(this,
					               Resource.String.error_could_not_create_parent,
					               ToastLength.Long).Show();
					return null;
					
				}
			}
			string filename = targetFile.AbsolutePath;
			Android.Net.Uri fileUri = Android.Net.Uri.FromFile(targetFile);

			byte[] pbData = pb.ReadData();
			try
			{
				System.IO.File.WriteAllBytes(filename, pbData);
			} catch (Exception exWrite)
			{
				Toast.MakeText(this, GetString(Resource.String.SaveAttachment_Failed, new Java.Lang.Object[]{ filename})
					+ exWrite.Message, ToastLength.Long).Show();
				return null;
			} finally
			{
				MemUtil.ZeroByteArray(pbData);
			}
			Toast.MakeText(this, GetString(Resource.String.SaveAttachment_doneMessage, new Java.Lang.Object[]{filename}), ToastLength.Short).Show();			
			if (writeToCacheDirectory)
			{
				return Android.Net.Uri.Parse("content://" + AttachmentContentProvider.AUTHORITY + "/"
				                              + filename);
			}
			else
			{
				return fileUri;
			}

		}

		void openBinaryFile(Android.Net.Uri uri)
		{
			String theMIMEType = getMimeType(uri.Path);
			if (theMIMEType != null)
			{
				Intent theIntent = new Intent(Intent.ActionView);
				theIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ExcludeFromRecents);
				theIntent.SetDataAndType(uri, theMIMEType);
				try
				{
					StartActivity(theIntent);
				}
				catch (ActivityNotFoundException anfe)
				{
					//ignore
					Toast.MakeText(this, "Couldn't open file", ToastLength.Short).Show();
				}
			}
		}

		void populateBinaries(bool trimList)
		{
			ViewGroup binariesGroup = (ViewGroup)FindViewById(Resource.Id.binaries);
			if (trimList)
			{
				binariesGroup.RemoveAllViews();
			}
			foreach (KeyValuePair<string, ProtectedBinary> pair in mEntry.Binaries)
			{
				String key = pair.Key;
				Button binaryButton = new Button(this);
				RelativeLayout.LayoutParams layoutParams = new RelativeLayout.LayoutParams(RelativeLayout.LayoutParams.FillParent, RelativeLayout.LayoutParams.WrapContent);
				binaryButton.Text = key;
				binaryButton.SetCompoundDrawablesWithIntrinsicBounds( Resources.GetDrawable(Android.Resource.Drawable.IcMenuSave),null, null, null);
				binaryButton.Click += (object sender, EventArgs e) => 
				{
					Button btnSender = (Button)(sender);

					AlertDialog.Builder builder = new AlertDialog.Builder(this);
					builder.SetTitle(GetString(Resource.String.SaveAttachmentDialog_title));
					
					builder.SetMessage(GetString(Resource.String.SaveAttachmentDialog_text));
					
					builder.SetPositiveButton(GetString(Resource.String.SaveAttachmentDialog_save), new EventHandler<DialogClickEventArgs>((dlgSender, dlgEvt) => 
					                                                                                                                    {
						writeBinaryToFile(btnSender.Text, false);
					}));
					
					builder.SetNegativeButton(GetString(Resource.String.SaveAttachmentDialog_open), new EventHandler<DialogClickEventArgs>((dlgSender, dlgEvt) => 
					                                                                                                                   {
						Android.Net.Uri newUri = writeBinaryToFile(btnSender.Text, true);
						if (newUri != null)
						{
							openBinaryFile(newUri);
						}
					}));

					Dialog dialog = builder.Create();
					dialog.Show();


				};
				binariesGroup.AddView(binaryButton,layoutParams);

				
			}
			FindViewById(Resource.Id.entry_binaries_label).Visibility = mEntry.Binaries.UCount > 0 ? ViewStates.Visible : ViewStates.Gone;
		}

		// url = file path or whatever suitable URL you want.
		public static String getMimeType(String url)
		{
			String type = null;
			String extension = MimeTypeMap.GetFileExtensionFromUrl(url);
			if (extension != null) {
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

		protected void fillData(bool trimList)
		{
			ImageView iv = (ImageView)FindViewById(Resource.Id.entry_icon);
			if (iv != null)
			{
			App.getDB().drawFactory.assignDrawableTo(iv, Resources, App.getDB().pm, mEntry.IconId, mEntry.CustomIconUuid);
			}
			
			//populateText(Resource.Id.entry_title, mEntry.Strings.ReadSafe(PwDefs.TitleField));
			var button = ((Button)FindViewById(Resource.Id.entry_title));
			if (button != null)
			{
			button.Text = mEntry.Strings.ReadSafe(PwDefs.TitleField);
			button.Click += (object sender, EventArgs e) => {
				Finish(); };
			}
			if (Util.HasActionBar(this))
			{
				ActionBar.Title = mEntry.Strings.ReadSafe(PwDefs.TitleField);
				ActionBar.SetDisplayHomeAsUpEnabled(true);
			}
			populateText(Resource.Id.entry_user_name, Resource.Id.entry_user_name_label, mEntry.Strings.ReadSafe(PwDefs.UserNameField));
			
			populateText(Resource.Id.entry_url, Resource.Id.entry_url_label, mEntry.Strings.ReadSafe(PwDefs.UrlField));
			populateText(Resource.Id.entry_password, Resource.Id.entry_password_label, mEntry.Strings.ReadSafe(PwDefs.PasswordField));
			setPasswordStyle();
			
			populateText(Resource.Id.entry_created, Resource.Id.entry_created_label, getDateTime(mEntry.CreationTime));
			populateText(Resource.Id.entry_modified, Resource.Id.entry_modified_label, getDateTime(mEntry.LastModificationTime));
			populateText(Resource.Id.entry_accessed, Resource.Id.entry_accessed_label, getDateTime(mEntry.LastAccessTime));
			
			if (mEntry.Expires)
			{
				populateText(Resource.Id.entry_expires, Resource.Id.entry_expires_label, getDateTime(mEntry.ExpiryTime));
			} else
			{
				populateText(Resource.Id.entry_expires, Resource.Id.entry_expires_label, Resource.String.never);
			}
			populateText(Resource.Id.entry_comment, Resource.Id.entry_comment_label, mEntry.Strings.ReadSafe(PwDefs.NotesField));

			populateText(Resource.Id.entry_tags, Resource.Id.entry_tags_label, concatTags(mEntry.Tags));

			populateText(Resource.Id.entry_override_url, Resource.Id.entry_override_url_label, mEntry.OverrideUrl);

			populateExtraStrings(trimList);

			populateBinaries(trimList);


		}
		
		private void populateText(int viewId, int headerViewId,int resId) {
			View header = FindViewById(headerViewId);
			TextView tv = (TextView)FindViewById(viewId);

			header.Visibility = tv.Visibility = ViewStates.Visible;
			tv.SetText (resId);
		}
		
		private void populateText(int viewId, int headerViewId, String text)
		{
			View header = FindViewById(headerViewId);
			TextView tv = (TextView)FindViewById(viewId);
			if (String.IsNullOrEmpty(text))
			{
				header.Visibility = tv.Visibility = ViewStates.Gone;
			}
			else
			{
				header.Visibility = tv.Visibility = ViewStates.Visible;
				tv.Text = text;

			}
		}

		void requiresRefresh ()
		{
			Intent ret = new Intent ();
			ret.PutExtra (KEY_REFRESH_POS, mPos);
			SetResult (KeePass.EXIT_REFRESH, ret);
		}
		
		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
			base.OnActivityResult(requestCode, resultCode, data);
			if ( resultCode == KeePass.EXIT_REFRESH || resultCode == KeePass.EXIT_REFRESH_TITLE ) {
				fillData(true);
				if ( resultCode == KeePass.EXIT_REFRESH_TITLE ) {
					requiresRefresh ();
				}
			}
		}
		
		public override bool OnCreateOptionsMenu(IMenu menu) {
			base.OnCreateOptionsMenu(menu);

			MenuInflater inflater = MenuInflater;
			inflater.Inflate(Resource.Menu.entry, menu);
			
			IMenuItem togglePassword = menu.FindItem(Resource.Id.menu_toggle_pass);
			if ( mShowPassword ) {
				togglePassword.SetTitle(Resource.String.menu_hide_password);
			} else {
				togglePassword.SetTitle(Resource.String.show_password);
			}

			IMenuItem gotoUrl = menu.FindItem(Resource.Id.menu_goto_url);
			//Disabled IMenuItem copyUser = menu.FindItem(Resource.Id.menu_copy_user);
			//Disabled IMenuItem copyPass = menu.FindItem(Resource.Id.menu_copy_pass);
			
			// In API >= 11 onCreateOptionsMenu may be called before onCreate completes
			// so mEntry may not be set
			if (mEntry == null) {
				gotoUrl.SetVisible(false);
				//Disabled copyUser.SetVisible(false);
				//Disabled copyPass.SetVisible(false);
			}
			else {
				String url = mEntry.Strings.ReadSafe (PwDefs.UrlField);
				if (String.IsNullOrEmpty(url)) {
					// disable button if url is not available
					gotoUrl.SetVisible(false);
				}
				if ( String.IsNullOrEmpty(mEntry.Strings.ReadSafe(PwDefs.UserNameField ))) {
					// disable button if username is not available
					//Disabled copyUser.SetVisible(false);
				}
				if ( String.IsNullOrEmpty(mEntry.Strings.ReadSafe(PwDefs.PasswordField ))) {
					// disable button if password is not available
					//Disabled copyPass.SetVisible(false);
				}
			}
			return true;
		}
		
		private void setPasswordStyle() {
			TextView password = (TextView) FindViewById(Resource.Id.entry_password);
			
			if ( mShowPassword ) {
				password.TransformationMethod = null;
			} else {
				password.TransformationMethod = PasswordTransformationMethod.Instance;
			}
		}
		protected override void OnResume()
		{
			ClearCache();
			base.OnResume();
		}
		
		public void ClearCache() {
			try {
				File dir = CacheDir;
				if (dir != null && dir.IsDirectory) {
					deleteDir(dir);
				}
			} catch (Exception e) {

			}
		}
		
		public static bool deleteDir(File dir) {
			if (dir != null && dir.IsDirectory) {
				String[] children = dir.List();
				for (int i = 0; i < children.Length; i++) {
					bool success = deleteDir(new File(dir, children[i]));
					if (!success) {
						return false;
					}
				}
			}
			
			// The directory is now empty so delete it
			return dir.Delete();
		}

		public override bool OnOptionsItemSelected(IMenuItem item) {
			switch ( item.ItemId ) {
			case Resource.Id.menu_donate:
				try {
						Util.gotoDonateUrl(this);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.error_failed_to_launch_link, ToastLength.Long).Show();
					return false;
				}
				
				return true;
			case Resource.Id.menu_toggle_pass:
				if ( mShowPassword ) {
					item.SetTitle(Resource.String.show_password);
					mShowPassword = false;
				} else {
					item.SetTitle(Resource.String.menu_hide_password);
					mShowPassword = true;
				}
				setPasswordStyle();
				
				return true;
				
			case Resource.Id.menu_goto_url:
				String url;
				url = mEntry.Strings.ReadSafe (PwDefs.UrlField);
				
				// Default http:// if no protocol specified
				if ( ! url.Contains("://") ) {
					url = "http://" + url;
				}
				
				try {
					Util.gotoUrl(this, url);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
				}
				return true;
				/* TODO: required?
			case Resource.Id.menu_copy_user:
				timeoutCopyToClipboard(mEntry.Strings.ReadSafe (PwDefs.UserNameField));
				return true;
				
			case Resource.Id.menu_copy_pass:
				timeoutCopyToClipboard(mEntry.Strings.ReadSafe (PwDefs.UserNameField));
				return true;
				*/
			case Resource.Id.menu_rate:
				try {
						Util.gotoMarket(this);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
				}
					return true;
			case Resource.Id.menu_suggest_improvements:
				try {
						Util.gotoUrl(this, Resource.String.SuggestionsURL);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
				}
					return true;
			case Resource.Id.menu_lock:
				App.setShutdown();
				SetResult(KeePass.EXIT_LOCK);
				Finish();
				return true;
			case Resource.Id.menu_translate:
				try {
					Util.gotoUrl(this, Resource.String.TranslationURL);
				} catch (ActivityNotFoundException) {
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
	}

}

