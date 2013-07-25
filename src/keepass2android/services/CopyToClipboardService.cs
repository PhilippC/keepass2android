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
using Java.Util;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using Android.Preferences;
using KeePassLib;
using KeePassLib.Utility;
using Android.Views.InputMethods;
using KeePass.Util.Spr;

namespace keepass2android
{
	/// <summary>
	/// Service to show the notifications to make the current entry accessible through clipboard or the KP2A keyboard.
	/// </summary>
	/// The name reflects only the possibility through clipboard because keyboard was introduced later.
	/// The notifications require to be displayed by a service in order to be kept when the activity is closed
	/// after searching for a URL.
	[Service]
	public class CopyToClipboardService: Service
	{

		
		public const int NotifyUsername = 1;
		public const int NotifyPassword = 2;
		public const int NotifyKeyboard = 3;
		public const int ClearClipboard = 4;


		public CopyToClipboardService (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
		}

		CopyToClipboardBroadcastReceiver _copyToClipBroadcastReceiver;
		NotificationDeletedBroadcastReceiver _notificationDeletedBroadcastReceiver;
		StopOnLockBroadcastReceiver _stopOnLockBroadcastReceiver;

		public CopyToClipboardService()
		{


		}

		public override IBinder OnBind(Intent intent)
		{
			return null;
		}

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{
			Kp2aLog.Log("Received intent to provide access to entry");

			_stopOnLockBroadcastReceiver = new StopOnLockBroadcastReceiver(this);
			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intents.LockDatabase);
			RegisterReceiver(_stopOnLockBroadcastReceiver, filter);

			String uuidBytes =  intent.GetStringExtra(EntryActivity.KeyEntry);
			bool closeAfterCreate = intent.GetBooleanExtra(EntryActivity.KeyCloseAfterCreate, false);
			
			PwUuid entryId = PwUuid.Zero;
			if (uuidBytes != null)
				entryId = new PwUuid(MemUtil.HexStringToByteArray(uuidBytes));
			
			PwEntry entry;
			try
			{
				entry = App.Kp2a.GetDb().Entries[entryId];
			}
			catch(Exception)
			{
				//seems like restarting the service happened after closing the DB
				StopSelf();
				return StartCommandResult.NotSticky;
			}
			
			DisplayAccessNotifications(entry, closeAfterCreate);


			return StartCommandResult.RedeliverIntent;
		}

		private void OnLockDatabase()
		{
			Kp2aLog.Log("Stopping clipboard service due to database lock");

			StopSelf();
		}

		private NotificationManager _notificationManager;
		private int _numElementsToWaitFor;
		
		public override void OnDestroy()
		{
			// These members might never get initialized if the app timed out
			if (_stopOnLockBroadcastReceiver != null)
			{
				UnregisterReceiver(_stopOnLockBroadcastReceiver);
			}
			if (_copyToClipBroadcastReceiver != null)
			{
				UnregisterReceiver(_copyToClipBroadcastReceiver);
			}
			if (_notificationDeletedBroadcastReceiver != null)
			{
				UnregisterReceiver(_notificationDeletedBroadcastReceiver);
			}
			if ( _notificationManager != null ) {
				_notificationManager.CancelAll();
				_numElementsToWaitFor= 0;
				clearKeyboard();
			}

			Kp2aLog.Log("Destroyed Show-Notification-Receiver.");
			
			base.OnDestroy();
		}

		private const string ActionNotificationCancelled = "notification_cancelled";

		//creates a delete intent (started when notification is cancelled by user or something else)
		//requires different request codes for every item (otherwise the intents are identical)
		PendingIntent CreateDeleteIntent(int requestCode)
		{
			Intent intent = new Intent(ActionNotificationCancelled);
			Bundle extra = new Bundle();
			extra.PutInt("requestCode", requestCode);
			intent.PutExtras(extra);

			return PendingIntent.GetBroadcast(this, requestCode, intent, PendingIntentFlags.CancelCurrent);
		}

