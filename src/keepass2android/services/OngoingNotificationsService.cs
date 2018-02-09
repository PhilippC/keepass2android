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
using Android.Graphics;
using Android.OS;
using Android.Preferences;
using Android.Support.V4.App;
using KeePassLib.Utility;

namespace keepass2android
{
	/// <summary>
	/// Service for showing ongoing notifications
	/// 
	/// Shows database unlocked warning persistent notification
	/// Shows Quick-Unlock notification
	/// </summary>
	/// This service is running as foreground service to keep the app alive even when it's not currently
	/// used by the user. This ensures the database is kept in memory (until Android kills it due to low memory).
	/// It is important to also have a foreground service also for the "unlocked" state because it's really
	/// irritating if the db is closed while switching between apps.
	[Service]
	public class OngoingNotificationsService : Service
	{
		private ScreenOffReceiver _screenOffReceiver;

		#region Service
		private const int QuickUnlockId = 100;
		private const int UnlockedWarningId = 200;

		public override void OnCreate()
		{
			base.OnCreate();
			
			_screenOffReceiver = new ScreenOffReceiver();
			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intent.ActionScreenOff);
			RegisterReceiver(_screenOffReceiver, filter);
		}


		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{
			Kp2aLog.Log("Starting/Updating OngoingNotificationsService. Database " + (App.Kp2a.DatabaseIsUnlocked ? "Unlocked" : (App.Kp2a.QuickLocked ? "QuickLocked" : "Locked")));

			var notificationManager = (NotificationManager)GetSystemService(NotificationService);
					
			// Set the icon to reflect the current state
			if (App.Kp2a.DatabaseIsUnlocked)
			{
				// Clear current foreground status and QuickUnlock icon
				StopForeground(true);

				//use foreground again to let the app not be killed too easily.
				StartForeground(UnlockedWarningId, GetUnlockedNotification());
			}
			else 
			{
				notificationManager.Cancel(UnlockedWarningId);

				if (App.Kp2a.QuickLocked)
				{
					// Show the Quick Unlock notification
					StartForeground(QuickUnlockId, GetQuickUnlockNotification());
				}
				else
				{
					// Not showing any notification, database is locked, no point in keeping running
					StopSelf();
				}
			}

			return StartCommandResult.NotSticky;
		}

		public override void OnTaskRemoved(Intent rootIntent)
		{
			base.OnTaskRemoved(rootIntent);

			Kp2aLog.Log("OngoingNotificationsService.OnTaskRemoved: " + rootIntent.Action);

			// If the user has closed the task (probably by swiping it out of the recent apps list) then lock the database
			App.Kp2a.LockDatabase();
		}

		public override void OnDestroy()
		{
			base.OnDestroy();

			var notificationManager = (NotificationManager)GetSystemService(NotificationService);
			notificationManager.Cancel(UnlockedWarningId);
			// Quick Unlock notification should be removed automatically by the service (if present), as it was the foreground notification.

			Kp2aLog.Log("OngoingNotificationsService.OnDestroy");

			// If the service is killed, then lock the database immediately
			if (App.Kp2a.DatabaseIsUnlocked)
			{
				App.Kp2a.LockDatabase();
			}
			//also remove any notifications of the app
			notificationManager.CancelAll();

			UnregisterReceiver(_screenOffReceiver);
		}
		
		public override IBinder OnBind(Intent intent)
		{
			return null;
		}

		#endregion

		#region QuickUnlock

