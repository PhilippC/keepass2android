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

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

using keepass2android.view;

namespace keepass2android
{
	[Activity (Label = AppNames.AppName, MainLauncher = true, Theme="@style/Base")]
	public class KeePass : LifecycleDebugActivity
	{
		public const Android.App.Result EXIT_NORMAL = Android.App.Result.FirstUser;
		public const Android.App.Result EXIT_LOCK = Android.App.Result.FirstUser+1;
		public const Android.App.Result EXIT_REFRESH = Android.App.Result.FirstUser+2;
		public const Android.App.Result EXIT_REFRESH_TITLE = Android.App.Result.FirstUser+3;
		public const Android.App.Result EXIT_FORCE_LOCK = Android.App.Result.FirstUser+4;
		public const Android.App.Result EXIT_QUICK_UNLOCK = Android.App.Result.FirstUser+5;
		public const Android.App.Result EXIT_CLOSE_AFTER_SEARCH = Android.App.Result.FirstUser+6;
		public const Android.App.Result EXIT_CHANGE_DB = Android.App.Result.FirstUser+7;
		public const Android.App.Result EXIT_FORCE_LOCK_AND_CHANGE_DB = Android.App.Result.FirstUser+8;
		public const Android.App.Result EXIT_RELOAD_DB = Android.App.Result.FirstUser+9;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			Android.Util.Log.Debug("DEBUG","KeePass.OnCreate");
		}

		protected override void OnResume()
		{
			base.OnResume();
			Android.Util.Log.Debug("DEBUG","KeePass.OnResume");
		}
		

		protected override void OnStart() {
			base.OnStart();
			Android.Util.Log.Debug("DEBUG","KeePass.OnStart");
			startFileSelect();
		}
		
		private void startFileSelect() {
			Intent intent = new Intent(this, typeof(FileSelectActivity));
			//TEST Intent intent = new Intent(this, typeof(EntryActivity));
			//Intent intent = new Intent(this, typeof(SearchActivity));
			//Intent intent = new Intent(this, typeof(QuickUnlock));

			StartActivityForResult(intent, 0);
			Finish();
		}
		

		protected override void OnDestroy() {
			Android.Util.Log.Debug("DEBUG","KeePass.OnDestry"+IsFinishing.ToString());
			base.OnDestroy();
		}


		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
			base.OnActivityResult(requestCode, resultCode, data);
			
			Finish();
		}
	}
}


