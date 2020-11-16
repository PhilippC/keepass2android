using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
    [Activity(Label = AppNames.AppName, Theme = "@style/MyTheme_ActionBar", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
    public class CloseImmediatelyActivity : AndroidX.AppCompat.App.AppCompatActivity
    {
        protected override void OnResume()
        {
            SetContentView(Resource.Layout.group);
            base.OnResume();
            SetResult(Result.Ok);
            FindViewById<RelativeLayout>(Resource.Id.bottom_bar).PostDelayed(() =>
            {
                Finish();
                OverridePendingTransition(0, 0);
            }, 200);
        }
    }
}