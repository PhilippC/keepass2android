using Android.Content;

namespace keepass2android
{
    public class LockCloseActivityBroadcastReceiver : BroadcastReceiver
    {
        readonly ILockCloseActivity _activity;
        public LockCloseActivityBroadcastReceiver(ILockCloseActivity activity)
        {
            _activity = activity;
        }

        public override void OnReceive(Context context, Intent intent)
        {
            switch (intent.Action)
            {
                case Intents.DatabaseLocked:
                    _activity.OnLockDatabase(intent.GetBooleanExtra("ByTimeout", false));
                    break;
                case Intent.ActionScreenOff:
                    App.Kp2a.OnScreenOff();
                    break;
            }
        }
    }
}