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
using Android.Preferences;
using KeePassLib.Serialization;

namespace keepass2android
{
	
	public class TimeoutHelper {
		public static void Pause(Activity act) {
			// Record timeout time in case timeout service is killed
			long time = Java.Lang.JavaSystem.CurrentTimeMillis();
			
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(act);
			ISharedPreferencesEditor edit = prefs.Edit();
			edit.PutLong(act.GetString(Resource.String.timeout_key), time);
			
			EditorCompat.Apply(edit);
			
			if ( App.Kp2a.GetDb().Open ) {
				Timeout.Start(act);
			}
			
		}
		
		public static void Resume(Activity act) {
			if ( App.Kp2a.GetDb().Loaded ) {
				Timeout.Cancel(act);
			}
			
			
			// Check whether the timeout has expired
			long curTime = Java.Lang.JavaSystem.CurrentTimeMillis();
			
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(act);
			long timeoutStart = prefs.GetLong(act.GetString(Resource.String.timeout_key), -1);
			// The timeout never started
			if (timeoutStart == -1) {
				return;
			}
			
			
			String sTimeout = prefs.GetString(act.GetString(Resource.String.app_timeout_key), act.GetString(Resource.String.clipboard_timeout_default));
			long timeout;
			if (!long.TryParse(sTimeout, out timeout))
			{
				// We are set to never timeout
				return;
			}

			long diff = curTime - timeoutStart;
			if (diff >= timeout) {
				// We have timed out
                App.Kp2a.SetShutdown();
			}
		}

		static bool IocChanged(IOConnectionInfo ioc, IOConnectionInfo other)
		{
			if ((ioc == null) || (other == null)) return false;
			return ioc.GetDisplayName() != other.GetDisplayName();
		}
		
		public static bool CheckShutdown(Activity act, IOConnectionInfo ioc) {
			if ((  App.Kp2a.GetDb().Loaded && (App.Kp2a.IsShutdown() || App.Kp2a.GetDb().Locked) ) 
			    || (IocChanged(ioc, App.Kp2a.GetDb().Ioc))) //file was changed from ActionSend-Intent
			{
				act.SetResult(KeePass.ExitLock);
				act.Finish();
				return true;
			}
			return false;
		}
	}
}

