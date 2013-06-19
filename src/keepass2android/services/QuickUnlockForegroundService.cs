/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

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

using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.App;
using Android.Graphics;

namespace keepass2android
{
	/// <summary>
	/// This service is started as soon as a Database with QuickUnlock enabled is opened.
	/// Its only purpose is to be a foreground service which prevents the App from being killed (in most situations)
	/// </summary>
[Service]
	public class QuickUnlockForegroundService : Service
	{
		public override IBinder OnBind(Intent intent)
		{
			return null;
		}		

		const int QuickUnlockForegroundServiceId = 238787;

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{
			Android.Util.Log.Debug("DEBUG", "Starting QuickUnlockForegroundService");

			//create notification item
			NotificationCompat.Builder mBuilder =
				new NotificationCompat.Builder(this)
					.SetSmallIcon(Resource.Drawable.ic_launcher_gray)
					.SetLargeIcon(BitmapFactory.DecodeResource(Resources,  Resource.Drawable.ic_launcher))
 					.SetContentTitle(GetText(Resource.String.app_name))
					.SetContentText(GetText(Resource.String.database_loaded_quickunlock_enabled));

			Intent startKp2aIntent = new Intent(this, typeof(KeePass));
			startKp2aIntent.SetAction(Intent.ActionMain);
			startKp2aIntent.AddCategory(Intent.CategoryLauncher);

			PendingIntent startKp2APendingIntent =
				PendingIntent.GetActivity(this, 0, startKp2aIntent, PendingIntentFlags.UpdateCurrent);
			mBuilder.SetContentIntent(startKp2APendingIntent); 
			StartForeground(QuickUnlockForegroundServiceId , mBuilder.Build());

			return StartCommandResult.NotSticky;

		}

		public override void OnCreate()
		{
			base.OnCreate();
			Android.Util.Log.Debug("DEBUG", "Creating QuickUnlockForegroundService");
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			Android.Util.Log.Debug("DEBUG", "Destroying QuickUnlockForegroundService");
		}
	}
}

