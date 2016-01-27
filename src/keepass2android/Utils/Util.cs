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
using System.IO;
using Android.App;
using Android.Content;
using Android.Database;
using Android.OS;
using Android.Preferences;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using KeePassLib.Serialization;
using Uri = Android.Net.Uri;

namespace keepass2android
{
	
	public class Util {

		public static Bitmap DrawableToBitmap(Drawable drawable)
		{
			Bitmap bitmap = null;

			if (drawable is BitmapDrawable)
			{
				BitmapDrawable bitmapDrawable = (BitmapDrawable)drawable;
				if (bitmapDrawable.Bitmap != null)
				{
					return bitmapDrawable.Bitmap;
				}
			}

			if (drawable.IntrinsicWidth <= 0 || drawable.IntrinsicHeight <= 0)
			{
				bitmap = Bitmap.CreateBitmap(1, 1, Bitmap.Config.Argb8888); // Single color bitmap will be created of 1x1 pixel
			}
			else
			{
				bitmap = Bitmap.CreateBitmap(drawable.IntrinsicWidth, drawable.IntrinsicHeight, Bitmap.Config.Argb8888);
			}

			Canvas canvas = new Canvas(bitmap);
			drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
			drawable.Draw(canvas);
			return bitmap;
		}


		public static float convertDpToPixel(float dp, Context context)
		{
			Resources resources = context.Resources;
			DisplayMetrics metrics = resources.DisplayMetrics;
			float px = dp * metrics.Density;
			return px;
		}
		public static String GetClipboard(Context context) {
			Android.Text.ClipboardManager clipboard = (Android.Text.ClipboardManager) context.GetSystemService(Context.ClipboardService);
			return clipboard.Text;
		}
		
		public static void CopyToClipboard(Context context, String text) {
			Android.Text.ClipboardManager clipboard = (Android.Text.ClipboardManager) context.GetSystemService(Context.ClipboardService);
			clipboard.Text = text;
		}
		
		public static void GotoUrl(Context context, String url) {
			if ( !string.IsNullOrEmpty(url) ) {

				if (url.StartsWith("androidapp://"))
				{
					string packageName = url.Substring("androidapp://".Length);
					Intent startKp2aIntent = context.PackageManager.GetLaunchIntentForPackage(packageName);
					if (startKp2aIntent != null)
					{
						startKp2aIntent.AddCategory(Intent.CategoryLauncher);
						startKp2aIntent.AddFlags(ActivityFlags.NewTask);
						context.StartActivity(startKp2aIntent);
					}
				}
				else
				{
					Uri uri = Uri.Parse(url);
					context.StartActivity(new Intent(
						url.StartsWith("tel:") ? Intent.ActionDial : Intent.ActionView, 
						uri));
				}
			}
		}
		
		public static void GotoUrl(Context context, int resId)  {
			GotoUrl(context, context.GetString(resId));
		}

		public static void GotoMarket(Context context)
		{
			GotoUrl(context, context.GetString(Resource.String.MarketURL)+context.PackageName);
		}

		public static bool GotoDonateUrl(Context context)
		{
			string donateUrl = context.GetString(Resource.String.donate_url, 
			                         new Java.Lang.Object[]{context.Resources.Configuration.Locale.Language,
															context.PackageName
			});
			try
			{
				GotoUrl(context, donateUrl);
				return true;
			}
			catch (ActivityNotFoundException)
			{
				Toast.MakeText(context, Resource.String.error_failed_to_launch_link, ToastLength.Long).Show();
				return false;
			}
			
		}
		
		public static String GetEditText(Activity act, int resId) {
			TextView te =  (TextView) act.FindViewById(resId);
			System.Diagnostics.Debug.Assert(te != null);
			
			if (te != null) {
				return te.Text;
			} else {
				return "";
			}
		}
		
		public static void SetEditText(Activity act, int resId, String str) {
			TextView te =  (TextView) act.FindViewById(resId);
			System.Diagnostics.Debug.Assert(te != null);
			
			if (te != null) {
				te.Text = str;
			}
		}

