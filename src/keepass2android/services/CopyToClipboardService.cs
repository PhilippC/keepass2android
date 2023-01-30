/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

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
using System.Collections.Generic;
using System.Linq;
using Android.AccessibilityServices;
using Android.Support.V4.App;
using Java.Util;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using Android.Preferences;
using Android.Views.Accessibility;
using KeePassLib;
using KeePassLib.Utility;
using Android.Views.InputMethods;
using KeePass.Util.Spr;
using KeePassLib.Serialization;
using PluginTOTP;

namespace keepass2android
{
    /// <summary>
    /// Service to show the notifications to make the current entry accessible through clipboard or the KP2A keyboard.
    /// </summary>
    /// The name reflects only the possibility through clipboard because keyboard was introduced later.
    /// The notifications require to be displayed by a service in order to be kept when the activity is closed
    /// after searching for a URL.
    [Service]
    public class CopyToClipboardService : Service
    {
        
        protected override void AttachBaseContext(Context baseContext)
        {
            base.AttachBaseContext(LocaleManager.setLocale(baseContext));
        }
        class PasswordAccessNotificationBuilder
        {
            private readonly Context _ctx;
            private readonly NotificationManager _notificationManager;

            public PasswordAccessNotificationBuilder(Context ctx, NotificationManager notificationManager)
            {
                _ctx = ctx;
                _notificationManager = notificationManager;
            }

            private bool _hasPassword;
            private bool _hasUsername;
            private bool _hasTotp;
            private bool _hasKeyboard;

            public void AddPasswordAccess()
            {
                _hasPassword = true;
            }

            public void AddUsernameAccess()
            {
                _hasUsername = true;
            }
            public void AddTotpAccess()
            {
                _hasTotp = true;
            }

            public void AddKeyboardAccess()
            {
                _hasKeyboard = true;
            }

            public int CreateNotifications(string entryName, Bitmap entryIcon)
            {
                if (((int)Build.VERSION.SdkInt < 16) ||
                    (PreferenceManager.GetDefaultSharedPreferences(_ctx)
                                      .GetBoolean(_ctx.GetString(Resource.String.ShowSeparateNotifications_key),
                                                  _ctx.Resources.GetBoolean(Resource.Boolean.ShowSeparateNotifications_default))))
                {
                    return CreateSeparateNotifications(entryName, entryIcon);
                }
                else
                {
                    return CreateCombinedNotification(entryName, entryIcon);
                }

            }

            private int CreateCombinedNotification(string entryName, Bitmap entryIcon)
            {
                Kp2aLog.Log("Create Combined Notifications: " + _hasKeyboard + " " + _hasPassword + " " + _hasUsername +
                            " " + _hasTotp);

                if ((!_hasUsername) && (!_hasPassword) && (!_hasKeyboard) && (!_hasTotp))
                    return 0;

                NotificationCompat.Builder notificationBuilder;
                if (_hasKeyboard)
                {
                    notificationBuilder = GetNotificationBuilder(Intents.CheckKeyboard, Resource.String.available_through_keyboard,
                                                            Resource.Drawable.ic_notify_keyboard, entryName, entryIcon);
                }
                else
                {
                    notificationBuilder = GetNotificationBuilder(null, Resource.String.entry_is_available, Resource.Drawable.ic_launcher_gray,
                                                       entryName, entryIcon);
                }

                //add action buttons to base notification:

                if (_hasUsername)
                    notificationBuilder.AddAction(new NotificationCompat.Action(Resource.Drawable.ic_action_username,
                        _ctx.GetString(Resource.String.menu_copy_user),
                        GetPendingIntent(Intents.CopyUsername, Resource.String.menu_copy_user)));
                if (_hasPassword)
                    notificationBuilder.AddAction(new NotificationCompat.Action(Resource.Drawable.ic_action_password,
                        _ctx.GetString(Resource.String.menu_copy_pass),
                        GetPendingIntent(Intents.CopyPassword, Resource.String.menu_copy_pass)));
                if (_hasTotp)
                    notificationBuilder.AddAction(new NotificationCompat.Action(Resource.Drawable.ic_action_password,
                        _ctx.GetString(Resource.String.menu_copy_totp),
                        GetPendingIntent(Intents.CopyTotp, Resource.String.menu_copy_totp)));

                // Don't show on wearable devices if possible
                if ((int)Build.VERSION.SdkInt >= 20)
                    notificationBuilder.SetLocalOnly(true);

                notificationBuilder.SetPriority((int)Android.App.NotificationPriority.Max);
                var notification = notificationBuilder.Build();
                notification.DeleteIntent = CreateDeleteIntent(NotifyCombined);
                _notificationManager.Notify(NotifyCombined, notification);

                return 1;
            }

