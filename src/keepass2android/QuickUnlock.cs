/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

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
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Content.PM;
using KeePassLib.Keys;
using Android.Preferences;
using Android.Views.InputMethods;
using KeePassLib.Serialization;

namespace keepass2android
{
	[Activity(Label = "@string/app_name", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
        Theme = "@style/MyTheme")]
	public class QuickUnlock : LifecycleDebugActivity
	{
		private IOConnectionInfo _ioc;
		private QuickUnlockBroadcastReceiver _intentReceiver;

		private ActivityDesign _design;

		public QuickUnlock()
		{
			_design = new ActivityDesign(this);
		}

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);
			_design.ApplyTheme();

			//use FlagSecure to make sure the last (revealed) character of the password is not visible in recent apps
			if (PreferenceManager.GetDefaultSharedPreferences(this).GetBoolean(
				GetString(Resource.String.ViewDatabaseSecure_key), true))
			{
				Window.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);
			}

			_ioc = App.Kp2a.GetDb().Ioc;

			if (_ioc == null)
			{
				Finish();
				return;
			}


			SetContentView(Resource.Layout.QuickUnlock);

			if (App.Kp2a.GetDb().KpDatabase.Name != "")
			{
				FindViewById(Resource.Id.filename_label).Visibility = ViewStates.Invisible;
				((TextView) FindViewById(Resource.Id.qu_filename)).Text = App.Kp2a.GetDb().KpDatabase.Name;
			}
			else
			{
				if (
					PreferenceManager.GetDefaultSharedPreferences(this)
					                 .GetBoolean(GetString(Resource.String.RememberRecentFiles_key),
					                             Resources.GetBoolean(Resource.Boolean.RememberRecentFiles_default)))
				{
					((TextView) FindViewById(Resource.Id.qu_filename)).Text = App.Kp2a.GetFileStorage(_ioc).GetDisplayName(_ioc);
				}
				else
				{
					((TextView) FindViewById(Resource.Id.qu_filename)).Text = "*****";
				}

			}


			TextView txtLabel = (TextView) FindViewById(Resource.Id.QuickUnlock_label);

			int quickUnlockLength = App.Kp2a.QuickUnlockKeyLength;

			txtLabel.Text = GetString(Resource.String.QuickUnlock_label, new Java.Lang.Object[] {quickUnlockLength});

			EditText pwd = (EditText) FindViewById(Resource.Id.QuickUnlock_password);
			pwd.SetEms(quickUnlockLength);


			Button btnUnlock = (Button) FindViewById(Resource.Id.QuickUnlock_button);
			btnUnlock.Click += (object sender, EventArgs e) =>
				{
					OnUnlock(quickUnlockLength, pwd);
				};

		    FindViewById(Resource.Id.unlock_img_button).Click += (sender, args) =>
		    {
                OnUnlock(quickUnlockLength, pwd);
		    };

			Button btnLock = (Button) FindViewById(Resource.Id.QuickUnlock_buttonLock);
			btnLock.Click += (object sender, EventArgs e) =>
				{
					App.Kp2a.LockDatabase(false);
					Finish();
				};
			pwd.EditorAction += (sender, args) =>
				{
					if ((args.ActionId == ImeAction.Done) || ((args.ActionId == ImeAction.ImeNull) && (args.Event.Action == KeyEventActions.Down)))
						OnUnlock(quickUnlockLength, pwd);
				};

			_intentReceiver = new QuickUnlockBroadcastReceiver(this);
			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intents.DatabaseLocked);
			RegisterReceiver(_intentReceiver, filter);
		}

		private void OnUnlock(int quickUnlockLength, EditText pwd)
		{
			KcpPassword kcpPassword = (KcpPassword) App.Kp2a.GetDb().KpDatabase.MasterKey.GetUserKey(typeof (KcpPassword));
			String password = kcpPassword.Password.ReadString();
			String expectedPasswordPart = password.Substring(Math.Max(0, password.Length - quickUnlockLength),
			                                                 Math.Min(password.Length, quickUnlockLength));
			if (pwd.Text == expectedPasswordPart)
			{
				App.Kp2a.UnlockDatabase();
			}
			else
			{
				App.Kp2a.LockDatabase(false);
				Toast.MakeText(this, GetString(Resource.String.QuickUnlock_fail), ToastLength.Long).Show();
			}
			Finish();
		}

		private void OnLockDatabase()
		{
			CheckIfUnloaded();
		}

		protected override void OnResume()
		{
			base.OnResume();

			_design.ReapplyTheme();

			CheckIfUnloaded();

			EditText pwd = (EditText) FindViewById(Resource.Id.QuickUnlock_password);
			pwd.PostDelayed(() =>
				{
					InputMethodManager keyboard = (InputMethodManager) GetSystemService(Context.InputMethodService);
					keyboard.ShowSoftInput(pwd, 0);
				}, 50);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			try
			{
				UnregisterReceiver(_intentReceiver);
			}
			catch (Exception e)
			{
				Kp2aLog.Log(e.ToString());
			}
			
		}

		private void CheckIfUnloaded()
		{
			if ((App.Kp2a.GetDb() == null) || (App.Kp2a.GetDb().Loaded == false))
			{
				Finish();
			}
		}

		public override void OnBackPressed()
		{
			SetResult(KeePass.ExitClose);
			base.OnBackPressed();
		}

		private class QuickUnlockBroadcastReceiver : BroadcastReceiver
		{
			readonly QuickUnlock _activity;
			public QuickUnlockBroadcastReceiver(QuickUnlock activity)
			{
				_activity = activity;
			}

			public override void OnReceive(Context context, Intent intent)
			{
				switch (intent.Action)
				{
					case Intents.DatabaseLocked:
						_activity.OnLockDatabase();
						break;
				}
			}
		}


	}
}

