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

using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Content.PM;

namespace keepass2android
{
	/// <summary>
	/// Dialog to offer to install OpenIntent file manager if there's no other browser installed
	/// </summary>
	public class BrowserDialog : Dialog {
		
		public BrowserDialog(Context context) : base(context)
		{
		}
		
		protected override void OnCreate(Bundle savedInstanceState) {
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.browser_install);
			SetTitle(Resource.String.file_browser);
			
			Button cancel = (Button) FindViewById(Resource.Id.cancel);
			cancel.Click += (sender, e) => Cancel();
			
			Button market = (Button) FindViewById(Resource.Id.install_market);
			market.Click += (sender, e) => {
					Util.gotoUrl(Context, Resource.String.oi_filemanager_market);
					Cancel();
				}
			;
			if (!IsMarketInstalled()) {
				market.Visibility = ViewStates.Gone;
			}
			
			Button web = (Button) FindViewById(Resource.Id.install_web);
			web.Click += (sender, e) => {
					Util.gotoUrl(Context, Resource.String.oi_filemanager_web);
					Cancel();
				}
			;
		}
		
		private bool IsMarketInstalled() {
			PackageManager pm = Context.PackageManager;
			
			try {
				pm.GetPackageInfo("com.android.vending", 0);
			} catch (PackageManager.NameNotFoundException) {
				return false;
			}
			
			return true;
			
		}
		
	}

}