            private int CreateSeparateNotifications(string entryName, Bitmap entryIcon)
            {
                Kp2aLog.Log("Create Separate Notifications: " + _hasKeyboard + " " + _hasPassword + " " + _hasUsername +
                            " " + _hasTotp);
                int numNotifications = 0;
                if (_hasPassword)
                {
                    // only show notification if password is available
                    Notification password = GetNotification(Intents.CopyPassword, Resource.String.copy_password,
                                                            Resource.Drawable.ic_action_password, entryName, entryIcon);
                    numNotifications++;
                    password.DeleteIntent = CreateDeleteIntent(NotifyPassword);
                    _notificationManager.Notify(NotifyPassword, password);
                }
                if (_hasUsername)
                {
                    // only show notification if username is available
                    Notification username = GetNotification(Intents.CopyUsername, Resource.String.copy_username,
                                                            Resource.Drawable.ic_action_username, entryName, entryIcon);
                    username.DeleteIntent = CreateDeleteIntent(NotifyUsername);
                    _notificationManager.Notify(NotifyUsername, username);
                    numNotifications++;
                }
                if (_hasTotp)
                {
                    // only show notification if totp is available
                    Notification totp = GetNotification(Intents.CopyTotp, Resource.String.copy_totp,
                        Resource.Drawable.ic_action_password, entryName, entryIcon);
                    totp.DeleteIntent = CreateDeleteIntent(NotifyTotp);
                    _notificationManager.Notify(NotifyTotp, totp);
                    numNotifications++;
                }
                if (_hasKeyboard)
                {
                    // only show notification if username is available
                    Notification keyboard = GetNotification(Intents.CheckKeyboard, Resource.String.available_through_keyboard,
                                                            Resource.Drawable.ic_notify_keyboard, entryName, entryIcon);
                    keyboard.DeleteIntent = CreateDeleteIntent(NotifyKeyboard);
                    _notificationManager.Notify(NotifyKeyboard, keyboard);
                    numNotifications++;
                }
                return numNotifications;
            }

            //creates a delete intent (started when notification is cancelled by user or something else)
            //requires different request codes for every item (otherwise the intents are identical)
            PendingIntent CreateDeleteIntent(int requestCode)
            {
                Intent intent = new Intent(ActionNotificationCancelled);
                Bundle extra = new Bundle();
                extra.PutInt("requestCode", requestCode);
                intent.PutExtras(extra);

                return PendingIntent.GetBroadcast(_ctx, requestCode, intent, PendingIntentFlags.CancelCurrent);
            }


            private Notification GetNotification(string intentText, int descResId, int drawableResId, string entryName, Bitmap entryIcon)
            {
                var builder = GetNotificationBuilder(intentText, descResId, drawableResId, entryName, entryIcon);

                return builder.Build();
            }

