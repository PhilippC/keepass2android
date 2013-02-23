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
using Android.Util;

namespace keepass2android
{
	[Service]
	public class TimeoutService : Service {
		private const String TAG = "KeePass2Android Timer"; 
		private BroadcastReceiver mIntentReceiver;


		public TimeoutService (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			mBinder	= new TimeoutBinder(this);
		}
		public TimeoutService()
		{
			mBinder	= new TimeoutBinder(this);
		}
		
		public override void OnCreate() {
			base.OnCreate();
			
			mIntentReceiver = new MyBroadcastReceiver(this);

			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intents.TIMEOUT);
			RegisterReceiver(mIntentReceiver, filter);
			
		}


		public override void OnStart(Intent intent, int startId) {
			base.OnStart(intent, startId);
			
			Log.Debug(TAG, "Timeout service started");
		}
		
		private void timeout(Context context) {
			Log.Debug(TAG, "Timeout");
			App.setShutdown();
			
			NotificationManager nm = (NotificationManager) GetSystemService(NotificationService);
			nm.CancelAll();
			StopService(new Intent(this, typeof(CopyToClipboardService)));
			StopSelf();
		}
		
		public override void OnDestroy() {
			base.OnDestroy();
			
			Log.Debug(TAG, "Timeout service stopped");
			
			UnregisterReceiver(mIntentReceiver);
		}
		
		public class TimeoutBinder : Binder 
		{
			TimeoutService service;

			public TimeoutBinder(TimeoutService service)
			{
				this.service = service;
			}

			public TimeoutService getService() {
				return service;
			}
		}
		
		private IBinder mBinder;
		
		
		public override IBinder OnBind(Intent intent) {
			return mBinder;
		}

		[BroadcastReceiver]
		public class MyBroadcastReceiver: BroadcastReceiver
		{
			public MyBroadcastReceiver()
			{
				//dummy constructor required for MonoForAndroid, not called.
				throw new NotImplementedException();
			}

			TimeoutService timeoutService;
			public MyBroadcastReceiver (TimeoutService timeoutService)
			{
				this.timeoutService = timeoutService;
			}

			public override void OnReceive(Context context, Intent intent) {
				String action = intent.Action;
				
				if ( action.Equals(Intents.TIMEOUT) ) {
					timeoutService.timeout(context);
				}
			}
		}
		
	}
}

