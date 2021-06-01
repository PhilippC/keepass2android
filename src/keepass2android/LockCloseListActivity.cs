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
	/// Base class for list activities displaying sensitive information. 
	/// </summary>
	/// Checks in OnResume whether the timeout occured and the database must be locked/closed.
	public class LockCloseListActivity : LockingListActivity, ILockCloseActivity
    {
		public LockCloseListActivity()
		{
			_design = new ActivityDesign(this);
		}

		IOConnectionInfo _ioc;
		private BroadcastReceiver _intentReceiver;
		private ActivityDesign _design;
		
		protected override void OnCreate(Bundle savedInstanceState)
		{
			_design.ApplyTheme();
			base.OnCreate(savedInstanceState);


		    Util.MakeSecureDisplay(this);

			_ioc = App.Kp2a.CurrentDb.Ioc;

			_intentReceiver = new LockCloseActivityBroadcastReceiver(this);
			IntentFilter filter = new IntentFilter();

			filter.AddAction(Intents.DatabaseLocked);
			filter.AddAction(Intent.ActionScreenOff);
			RegisterReceiver(_intentReceiver, filter);

		}
		
		public LockCloseListActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			_design = new ActivityDesign(this);
		}

		protected override void OnResume()
		{
			base.OnResume();
			_design.ReapplyTheme();

		    if (TimeoutHelper.CheckDbChanged(this, _ioc))
		    {
		        Finish();
		        return;
		    }

		    //todo: see LockCloseActivity.OnResume
			App.Kp2a.CheckForOpenFileChanged(this);
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