            private NotificationCompat.Builder GetNotificationBuilder(string intentText, int descResId, int drawableResId, string entryName, Bitmap entryIcon)
            {
                String desc = _ctx.GetString(descResId);

                String title = _ctx.GetString(Resource.String.app_name);
                if (!String.IsNullOrEmpty(entryName))
                    title += " (" + entryName + ")";

                PendingIntent pending;
                if (intentText == null)
                {
                    pending = PendingIntent.GetActivity(_ctx.ApplicationContext, 0, new Intent(), 0);
                }
                else
                {
                    pending = GetPendingIntent(intentText, descResId);
                }

                var builder = new NotificationCompat.Builder(_ctx, App.NotificationChannelIdEntry);
                builder.SetSmallIcon(drawableResId)
                       .SetContentText(desc)
                       .SetContentTitle(entryName)
                       .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
                       .SetTicker(entryName + ": " + desc)
                       .SetVisibility((int)Android.App.NotificationVisibility.Secret)
                       .SetContentIntent(pending);
                if (entryIcon != null)
                    builder.SetLargeIcon(entryIcon);
                return builder;
            }

            private PendingIntent GetPendingIntent(string intentText, int descResId)
            {
                PendingIntent pending;
                Intent intent = new Intent(_ctx, typeof(CopyToClipboardBroadcastReceiver));
                intent.SetAction(intentText);
                pending = PendingIntent.GetBroadcast(_ctx, descResId, intent, PendingIntentFlags.CancelCurrent);
                return pending;
            }

            
        }

        public const int NotifyUsername = 1;
        public const int NotifyPassword = 2;
        public const int NotifyKeyboard = 3;
        public const int ClearClipboard = 4;
        public const int NotifyCombined = 5;
        public const int NotifyTotp = 6;

        static public void CopyValueToClipboardWithTimeout(Context ctx, string text, bool isProtected)
        {
            Intent i = new Intent(ctx, typeof(CopyToClipboardService));
            i.SetAction(Intents.CopyStringToClipboard);
            i.PutExtra(_stringtocopy, text);
            i.PutExtra(_stringisprotected, isProtected);
            ctx.StartService(i);
        }

        static public void ActivateKeyboard(Context ctx)
        {
            Intent i = new Intent(ctx, typeof(CopyToClipboardService));
            i.SetAction(Intents.ActivateKeyboard);
            ctx.StartService(i);
        }

        public static void CancelNotifications(Context ctx)
        {

            Intent i = new Intent(ctx, typeof(CopyToClipboardService));
            i.SetAction(Intents.ClearNotificationsAndData);
            ctx.StartService(i);
        }

