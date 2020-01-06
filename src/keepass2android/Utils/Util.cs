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
using System.Text.RegularExpressions;
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
using Android.Hardware.Display;
using Android.Util;
using Android.Views.InputMethods;
using AndroidX.Core.View.InputMethod;
using KeePassLib.Serialization;
using Uri = Android.Net.Uri;

namespace keepass2android
{
	
	public class Util {

	    public const String KeyFilename = "fileName";
	    public const String KeyServerusername = "serverCredUser";
	    public const String KeyServerpassword = "serverCredPwd";
	    public const String KeyServercredmode = "serverCredRememberMode";


        public static void PutIoConnectionToIntent(IOConnectionInfo ioc, Intent i, string prefix="")
	    {
	        i.PutExtra(prefix+KeyFilename, ioc.Path);
	        i.PutExtra(prefix + KeyServerusername, ioc.UserName);
	        i.PutExtra(prefix + KeyServerpassword, ioc.Password);
	        i.PutExtra(prefix + KeyServercredmode, (int)ioc.CredSaveMode);
	    }

	    public static void SetIoConnectionFromIntent(IOConnectionInfo ioc, Intent i, string prefix="")
	    {
	        ioc.Path = i.GetStringExtra(prefix + KeyFilename);
	        ioc.UserName = i.GetStringExtra(prefix + KeyServerusername) ?? "";
	        ioc.Password = i.GetStringExtra(prefix + KeyServerpassword) ?? "";
	        ioc.CredSaveMode = (IOCredSaveMode)i.GetIntExtra(prefix + KeyServercredmode, (int)IOCredSaveMode.NoSave);
	    }

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

	    public static Bitmap ChangeImageColor(Bitmap sourceBitmap, Color color)
	    {
	        Bitmap temp = Bitmap.CreateBitmap(sourceBitmap, 0, 0,
	            sourceBitmap.Width, sourceBitmap.Height);
	        Bitmap resultBitmap = temp.Copy(Bitmap.Config.Argb8888, true);
            Paint p = new Paint();
	        ColorFilter filter = new LightingColorFilter(color.ToArgb(), 0);
	        p.SetColorFilter(filter);

	        Canvas canvas = new Canvas(resultBitmap);
	        canvas.DrawBitmap(resultBitmap, 0, 0, p);
	        return resultBitmap;
	    }


        public static float convertDpToPixel(float dp, Context context)
		{
			Resources resources = context.Resources;
			DisplayMetrics metrics = resources.DisplayMetrics;
			float px = dp * metrics.Density;
			return px;
		}
		public static String GetClipboard(Context context)
        {
            Android.Content.ClipboardManager clipboardManager = (ClipboardManager)context.GetSystemService(Context.ClipboardService);
            var clip = clipboardManager.PrimaryClip;
            if (clip != null && clip.ItemCount > 0)
            {
                return clip.GetItemAt(0).CoerceToText(context);
            }
            return "";
        }
		
		public static void CopyToClipboard(Context context, String text) {
            Android.Content.ClipboardManager clipboardManager = (ClipboardManager)context.GetSystemService(Context.ClipboardService);
            ClipData clipData = Android.Content.ClipData.NewPlainText("KP2A", text);
            clipboardManager.PrimaryClip = clipData;
		    if (text == "")
		    {
                //on some devices, adding empty text does not seem to work. Try again with some garbage.
		        clipData = Android.Content.ClipData.NewPlainText("KP2A", "***");
		        clipboardManager.PrimaryClip = clipData;
                //seems to work better on some devices:
		        try
		        {
		            clipboardManager.Text = text;
                }
		        catch (Exception exception)
		        {
		            Kp2aLog.LogUnexpectedError(exception);

		        }
		        
            }


        }

	    private static readonly Regex ARC_DEVICE_PATTERN = new Regex(".+_cheets|cheets_.+");

