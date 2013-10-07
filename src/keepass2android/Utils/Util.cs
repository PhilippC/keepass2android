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
using System.Collections;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Database;
using Android.Provider;
using Android.Widget;
using Android.Content.PM;
using Uri = Android.Net.Uri;

namespace keepass2android
{
	
	public class Util {
		public static String GetClipboard(Context context) {
			Android.Text.ClipboardManager clipboard = (Android.Text.ClipboardManager) context.GetSystemService(Context.ClipboardService);
			return clipboard.Text;
		}
		
		public static void CopyToClipboard(Context context, String text) {
			Android.Text.ClipboardManager clipboard = (Android.Text.ClipboardManager) context.GetSystemService(Context.ClipboardService);
			clipboard.Text = text;
		}
		
		public static void GotoUrl(Context context, String url) {
			if ( url != null && url.Length > 0 ) {

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
					context.StartActivity(new Intent(Intent.ActionView, uri));
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

		public static void GotoDonateUrl(Context context)
		{
			string donateUrl = context.GetString(Resource.String.donate_url, 
			                         new Java.Lang.Object[]{context.Resources.Configuration.Locale.Language,
															context.PackageName
			});
			GotoUrl(context, donateUrl);
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
		static bool IsIntentAvailable(Context context, String action, String type)
		{
			PackageManager packageManager = context.PackageManager;
			Intent intent = new Intent(action);
			if (type != null)
				intent.SetType(type);
			IList<ResolveInfo> list =
				packageManager.QueryIntentActivities(intent,
													 PackageInfoFlags.MatchDefaultOnly);
			foreach (ResolveInfo i in list)
				Kp2aLog.Log(i.ActivityInfo.ApplicationInfo.PackageName);
			return list.Count > 0;
		}

		public static void ShowBrowseDialog(string filename, Activity act, int requestCodeBrowse, bool forSaving)
		{
			if ((!forSaving) && (IsIntentAvailable(act, Intent.ActionGetContent, "file/*")))
			{
				Intent i = new Intent(Intent.ActionGetContent);
				i.SetType("file/*");

				act.StartActivityForResult(i, requestCodeBrowse);
			}
			else
			{
				string defaultPath = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;


				ShowInternalLocalFileChooser(act, requestCodeBrowse, forSaving, defaultPath);
			}
		}

		private static void ShowInternalLocalFileChooser(Activity act, int requestCodeBrowse, bool forSaving, string defaultPath)
		{
			const string fileProviderAuthority = "keepass2android.keepass2android.android-filechooser.localfile";

#if !EXCLUDE_FILECHOOSER
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

		public static string IntentToFilename(Intent data, Context ctx)
		{
#if !EXCLUDE_FILECHOOSER
			string EXTRA_RESULTS = "group.pals.android.lib.ui.filechooser.FileChooserActivity.results";
			if (data.HasExtra(EXTRA_RESULTS))
			{
				IList uris = data.GetParcelableArrayListExtra(EXTRA_RESULTS);
				Uri uri = (Uri) uris[0];
				return Group.Pals.Android.Lib.UI.Filechooser.Providers.BaseFileProviderUtils.GetRealUri(ctx, uri).ToString();
			}

#endif
			try
			{
				Uri uri = data.Data;
				if ((uri != null) && (uri.Scheme == "content"))
				{
					String[] col = new String[] {MediaStore.MediaColumns.Data};
					
					ICursor c1 = ctx.ContentResolver.Query(uri, col, null, null, null);
					c1.MoveToFirst();

					return c1.GetString(0);
				}
			}
			catch (Exception e)
			{
				Kp2aLog.Log(e.ToString());
			}

			String filename = data.Data.Path;
			if (String.IsNullOrEmpty(filename))
			 	filename = data.DataString;
			return filename;
		}

		
		public static bool HasActionBar(Activity activity)
		{
			//Actionbar is available since 11, but the layout has its own "pseudo actionbar" until 13
			return ((int)Android.OS.Build.VERSION.SdkInt >= 14) && (activity.ActionBar != null);
		}
	}
}

