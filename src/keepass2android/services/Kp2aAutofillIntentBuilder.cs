using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using keepass2android.services.AutofillBase;
using keepass2android.services.Kp2aAutofill;

namespace keepass2android.services
{
    class Kp2aAutofillIntentBuilder: IAutofillIntentBuilder
    {

        public IntentSender GetAuthIntentSenderForResponse(Context context, string query, bool isManualRequest)
        {
            Intent intent = new Intent(context, typeof(ChooseForAutofillActivity));
            intent.PutExtra(ChooseForAutofillActivityBase.ExtraQueryString, query);
            intent.PutExtra(ChooseForAutofillActivityBase.ExtraIsManualRequest, isManualRequest);
            return PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.CancelCurrent).IntentSender;
        }

        public Intent GetRestartAppIntent(Context context)
        {
            var intent = new Intent(context, typeof(FileSelectActivity));
            intent.AddFlags(ActivityFlags.ForwardResult);
            return intent;
        }

        public int AppIconResource
        {
            get { return AppNames.LauncherIcon; }
        }
    }
}