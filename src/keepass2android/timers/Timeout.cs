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
using Android.Util;

namespace keepass2android
{
	
	public class Timeout {
		private const int RequestId = 0;
		private const long DefaultTimeout = 5 * 60 * 1000;  // 5 minutes
		private const String Tag = "Keepass2Android Timeout";

		private static PendingIntent BuildIntent(Context ctx) {
			Intent intent = new Intent(Intents.Timeout);
			PendingIntent sender = PendingIntent.GetBroadcast(ctx, RequestId, intent, PendingIntentFlags.CancelCurrent);
			
			return sender;
		}
		
		public static void Start(Context ctx) {
			
			
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(ctx);
			String sTimeout = prefs.GetString(ctx.GetString(Resource.String.app_timeout_key), ctx.GetString(Resource.String.clipboard_timeout_default));
			
			long timeout;
			if (!long.TryParse(sTimeout, out timeout))
			{
				timeout = DefaultTimeout;
			}
			
			if ( timeout == -1 ) {
				// No timeout don't start timeout service
				return;
			}
			
			ctx.StartService(new Intent(ctx, typeof(TimeoutService)));
			
			long triggerTime = Java.Lang.JavaSystem.CurrentTimeMillis() + timeout;
			AlarmManager am = (AlarmManager) ctx.GetSystemService(Context.AlarmService);
			
			Log.Debug(Tag, "Timeout start");
			am.Set(AlarmType.Rtc, triggerTime, BuildIntent(ctx));
		}
		
		public static void Cancel(Context ctx) {
			AlarmManager am = (AlarmManager) ctx.GetSystemService(Context.AlarmService);
			
			Log.Debug(Tag, "Timeout cancel");
			am.Cancel(BuildIntent(ctx));
			
			ctx.StopService(new Intent(ctx, typeof(TimeoutService)));
			
		}
		
	}
}

