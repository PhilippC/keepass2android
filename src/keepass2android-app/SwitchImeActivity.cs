using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using keepass2android;

namespace keepass2android
{
    [Activity(Label = AppNames.AppName, Theme = "@style/Kp2aTheme_BlueActionBar")]
    public class SwitchImeActivity : AndroidX.AppCompat.App.AppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.switch_ime_activity_layout);
            FindViewById<Button>(Resource.Id.btn_reopen).Click += (sender, args) => TrySwitchKeyboard();
            FindViewById<Button>(Resource.Id.btn_cancel).Click += (sender, args) => Finish();
            FindViewById<Button>(Resource.Id.btn_open_settings).Click += (sender, args) => AppSettingsActivity.Launch(this);

            var prefs = PreferenceManager.GetDefaultSharedPreferences(LocaleManager.LocalizedAppContext);

            bool useKp2aKeyboardInKp2a = prefs.GetBoolean(GetString(Resource.String.UseKp2aKeyboardInKp2a_key), false);
            bool kp2a_switch_rooted = prefs.GetBoolean("kp2a_switch_rooted", false);
            bool AutoFillTotp_prefs_ActivateKeyboard = prefs.GetBoolean("AutoFillTotp_prefs_ActivateKeyboard_key", false);
            bool OpenKp2aKeyboardAutomatically = prefs.GetBoolean(GetString(Resource.String.OpenKp2aKeyboardAutomatically_key), Resources.GetBoolean(Resource.Boolean.OpenKp2aKeyboardAutomatically_default));

            FindViewById(Resource.Id.note_UseKp2aKeyboardInKp2a).Visibility = useKp2aKeyboardInKp2a ? ViewStates.Visible : ViewStates.Gone;
            FindViewById(Resource.Id.note_kp2a_switch_rooted).Visibility = kp2a_switch_rooted ? ViewStates.Visible : ViewStates.Gone;
            FindViewById(Resource.Id.note_AutoFillTotp_prefs_ActivateKeyboard).Visibility = AutoFillTotp_prefs_ActivateKeyboard ? ViewStates.Visible : ViewStates.Gone;
            FindViewById(Resource.Id.note_OpenKp2aKeyboardAutomatically).Visibility = OpenKp2aKeyboardAutomatically ? ViewStates.Visible : ViewStates.Gone;

            bool hasNote = useKp2aKeyboardInKp2a || kp2a_switch_rooted || AutoFillTotp_prefs_ActivateKeyboard || OpenKp2aKeyboardAutomatically;
            ((LinearLayout)FindViewById(Resource.Id.settings_notes_container)).Visibility = hasNote ? ViewStates.Visible : ViewStates.Gone;

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
            return needsKeyboardSwitch;
        }
    }
}