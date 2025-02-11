using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
    public abstract class ActivityLaunchMode
    {
        public abstract void Launch(Activity act, Intent i);
    }

    public class ActivityLaunchModeForward : ActivityLaunchMode
    {
        public override void Launch(Activity act, Intent i)
        {
            i.AddFlags(ActivityFlags.ForwardResult);
            act.StartActivity(i);
        }
    }

    public class ActivityLaunchModeRequestCode : ActivityLaunchMode
    {
        private readonly int _reqCode;

        public ActivityLaunchModeRequestCode(int reqCode)
        {
            _reqCode = reqCode;
        }
        public override void Launch(Activity act, Intent i)
        {
            act.StartActivityForResult(i, _reqCode);
        }
    }

    public class ActivityLaunchModeSimple : ActivityLaunchMode
    {
        public override void Launch(Activity act, Intent i)
        {
            act.StartActivity(i);
        }
    }
}