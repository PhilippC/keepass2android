using System;
using Android;
using Android.App;
using Android.Content;
using Android.Runtime;
using keepass2android.services.AutofillBase;
using keepass2android.services.AutofillBase.model;
using keepass2android.services.Kp2aAutofill;
using AutofillServiceBase = keepass2android.services.AutofillBase.AutofillServiceBase;

namespace keepass2android.services
{
    [Service(Label = AppNames.AppName, Permission=Manifest.Permission.BindAutofillService)]
    [IntentFilter(new [] {"android.service.autofill.AutofillService"})]
    [MetaData("android.autofill", Resource = "@xml/autofillservice")]
    [Register("keepass2android.services.Kp2aAutofillService")]
    public class Kp2aAutofillService: AutofillServiceBase
    {
        public Kp2aAutofillService()
        {

        }

        public Kp2aAutofillService(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        protected override FilledAutofillFieldCollection GetSuggestedEntry(string query)
        {
            if (App.Kp2a.GetDb()?.LastOpenedEntry?.SearchUrl == query)
                return ChooseForAutofillActivity.GetFilledAutofillFieldCollectionFromEntry(
                    App.Kp2a.GetDb()?.LastOpenedEntry, this);
            return null;
        }

        public override IAutofillIntentBuilder IntentBuilder => new Kp2aAutofillIntentBuilder();
    }
}