		public void DisplayAccessNotifications(PwEntry entry, bool closeAfterCreate)
		{
			// Notification Manager
			_notificationManager = (NotificationManager)GetSystemService(NotificationService);
		
			_notificationManager.CancelAll();
			_numElementsToWaitFor = 0;
			clearKeyboard();

			String entryName = entry.Strings.ReadSafe(PwDefs.TitleField);

			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
			if (prefs.GetBoolean(GetString(Resource.String.CopyToClipboardNotification_key), Resources.GetBoolean(Resource.Boolean.CopyToClipboardNotification_default)))
			{

				if (GetStringAndReplacePlaceholders(entry, PwDefs.PasswordField).Length > 0)
				{
					// only show notification if password is available
					Notification password = GetNotification(Intents.CopyPassword, Resource.String.copy_password, Resource.Drawable.notify, entryName);

					password.DeleteIntent = CreateDeleteIntent(NotifyPassword);
					_notificationManager.Notify(NotifyPassword, password);
					_numElementsToWaitFor++;

				}
				
				if (GetStringAndReplacePlaceholders(entry, PwDefs.UserNameField).Length > 0)
				{
					// only show notification if username is available
					Notification username = GetNotification(Intents.CopyUsername, Resource.String.copy_username, Resource.Drawable.notify, entryName);
					username.DeleteIntent = CreateDeleteIntent(NotifyUsername);
					_numElementsToWaitFor++;
					_notificationManager.Notify(NotifyUsername, username);
				}
			}

			if (prefs.GetBoolean(GetString(Resource.String.UseKp2aKeyboard_key), Resources.GetBoolean(Resource.Boolean.UseKp2aKeyboard_default)))
			{

				//keyboard
				if (MakeAccessibleForKeyboard(entry))
				{
					// only show notification if username is available
					Notification keyboard = GetNotification(Intents.CheckKeyboard, Resource.String.available_through_keyboard, Resource.Drawable.notify_keyboard, entryName);
					keyboard.DeleteIntent = CreateDeleteIntent(NotifyKeyboard);
					_numElementsToWaitFor++;
					_notificationManager.Notify(NotifyKeyboard, keyboard);

                    //if the app is about to be closed again (e.g. after searching for a URL and returning to the browser:
                    // automatically bring up the Keyboard selection dialog
					if ((closeAfterCreate) && prefs.GetBoolean(GetString(Resource.String.OpenKp2aKeyboardAutomatically_key), Resources.GetBoolean(Resource.Boolean.OpenKp2aKeyboardAutomatically_default)))
                    {
                        ActivateKp2aKeyboard(this);        
                    }
				}

			}

			if (_numElementsToWaitFor == 0)
			{
				StopSelf();
				return;
			}

			_copyToClipBroadcastReceiver = new CopyToClipboardBroadcastReceiver(entry, this);
			
			IntentFilter filter = new IntentFilter();
			filter.AddAction(Intents.CopyUsername);
			filter.AddAction(Intents.CopyPassword);
			filter.AddAction(Intents.CheckKeyboard);
			RegisterReceiver(_copyToClipBroadcastReceiver, filter);

			//register receiver to get notified when notifications are discarded in which case we can shutdown the service
			_notificationDeletedBroadcastReceiver = new NotificationDeletedBroadcastReceiver(this);
			IntentFilter deletefilter = new IntentFilter();
			deletefilter.AddAction(ActionNotificationCancelled);
			RegisterReceiver(_notificationDeletedBroadcastReceiver, deletefilter);
		}

		bool MakeAccessibleForKeyboard(PwEntry entry)
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
				String value = GetStringAndReplacePlaceholders(entry, key);

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

				var value = GetStringAndReplacePlaceholders(entry, key);


