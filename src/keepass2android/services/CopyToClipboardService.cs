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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Java.Util;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Preferences;
using KeePassLib;
using KeePassLib.Utility;

namespace keepass2android
{
	[Service]
	public class CopyToClipboardService: Service
	{

		
		public const int NOTIFY_USERNAME = 1;
		public const int NOTIFY_PASSWORD = 2;


		public CopyToClipboardService (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
		}

		CopyToClipboardBroadcastReceiver mCopyToClipBroadcastReceiver;
		NotificationDeletedBroadcastReceiver mNotificationDeletedBroadcastReceiver;


		public CopyToClipboardService()
		{


		}
		public override void OnCreate()
		{
			base.OnCreate();
		}
	
		public override IBinder OnBind(Intent intent)
		{
			return null;
		}

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{
			Android.Util.Log.Debug("DEBUG","Received intent to copy to clipboard");
			
			String uuidBytes =  intent.GetStringExtra(EntryActivity.KEY_ENTRY);
			
			PwUuid entryId = PwUuid.Zero;
			if (uuidBytes != null)
				entryId = new KeePassLib.PwUuid(MemUtil.HexStringToByteArray(uuidBytes));
			
			/*Android.Util.Log.Debug("DEBUG","Uuid="+uuidBytes);
			
				
				foreach (PwUuid key in App.getDB().entries.Keys)
				{
					Android.Util.Log.Debug("DEBUG",key.ToHexString() + " -> " + App.getDB().entries[key].Uuid.ToHexString());
				}*/
			PwEntry entry;
			try
			{
				entry = App.getDB().entries[entryId];
			}
			catch(Exception)
			{
				//seems like restarting the service happened after closing the DB
				StopSelf();
				return StartCommandResult.NotSticky;
			}
			
			displayCopyNotifications(entry);

			return StartCommandResult.RedeliverIntent;
		}

		private NotificationManager mNM;
		private int mNumElementsToWaitFor = 0;
		
		public override void OnDestroy()
		{
			// These members might never get initialized if the app timed out
			if (mCopyToClipBroadcastReceiver != null)
			{
				UnregisterReceiver(mCopyToClipBroadcastReceiver);
			}
			if (mNotificationDeletedBroadcastReceiver != null)
			{
				UnregisterReceiver(mNotificationDeletedBroadcastReceiver);
			}
			if ( mNM != null ) {
				mNM.CancelAll();
				mNumElementsToWaitFor= 0;
			}

			Android.Util.Log.Debug("DEBUG", "Destroyed Show-Notification-Receiver.");
			
			base.OnDestroy();
		}

		static string ACTION_NOTIFICATION_CANCELLED = "notification_cancelled";

		//creates a delete intent (started when notification is cancelled by user or something else)
		//requires different request codes for every item (otherwise the intents are identical)
		PendingIntent createDeleteIntent(int requestCode)
		{
			Intent intent = new Intent(ACTION_NOTIFICATION_CANCELLED);

			return PendingIntent.GetBroadcast(this, requestCode, intent, PendingIntentFlags.CancelCurrent);
		}

		public void displayCopyNotifications(PwEntry entry)
		{
			// Notification Manager
			mNM = (NotificationManager)GetSystemService(NotificationService);
		
			mNM.CancelAll();
			mNumElementsToWaitFor = 0;


			String entryName = entry.Strings.ReadSafe(PwDefs.TitleField);

			if (entry.Strings.ReadSafe(PwDefs.PasswordField).Length > 0)
			{
				// only show notification if password is available
				Notification password = GetNotification(Intents.COPY_PASSWORD, Resource.String.copy_password, entryName);

				password.DeleteIntent = createDeleteIntent(0);
				mNM.Notify(NOTIFY_PASSWORD, password);
				mNumElementsToWaitFor++;

			}
			
			if (entry.Strings.ReadSafe(PwDefs.UserNameField).Length > 0)
			{
				// only show notification if username is available
				Notification username = GetNotification(Intents.COPY_USERNAME, Resource.String.copy_username, entryName);
				username.DeleteIntent = createDeleteIntent(1);
				mNumElementsToWaitFor++;
				mNM.Notify(NOTIFY_USERNAME, username);
			}

			if (mNumElementsToWaitFor == 0)
			{
				StopSelf();
				return;
			}
			
			mCopyToClipBroadcastReceiver = new CopyToClipboardBroadcastReceiver(entry, this);
			
			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intents.COPY_USERNAME);
			filter.AddAction(Intents.COPY_PASSWORD);
			RegisterReceiver(mCopyToClipBroadcastReceiver, filter);

