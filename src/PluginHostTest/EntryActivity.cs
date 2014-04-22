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

using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Preferences;
using Android.Text.Method;
using System.Globalization;
using Android.Content.PM;
using Android.Webkit;
using Android.Graphics;
using Java.IO;
using PluginHostTest;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.KeyboardHidden, Theme="@style/NoTitleBar")]			
	public class EntryActivity : Activity {
		public const String KeyEntry = "entry";
		public const String KeyRefreshPos = "refresh_pos";
		public const String KeyCloseAfterCreate = "close_after_create";

		private static Typeface _passwordFont;

		private bool _showPassword;
		private int _pos;

		private List<TextView> _protectedTextViews;


		protected void SetEntryView() {
			SetContentView(Resource.Layout.entry_view);
		}
		
		protected void SetupEditButtons() {
			View edit =  FindViewById(Resource.Id.entry_edit);
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
		
		protected override void OnCreate(Bundle savedInstanceState)
		{
			
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);

			long usageCount = prefs.GetLong(GetString(Resource.String.UsageCount_key), 0);

			ISharedPreferencesEditor edit = prefs.Edit();
			edit.PutLong(GetString(Resource.String.UsageCount_key), usageCount+1);
			edit.Commit();

			_showPassword = ! prefs.GetBoolean(GetString(Resource.String.maskpass_key), Resources.GetBoolean(Resource.Boolean.maskpass_default));
			
			base.OnCreate(savedInstanceState);
			SetEntryView();

			FillData(false);
			
			SetupEditButtons();

			
		}

		public void CompleteOnCreate()
		{
			
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
			foreach (var view in from pair in new Dictionary<string, string>() { { "Field header", "field value" }, { "another header", "_aiaeiae" } }
								 orderby pair.Key 
								 select CreateEditSection(pair.Key, pair.Value, true))
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


		void PopulateBinaries(bool trimList)
		{
			ViewGroup binariesGroup = (ViewGroup)FindViewById(Resource.Id.binaries);
			if (trimList)
			{
				binariesGroup.RemoveAllViews();
			}
			foreach (KeyValuePair<string, string> pair in new Dictionary<string, string>() { {"abc",""}, {"test.png","uia"} })
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
							
						});
					
					builder.SetNegativeButton(GetString(Resource.String.SaveAttachmentDialog_open), (dlgSender, dlgEvt) => 
					                                                                                                                   {
							
						});

					Dialog dialog = builder.Create();
					dialog.Show();


				};
				binariesGroup.AddView(binaryButton,layoutParams);

				
			}
			FindViewById(Resource.Id.entry_binaries_label).Visibility = true ? ViewStates.Visible : ViewStates.Gone;
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
		}

		protected void FillData(bool trimList)
		{
			_protectedTextViews = new List<TextView>();
			ImageView iv = (ImageView)FindViewById(Resource.Id.entry_icon);
			if (iv != null)
			{
				iv.SetImageDrawable(Resources.GetDrawable(Resource.Drawable.ic00));
			}
			
			

			ActionBar.Title = "Entry title";
			ActionBar.SetDisplayHomeAsUpEnabled(true);
			
			PopulateText(Resource.Id.entry_user_name, Resource.Id.entry_user_name_label, "user name");
			
			PopulateText(Resource.Id.entry_url, Resource.Id.entry_url_label, "www.google.com");
			PopulateText(Resource.Id.entry_password, Resource.Id.entry_password_label, "my password");
			RegisterProtectedTextView(FindViewById<TextView>(Resource.Id.entry_password));
			SetPasswordTypeface(FindViewById<TextView>(Resource.Id.entry_password));
			
			
			PopulateText(Resource.Id.entry_created, Resource.Id.entry_created_label, getDateTime(DateTime.Now));
			PopulateText(Resource.Id.entry_modified, Resource.Id.entry_modified_label, getDateTime(DateTime.Now));
			
			if (true)
			{
				FindViewById(Resource.Id.entry_expires).Visibility = ViewStates.Visible;
				FindViewById(Resource.Id.entry_expires_label).Visibility = ViewStates.Visible;
				
				PopulateText(Resource.Id.entry_expires, Resource.Id.entry_expires_label, getDateTime(DateTime.Now));

			} 
			else
			{
				FindViewById(Resource.Id.entry_expires).Visibility = ViewStates.Gone;
				FindViewById(Resource.Id.entry_expires_label).Visibility = ViewStates.Gone;
			}
			PopulateText(Resource.Id.entry_comment, Resource.Id.entry_comment_label, "some text about this entry");

			PopulateText(Resource.Id.entry_tags, Resource.Id.entry_tags_label, "bla; blubb; blablubb");

			PopulateExtraStrings(trimList);

			PopulateBinaries(trimList);

			SetPasswordStyle();
		}

		private void SetPasswordTypeface(TextView textView)
		{
			
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
			
			base.OnResume();
		}
		
		/// <summary>
		/// brings up a dialog asking the user whether he wants to add the given URL to the entry for automatic finding
		/// </summary>
		public void AskAddUrlThenCompleteCreate(string url)
		{
			AlertDialog.Builder builder = new AlertDialog.Builder(this);
			builder.SetTitle(GetString(Resource.String.AddUrlToEntryDialog_title));

			builder.SetMessage(GetString(Resource.String.AddUrlToEntryDialog_text, new Java.Lang.Object[] { url } ));

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

	}

}