		/**
	 * Indicates whether the specified action can be used as an intent. This
	 * method queries the package manager for installed packages that can
	 * respond to an intent with the specified action. If no suitable package is
	 * found, this method returns false.
	 *
	 * @param context The application's environment.
	 * @param action The Intent action to check for availability.
	 *
	 * @return True if an Intent with the specified action can be sent and
	 *         responded to, false otherwise.
	 */
		static bool IsIntentAvailable(Context context, String action, String type, List<String> categories )
		{
			PackageManager packageManager = context.PackageManager;
			Intent intent = new Intent(action);
			if (type != null)
				intent.SetType(type);
			if (categories != null)
				categories.ForEach(c => intent.AddCategory(c));
			IList<ResolveInfo> list =
				packageManager.QueryIntentActivities(intent,
													 PackageInfoFlags.MatchDefaultOnly);
			foreach (ResolveInfo i in list)
				Kp2aLog.Log(i.ActivityInfo.ApplicationInfo.PackageName);
			return list.Count > 0;
		}

		/// <summary>
		/// Opens a browse dialog for selecting a file.
		/// </summary>
		/// <param name="activity">context activity</param>
		/// <param name="requestCodeBrowse">requestCode for onActivityResult</param>
		/// <param name="forSaving">if true, the file location is meant for saving</param>
		/// <param name="tryGetPermanentAccess">if true, the caller prefers a location that can be used permanently
		/// This means that ActionOpenDocument should be used instead of ActionGetContent (for not saving), as ActionGetContent
		/// is more for one-time access, but therefore allows possibly more available sources.</param>
		public static void ShowBrowseDialog(Activity activity, int requestCodeBrowse, bool forSaving, bool tryGetPermanentAccess)
		{
			var loadAction = (tryGetPermanentAccess && IsKitKatOrLater) ? 
							Intent.ActionOpenDocument : Intent.ActionGetContent;
			if ((!forSaving) && (IsIntentAvailable(activity, loadAction, "*/*", new List<string> { Intent.CategoryOpenable})))
			{
				Intent i = new Intent(loadAction);
				i.SetType("*/*");
				i.AddCategory(Intent.CategoryOpenable);

				activity.StartActivityForResult(i, requestCodeBrowse);
			}
			else
			{
				if ((forSaving) && (IsKitKatOrLater))
				{
					Intent i = new Intent(Intent.ActionCreateDocument);
					i.SetType("*/*");
					i.AddCategory(Intent.CategoryOpenable);

					activity.StartActivityForResult(i, requestCodeBrowse);
				}
				else
				{
					string defaultPath = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;

					ShowInternalLocalFileChooser(activity, requestCodeBrowse, forSaving, defaultPath);	
				}
				
			}
		}

		public static bool IsKitKatOrLater
		{
			get { return (int)Build.VERSION.SdkInt >= 19; }
		}

		private static void ShowInternalLocalFileChooser(Activity act, int requestCodeBrowse, bool forSaving, string defaultPath)
		{
			
#if !EXCLUDE_FILECHOOSER
			string fileProviderAuthority = act.PackageName+".android-filechooser.localfile";

			Intent i = Keepass2android.Kp2afilechooser.Kp2aFileChooserBridge.GetLaunchFileChooserIntent(act,
			                                                                                            fileProviderAuthority,
			                                                                                            defaultPath);
			if (forSaving)
				i.PutExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.save_dialog", true);

			act.StartActivityForResult(i, requestCodeBrowse);
#else
			Toast.MakeText(act, "File Chooser excluded!",ToastLength.Long).Show();
#endif
		}

		/// <summary>
		/// Tries to extract the filename from the intent. Returns that filename or null if no success
		/// (e.g. on content-URIs in Android KitKat+). 
		/// Guarantees that the file exists.
		/// </summary>
		public static string IntentToFilename(Intent data, Context ctx)
		{
			string s = GetFilenameFromInternalFileChooser(data, ctx);
			if (!String.IsNullOrEmpty(s))
				return s;

			try
			{
				Uri uri = data.Data;
				if ((uri != null) && (uri.Scheme == "content"))
				{
					String[] col = new String[] {MediaStore.MediaColumns.Data};
					
					ICursor c1 = ctx.ContentResolver.Query(uri, col, null, null, null);
					c1.MoveToFirst();

					var possibleFilename = c1.GetString(0);
					if (File.Exists(possibleFilename))
						return possibleFilename;
				}
			}
			catch (Exception e)
			{
				Kp2aLog.LogUnexpectedError(e);
			}

			String filename = data.Data.Path;
			if ((String.IsNullOrEmpty(filename) || (!File.Exists(filename))))
			 	filename = data.DataString;
			if (File.Exists(filename))
				return filename;
			//found no valid file
			return null;
		}

