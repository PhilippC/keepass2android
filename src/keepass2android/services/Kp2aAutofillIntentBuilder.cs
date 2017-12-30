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
using keepass2android.services.AutofillBase;
using keepass2android.services.Kp2aAutofill;

namespace keepass2android.services
{
    class Kp2aAutofillIntentBuilder: IAutofillIntentBuilder
    {

        public IntentSender GetAuthIntentSenderForResponse(Context context, string query)
        {
            Intent intent = new Intent(context, typeof(ChooseForAutofillActivity));
            intent.PutExtra(ChooseForAutofillActivityBase.ExtraQueryString, query);
            return PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.CancelCurrent).IntentSender;
        }

        public IntentSender GetAuthIntentSenderForDataset(Context context, string dataset)
        {
            //TODO implement
            //return GetAuthIntentSenderForResponse(context, null);
            throw new NotImplementedException();
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