			//register receiver to get notified when notifications are discarded in which case we can shutdown the service
			mNotificationDeletedBroadcastReceiver = new NotificationDeletedBroadcastReceiver(this);
			IntentFilter deletefilter = new IntentFilter();
			deletefilter.AddAction(ACTION_NOTIFICATION_CANCELLED);
			RegisterReceiver(mNotificationDeletedBroadcastReceiver, deletefilter);
		}

		public void OnWaitElementDeleted()
		{
			mNumElementsToWaitFor--;
			if (mNumElementsToWaitFor <= 0)
			{
				StopSelf();
			}
		}


		private Timer mTimer = new Timer();
		
		internal void timeoutCopyToClipboard(String text) {
			Util.copyToClipboard(this, text);
			
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
			String sClipClear = prefs.GetString(GetString(Resource.String.clipboard_timeout_key), GetString(Resource.String.clipboard_timeout_default));
			
			long clipClearTime = long.Parse(sClipClear);
			
			if ( clipClearTime > 0 ) {
				mNumElementsToWaitFor++;
				mTimer.Schedule(new ClearClipboardTask(this, text, uiThreadCallback), clipClearTime);
			}
		}

		// Task which clears the clipboard, and sends a toast to the foreground.
		private class ClearClipboardTask : Java.Util.TimerTask {
			
			private String mClearText;
			private CopyToClipboardService mService;
			private Handler handler;
			
			public ClearClipboardTask(CopyToClipboardService service, String clearText, Handler handler) {
				mClearText = clearText;
				mService = service;
				this.handler = handler;
			}
			
			public override void Run() {
				String currentClip = Util.getClipboard(mService);
				handler.Post( () => { mService.OnWaitElementDeleted(); });
				if ( currentClip.Equals(mClearText) ) {
					Util.copyToClipboard(mService, "");
					handler.Post( () => {
						Toast.MakeText(mService, Resource.String.ClearClipboard, ToastLength.Long).Show();
					});
				}
			}
		}

		
		// Setup to allow the toast to happen in the foreground
		Handler uiThreadCallback = new Handler();

		private Notification GetNotification(String intentText, int descResId, String entryName) {
			String desc = GetString(descResId);

			String title = GetString(Resource.String.app_name);
			if (!String.IsNullOrEmpty(entryName))
				title += " (" + entryName +")";


			Notification notify = new Notification(Resource.Drawable.notify, desc, Java.Lang.JavaSystem.CurrentTimeMillis());
			
			Intent intent = new Intent(intentText);
			PendingIntent pending = PendingIntent.GetBroadcast(this, 0, intent, PendingIntentFlags.CancelCurrent);
			
			notify.SetLatestEventInfo(this, title, desc, pending);
			
			return notify;
		}


		

		class CopyToClipboardBroadcastReceiver: BroadcastReceiver
		{
			CopyToClipboardService mService;
			public CopyToClipboardBroadcastReceiver(PwEntry entry, CopyToClipboardService service)
			{
				mEntry = entry;
				this.mService = service;
			}
			
			PwEntry mEntry;
			
			public override void OnReceive(Context context, Intent intent) {
				String action = intent.Action;
				
				if ( action.Equals(Intents.COPY_USERNAME) ) {
					String username = mEntry.Strings.ReadSafe (PwDefs.UserNameField);
					if ( username.Length > 0 ) {
						mService.timeoutCopyToClipboard(username);
					}
				} else if ( action.Equals(Intents.COPY_PASSWORD) ) {
					String password = mEntry.Strings.ReadSafe(PwDefs.PasswordField);
					if ( password.Length > 0 ) {
						mService.timeoutCopyToClipboard(password);
					}
				}
			}
		};

		class NotificationDeletedBroadcastReceiver: BroadcastReceiver
		{
			CopyToClipboardService mService;
			public NotificationDeletedBroadcastReceiver(CopyToClipboardService service)
			{
				this.mService = service;
			}

			#region implemented abstract members of BroadcastReceiver
			public override void OnReceive(Context context, Intent intent)
			{
				if (intent.Action == CopyToClipboardService.ACTION_NOTIFICATION_CANCELLED)
				{
					mService.OnWaitElementDeleted();
				}
			}
			#endregion
		}
	}
}

