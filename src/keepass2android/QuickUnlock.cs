/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

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
	[Activity (Label = "@string/app_name", ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.KeyboardHidden, Theme="@style/Base")]					
	public class QuickUnlock : LifecycleDebugActivity
	{
		IOConnectionInfo _ioc;

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

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
				((TextView)FindViewById(Resource.Id.qu_filename)).Text = App.Kp2a.GetDb().KpDatabase.Name;
			} else
			{
				((TextView)FindViewById(Resource.Id.qu_filename)).Text = _ioc.Path;
			}


			TextView txtLabel = (TextView)FindViewById(Resource.Id.QuickUnlock_label);

			int quickUnlockLength = App.Kp2a.QuickUnlockKeyLength;

			txtLabel.Text = GetString(Resource.String.QuickUnlock_label, new Java.Lang.Object[]{quickUnlockLength});

			EditText pwd= (EditText)FindViewById(Resource.Id.QuickUnlock_password);
			pwd.SetEms(quickUnlockLength);
			pwd.PostDelayed(() => {
				InputMethodManager keyboard = (InputMethodManager)GetSystemService(Context.InputMethodService);
				keyboard.ShowSoftInput(pwd, 0);
			}, 50);

			SetResult(KeePass.ExitChangeDb);

			Button btnUnlock = (Button)FindViewById(Resource.Id.QuickUnlock_button);
			btnUnlock.Click += (object sender, EventArgs e) => 
			{
				KcpPassword kcpPassword = (KcpPassword)App.Kp2a.GetDb().KpDatabase.MasterKey.GetUserKey(typeof(KcpPassword));
				String password = kcpPassword.Password.ReadString();
				String expectedPasswordPart = password.Substring(Math.Max(0,password.Length-quickUnlockLength),Math.Min(password.Length, quickUnlockLength));
				if (pwd.Text == expectedPasswordPart)
				{
					SetResult(KeePass.ExitQuickUnlock);
				}
				else
				{
					SetResult(KeePass.ExitForceLock);
					Toast.MakeText(this, GetString(Resource.String.QuickUnlock_fail), ToastLength.Long).Show();
				}
				Finish();
			};

			Button btnLock = (Button)FindViewById(Resource.Id.QuickUnlock_buttonLock);
			btnLock.Click += (object sender, EventArgs e) => 
			{
				SetResult(KeePass.ExitForceLockAndChangeDb);
				Finish();
			};
		}


		protected override void OnResume()
		{
			base.OnResume();

			if ( ! App.Kp2a.GetDb().Loaded ) {
				SetResult(KeePass.ExitChangeDb);
				Finish();
				return;
			}
		}
	}
}

