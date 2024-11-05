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
using KeePassLib.Serialization;

namespace keepass2android
{
	
	public class LockingClosePreferenceActivity : LockingPreferenceActivity, ILockCloseActivity
    {

		
		IOConnectionInfo _ioc;
		private BroadcastReceiver _intentReceiver;
		
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			_ioc = App.Kp2a.CurrentDb.Ioc;


			_intentReceiver = new LockCloseActivityBroadcastReceiver(this);
			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intents.DatabaseLocked);
			RegisterReceiver(_intentReceiver, filter);
		}

		protected override void OnResume() {
			base.OnResume();

		    if (TimeoutHelper.CheckDbChanged(this, _ioc))
		    {
		        Finish();
		        return;
		    }
		}


		protected override void OnDestroy()
		{
			try
			{
				UnregisterReceiver(_intentReceiver);
			}
			catch (Exception ex)
			{
				Kp2aLog.LogUnexpectedError(ex);
			}

			base.OnDestroy();
		}


        public void OnLockDatabase(bool lockedByTimeout)
        {
            TimeoutHelper.Lock(this, lockedByTimeout);

        }
    }

}

