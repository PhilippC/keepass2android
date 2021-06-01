using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;

namespace keepass2android
{
    [Activity(Label = AppNames.AppName, TaskAffinity = "", NoHistory = true)]
    public class SwitchImeActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.switch_ime_activity_layout);
            FindViewById<Button>(Resource.Id.btn_reopen).Click += (sender, args) => TrySwitchKeyboard();
            FindViewById<Button>(Resource.Id.btn_cancel).Click += (sender, args) => Finish();
        }
        private string Kp2aInputMethodName
        {
            get { return PackageName + "/keepass2android.softkeyboard.KP2AKeyboard"; }
        }

        private Timer _timer;

        protected override void OnResume()
        {
            Kp2aLog.Log("SwitchImeActivity.OnResume");
            base.OnResume();

            TrySwitchKeyboard();
        }

        private void TrySwitchKeyboard()
        {
            Kp2aLog.Log("SwitchImeActivity.TrySwitchKeyboard");
            var needsKeyboardSwitch = NeedsKeyboardSwitch();

            if (needsKeyboardSwitch)
            {
                new Handler().PostDelayed(() =>
                {
                    Kp2aLog.Log("ShowIMEPicker");
                    Keepass2android.Kbbridge.ImeSwitcher.SwitchToKeyboard(this, Kp2aInputMethodName, false);
                    Kp2aLog.Log( "ShowIMEPicker done.");
                }, 1000);
                var timeToWait = TimeSpan.FromMilliseconds(500);
                _timer = new Timer(obj =>
                {
                    RunOnUiThread(() =>
                    {
                        if (!NeedsKeyboardSwitch()) Finish();
                    });
                }, null, timeToWait, timeToWait);
            }
            else
                Finish();
        }

        protected override void OnPause()
        {
            Kp2aLog.Log("SwitchImeActivity.OnPause");
            base.OnPause();
            Finish();
        }

        private bool NeedsKeyboardSwitch()
        {
            string currentIme = Android.Provider.Settings.Secure.GetString(
                ContentResolver,
                Android.Provider.Settings.Secure.DefaultInputMethod);
            bool needsKeyboardSwitch = currentIme != Kp2aInputMethodName;
            Kp2aLog.Log("SwitchImeActivity.NeedsKeyboardSwitch: " + currentIme +  " vs " + Kp2aInputMethodName);
            return needsKeyboardSwitch;
        }
    }
}