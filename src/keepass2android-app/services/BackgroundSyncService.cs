// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using System;
using System.Threading;
using System.Threading.Tasks;
using AndroidX.Core.App;
using Android.Content.PM;


namespace keepass2android.services
{
    [Service(ForegroundServiceType = ForegroundService.TypeDataSync)]
    public class BackgroundSyncService : Service, IProgressUiProvider, IProgressUi
    {
        private const string ChannelId = "BackgroundSyncServiceChannel";
        private const int NotificationId = 1;
        private const string Tag = "BackgroundSyncService";
        private CancellationTokenSource _cts;
        private string _message;
        private string _submessage;

        public override void OnCreate()
        {
            base.OnCreate();
            Log.Debug(Tag, "OnCreate");
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if (intent.Action == ActionStop)
            {
                Log.Debug(Tag, "OnStartCommand: STOP");
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
                return StartCommandResult.NotSticky;
            }

            Log.Debug(Tag, "OnStartCommand");
            try
            {
                _cts = new CancellationTokenSource();
                CreateNotificationChannel();
                StartForeground(NotificationId, BuildNotification());
                App.Kp2a.ActiveContext = this;
                return StartCommandResult.Sticky;
            }
            catch (Exception ex)
            {
                Log.Error(Tag, ex.ToString());
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
                return StartCommandResult.NotSticky;
            }
        }


        private Notification BuildNotification()
        {
            Intent notificationIntent = PackageManager.GetLaunchIntentForPackage(PackageName);
            if (notificationIntent == null)
            {
                notificationIntent = new Intent(this, typeof(FileSelectActivity));
                notificationIntent.SetFlags(ActivityFlags.BroughtToFront | ActivityFlags.SingleTop |
                                            ActivityFlags.ReorderToFront | ActivityFlags.NewTask);
            }

            PendingIntent pendingIntent = PendingIntent.GetActivity(this, 0, notificationIntent,
                PendingIntentFlags.Immutable);

            NotificationCompat.Builder builder = new NotificationCompat.Builder(this, ChannelId)
                .SetSmallIcon(Resource.Drawable.ic_launcher_gray)
                .SetPriority(NotificationCompat.PriorityLow)
                .SetSilent(true)
                .SetContentIntent(pendingIntent)
                .SetProgress(100, 0, true)
                .SetOngoing(true);
            if (!string.IsNullOrEmpty(_message))
            {
                builder.SetContentTitle(_message);
            }
            if (!string.IsNullOrEmpty(_submessage))
            {
                builder.SetContentText(_submessage);
            }

            return builder.Build();
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channelName = GetString(Resource.String.BackgroundSyncChannel_name);
                var channelDescription = GetString(Resource.String.BackgroundSyncChannel_desc);
                var channelImportance = NotificationImportance.Default;
                var channel = new NotificationChannel(ChannelId, channelName, channelImportance)
                {
                    Description = channelDescription
                };
                channel.EnableLights(false);
                channel.EnableVibration(false);
                channel.SetSound(null, null);
                channel.SetShowBadge(false);

                var notificationManager = (NotificationManager)GetSystemService(NotificationService);
                notificationManager.CreateNotificationChannel(channel);
            }
        }

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Log.Debug(Tag, "OnDestroy");
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
        }

        public IProgressUi? ProgressUi
        {
            get { return this; }
        }

        public static string ActionStop => "STOP";

        public static string ActionStart => "START";

        public void Show()
        {

        }

        public void Hide()
        {
            CloseNotification();
            StopSelf();
            StopForeground(StopForegroundFlags.Remove);
        }

        private void CloseNotification()
        {
            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            notificationManager.Cancel(NotificationId);
        }

        public void UpdateMessage(string message)
        {
            _message = message;
            UpdateNotification();
        }

        private void UpdateNotification()
        {
            var notification = BuildNotification();
            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            notificationManager.Notify(NotificationId, notification);
        }

        public void UpdateSubMessage(string submessage)
        {
            _submessage = submessage;
            UpdateNotification();
        }
    }

}
