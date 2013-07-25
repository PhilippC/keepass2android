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

using Android.Content;
using Android.OS;
using KeePassLib.Serialization;

namespace keepass2android
{
	
	public class LockingClosePreferenceActivity : LockingPreferenceActivity {

		
		IOConnectionInfo _ioc;
		private BroadcastReceiver _intentReceiver;
		
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			_ioc = App.Kp2a.GetDb().Ioc;


			_intentReceiver = new LockingClosePreferenceActivityBroadcastReceiver(this);
			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intents.LockDatabase);
			RegisterReceiver(_intentReceiver, filter);
		}

		protected override void OnResume() {
			base.OnResume();
			
			TimeoutHelper.CheckShutdown(this, _ioc);
		}


		protected override void OnDestroy()
		{
			UnregisterReceiver(_intentReceiver);

			base.OnDestroy();
		}

		private void OnLockDatabase()
		{
			Kp2aLog.Log("Finishing " + ComponentName.ClassName + " due to database lock");

			SetResult(KeePass.ExitLock);
			Finish();
		}

		private class LockingClosePreferenceActivityBroadcastReceiver : BroadcastReceiver
		{
			readonly LockingClosePreferenceActivity _service;
			public LockingClosePreferenceActivityBroadcastReceiver(LockingClosePreferenceActivity service)
			{
				_service = service;
			}

			public override void OnReceive(Context context, Intent intent)
			{
				switch (intent.Action)
				{
					case Intents.LockDatabase:
						_service.OnLockDatabase();
						break;
				}
			}
		}
	}

}

