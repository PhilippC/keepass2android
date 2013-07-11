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
using Android.OS;
using Android.Runtime;
using KeePassLib.Serialization;

namespace keepass2android
{
	/// <summary>
	/// Base class for list activities displaying sensitive information. 
	/// </summary>
	/// Checks in OnResume whether the timeout occured and the database must be locked/closed.
	public class LockCloseListActivity : LockingListActivity {
		public LockCloseListActivity()
		{

		}

		IOConnectionInfo _ioc;
		
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			_ioc = App.Kp2a.GetDb().Ioc;
		}
		
		public LockCloseListActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}
			
		protected override void OnResume()
		{
			base.OnResume();
			
			if (TimeoutHelper.CheckShutdown(this, _ioc))
				return;
			
			//todo: see LockCloseActivity.OnResume
			App.Kp2a.CheckForOpenFileChanged(this);
		}

	}
}