		public static string GetFilenameFromInternalFileChooser(Intent data, Context ctx)
		{
#if !EXCLUDE_FILECHOOSER
			string EXTRA_RESULTS = "group.pals.android.lib.ui.filechooser.FileChooserActivity.results";
			if (data.HasExtra(EXTRA_RESULTS))
			{
				IList uris = data.GetParcelableArrayListExtra(EXTRA_RESULTS);
				Uri uri = (Uri) uris[0];
				{
					return Group.Pals.Android.Lib.UI.Filechooser.Providers.BaseFileProviderUtils.GetRealUri(ctx, uri).ToString();
				}
			}

#endif
			return null;
		}


		public static bool HasActionBar(Activity activity)
		{
			//Actionbar is available since 11, but the layout has its own "pseudo actionbar" until 13
			return ((int)Android.OS.Build.VERSION.SdkInt >= 14) && (activity.ActionBar != null);
		}

		public delegate bool FileSelectedHandler(string filename);

		public static void ShowSftpDialog(Activity activity, FileSelectedHandler onStartBrowse, Action onCancel)
		{
#if !EXCLUDE_JAVAFILESTORAGE && !NoNet
			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			View dlgContents = activity.LayoutInflater.Inflate(Resource.Layout.sftpcredentials, null);
			builder.SetView(dlgContents);
			builder.SetPositiveButton(Android.Resource.String.Ok, 
			                          (sender, args) =>
				                          {
					                          string host = dlgContents.FindViewById<EditText>(Resource.Id.sftp_host).Text;
					                          string portText = dlgContents.FindViewById<EditText>(Resource.Id.sftp_port).Text;
					                          int port = Keepass2android.Javafilestorage.SftpStorage.DefaultSftpPort;
					                          if (!string.IsNullOrEmpty(portText))
						                          int.TryParse(portText, out port);
											  string user = dlgContents.FindViewById<EditText>(Resource.Id.sftp_user).Text;
											  string password = dlgContents.FindViewById<EditText>(Resource.Id.sftp_password).Text;
					                          string initialPath = dlgContents.FindViewById<EditText>(Resource.Id.sftp_initial_dir).Text;
					                          string sftpPath = new Keepass2android.Javafilestorage.SftpStorage().BuildFullPath(host, port, initialPath, user,
					                                                                                          password);
					                          onStartBrowse(sftpPath);
				                          });
			EventHandler<DialogClickEventArgs> evtH = new EventHandler<DialogClickEventArgs>( (sender, e) => onCancel());

			builder.SetNegativeButton(Android.Resource.String.Cancel, evtH);
			builder.SetTitle(activity.GetString(Resource.String.enter_sftp_login_title));
			Dialog dialog = builder.Create();
			
			dialog.Show();
#endif
		}

		public class DismissListener:  Java.Lang.Object, IDialogInterfaceOnDismissListener
		{
			private readonly Action _onDismiss;

			public DismissListener(Action onDismiss)
			{
				_onDismiss = onDismiss;
			}

			public void OnDismiss(IDialogInterface dialog)
			{
				_onDismiss();
			}
		}


		class CancelListener: Java.Lang.Object, IDialogInterfaceOnCancelListener
		{
			private readonly Action _onCancel;

			public CancelListener(Action onCancel)
			{
				_onCancel = onCancel;
			}

			public void OnCancel(IDialogInterface dialog)
			{
				_onCancel();
			}
		}

