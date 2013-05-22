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
using Android.Views.InputMethods;

namespace keepass2android
{
	[Service]
	public class CopyToClipboardService: Service
	{

		
		public const int NOTIFY_USERNAME = 1;
		public const int NOTIFY_PASSWORD = 2;
		public const int NOTIFY_KEYBOARD = 3;
		public const int CLEAR_CLIPBOARD = 4;


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
			Android.Util.Log.Debug("DEBUG","Received intent to provide access to entry");
			
			String uuidBytes =  intent.GetStringExtra(EntryActivity.KEY_ENTRY);
			bool closeAfterCreate = intent.GetBooleanExtra(EntryActivity.KEY_CLOSE_AFTER_CREATE, false);
			
			PwUuid entryId = PwUuid.Zero;
			if (uuidBytes != null)
				entryId = new KeePassLib.PwUuid(MemUtil.HexStringToByteArray(uuidBytes));
			
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
			
			displayAccessNotifications(entry, closeAfterCreate);


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
				clearKeyboard();
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
			Bundle extra = new Bundle();
			extra.PutInt("requestCode", requestCode);
			intent.PutExtras(extra);

			return PendingIntent.GetBroadcast(this, requestCode, intent, PendingIntentFlags.CancelCurrent);
		}

		public void displayAccessNotifications(PwEntry entry, bool closeAfterCreate)
		{
			// Notification Manager
			mNM = (NotificationManager)GetSystemService(NotificationService);
		
			mNM.CancelAll();
			mNumElementsToWaitFor = 0;
			clearKeyboard();

			String entryName = entry.Strings.ReadSafe(PwDefs.TitleField);

			ISharedPreferences prefs = Android.Preferences.PreferenceManager.GetDefaultSharedPreferences(this);
			if (prefs.GetBoolean(GetString(Resource.String.CopyToClipboardNotification_key), Resources.GetBoolean(Resource.Boolean.CopyToClipboardNotification_default)))
			{

				if (entry.Strings.ReadSafe(PwDefs.PasswordField).Length > 0)
				{
					// only show notification if password is available
					Notification password = GetNotification(Intents.COPY_PASSWORD, Resource.String.copy_password, Resource.Drawable.notify, entryName);

					password.DeleteIntent = createDeleteIntent(NOTIFY_PASSWORD);
					mNM.Notify(NOTIFY_PASSWORD, password);
					mNumElementsToWaitFor++;

				}
				
				if (entry.Strings.ReadSafe(PwDefs.UserNameField).Length > 0)
				{
					// only show notification if username is available
					Notification username = GetNotification(Intents.COPY_USERNAME, Resource.String.copy_username, Resource.Drawable.notify, entryName);
					username.DeleteIntent = createDeleteIntent(NOTIFY_USERNAME);
					mNumElementsToWaitFor++;
					mNM.Notify(NOTIFY_USERNAME, username);
				}
			}

			if (prefs.GetBoolean(GetString(Resource.String.UseKp2aKeyboard_key), Resources.GetBoolean(Resource.Boolean.UseKp2aKeyboard_default)))
			{

				//keyboard
				if (makeAccessibleForKeyboard(entry))
				{
					// only show notification if username is available
					Notification keyboard = GetNotification(Intents.CHECK_KEYBOARD, Resource.String.available_through_keyboard, Resource.Drawable.notify_keyboard, entryName);
					keyboard.DeleteIntent = createDeleteIntent(NOTIFY_KEYBOARD);
					mNumElementsToWaitFor++;
					mNM.Notify(NOTIFY_KEYBOARD, keyboard);

                    //if the app is about to be closed again (e.g. after searching for a URL and returning to the browser:
                    // automatically bring up the Keyboard selection dialog
                    if (closeAfterCreate)
                    {
                        ActivateKp2aKeyboard(this);        
                    }
				}

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
			filter.AddAction(Intents.CHECK_KEYBOARD);
			RegisterReceiver(mCopyToClipBroadcastReceiver, filter);

			//register receiver to get notified when notifications are discarded in which case we can shutdown the service
			mNotificationDeletedBroadcastReceiver = new NotificationDeletedBroadcastReceiver(this);
			IntentFilter deletefilter = new IntentFilter();
			deletefilter.AddAction(ACTION_NOTIFICATION_CANCELLED);
			RegisterReceiver(mNotificationDeletedBroadcastReceiver, deletefilter);
		}

		bool makeAccessibleForKeyboard(PwEntry entry)
		{
			bool hasData = false;
			Keepass2android.Kbbridge.KeyboardDataBuilder kbdataBuilder = new Keepass2android.Kbbridge.KeyboardDataBuilder();

			String[] keys = {PwDefs.UserNameField, 
				PwDefs.PasswordField, 
				PwDefs.UrlField,
				PwDefs.NotesField,
				PwDefs.TitleField
			};
			int[] resIds = {Resource.String.entry_user_name,
				Resource.String.entry_password,
				Resource.String.entry_url,
				Resource.String.entry_comment,
				Resource.String.entry_title };

			//add standard fields:
			int i=0;
			foreach (string key in keys)
			{
				String value = entry.Strings.ReadSafe(key);
				if (value.Length > 0)
				{
					kbdataBuilder.AddPair(GetString(resIds[i]), value);
					hasData = true;
				}
				i++;
			}
			//add additional fields:
			foreach (var pair in entry.Strings)
			{
				String key = pair.Key;
				
				if (!PwDefs.IsStandardField(key)) {
					kbdataBuilder.AddPair(pair.Key, entry.Strings.ReadSafe(pair.Key));
				}
			}


			kbdataBuilder.Commit();
			Keepass2android.Kbbridge.KeyboardData.EntryName = entry.Strings.ReadSafe(PwDefs.TitleField);

			return hasData;

		}

		public void OnWaitElementDeleted(int itemId)
		{
			mNumElementsToWaitFor--;
			if (mNumElementsToWaitFor <= 0)
			{
				StopSelf();
			}
			if (itemId == NOTIFY_KEYBOARD)
			{
				//keyboard notification was deleted -> clear entries in keyboard
				clearKeyboard();
			}
		}

		void clearKeyboard()
		{
			Keepass2android.Kbbridge.KeyboardData.AvailableFields.Clear();
			Keepass2android.Kbbridge.KeyboardData.EntryName = null;
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
				handler.Post( () => { mService.OnWaitElementDeleted(CLEAR_CLIPBOARD); });
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

		private Notification GetNotification(String intentText, int descResId, int drawableResId, String entryName) {
			String desc = GetString(descResId);

			String title = GetString(Resource.String.app_name);
			if (!String.IsNullOrEmpty(entryName))
				title += " (" + entryName +")";


			Notification notify = new Notification(drawableResId, desc, Java.Lang.JavaSystem.CurrentTimeMillis());
			
			Intent intent = new Intent(intentText);
			PendingIntent pending = PendingIntent.GetBroadcast(this, descResId, intent, PendingIntentFlags.CancelCurrent);
			
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
			
			public override void OnReceive(Context context, Intent intent)
			{
				String action = intent.Action;
				
				if (action.Equals(Intents.COPY_USERNAME))
				{
					String username = mEntry.Strings.ReadSafe(PwDefs.UserNameField);
					if (username.Length > 0)
					{
						mService.timeoutCopyToClipboard(username);
					}
				} else if (action.Equals(Intents.COPY_PASSWORD))
				{
					String password = mEntry.Strings.ReadSafe(PwDefs.PasswordField);
					if (password.Length > 0)
					{
						mService.timeoutCopyToClipboard(password);
					}
				} else if (action.Equals(Intents.CHECK_KEYBOARD))
				{
                    CopyToClipboardService.ActivateKp2aKeyboard(mService);
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
					mService.OnWaitElementDeleted(intent.Extras.GetInt("requestCode"));
				}
			}
			#endregion
		}

        internal static void ActivateKp2aKeyboard(CopyToClipboardService service)
        {
            string currentIme = Android.Provider.Settings.Secure.GetString(
                                service.ContentResolver,
                                Android.Provider.Settings.Secure.DefaultInputMethod);

            string kp2aIme = service.PackageName + "/keepass2android.softkeyboard.KP2AKeyboard";

            InputMethodManager imeManager = (InputMethodManager)service.ApplicationContext.GetSystemService(Context.InputMethodService);

            if (currentIme == kp2aIme)
            {
                imeManager.ToggleSoftInput(ShowFlags.Forced, HideSoftInputFlags.None);
            }
            else
            {

                IList<InputMethodInfo> inputMethodProperties = imeManager.EnabledInputMethodList;

                if (!inputMethodProperties.Any(imi => imi.Id.Equals(kp2aIme)))
                {
                    Toast.MakeText(service, Resource.String.please_activate_keyboard, ToastLength.Long).Show();
                    Intent settingsIntent = new Intent(Android.Provider.Settings.ActionInputMethodSettings);
                    settingsIntent.SetFlags(ActivityFlags.NewTask);
                    service.StartActivity(settingsIntent);
                }
                else
                {
                    if (imeManager != null)
                    {
                        imeManager.ShowInputMethodPicker();
                    }
                    else
                    {
                        Toast.MakeText(service, Resource.String.not_possible_im_picker, ToastLength.Long).Show();
                    }
                }
            }
        }
        
    }
}

