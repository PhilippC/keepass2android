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
using Android.Content.PM;

namespace keepass2android
{
	
	public class Util {
		public static String getClipboard(Context context) {
			Android.Text.ClipboardManager clipboard = (Android.Text.ClipboardManager) context.GetSystemService(Context.ClipboardService);
			return clipboard.Text;
		}
		
		public static void copyToClipboard(Context context, String text) {
			Android.Text.ClipboardManager clipboard = (Android.Text.ClipboardManager) context.GetSystemService(Context.ClipboardService);
			clipboard.Text = text;
		}
		
		public static void gotoUrl(Context context, String url) {
			if ( url != null && url.Length > 0 ) {
				Android.Net.Uri uri = Android.Net.Uri.Parse(url);
				context.StartActivity(new Intent(Intent.ActionView, uri));
			}
		}
		
		public static void gotoUrl(Context context, int resId)  {
			gotoUrl(context, context.GetString(resId));
		}

		public static void gotoMarket(Context context)
		{
			gotoUrl(context, context.GetString(Resource.String.MarketURL)+context.PackageName);
		}

		public static void gotoDonateUrl(Context context)
		{
			string donateUrl = context.GetString(Resource.String.donate_url, 
			                         new Java.Lang.Object[]{context.Resources.Configuration.Locale.Language,
															context.PackageName
			});
			gotoUrl(context, donateUrl);
		}
		
		public static String getEditText(Activity act, int resId) {
			TextView te =  (TextView) act.FindViewById(resId);
			System.Diagnostics.Debug.Assert(te != null);
			
			if (te != null) {
				return te.Text.ToString();
			} else {
				return "";
			}
		}
		
		public static void setEditText(Activity act, int resId, String str) {
			TextView te =  (TextView) act.FindViewById(resId);
			System.Diagnostics.Debug.Assert(te != null);
			
			if (te != null) {
				te.Text = str;
			}
		}
		

		public static void showBrowseDialog(string filename, Activity act)
		{
			if (Interaction.isIntentAvailable(act, Intents.FILE_BROWSE))
			{
				Intent i = new Intent(Intents.FILE_BROWSE);
				i.SetData(Android.Net.Uri.Parse("file://" + filename));
				try
				{
					act.StartActivityForResult(i, Intents.REQUEST_CODE_FILE_BROWSE);
				}
				catch (ActivityNotFoundException)
				{
					BrowserDialog diag = new BrowserDialog(act);
					diag.Show();
				}
			}
			else
			{
				BrowserDialog diag = new BrowserDialog(act);
				diag.Show();
			}
		}

		
	}
}