        public CopyToClipboardService(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

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

            if (_stopOnLockBroadcastReceiver == null)
            {
                _stopOnLockBroadcastReceiver = new StopOnLockBroadcastReceiver(this);
                IntentFilter filter = new IntentFilter();
                filter.AddAction(Intents.DatabaseLocked);
                RegisterReceiver(_stopOnLockBroadcastReceiver, filter);
            }

            if ((intent.Action == Intents.ShowNotification) || (intent.Action == Intents.UpdateKeyboard))
            {
                String entryId = intent.GetStringExtra(EntryActivity.KeyEntry);
                String searchUrl = intent.GetStringExtra(SearchUrlTask.UrlToSearchKey);

                if (entryId == null)
                {
                    Kp2aLog.Log("received intent " + intent.Action + " without KeyEntry!");
#if DEBUG
                    throw new Exception("invalid intent received!");
#endif
                    return StartCommandResult.NotSticky;
                }


                PwEntryOutput entry;
                try
                {
                    ElementAndDatabaseId fullId = new ElementAndDatabaseId(entryId);


                    if (((App.Kp2a.LastOpenedEntry != null)
                                           && (fullId.ElementId.Equals(App.Kp2a.LastOpenedEntry.Uuid))))
                    {
                        entry = App.Kp2a.LastOpenedEntry;
                    }
                    else
                    {
                        Database entryDb = App.Kp2a.GetDatabase(fullId.DatabaseId);
                        entry = new PwEntryOutput(entryDb.EntriesById[fullId.ElementId], entryDb);
                    }

                }
                catch (Exception e)
                {
                    Kp2aLog.LogUnexpectedError(e);
                    //seems like restarting the service happened after closing the DB
                    StopSelf();
                    return StartCommandResult.NotSticky;
                }

                if (intent.Action == Intents.ShowNotification)
                {
                    //first time opening the entry -> bring up the notifications
                    bool activateKeyboard = intent.GetBooleanExtra(EntryActivity.KeyActivateKeyboard, false);
                    DisplayAccessNotifications(entry, activateKeyboard, searchUrl);
                }
                else //UpdateKeyboard
                {
#if !EXCLUDE_KEYBOARD
                    //this action is received when the data in the entry has changed (e.g. by plugins)
                    //update the keyboard data.
                    //Check if keyboard is (still) available
                    if (Keepass2android.Kbbridge.KeyboardData.EntryId == entry.Uuid.ToHexString())
                        MakeAccessibleForKeyboard(entry, searchUrl);
#endif
                }
            }
            if (intent.Action == Intents.CopyStringToClipboard)
            {

                TimeoutCopyToClipboard(intent.GetStringExtra(_stringtocopy), intent.GetBooleanExtra(_stringisprotected, false));
            }
            if (intent.Action == Intents.ActivateKeyboard)
            {
                ActivateKp2aKeyboard();
            }
            if (intent.Action == Intents.ClearNotificationsAndData)
            {
                ClearNotifications();
            }


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
            Kp2aLog.Log("CopyToClipboardService.OnDestroy");

            // These members might never get initialized if the app timed out
            if (_stopOnLockBroadcastReceiver != null)
            {
                UnregisterReceiver(_stopOnLockBroadcastReceiver);
                _stopOnLockBroadcastReceiver = null;
            }
            if (_notificationDeletedBroadcastReceiver != null)
            {
                UnregisterReceiver(_notificationDeletedBroadcastReceiver);
                _notificationDeletedBroadcastReceiver = null;
            }
            if (_notificationManager != null)
            {
                _notificationManager.Cancel(NotifyPassword);
                _notificationManager.Cancel(NotifyUsername);
                _notificationManager.Cancel(NotifyKeyboard);
                _notificationManager.Cancel(NotifyCombined);

                _numElementsToWaitFor = 0;
                ClearKeyboard(true);
            }
            if (_clearClipboardTask != null)
            {
                Kp2aLog.Log("Clearing clipboard due to stop CopyToClipboardService");
                _clearClipboardTask.Run();
            }

            Kp2aLog.Log("Destroyed Show-Notification-Receiver.");

            base.OnDestroy();
        }

        private const string ActionNotificationCancelled = "notification_cancelled";