		private Notification GetQuickUnlockNotification()
		{
			int grayIconResouceId = Resource.Drawable.ic_launcher_gray;
			if ((int)Android.OS.Build.VERSION.SdkInt < 16)
			if (PreferenceManager.GetDefaultSharedPreferences(this).GetBoolean(GetString(Resource.String.QuickUnlockIconHidden_key), false))
			{
				grayIconResouceId = Resource.Drawable.transparent;
			}
			NotificationCompat.Builder builder = 
				new NotificationCompat.Builder(this, App.NotificationChannelIdQuicklocked)
					.SetSmallIcon(grayIconResouceId)
					.SetLargeIcon(MakeLargeIcon(BitmapFactory.DecodeResource(Resources, AppNames.NotificationLockedIcon)))
					.SetVisibility((int)Android.App.NotificationVisibility.Secret)
					.SetContentTitle(GetString(Resource.String.app_name))
					.SetContentText(GetString(Resource.String.database_loaded_quickunlock_enabled, GetDatabaseName()));

			if ((int)Build.VERSION.SdkInt >= 16)
			{
				if (PreferenceManager.GetDefaultSharedPreferences(this)
								 .GetBoolean(GetString(Resource.String.QuickUnlockIconHidden16_key), true))
				{
					builder.SetPriority((int) NotificationPriority.Min);
				}
				else
				{
					builder.SetPriority((int)NotificationPriority.Default);
				}
			}
			
			// Default action is to show Kp2A
			builder.SetContentIntent(GetSwitchToAppPendingIntent());
			// Additional action to allow locking the database
			builder.AddAction(Android.Resource.Drawable.IcLockLock, GetString(Resource.String.QuickUnlock_lockButton), 
				PendingIntent.GetBroadcast(this, 0, new Intent(this, typeof(ApplicationBroadcastReceiver)).SetAction(Intents.CloseDatabase), PendingIntentFlags.UpdateCurrent));
			

			return builder.Build();
		}

		private Bitmap MakeLargeIcon(Bitmap unscaled)
		{
			int height = (int)(0.9*Resources.GetDimension(Android.Resource.Dimension.NotificationLargeIconHeight));
			int width = (int)(0.9*Resources.GetDimension(Android.Resource.Dimension.NotificationLargeIconWidth));
			return Bitmap.CreateScaledBitmap(unscaled, width, height, true);
		}

		#endregion

		#region Unlocked Warning

		private Notification GetUnlockedNotification()
		{
			NotificationCompat.Builder builder =
				new NotificationCompat.Builder(this, App.NotificationChannelIdUnlocked)
					.SetOngoing(true)
					.SetSmallIcon(Resource.Drawable.ic_notify)
					.SetLargeIcon(MakeLargeIcon(BitmapFactory.DecodeResource(Resources, AppNames.NotificationUnlockedIcon)))
					.SetVisibility((int)Android.App.NotificationVisibility.Public)
					.SetContentTitle(GetString(Resource.String.app_name))
					.SetContentText(GetString(Resource.String.database_loaded_unlocked, GetDatabaseName()));

			if ((int)Build.VERSION.SdkInt >= 16)
			{
				if (PreferenceManager.GetDefaultSharedPreferences(this)
								 .GetBoolean(GetString(Resource.String.ShowUnlockedNotification_key),
											 Resources.GetBoolean(Resource.Boolean.ShowUnlockedNotification_default)))
				{
					builder.SetPriority((int)NotificationPriority.Default);
				}
				else
				{
					builder.SetPriority((int) NotificationPriority.Min);
				}
			}

			// Default action is to show Kp2A
			builder.SetContentIntent(GetSwitchToAppPendingIntent());
			// Additional action to allow locking the database
			builder.AddAction(Resource.Drawable.ic_action_lock, GetString(Resource.String.menu_lock), PendingIntent.GetBroadcast(this, 0, new Intent(this, typeof(ApplicationBroadcastReceiver)).SetAction(Intents.LockDatabase), PendingIntentFlags.UpdateCurrent));
			
			return builder.Build();
		}

		private PendingIntent GetSwitchToAppPendingIntent()
		{
			var startKp2aIntent = new Intent(this, typeof(KeePass));
			startKp2aIntent.SetAction(Intent.ActionMain);
			startKp2aIntent.AddCategory(Intent.CategoryLauncher);

			return PendingIntent.GetActivity(this, 0, startKp2aIntent, PendingIntentFlags.UpdateCurrent);
		}

		private static string GetDatabaseName()
		{
			
			var db = App.Kp2a.GetDb().KpDatabase;
			var name = db.Name;
			if (String.IsNullOrEmpty(name))
			{
				//todo: if paranoid ("don't remember recent files") return "***"
				name = App.Kp2a.GetFileStorage(db.IOConnectionInfo).GetFilenameWithoutPathAndExt(db.IOConnectionInfo);
			}

			return name;
		}
		#endregion

		class ScreenOffReceiver: BroadcastReceiver
		{
			public override void OnReceive(Context context, Intent intent)
			{
				App.Kp2a.OnScreenOff();
			}
		}
	}
}

