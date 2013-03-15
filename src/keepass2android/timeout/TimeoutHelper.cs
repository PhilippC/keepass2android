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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Preferences;
using KeePassLib.Serialization;

namespace keepass2android
{
	
	public class TimeoutHelper {
		
		private const long DEFAULT_TIMEOUT = 5 * 60 * 1000;  // 5 minutes
		
		public static void pause(Activity act) {
			// Record timeout time in case timeout service is killed
			long time = Java.Lang.JavaSystem.CurrentTimeMillis();
			
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(act);
			ISharedPreferencesEditor edit = prefs.Edit();
			edit.PutLong(act.GetString(Resource.String.timeout_key), time);
			
			EditorCompat.apply(edit);
			
			if ( App.getDB().Open ) {
				Timeout.start(act);
			}
			
		}
		
		public static void resume(Activity act) {
			if ( App.getDB().Loaded ) {
				Timeout.cancel(act);
			}
			
			
			// Check whether the timeout has expired
			long cur_time = Java.Lang.JavaSystem.CurrentTimeMillis();
			
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(act);
			long timeout_start = prefs.GetLong(act.GetString(Resource.String.timeout_key), -1);
			// The timeout never started
			if (timeout_start == -1) {
				return;
			}
			
			
			String sTimeout = prefs.GetString(act.GetString(Resource.String.app_timeout_key), act.GetString(Resource.String.clipboard_timeout_default));
			long timeout = DEFAULT_TIMEOUT;
			long.TryParse(sTimeout,out timeout);
			
			// We are set to never timeout
			if (timeout == -1) {
				return;
			}
			
			long diff = cur_time - timeout_start;
			if (diff >= timeout) {
				// We have timed out
				App.setShutdown();
			}
		}

		static bool iocChanged(IOConnectionInfo ioc, IOConnectionInfo other)
		{
			if ((ioc == null) || (other == null)) return false;
			return ioc.GetDisplayName() != other.GetDisplayName();
		}
		
		public static bool checkShutdown(Activity act, IOConnectionInfo ioc) {
			if ((  App.getDB().Loaded && (App.isShutdown() || App.getDB().Locked) ) 
			    || (iocChanged(ioc, App.getDB().mIoc))) //file was changed from ActionSend-Intent
			{
				act.SetResult(KeePass.EXIT_LOCK);
				act.Finish();
				return true;
			}
			return false;
		}
	}
}