        public void DisplayAccessNotifications(PwEntryOutput entry, bool activateKeyboard, string searchUrl)
        {
            var hadKeyboardData = ClearNotifications();

            String entryName = entry.OutputStrings.ReadSafe(PwDefs.TitleField);
            Database db = App.Kp2a.FindDatabaseForElement(entry.Entry);

            var bmp = Util.DrawableToBitmap(db.DrawableFactory.GetIconDrawable(this,
                db.KpDatabase, entry.Entry.IconId, entry.Entry.CustomIconUuid, false));


            if (!(((entry.Entry.CustomIconUuid != null) && (!entry.Entry.CustomIconUuid.Equals(PwUuid.Zero))))
                && PreferenceManager.GetDefaultSharedPreferences(this).GetString("IconSetKey", PackageName) == PackageName)
            {
                Color drawingColor = new Color(189, 189, 189);
                bmp = Util.ChangeImageColor(bmp, drawingColor);
            }

            Bitmap entryIcon = Util.MakeLargeIcon(bmp, this);

            ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
            var notBuilder = new PasswordAccessNotificationBuilder(this, _notificationManager);
            if (prefs.GetBoolean(GetString(Resource.String.CopyToClipboardNotification_key), Resources.GetBoolean(Resource.Boolean.CopyToClipboardNotification_default)))
            {

                if (entry.OutputStrings.ReadSafe(PwDefs.PasswordField).Length > 0)
                {
                    notBuilder.AddPasswordAccess();

                }

                if (entry.OutputStrings.ReadSafe(PwDefs.UserNameField).Length > 0)
                {
                    notBuilder.AddUsernameAccess();
                }
                if (entry.OutputStrings.ReadSafe(UpdateTotpTimerTask.TotpKey).Length > 0)
                {
                    notBuilder.AddTotpAccess();
                }
            }

            bool hasKeyboardDataNow = false;
            if (prefs.GetBoolean(GetString(Resource.String.UseKp2aKeyboard_key), Resources.GetBoolean(Resource.Boolean.UseKp2aKeyboard_default)))
            {

                //keyboard
                hasKeyboardDataNow = MakeAccessibleForKeyboard(entry, searchUrl);
                if (hasKeyboardDataNow)
                {
                    notBuilder.AddKeyboardAccess();
                    if (prefs.GetBoolean("kp2a_switch_rooted", false))
                    {
                        //switch rooted
                        bool onlySwitchOnSearch = prefs.GetBoolean(GetString(Resource.String.OpenKp2aKeyboardAutomaticallyOnlyAfterSearch_key), false);
                        if (activateKeyboard || (!onlySwitchOnSearch))
                        {
                            ActivateKp2aKeyboard();
                        }
                    }
                    else
                    {
                        //if the app is about to be closed again (e.g. after searching for a URL and returning to the browser:
                        // automatically bring up the Keyboard selection dialog
                        if ((activateKeyboard) && prefs.GetBoolean(GetString(Resource.String.OpenKp2aKeyboardAutomatically_key), Resources.GetBoolean(Resource.Boolean.OpenKp2aKeyboardAutomatically_default)))
                        {
                            ActivateKp2aKeyboard();
                        }
                    }

                }

            }

            if ((!hasKeyboardDataNow) && (hadKeyboardData))
            {
                ClearKeyboard(true); //this clears again and then (this is the point) broadcasts that we no longer have keyboard data
            }
            _numElementsToWaitFor = notBuilder.CreateNotifications(entryName, entryIcon);

            if (_numElementsToWaitFor == 0)
            {
                Kp2aLog.Log("Stopping CopyToClipboardService, created empty notification");
                StopSelf();
                return;
            }

            //register receiver to get notified when notifications are discarded in which case we can shutdown the service
            if (_notificationDeletedBroadcastReceiver == null)
            {
                _notificationDeletedBroadcastReceiver = new NotificationDeletedBroadcastReceiver(this);
                IntentFilter deletefilter = new IntentFilter();
                deletefilter.AddAction(ActionNotificationCancelled);
                RegisterReceiver(_notificationDeletedBroadcastReceiver, deletefilter);
            }
            
        }

        public void ActivateKeyboardIfAppropriate(bool closeAfterCreate, ISharedPreferences prefs)
        {
            if (prefs.GetBoolean("kp2a_switch_rooted", false))
            {
                //switch rooted
                bool onlySwitchOnSearch = prefs.GetBoolean(
                    GetString(Resource.String.OpenKp2aKeyboardAutomaticallyOnlyAfterSearch_key), false);
                if (closeAfterCreate || (!onlySwitchOnSearch))
                {
                    ActivateKp2aKeyboard();
                }
            }
            else
            {
                //if the app is about to be closed again (e.g. after searching for a URL and returning to the browser:
                // automatically bring up the Keyboard selection dialog
                if ((closeAfterCreate) &&
                    prefs.GetBoolean(GetString(Resource.String.OpenKp2aKeyboardAutomatically_key),
                        Resources.GetBoolean(Resource.Boolean.OpenKp2aKeyboardAutomatically_default)))
                {
                    ActivateKp2aKeyboard();
                }
            }
        }