	    public static bool IsChromeOS(Context context)
	    {
	        return
	            context.PackageManager.HasSystemFeature(
	                "org.chromium.arc.device_management") // https://stackoverflow.com/a/39843396/292233
	            || (Build.Device != null && ARC_DEVICE_PATTERN.IsMatch(Build.Device))
                ;
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
			//even though GetContent is not well supported (since Android 7, see https://commonsware.com/Android/previews/appendix-b-android-70) 
			//we still offer it.
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

		public static bool GetShowKeyboardDuringFingerprintUnlock(Context ctx)
		{
				return (PreferenceManager.GetDefaultSharedPreferences(ctx).GetBoolean(
					ctx.GetString(Resource.String.ShowKeyboardWhileFingerprint_key), true));
			
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

		public static MemoryStream StreamToMemoryStream(Stream stream)
		{
		
			var memoryStream = stream as MemoryStream;
			if (memoryStream == null)
			{
				// Read the stream into memory
				int capacity = 4096; // Default initial capacity, if stream can't report it.
				if (stream.CanSeek)
				{
					capacity = (int) stream.Length;
				}
				memoryStream = new MemoryStream(capacity);
				stream.CopyTo(memoryStream);
				stream.Close();
				memoryStream.Seek(0, SeekOrigin.Begin);
			}
			return memoryStream;
		
		}

	    public static Bitmap MakeLargeIcon(Bitmap unscaled, Context context)
	    {
	        int height = (int)(0.9 * context.Resources.GetDimension(Android.Resource.Dimension.NotificationLargeIconHeight));
	        int width = (int)(0.9 * context.Resources.GetDimension(Android.Resource.Dimension.NotificationLargeIconWidth));
	        return Bitmap.CreateScaledBitmap(unscaled, width, height, true);
        }

	    public static string GetProtocolId(IOConnectionInfo ioc)
	    {
	        string displayPath = App.Kp2a.GetFileStorage(ioc).GetDisplayName(ioc);
	        int protocolSeparatorPos = displayPath.IndexOf("://", StringComparison.Ordinal);
	        string protocolId = protocolSeparatorPos < 0 ?
	            "file" : displayPath.Substring(0, protocolSeparatorPos);
	        return protocolId;
	    }

	    public static void MakeSecureDisplay(Activity context)
	    {
	        if (SecureDisplayConfigured(context))
	        {
	            var hasUnsecureDisplay = HasUnsecureDisplay(context);
	            if (hasUnsecureDisplay)
	            {
	                var intent = new Intent(context, typeof(NoSecureDisplayActivity));
	                intent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
	                context.StartActivityForResult(intent,9999);
	            }
	            context.Window.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);
	        }
        }

	    public static bool SecureDisplayConfigured(Activity context)
	    {
	        return PreferenceManager.GetDefaultSharedPreferences(context).GetBoolean(
	            context.GetString(Resource.String.ViewDatabaseSecure_key), true);
	    }

	    public static bool HasUnsecureDisplay(Activity context)
	    {
	        bool hasUnsecureDisplay = false;
	        if ((int) Build.VERSION.SdkInt >= 17)
	        {
	            foreach (var display in ((DisplayManager) context.GetSystemService(Context.DisplayService)).GetDisplays())
	            {
	                if ((display.Flags & DisplayFlags.Secure) == 0)
	                {
	                    hasUnsecureDisplay = true;
	                }
	            }
	        }
	        return hasUnsecureDisplay;
	    }

        public static void SetNoPersonalizedLearning(EditText editText)
        {
            if (editText == null)
                return;
            if ((int) Build.VERSION.SdkInt >= 26)
                editText.ImeOptions = (ImeAction)EditorInfoCompat.ImeFlagNoPersonalizedLearning;
            ;

        }

        public static void SetNoPersonalizedLearning(View view)
        {
            if (view is ViewGroup vg)
            {
                for (int i=0;i<vg.ChildCount;i++)
                {
                    SetNoPersonalizedLearning(vg.GetChildAt(i));
                }
            }

            if (view is EditText editText)
            {
                SetNoPersonalizedLearning(editText);
            }
        }
    }
}

