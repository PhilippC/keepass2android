using Android.Content.PM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace keepass2android
{
    [Activity(Label = AppNames.AppName,
        MainLauncher = false,
        Theme = "@style/Kp2aTheme_BlueNoActionBar",
        Exported = true)]
    ///For autofill, we sometimes need to pass an intent to an inline presentation which never gets fired. We use this as a dummy activity.
    public class NullActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            Kp2aLog.Log("NullActivity.OnCreate - this is unexpected.");
            base.OnCreate(savedInstanceState);
            Finish();
        }
    }
}