        private bool ClearNotifications()
        {
            // Notification Manager
            _notificationManager = (NotificationManager)GetSystemService(NotificationService);

            _notificationManager.Cancel(NotifyPassword);
            _notificationManager.Cancel(NotifyUsername);
            _notificationManager.Cancel(NotifyKeyboard);
            _notificationManager.Cancel(NotifyCombined);
            _numElementsToWaitFor = 0;
            bool hadKeyboardData = ClearKeyboard(false); //do not broadcast if the keyboard was changed
            return hadKeyboardData;
        }

        bool MakeAccessibleForKeyboard(PwEntryOutput entry, string searchUrl)
        {
#if EXCLUDE_KEYBOARD
			return false;
#else
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
            int i = 0;
            foreach (string key in keys)
            {
                String value = entry.OutputStrings.ReadSafe(key);

                if (value.Length > 0)
                {
                    kbdataBuilder.AddString(key, GetString(resIds[i]), value);
                    hasData = true;
                }
                i++;
            }
            //add additional fields:
            foreach (var pair in entry.OutputStrings)
            {
                var key = pair.Key;
                var value = pair.Value.ReadString();

                if (!PwDefs.IsStandardField(key))
                {
                    kbdataBuilder.AddString(pair.Key, pair.Key, value);
                    hasData = true;
                }
            }


            kbdataBuilder.Commit();
            Keepass2android.Kbbridge.KeyboardData.EntryName = entry.OutputStrings.ReadSafe(PwDefs.TitleField);
            Keepass2android.Kbbridge.KeyboardData.EntryId = entry.Uuid.ToHexString();
            if (hasData)
                Keepass2android.Autofill.AutoFillService.NotifyNewData(searchUrl);

            return hasData;
#endif
        }


        public void OnWaitElementDeleted(int itemId)
        {
            Kp2aLog.Log("Wait element deleted: " + itemId);
            _numElementsToWaitFor--;
            if (_numElementsToWaitFor <= 0)
            {
                Kp2aLog.Log("Stopping CopyToClipboardService, no more elements");
                StopSelf();
            }
            if ((itemId == NotifyKeyboard) || (itemId == NotifyCombined))
            {
                //keyboard notification was deleted -> clear entries in keyboard
                ClearKeyboard(true);
            }
        }

        bool ClearKeyboard(bool broadcastClear)
        {
#if !EXCLUDE_KEYBOARD
            Keepass2android.Kbbridge.KeyboardData.AvailableFields.Clear();
            Keepass2android.Kbbridge.KeyboardData.EntryName = null;
            bool hadData = Keepass2android.Kbbridge.KeyboardData.EntryId != null;
            Keepass2android.Kbbridge.KeyboardData.EntryId = null;

            if ((hadData) && broadcastClear)
                SendBroadcast(new Intent(Intents.KeyboardCleared));

            return hadData;
#else
			return false;
#endif
        }

        private readonly Timer _timer = new Timer();

        internal void TimeoutCopyToClipboard(String text, bool isProtected)
        {
            Util.CopyToClipboard(this, text, isProtected);

            ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
            String sClipClear = prefs.GetString(GetString(Resource.String.clipboard_timeout_key), GetString(Resource.String.clipboard_timeout_default));

            long clipClearTime = long.Parse(sClipClear);

            _clearClipboardTask = new ClearClipboardTask(this, text, _uiThreadCallback);
            if (clipClearTime > 0)
            {
                _numElementsToWaitFor++;
                _timer.Schedule(_clearClipboardTask, clipClearTime);
            }
        }

        // Task which clears the clipboard, and sends a toast to the foreground.
        private class ClearClipboardTask : TimerTask
        {

            private readonly String _clearText;
            private readonly CopyToClipboardService _service;
            private readonly Handler _handler;

