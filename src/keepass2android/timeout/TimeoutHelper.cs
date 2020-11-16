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
using Android.App;
using Android.Content;
using Android.Preferences;
using Android.Util;
using KeePassLib.Serialization;

namespace keepass2android
{
	/// <summary>
	/// Helper class to simplify usage of timeout (lock after idle time) from the activities
	/// </summary>
	public class TimeoutHelper {

		private static class Timeout
		{
			private const long DefaultTimeout = 5 * 60 * 1000;  // 5 minutes
			
			private static PendingIntent BuildPendingBroadcastIntent(Context ctx)
			{
				return PendingIntent.GetBroadcast(ctx, 0, BuildBroadcastIntent(ctx), PendingIntentFlags.UpdateCurrent);
			}

            private static Intent BuildBroadcastIntent(Context ctx)
            {
                return new Intent(ctx, typeof(ApplicationBroadcastReceiver)).SetAction(Intents.LockDatabaseByTimeout);
            }

            public static void LeavingApp(Context ctx)
			{
				ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(ctx);
				String sTimeout = prefs.GetString(ctx.GetString(Resource.String.app_timeout_key), ctx.GetString(Resource.String.clipboard_timeout_default));

				long timeout;
				if (!long.TryParse(sTimeout, out timeout))
				{
					timeout = DefaultTimeout;
				}

				if (timeout == -1)
				{
					// No timeout don't start timeout service
					return;
				}

				//in addition to starting an alarm, check the time in the next resume. This might be required as alarms seems to be unrealiable in more recent android versions
                App.Kp2a.TimeoutTime = DateTime.Now + TimeSpan.FromMilliseconds(timeout);

				long triggerTime = Java.Lang.JavaSystem.CurrentTimeMillis() + timeout;
				AlarmManager am = (AlarmManager)ctx.GetSystemService(Context.AlarmService);

				Kp2aLog.Log("Timeout start");
				am.Set(AlarmType.Rtc, triggerTime, BuildPendingBroadcastIntent(App.Context));
			}

			public static void ResumingApp(Context ctx)
			{
                
                
				AlarmManager am = (AlarmManager)ctx.GetSystemService(Context.AlarmService);
				//cancel alarm
				Kp2aLog.Log("Timeout cancel");
				am.Cancel(BuildPendingBroadcastIntent(App.Context));
                App.Kp2a.TimeoutTime = DateTime.MaxValue;
            }


            public static void CheckIfTimedOutWithoutAlarm(Context ctx)
            {
                if (DateTime.Now > App.Kp2a.TimeoutTime)
                {
                    ctx.SendBroadcast(BuildBroadcastIntent(ctx));
                }
			}
        }

		public static void Pause(Activity act) 
		{
			if ( App.Kp2a.DatabaseIsUnlocked )
			{
				Timeout.LeavingApp(act);
			}
			
		}
		
		public static void Resume(Activity act)
        {
            if ( App.Kp2a.CurrentDb!= null)
            {
                Timeout.CheckIfTimedOutWithoutAlarm(act);
				Timeout.ResumingApp(act);
			}
		}

        public static void ResumingApp()
        {
            Timeout.ResumingApp(App.Context);
		}


		static bool IocChanged(IOConnectionInfo ioc, IOConnectionInfo other)
		{
			if ((ioc == null) || (other == null)) return false;
			return !ioc.IsSameFileAs(other);
		}
		
		public static bool CheckDbChanged(Activity act, IOConnectionInfo ioc) {
			if ((  !App.Kp2a.DatabaseIsUnlocked ) 
			    || (IocChanged(ioc, App.Kp2a.CurrentDb.Ioc))) //file was changed from ActionSend-Intent
			{
				return true;
			}
			return false;
		}

        public static void Lock(Activity activity, in bool lockedByTimeout)
        {
            Kp2aLog.Log("Finishing " + activity.ComponentName.ClassName + " due to database lock");

            activity.SetResult(lockedByTimeout ? KeePass.ExitLockByTimeout : KeePass.ExitLock);
            activity.Finish();
		}
    }
}

