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
using Android.Content;
using Android.OS;
using Android.App;
using KeePassLib.Serialization;

namespace keepass2android
{
	/// <summary>
	/// Base class for activities displaying sensitive information. 
	/// </summary>
	/// Checks in OnResume whether the timeout occured and the database must be locked/closed.
	public class LockCloseActivity : LockingActivity {

		private IOConnectionInfo _ioc;
		private BroadcastReceiver _intentReceiver;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			_ioc = App.Kp2a.GetDb().Ioc;

			_intentReceiver = new LockCloseActivityBroadcastReceiver(this);
			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intents.DatabaseLocked);
			RegisterReceiver(_intentReceiver, filter);
		}

		protected override void OnDestroy()
		{
			UnregisterReceiver(_intentReceiver);

			base.OnDestroy();
		}


		protected override void OnResume()
		{
			base.OnResume();

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
			readonly LockCloseActivity _service;
			public LockCloseActivityBroadcastReceiver(LockCloseActivity service)
			{
				_service = service;
			}

			public override void OnReceive(Context context, Intent intent)
			{
				switch (intent.Action)
				{
					case Intents.DatabaseLocked:
						_service.OnLockDatabase();
						break;
				}
			}
		}
	}

}

