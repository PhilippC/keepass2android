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
	/// Service for showing ongoing notifications
	/// 
	/// Shows database unlocked warning persistent notification
	/// Shows Quick-Unlock notification
	/// </summary>
	[Service]
	public class OngoingNotificationsService : Service
	{
		
		#region Service
		private const int QuickUnlockId = 100;
		private const int UnlockedWarningId = 200;

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{
			Kp2aLog.Log("Starting/Updating OngoingNotificationsService. Database " + (App.Kp2a.DatabaseIsUnlocked ? "Unlocked" : (App.Kp2a.QuickLocked ? "QuickLocked" : "Locked")));

			// Clear current foreground status and QuickUnlock icon
			StopForeground(true);

			// Set the icon to reflect the current state
			if (App.Kp2a.DatabaseIsUnlocked && ShowUnlockedNotification)
			{
				StartForeground(UnlockedWarningId, GetUnlockedNotification());
			}
			else if (App.Kp2a.QuickLocked)
			{
				// Show the Quick Unlock notification
				StartForeground(QuickUnlockId, GetQuickUnlockNotification());
			}

			return StartCommandResult.NotSticky;
		}

		private bool ShowUnlockedNotification
		{
			get { return PreferenceManager.GetDefaultSharedPreferences(this).GetBoolean(GetString(Resource.String.ShowUnlockedNotification_key), Resources.GetBoolean(Resource.Boolean.ShowUnlockedNotification_default)); }
		}

		public override void OnTaskRemoved(Intent rootIntent)
		{
			base.OnTaskRemoved(rootIntent);

			Kp2aLog.Log("OngoingNotificationsService.OnTaskRemoved");

			// If the user has closed the task (probably by swiping it out of the recent apps list) then lock the database
			App.Kp2a.LockDatabase();
		}

		public override void OnDestroy()
		{
			base.OnDestroy();

			Kp2aLog.Log("OngoingNotificationsService.OnDestroy2");

			if (ShowUnlockedNotification)
			{
				// If the service is killed, then lock the database immediately (as the unlocked warning icon will no longer display).
				App.Kp2a.LockDatabase();
			}
		}
		
		public override IBinder OnBind(Intent intent)
		{
			return null;
		}

		public override void OnLowMemory()
		{
			base.OnLowMemory();

			Kp2aLog.Log("OngoingNotificationsService.OnLowMemory");

			if (App.Kp2a.DatabaseIsUnlocked && !App.Kp2a.QuickUnlockEnabled)
			{
				// Although this is a foreground service, if it is only indicating that the database is unlocked then it isn't of foreground-importance,
				// and can be killed if Android requests it (which will clear the database and free up some memory)
				StopSelf();
			}
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
		#endregion
	}
}