		public static void ShowFilenameDialog(Activity activity, Func<string, Dialog, bool> onOpen, Func<string, Dialog, bool> onCreate, Action onCancel, bool showBrowseButton, string defaultFilename, string detailsText, int requestCodeBrowse)
		{
			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			builder.SetView(activity.LayoutInflater.Inflate(Resource.Layout.file_selection_filename, null));
			
			if (onCancel != null)
				builder.SetOnCancelListener(new CancelListener(onCancel));
			Dialog dialog = builder.Create();
			dialog.Show();

			Button openButton = (Button) dialog.FindViewById(Resource.Id.open);
			Button createButton = (Button) dialog.FindViewById(Resource.Id.create);
			
			TextView enterFilenameDetails = (TextView) dialog.FindViewById(Resource.Id.label_open_by_filename_details);
			openButton.Visibility = onOpen != null ? ViewStates.Visible : ViewStates.Gone;
			createButton.Visibility = onCreate != null? ViewStates.Visible : ViewStates.Gone;
			// Set the initial value of the filename
			EditText editFilename = (EditText) dialog.FindViewById(Resource.Id.file_filename);
			editFilename.Text = defaultFilename;
			enterFilenameDetails.Text = detailsText;
			enterFilenameDetails.Visibility = enterFilenameDetails.Text == "" ? ViewStates.Gone : ViewStates.Visible;

			// Open button
			if (onOpen != null)
				openButton.Click += (sender, args) =>
					{
						String fileName = ((EditText) dialog.FindViewById(Resource.Id.file_filename)).Text;
						if (onOpen(fileName, dialog))
							dialog.Dismiss();
					};

			// Create button
			if (onCreate != null)
				createButton.Click += (sender, args) =>
				{
					String fileName = ((EditText)dialog.FindViewById(Resource.Id.file_filename)).Text;
					if (onCreate(fileName, dialog))
						dialog.Dismiss();
				}; 
			
			Button cancelButton = (Button) dialog.FindViewById(Resource.Id.fnv_cancel);
			cancelButton.Click += delegate
				{
					dialog.Dismiss();
					if (onCancel != null)
					onCancel();
				};

			ImageButton browseButton = (ImageButton) dialog.FindViewById(Resource.Id.browse_button);
			if (!showBrowseButton)
			{
				browseButton.Visibility = ViewStates.Invisible;
			}
			browseButton.Click += (sender, evt) =>
				{
					string filename = ((EditText) dialog.FindViewById(Resource.Id.file_filename)).Text;

					Util.ShowBrowseDialog(activity, requestCodeBrowse, onCreate != null, /*TODO should we prefer ActionOpenDocument here?*/ false);

				};

		}

		public static void QueryCredentials(IOConnectionInfo ioc, Action<IOConnectionInfo> afterQueryCredentials, Activity activity)
		{
			//Build dialog to query credentials:
			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			builder.SetTitle(activity.GetString(Resource.String.credentials_dialog_title));
			builder.SetPositiveButton(activity.GetString(Android.Resource.String.Ok), (dlgSender, dlgEvt) =>
			{
				Dialog dlg = (Dialog)dlgSender;
				string username = ((EditText)dlg.FindViewById(Resource.Id.cred_username)).Text;
				string password = ((EditText)dlg.FindViewById(Resource.Id.cred_password)).Text;
				int credentialRememberMode = ((Spinner)dlg.FindViewById(Resource.Id.cred_remember_mode)).SelectedItemPosition;
				ioc.UserName = username;
				ioc.Password = password;
				ioc.CredSaveMode = (IOCredSaveMode)credentialRememberMode;
				afterQueryCredentials(ioc);
			});
			builder.SetView(activity.LayoutInflater.Inflate(Resource.Layout.url_credentials, null));
			builder.SetNeutralButton(activity.GetString(Android.Resource.String.Cancel),
									 (dlgSender, dlgEvt) => { });
			Dialog dialog = builder.Create();
			dialog.Show();
			((EditText)dialog.FindViewById(Resource.Id.cred_username)).Text = ioc.UserName;
			((EditText)dialog.FindViewById(Resource.Id.cred_password)).Text = ioc.Password;
			((Spinner)dialog.FindViewById(Resource.Id.cred_remember_mode)).SetSelection((int)ioc.CredSaveMode);
		}


		public static void FinishAndForward(Activity activity, Intent i)
		{
			i.SetFlags(ActivityFlags.ForwardResult);
			activity.StartActivity(i);
			activity.Finish();
		}

        public static void PrepareDonateOptionMenu(IMenu menu, Context ctx)
        {
            var donateItem = menu.FindItem(Resource.Id.menu_donate);
            if (donateItem != null)
            {
                donateItem.SetVisible(
                    !PreferenceManager.GetDefaultSharedPreferences(ctx)
                                     .GetBoolean(ctx.GetString(Resource.String.NoDonateOption_key), false)
                    );
            }
        }


		public static void MoveBottomBarButtons(int btn1Id, int btn2Id, int bottomBarId, Activity context)
		{
			var btn1 = context.FindViewById<Button>(btn1Id);
			var btn2 = context.FindViewById<Button>(btn2Id);
			var rl = context.FindViewById<RelativeLayout>(bottomBarId);
			rl.ViewTreeObserver.GlobalLayout += (sender, args) =>
			{
				if (btn1.Width + btn2.Width > rl.Width)
				{
					btn2.SetPadding(btn2.PaddingLeft, (int) Util.convertDpToPixel(40, context), btn2.PaddingRight, btn2.PaddingBottom);
				}
			};
		}
	}
}

