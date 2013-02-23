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
using Android.Util;

namespace keepass2android
{
	
	public class Timeout {
		private const int REQUEST_ID = 0;
		private const long DEFAULT_TIMEOUT = 5 * 60 * 1000;  // 5 minutes
		private static String TAG = "Keepass2Android Timeout";
		
		private static PendingIntent buildIntent(Context ctx) {
			Intent intent = new Intent(Intents.TIMEOUT);
			PendingIntent sender = PendingIntent.GetBroadcast(ctx, REQUEST_ID, intent, PendingIntentFlags.CancelCurrent);
			
			return sender;
		}
		
		public static void start(Context ctx) {
			
			
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(ctx);
			String sTimeout = prefs.GetString(ctx.GetString(Resource.String.app_timeout_key), ctx.GetString(Resource.String.clipboard_timeout_default));
			
			long timeout;
			if (!long.TryParse(sTimeout, out timeout))
			{
				timeout = DEFAULT_TIMEOUT;
			}
			
			if ( timeout == -1 ) {
				// No timeout don't start timeout service
				return;
			}
			
			ctx.StartService(new Intent(ctx, typeof(TimeoutService)));
			
			long triggerTime = Java.Lang.JavaSystem.CurrentTimeMillis() + timeout;
			AlarmManager am = (AlarmManager) ctx.GetSystemService(Context.AlarmService);
			
			Log.Debug(TAG, "Timeout start");
			am.Set(AlarmType.Rtc, triggerTime, buildIntent(ctx));
		}
		
		public static void cancel(Context ctx) {
			AlarmManager am = (AlarmManager) ctx.GetSystemService(Context.AlarmService);
			
			Log.Debug(TAG, "Timeout cancel");
			am.Cancel(buildIntent(ctx));
			
			ctx.StopService(new Intent(ctx, typeof(TimeoutService)));
			
		}
		
	}
}

