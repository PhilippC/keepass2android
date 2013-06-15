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
using Android.OS;
using Android.Runtime;
using Android.Util;

namespace keepass2android
{
	/// <summary>
	/// Manages timeout to lock the database after some idle time
	/// </summary>
	[Service]
	public class TimeoutService : Service {
		private const String Tag = "KeePass2Android Timer"; 
		private BroadcastReceiver _intentReceiver;


		public TimeoutService (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			_binder	= new TimeoutBinder(this);
		}
		public TimeoutService()
		{
			_binder	= new TimeoutBinder(this);
		}
		
		public override void OnCreate() {
			base.OnCreate();
			
			_intentReceiver = new MyBroadcastReceiver(this);

			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intents.Timeout);
			RegisterReceiver(_intentReceiver, filter);
			
		}


		public override void OnStart(Intent intent, int startId) {
			base.OnStart(intent, startId);
			
			Log.Debug(Tag, "Timeout service started");
		}
		
		private void Timeout() {
			Log.Debug(Tag, "Timeout");
            App.Kp2a.SetShutdown();
			
			NotificationManager nm = (NotificationManager) GetSystemService(NotificationService);
			nm.CancelAll();
			StopService(new Intent(this, typeof(CopyToClipboardService)));
			StopSelf();
		}
		
		public override void OnDestroy() {
			base.OnDestroy();
			
			Log.Debug(Tag, "Timeout service stopped");
			
			UnregisterReceiver(_intentReceiver);
		}
		
		public class TimeoutBinder : Binder 
		{
			readonly TimeoutService _service;

			public TimeoutBinder(TimeoutService service)
			{
				_service = service;
			}

			public TimeoutService GetService() {
				return _service;
			}
		}
		
		private readonly IBinder _binder;
		
		
		public override IBinder OnBind(Intent intent) {
			return _binder;
		}

		[BroadcastReceiver]
		public class MyBroadcastReceiver: BroadcastReceiver
		{
			public MyBroadcastReceiver()
			{
				//dummy constructor required for MonoForAndroid, not called.
				throw new NotImplementedException();
			}

			readonly TimeoutService _timeoutService;
			public MyBroadcastReceiver (TimeoutService timeoutService)
			{
				_timeoutService = timeoutService;
			}

			public override void OnReceive(Context context, Intent intent) {
				String action = intent.Action;
				
				if ( action.Equals(Intents.Timeout) ) {
					_timeoutService.Timeout();
				}
			}
		}
		
	}
}

