using System;
using System.Linq;
using Android.Content;
using Android.App;
using Android.Preferences;


namespace keepass2android
{
	[BroadcastReceiver]
	public class ApplicationBroadcastReceiver : BroadcastReceiver
	{
		public override void OnReceive(Context context, Intent intent)
		{
			Kp2aLog.Log("Received broadcast intent: " + intent.Action);

			switch (intent.Action)
			{
				case Intents.LockDatabase:
					App.Kp2a.LockDatabase();
					break;
				case Intents.CloseDatabase:
					App.Kp2a.LockDatabase(false /*no quick unlock*/);
					break;
			}
		}
	}
}