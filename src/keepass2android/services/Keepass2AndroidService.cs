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
using Android.Graphics;
using Android.OS;
using Android.Preferences;
using Android.Support.V4.App;
using KeePassLib.Utility;

namespace keepass2android
{
	/// <summary>
	/// General purpose service for Keepass2Android
	/// 
	/// Manages timeout to lock the database after some idle time
	/// Shows database unlocked warning persistent notification
	/// Shows Quick-Unlock notification
	/// </summary>
	[Service]
	public class Keepass2AndroidService : Service {
		
		#region Service
		private const int ServiceId = 238787;

		private BroadcastReceiver _intentReceiver;

		public override void OnCreate() {
			base.OnCreate();
			
			_intentReceiver = new Keepass2AndroidServiceBroadcastReceiver(this);

			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intents.Timeout);
			filter.AddAction(Intents.LockDatabase);
			filter.AddAction(Intents.UnlockDatabase);
			RegisterReceiver(_intentReceiver, filter);
			
		}

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{
			Kp2aLog.Log("Starting Keepass2AndroidService");

			var prefs = PreferenceManager.GetDefaultSharedPreferences(this);

			Notification notification = null;
			
			if (prefs.GetBoolean(GetString(Resource.String.ShowUnlockedNotification_key), Resources.GetBoolean(Resource.Boolean.ShowUnlockedNotification_default))
				&& App.Kp2a.DatabaseIsUnlocked)
			{
				// Show the Unlocked icon
				notification = GetUnlockedNotification();

			}
			else if (App.Kp2a.QuickUnlockEnabled)
			{
				// Show the Quick Unlock icon
				notification = GetQuickUnlockNotification();
			}
			
			if (notification != null)
			{
				if (App.Kp2a.QuickUnlockEnabled)
				{
					StartForeground(ServiceId, notification);
				}
				else
				{
					// Doesn't actually need to be persistent in memory, allow it to be killed as required
					var notificationManager = (NotificationManager)GetSystemService(NotificationService);
					notificationManager.Notify(ServiceId, notification);
				}
			}

			return StartCommandResult.NotSticky;
		}

		public override void OnDestroy()
		{
			base.OnDestroy();

			var notificationManager = (NotificationManager)GetSystemService(NotificationService);
			notificationManager.Cancel(ServiceId);

			Kp2aLog.Log("Destroying Keepass2AndroidService");
			UnregisterReceiver(_intentReceiver);

			// The service will be stopped deliberately to cause the database to lock.
			// If the service is killed, then also lock the database immediately (as timeout will no longer work, and the unlocked warning icon will no longer display)
			Application.Context.SendBroadcast(new Intent(Intents.LockDatabase)); // Ensure all other listeners receive the Lock broadcast
			App.Kp2a.LockDatabaseInternal(this); 
		}
		
		public override IBinder OnBind(Intent intent)
		{
			return null;
		}
		#endregion

		#region Timeout
		private void Timeout()
		{
			Kp2aLog.Log("Timeout");
			StopSelf();
		}
		#endregion

		#region QuickUnlock

		private Notification GetQuickUnlockNotification()
		{
			NotificationCompat.Builder builder = 
				new NotificationCompat.Builder(this)
					.SetSmallIcon(Resource.Drawable.ic_launcher_gray)
					.SetLargeIcon(BitmapFactory.DecodeResource(Resources, AppNames.LauncherIcon))
					.SetContentTitle(GetText(Resource.String.app_name))
					.SetContentText(GetString(Resource.String.database_loaded_quickunlock_enabled, GetDatabaseName()));

			Intent startKp2aIntent = new Intent(this, typeof(KeePass));
			startKp2aIntent.SetAction(Intent.ActionMain);
			startKp2aIntent.AddCategory(Intent.CategoryLauncher);

			PendingIntent startKp2APendingIntent =
				PendingIntent.GetActivity(this, 0, startKp2aIntent, PendingIntentFlags.UpdateCurrent);
			builder.SetContentIntent(startKp2APendingIntent);

			return builder.Build();
		}
		#endregion

		#region Unlocked Warning
		private Notification GetUnlockedNotification()
		{
			NotificationCompat.Builder builder =
				new NotificationCompat.Builder(this)
					.SetOngoing(true)
					.SetSmallIcon(Resource.Drawable.ic_unlocked_gray)
					.SetLargeIcon(BitmapFactory.DecodeResource(Resources, Resource.Drawable.ic_launcher_red))
					.SetContentTitle(GetText(Resource.String.app_name))
					.SetContentText(GetString(Resource.String.database_loaded_unlocked, GetDatabaseName()));

			builder.SetContentIntent(PendingIntent.GetBroadcast(this, 0, new Intent(Intents.LockDatabase), PendingIntentFlags.UpdateCurrent));

			return builder.Build();
		}

		private static string GetDatabaseName()
		{
			var db = App.Kp2a.GetDb().KpDatabase;
			var name = db.Name;
			if (String.IsNullOrEmpty(name))
			{
				name = UrlUtil.StripExtension(UrlUtil.GetFileName(db.IOConnectionInfo.Path));
			}

			return name;
		}

		private void LockDatabase()
		{
			Kp2aLog.Log("LockDatabase");
			StopSelf();
		}

		private void UnlockDatabase()
		{
			Kp2aLog.Log("UnlockDatabase");

			// Replace the QuickLock icon with the Unlocked icon. QuickLockEnabled must be true, so we need a foreground service to prevent being killed
			StartForeground(ServiceId, GetUnlockedNotification());

			App.Kp2a.UnlockDatabaseInternal(this);
		}
		#endregion

		[BroadcastReceiver]
		private class Keepass2AndroidServiceBroadcastReceiver: BroadcastReceiver
		{
			public Keepass2AndroidServiceBroadcastReceiver()
			{
				//dummy constructor required for MonoForAndroid, not called.
				throw new NotImplementedException();
			}

			readonly Keepass2AndroidService _service;
			public Keepass2AndroidServiceBroadcastReceiver(Keepass2AndroidService service)
			{
				_service = service;
			}

			public override void OnReceive(Context context, Intent intent)
			{
				switch (intent.Action)
				{
					case Intents.Timeout:
						_service.Timeout();
						break;
					case Intents.LockDatabase:
						_service.LockDatabase();
						break;
					case Intents.UnlockDatabase:
						_service.UnlockDatabase();
						break;

				}
			}
		}
	}
}