				if (!PwDefs.IsStandardField(key)) {
					kbdataBuilder.AddPair(pair.Key, value);
				}
			}


			kbdataBuilder.Commit();
			Keepass2android.Kbbridge.KeyboardData.EntryName = entry.Strings.ReadSafe(PwDefs.TitleField);

			return hasData;

		}

		static string GetStringAndReplacePlaceholders(PwEntry entry, string key)
		{
			String value = entry.Strings.ReadSafe(key);
			value = SprEngine.Compile(value, new SprContext(entry, App.Kp2a.GetDb().KpDatabase, SprCompileFlags.All));
			return value;
		}

		public void OnWaitElementDeleted(int itemId)
		{
			_numElementsToWaitFor--;
			if (_numElementsToWaitFor <= 0)
			{
				StopSelf();
			}
			if (itemId == NotifyKeyboard)
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

		private readonly Timer _timer = new Timer();
		
		internal void TimeoutCopyToClipboard(String text) {
			Util.copyToClipboard(this, text);
			
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
			String sClipClear = prefs.GetString(GetString(Resource.String.clipboard_timeout_key), GetString(Resource.String.clipboard_timeout_default));
			
			long clipClearTime = long.Parse(sClipClear);
			
			if ( clipClearTime > 0 ) {
				_numElementsToWaitFor++;
				_timer.Schedule(new ClearClipboardTask(this, text, _uiThreadCallback), clipClearTime);
			}
		}

		// Task which clears the clipboard, and sends a toast to the foreground.
		private class ClearClipboardTask : TimerTask {
			
			private readonly String _clearText;
			private readonly CopyToClipboardService _service;
			private readonly Handler _handler;
			
			public ClearClipboardTask(CopyToClipboardService service, String clearText, Handler handler) {
				_clearText = clearText;
				_service = service;
				_handler = handler;
			}
			
			public override void Run() {
				String currentClip = Util.getClipboard(_service);
				_handler.Post( () => _service.OnWaitElementDeleted(ClearClipboard));
				if ( currentClip.Equals(_clearText) ) {
					Util.copyToClipboard(_service, "");
					_handler.Post( () => {
						Toast.MakeText(_service, Resource.String.ClearClipboard, ToastLength.Long).Show();
					});
				}
			}
		}

		
		// Setup to allow the toast to happen in the foreground
		readonly Handler _uiThreadCallback = new Handler();

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

		private class StopOnLockBroadcastReceiver : BroadcastReceiver
		{
			readonly CopyToClipboardService _service;
			public StopOnLockBroadcastReceiver(CopyToClipboardService service)
			{
				_service = service;
			}

			public override void OnReceive(Context context, Intent intent)
			{
				switch (intent.Action)
				{
					case Intents.LockDatabase:
						_service.OnLockDatabase();
						break;
				}
			}
		}
		
		class CopyToClipboardBroadcastReceiver: BroadcastReceiver
		{
			readonly CopyToClipboardService _service;
			readonly PwEntry _entry;
			
			public CopyToClipboardBroadcastReceiver(PwEntry entry, CopyToClipboardService service)
			{
				_entry = entry;
				_service = service;
			}
			
			
			public override void OnReceive(Context context, Intent intent)
			{
				String action = intent.Action;
				
				if (action.Equals(Intents.CopyUsername))
				{
					String username = GetStringAndReplacePlaceholders(_entry, PwDefs.UserNameField);
					if (username.Length > 0)
					{
						_service.TimeoutCopyToClipboard(username);
					}
				} else if (action.Equals(Intents.CopyPassword))
				{
					String password = GetStringAndReplacePlaceholders(_entry, PwDefs.PasswordField);
					if (password.Length > 0)
					{
						_service.TimeoutCopyToClipboard(password);
					}
				} else if (action.Equals(Intents.CheckKeyboard))
				{
                    ActivateKp2aKeyboard(_service);
				}
			}

		};

		class NotificationDeletedBroadcastReceiver: BroadcastReceiver
		{
			readonly CopyToClipboardService _service;
			public NotificationDeletedBroadcastReceiver(CopyToClipboardService service)
			{
				_service = service;
			}

			#region implemented abstract members of BroadcastReceiver
			public override void OnReceive(Context context, Intent intent)
			{
				if (intent.Action == ActionNotificationCancelled)
				{
					_service.OnWaitElementDeleted(intent.Extras.GetInt("requestCode"));
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

            InputMethodManager imeManager = (InputMethodManager)service.ApplicationContext.GetSystemService(InputMethodService);

            if (currentIme == kp2aIme)
            {
                imeManager.ToggleSoftInput(ShowSoftInputFlags.Forced, HideSoftInputFlags.None);
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