            public ClearClipboardTask(CopyToClipboardService service, String clearText, Handler handler)
            {
                _clearText = clearText;
                _service = service;
                _handler = handler;
            }

            public override void Run()
            {
                String currentClip = Util.GetClipboard(_service);
                DoPostClear();
                if (currentClip.Equals(_clearText))
                {
                    Util.CopyToClipboard(_service, "", false);
                    DoPostWarn();
                }
            }

            private void DoPostWarn()
            {
                _handler.Post(ShowClipboardWarning);
            }

            private void DoPostClear()
            {
                _handler.Post(DoClearClipboard);
            }

            private void DoClearClipboard()
            {
                _service.OnWaitElementDeleted(CopyToClipboardService.ClearClipboard);
            }

            private void ShowClipboardWarning()
            {
                string message = _service.GetString(Resource.String.ClearClipboard) + " "
                                 + _service.GetString(Resource.String.ClearClipboardWarning);
                Android.Util.Log.Debug("KP2A", message);
                Toast.MakeText(_service,
                    message,
                    ToastLength.Long).Show();
            }
        }


        // Setup to allow the toast to happen in the foreground
        readonly Handler _uiThreadCallback = new Handler();
        private ClearClipboardTask _clearClipboardTask;
        private const string _stringtocopy = "StringToCopy";
        private const string _stringisprotected = "StringIsProtected";



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
                    case Intents.DatabaseLocked:
                        _service.OnLockDatabase();
                        break;
                }
            }
        }



        class NotificationDeletedBroadcastReceiver : BroadcastReceiver
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

        internal void ActivateKp2aKeyboard()
        {
            string currentIme = Android.Provider.Settings.Secure.GetString(
                                ContentResolver,
                                Android.Provider.Settings.Secure.DefaultInputMethod);

            string kp2aIme = Kp2aInputMethodName;



            if (currentIme == kp2aIme)
            {
                //keyboard already activated. bring it up.
                InputMethodManager imeManager = (InputMethodManager)ApplicationContext.GetSystemService(InputMethodService);
                if (imeManager == null)
                {
                    Toast.MakeText(this, Resource.String.not_possible_im_picker, ToastLength.Long).Show();
                    return;
                }
                try
                {
                    imeManager.ToggleSoftInput(ShowFlags.Forced, HideSoftInputFlags.None);
                }
                catch (Exception e)
                {
                    Kp2aLog.LogUnexpectedError(e);

                    try
                    {
                        imeManager.ToggleSoftInput(ShowFlags.Implicit, HideSoftInputFlags.ImplicitOnly);
                        return;
                    }
                    catch (Exception)
                    {
                        Toast.MakeText(this, Resource.String.not_possible_im_picker, ToastLength.Long).Show();
                    }
                    return;
                }
                
            }
            else
            {



                if (!IsKp2aInputMethodEnabled)
                {
                    //must be enabled in settings first
                    Toast.MakeText(this, Resource.String.please_activate_keyboard, ToastLength.Long).Show();
                    Intent settingsIntent = new Intent(Android.Provider.Settings.ActionInputMethodSettings);
                    try
                    {
                        settingsIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ExcludeFromRecents);
                        StartActivity(settingsIntent);
                    }
                    catch (Exception e)
                    {
                        //seems like on Huawei devices this call can fail. 
                        Kp2aLog.LogUnexpectedError(e);
                        Toast.MakeText(this, "Failed to switch keyboard.", ToastLength.Long).Show();

                    }
                }
                else
                {
                    //let's bring up the keyboard switching dialog.
                    //Unfortunately this no longer works starting with Android 9 if our app is not in foreground.
                    bool mustUseHelperActivity = false;
                    if ((int)Build.VERSION.SdkInt >= 28)
                    {
                        ActivityManager.RunningAppProcessInfo appProcessInfo = new ActivityManager.RunningAppProcessInfo();
                        ActivityManager.GetMyMemoryState(appProcessInfo);
                        //at least on Samsung devices, we always need the helper activity
                        mustUseHelperActivity = (appProcessInfo.Importance != Importance.Foreground) || (Build.Manufacturer != "Google");
                    }
                    if (mustUseHelperActivity)
                    {
                        try
                        {
                            Intent switchImeIntent = new Intent(this, typeof(SwitchImeActivity));
                            switchImeIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ExcludeFromRecents);
                            StartActivity(switchImeIntent);
                        }
                        catch (Exception e)
                        {
                            //seems like on Huawei devices this call can fail. 
                            Kp2aLog.LogUnexpectedError(e);
                            Toast.MakeText(this, "Failed to switch keyboard.", ToastLength.Long).Show();

                        }
                        
                    }
                    else
                    {
#if !EXCLUDE_KEYBOARD
                        Keepass2android.Kbbridge.ImeSwitcher.SwitchToKeyboard(this, kp2aIme, false);
#endif
                    }
                }
            }
        }

        public bool IsKp2aInputMethodEnabled
        {
            get
            {
                InputMethodManager imeManager = (InputMethodManager)ApplicationContext.GetSystemService(InputMethodService);
                if (imeManager == null)
                    return false;
                IList<InputMethodInfo> inputMethodProperties = imeManager.EnabledInputMethodList;
                return inputMethodProperties.Any(imi => imi.Id.Equals(Kp2aInputMethodName));
            }
        }

        private string Kp2aInputMethodName
        {
            get { return PackageName + "/keepass2android.softkeyboard.KP2AKeyboard"; }
        }
    }

    [BroadcastReceiver(Permission = "keepass2android." + AppNames.PackagePart + ".permission.CopyToClipboard")]
    class CopyToClipboardBroadcastReceiver : BroadcastReceiver
    {
        public CopyToClipboardBroadcastReceiver(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }


        public CopyToClipboardBroadcastReceiver()
        {
        }

        public override void OnReceive(Context context, Intent intent)
        {
            String action = intent.Action;

            //check if we have a last opened entry
            //this should always be non-null, but if the OS has killed the app, it might occur.
            if (App.Kp2a.LastOpenedEntry == null)
            {
                Intent i = new Intent(context, typeof(AppKilledInfo));
                i.SetFlags(ActivityFlags.ClearTask | ActivityFlags.NewTask | ActivityFlags.ExcludeFromRecents);
                context.StartActivity(i);
                return;
            }

            if (action.Equals(Intents.CopyUsername))
            {
                String username = App.Kp2a.LastOpenedEntry.OutputStrings.ReadSafe(PwDefs.UserNameField);
                if (username.Length > 0)
                {
                    CopyToClipboardService.CopyValueToClipboardWithTimeout(context, username, false);
                }
                context.SendBroadcast(new Intent(Intent.ActionCloseSystemDialogs)); //close notification drawer
            }
            else if (action.Equals(Intents.CopyPassword))
            {
                String password = App.Kp2a.LastOpenedEntry.OutputStrings.ReadSafe(PwDefs.PasswordField);
                if (password.Length > 0)
                {
                    CopyToClipboardService.CopyValueToClipboardWithTimeout(context, password, true);
                }
                context.SendBroadcast(new Intent(Intent.ActionCloseSystemDialogs)); //close notification drawer
            }
            else if (action.Equals(Intents.CopyTotp))
            {
                String totp = App.Kp2a.LastOpenedEntry.OutputStrings.ReadSafe(UpdateTotpTimerTask.TotpKey);
                if (totp.Length > 0)
                {
                    CopyToClipboardService.CopyValueToClipboardWithTimeout(context, totp, true);
                }
                context.SendBroadcast(new Intent(Intent.ActionCloseSystemDialogs)); //close notification drawer
            }
            else if (action.Equals(Intents.CheckKeyboard))
            {
                CopyToClipboardService.ActivateKeyboard(context);
            }
        }

    };
}

