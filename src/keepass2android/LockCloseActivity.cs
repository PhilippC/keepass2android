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
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using KeePassLib.Serialization;

namespace keepass2android
{
	/// <summary>
	/// Base class for activities displaying sensitive information. 
	/// </summary>
	/// Checks in OnResume whether the timeout occured and the database must be locked/closed.
	public class LockCloseActivity : LockingActivity {
		
		//the check if the database was locked/closed can be disabled by the caller for activities
		//which may be used "outside" the database (e.g. GeneratePassword for creating a master password)
		protected const string NoLockCheck = "NO_LOCK_CHECK";

		protected IOConnectionInfo _ioc;
		private BroadcastReceiver _intentReceiver;
		private ActivityDesign _design;

		public LockCloseActivity()
		{
			_design = new ActivityDesign(this);
		}

		protected LockCloseActivity(IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			_design = new ActivityDesign(this);
		}

		protected override void OnCreate(Bundle savedInstanceState)
		{
			_design.ApplyTheme();
			base.OnCreate(savedInstanceState);
			

			if (PreferenceManager.GetDefaultSharedPreferences(this).GetBoolean(
				GetString(Resource.String.ViewDatabaseSecure_key), true))
			{
				Window.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);	
			}
			

			_ioc = App.Kp2a.GetDb().Ioc;

			if (Intent.GetBooleanExtra(NoLockCheck, false))
				return;

			_intentReceiver = new LockCloseActivityBroadcastReceiver(this);
			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intents.DatabaseLocked);
			filter.AddAction(Intent.ActionScreenOff);
			RegisterReceiver(_intentReceiver, filter);
		}

		protected override void OnDestroy()
		{
			if (Intent.GetBooleanExtra(NoLockCheck, false) == false)
			{
				try
				{
					UnregisterReceiver(_intentReceiver);
				}
				catch (Exception ex)
				{
					Kp2aLog.Log(ex.ToString());
				}
				
			}
			

			

			base.OnDestroy();
		}


		protected override void OnResume()
		{
			base.OnResume();

			_design.ReapplyTheme();

			if (Intent.GetBooleanExtra(NoLockCheck, false))
				return;

			if (TimeoutHelper.CheckShutdown(this, _ioc))
				return;

			//todo: it seems like OnResume can be called after dismissing a dialog, e.g. the Delete-permanently-Dialog.
			//in this case the following check might run in parallel with the check performed during the SaveDb check (triggered after the 
			//aforementioned dialog is closed) which can cause odd behavior. However, this is a rare case and hard to resolve so this is currently
			//accepted. (If the user clicks cancel on the reload-dialog, everything will work.)
			App.Kp2a.CheckForOpenFileChanged(this);
		}

		private void OnLockDatabase()
		{
			Kp2aLog.Log("Finishing " + ComponentName.ClassName + " due to database lock");

			SetResult(KeePass.ExitLock);
			Finish();
		}

		private class LockCloseActivityBroadcastReceiver : BroadcastReceiver
		{			
			readonly LockCloseActivity _activity;
			public LockCloseActivityBroadcastReceiver(LockCloseActivity activity)
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
					case Intent.ActionScreenOff:
						App.Kp2a.OnScreenOff();
						break;
				}
			}
		}
	}

}

