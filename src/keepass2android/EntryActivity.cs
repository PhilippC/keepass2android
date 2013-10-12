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
using System.Text;
using System.Linq;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using KeePassLib;
using KeePassLib.Utility;
using Android.Preferences;
using Android.Text.Method;
using System.Globalization;
using Android.Content.PM;
using KeePassLib.Security;
using Android.Webkit;
using Android.Graphics;
using Java.IO;
using keepass2android.Io;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.KeyboardHidden, Theme="@style/NoTitleBar")]			
	public class EntryActivity : LockCloseActivity {
		public const String KeyEntry = "entry";
		public const String KeyRefreshPos = "refresh_pos";
		public const String KeyCloseAfterCreate = "close_after_create";

		private static Typeface _passwordFont;

		public static void Launch(Activity act, PwEntry pw, int pos, AppTask appTask) {
			Intent i = new Intent(act, typeof(EntryActivity));
			
			
			i.PutExtra(KeyEntry, pw.Uuid.ToHexString());
			i.PutExtra(KeyRefreshPos, pos);

			appTask.ToIntent(i);
			
			act.StartActivityForResult(i,0);
		}

		protected PwEntry Entry;
	
		private bool _showPassword;
		private int _pos;

		AppTask _appTask;
		private List<TextView> _protectedTextViews;


		protected void SetEntryView() {
			SetContentView(Resource.Layout.entry_view);
		}
		
		protected void SetupEditButtons() {
			View edit =  FindViewById(Resource.Id.entry_edit);
			edit.Click += (sender, e) => {
					EntryEditActivity.Launch(this, Entry,_appTask);
			};
		}
		
		protected override void OnCreate(Bundle savedInstanceState)
		{
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);

			long usageCount = prefs.GetLong(GetString(Resource.String.UsageCount_key), 0);

			ISharedPreferencesEditor edit = prefs.Edit();
			edit.PutLong(GetString(Resource.String.UsageCount_key), usageCount+1);
			EditorCompat.Apply(edit);

			_showPassword = ! prefs.GetBoolean(GetString(Resource.String.maskpass_key), Resources.GetBoolean(Resource.Boolean.maskpass_default));
			
			base.OnCreate(savedInstanceState);
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

			bool closeAfterCreate = _appTask.CloseEntryActivityAfterCreate;

			Entry = db.Entries [uuid];

			// Refresh Menu contents in case onCreateMenuOptions was called before _entry was set
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
			FillData(false);
			
			SetupEditButtons();

			Intent showNotIntent = new Intent(this, typeof(CopyToClipboardService));
			Intent.SetAction(Intents.ShowNotification);
			showNotIntent.PutExtra(KeyEntry, Entry.Uuid.ToHexString());
			showNotIntent.PutExtra(KeyCloseAfterCreate, closeAfterCreate);

			StartService(showNotIntent);

			Kp2aLog.Log("Requesting copy to clipboard for Uuid=" + Entry.Uuid.ToHexString());

			/*foreach (PwUuid key in App.Kp2a.GetDb().entries.Keys)
			{
				Kp2aLog.Log(this,key.ToHexString() + " -> " + App.Kp2a.GetDb().entries[key].Uuid.ToHexString());
			}*/

			if (closeAfterCreate)
			{
				SetResult(KeePass.ExitCloseAfterTaskComplete);
				Finish();
			}
		}
			

		private String getDateTime(DateTime dt) {
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

		void PopulateExtraStrings(bool trimList)
		{
			ViewGroup extraGroup = (ViewGroup)FindViewById(Resource.Id.extra_strings);
			if (trimList)
			{
				extraGroup.RemoveAllViews();
			}
			bool hasExtraFields = false;
			foreach (var view in from pair in Entry.Strings where !PwDefs.IsStandardField(pair.Key) orderby pair.Key 
								 select CreateEditSection(pair.Key, pair.Value.ReadString(), pair.Value.IsProtected))
			{
				extraGroup.AddView(view);
				hasExtraFields = true;
			}
			FindViewById(Resource.Id.entry_extra_strings_label).Visibility = hasExtraFields ? ViewStates.Visible : ViewStates.Gone;
		}

		View CreateEditSection(string key, string value, bool isProtected)
		{
			LinearLayout layout = new LinearLayout(this, null) {Orientation = Orientation.Vertical};
			LinearLayout.LayoutParams layoutParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
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
			SetPasswordTypeface(valueView);
			if (isProtected)
				RegisterProtectedTextView(valueView);


			if ((int)Build.VERSION.SdkInt >= 11)
				valueView.SetTextIsSelectable(true);
			layout.AddView(valueView);
			return layout;
		}

		private void RegisterProtectedTextView(TextView protectedTextView)
		{
			_protectedTextViews.Add(protectedTextView);
		}

		Android.Net.Uri WriteBinaryToFile(string key, bool writeToCacheDirectory)
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
				return Android.Net.Uri.Parse("content://" + AttachmentContentProvider.Authority + "/"
				                              + filename);
			}
				return fileUri;
			}

		void OpenBinaryFile(Android.Net.Uri uri)
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

		void PopulateBinaries(bool trimList)
		{
			ViewGroup binariesGroup = (ViewGroup)FindViewById(Resource.Id.binaries);
			if (trimList)
			{
				binariesGroup.RemoveAllViews();
			}
			foreach (KeyValuePair<string, ProtectedBinary> pair in Entry.Binaries)
			{
				String key = pair.Key;
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
							WriteBinaryToFile(btnSender.Text, false);
						});
					
					builder.SetNegativeButton(GetString(Resource.String.SaveAttachmentDialog_open), (dlgSender, dlgEvt) => 
					                                                                                                                   {
							Android.Net.Uri newUri = WriteBinaryToFile(btnSender.Text, true);
						if (newUri != null)
						{
								OpenBinaryFile(newUri);
						}
						});

					Dialog dialog = builder.Create();
					dialog.Show();


				};
				binariesGroup.AddView(binaryButton,layoutParams);

				
			}
			FindViewById(Resource.Id.entry_binaries_label).Visibility = Entry.Binaries.UCount > 0 ? ViewStates.Visible : ViewStates.Gone;
		}

		// url = file path or whatever suitable URL you want.
		public static String GetMimeType(String url)
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

		protected void FillData(bool trimList)
		{
			_protectedTextViews = new List<TextView>();
			ImageView iv = (ImageView)FindViewById(Resource.Id.entry_icon);
			if (iv != null)
			{
			App.Kp2a.GetDb().DrawableFactory.AssignDrawableTo(iv, Resources, App.Kp2a.GetDb().KpDatabase, Entry.IconId, Entry.CustomIconUuid);
			}
			
			//populateText(Resource.Id.entry_title, _entry.Strings.ReadSafe(PwDefs.TitleField));
			var button = ((Button)FindViewById(Resource.Id.entry_title));
			if (button != null)
			{
			button.Text = Entry.Strings.ReadSafe(PwDefs.TitleField);
			button.Click += (sender, e) => {
				Finish(); };
			}
			if (Util.HasActionBar(this))
			{
				ActionBar.Title = Entry.Strings.ReadSafe(PwDefs.TitleField);
				ActionBar.SetDisplayHomeAsUpEnabled(true);
			}
			PopulateText(Resource.Id.entry_user_name, Resource.Id.entry_user_name_label, Entry.Strings.ReadSafe(PwDefs.UserNameField));
			
			PopulateText(Resource.Id.entry_url, Resource.Id.entry_url_label, Entry.Strings.ReadSafe(PwDefs.UrlField));
			PopulateText(Resource.Id.entry_password, Resource.Id.entry_password_label, Entry.Strings.ReadSafe(PwDefs.PasswordField));
			RegisterProtectedTextView(FindViewById<TextView>(Resource.Id.entry_password));
			SetPasswordTypeface(FindViewById<TextView>(Resource.Id.entry_password));
			
			
			PopulateText(Resource.Id.entry_created, Resource.Id.entry_created_label, getDateTime(Entry.CreationTime));
			PopulateText(Resource.Id.entry_modified, Resource.Id.entry_modified_label, getDateTime(Entry.LastModificationTime));
			PopulateText(Resource.Id.entry_accessed, Resource.Id.entry_accessed_label, getDateTime(Entry.LastAccessTime));
			
			if (Entry.Expires)
			{
				PopulateText(Resource.Id.entry_expires, Resource.Id.entry_expires_label, getDateTime(Entry.ExpiryTime));
			} else
			{
				PopulateText(Resource.Id.entry_expires, Resource.Id.entry_expires_label, Resource.String.never);
			}
			PopulateText(Resource.Id.entry_comment, Resource.Id.entry_comment_label, Entry.Strings.ReadSafe(PwDefs.NotesField));

			PopulateText(Resource.Id.entry_tags, Resource.Id.entry_tags_label, concatTags(Entry.Tags));

			PopulateText(Resource.Id.entry_override_url, Resource.Id.entry_override_url_label, Entry.OverrideUrl);

			PopulateExtraStrings(trimList);

			PopulateBinaries(trimList);

			SetPasswordStyle();
		}

		private void SetPasswordTypeface(TextView textView)
		{
			if (_passwordFont == null)
				_passwordFont = Typeface.CreateFromAsset(Assets, "DejaVuSansMono.ttf" );
			textView.Typeface = _passwordFont;
		}

		private void PopulateText(int viewId, int headerViewId,int resId) {
			View header = FindViewById(headerViewId);
			TextView tv = (TextView)FindViewById(viewId);

			header.Visibility = tv.Visibility = ViewStates.Visible;
			tv.SetText (resId);
		}
		
		private void PopulateText(int viewId, int headerViewId, String text)
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

		void RequiresRefresh ()
		{
			Intent ret = new Intent ();
			ret.PutExtra (KeyRefreshPos, _pos);
			SetResult (KeePass.ExitRefresh, ret);
		}
		
		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
			base.OnActivityResult(requestCode, resultCode, data);
			if ( resultCode == KeePass.ExitRefresh || resultCode == KeePass.ExitRefreshTitle ) {
				FillData(true);
				if ( resultCode == KeePass.ExitRefreshTitle ) {
					RequiresRefresh ();
				}
			}
		}
		
		public override bool OnCreateOptionsMenu(IMenu menu) {
			base.OnCreateOptionsMenu(menu);

			MenuInflater inflater = MenuInflater;
			inflater.Inflate(Resource.Menu.entry, menu);
			
			IMenuItem togglePassword = menu.FindItem(Resource.Id.menu_toggle_pass);
			if ( _showPassword ) {
				togglePassword.SetTitle(Resource.String.menu_hide_password);
			} else {
				togglePassword.SetTitle(Resource.String.show_password);
			}

			IMenuItem gotoUrl = menu.FindItem(Resource.Id.menu_goto_url);
			//Disabled IMenuItem copyUser = menu.FindItem(Resource.Id.menu_copy_user);
			//Disabled IMenuItem copyPass = menu.FindItem(Resource.Id.menu_copy_pass);
			
			// In API >= 11 onCreateOptionsMenu may be called before onCreate completes
			// so _entry may not be set
			if (Entry == null) {
				gotoUrl.SetVisible(false);
				//Disabled copyUser.SetVisible(false);
				//Disabled copyPass.SetVisible(false);
			}
			else {
				String url = Entry.Strings.ReadSafe (PwDefs.UrlField);
				if (String.IsNullOrEmpty(url)) {
					// disable button if url is not available
					gotoUrl.SetVisible(false);
				}
				if ( String.IsNullOrEmpty(Entry.Strings.ReadSafe(PwDefs.UserNameField ))) {
					// disable button if username is not available
					//Disabled copyUser.SetVisible(false);
				}
				if ( String.IsNullOrEmpty(Entry.Strings.ReadSafe(PwDefs.PasswordField ))) {
					// disable button if password is not available
					//Disabled copyPass.SetVisible(false);
				}
			}
			return true;
		}
		
		private void SetPasswordStyle() {
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
		
		public void ClearCache() {
			try {
				File dir = new File(CacheDir.Path + File.Separator + AttachmentContentProvider.AttachmentCacheSubDir);
				if (dir.IsDirectory) {
					IoUtil.DeleteDir(dir);
				}
			} catch (Exception) {

			}
		}
		
		
		public override bool OnOptionsItemSelected(IMenuItem item) {
			switch ( item.ItemId ) {
			case Resource.Id.menu_donate:
				try {
						Util.GotoDonateUrl(this);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.error_failed_to_launch_link, ToastLength.Long).Show();
					return false;
				}
				
				return true;
			case Resource.Id.menu_toggle_pass:
				if ( _showPassword ) {
					item.SetTitle(Resource.String.show_password);
					_showPassword = false;
				} else {
					item.SetTitle(Resource.String.menu_hide_password);
					_showPassword = true;
				}
				SetPasswordStyle();
				
				return true;
				
			case Resource.Id.menu_goto_url:
					string url = Entry.Strings.ReadSafe (PwDefs.UrlField);
				
				// Default http:// if no protocol specified
				if ( ! url.Contains("://") ) {
					url = "http://" + url;
				}
				
				try {
					Util.GotoUrl(this, url);
				} catch (ActivityNotFoundException) {
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
				try {
						Util.GotoMarket(this);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
				}
					return true;
			case Resource.Id.menu_suggest_improvements:
				try {
						Util.GotoUrl(this, Resource.String.SuggestionsURL);
				} catch (ActivityNotFoundException) {
					Toast.MakeText(this, Resource.String.no_url_handler, ToastLength.Long).Show();
				}
					return true;
			case Resource.Id.menu_lock:
                App.Kp2a.LockDatabase();
				return true;
			case Resource.Id.menu_translate:
				try {
					Util.GotoUrl(this, Resource.String.TranslationURL);
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

