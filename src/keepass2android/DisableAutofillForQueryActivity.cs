using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.App.Assist;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Service.Autofill;
using Android.Util;
using Android.Views;
using Android.Views.Autofill;
using Android.Widget;
using keepass2android.services;
using keepass2android.services.AutofillBase;

namespace keepass2android
{
    [Activity(Label = "DisableAutofillForQueryActivity")]
    public class DisableAutofillForQueryActivity : Activity
    {
        public IAutofillIntentBuilder IntentBuilder = new Kp2aAutofillIntentBuilder();

        public const string ExtraIsDisable = "EXTRA_IS_DISABLE";

        protected void RestartApp()
        {
            Intent intent = IntentBuilder.GetRestartAppIntent(this);
            StartActivity(intent);
            Finish();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);


            string requestedUrl = Intent.GetStringExtra(ChooseForAutofillActivityBase.ExtraQueryString);
            if (requestedUrl == null)
            {
                Toast.MakeText(this, "Cannot execute query for null.", ToastLength.Long).Show();
                RestartApp();
                return;
            }

            var prefs = PreferenceManager.GetDefaultSharedPreferences(this);

            bool isDisable = Intent.GetBooleanExtra(ExtraIsDisable, true);

            var disabledValues = prefs.GetStringSet("AutoFillDisabledQueries", new HashSet<string>() { }).ToHashSet();
            if (isDisable)
            {
                disabledValues.Add(requestedUrl);
            }
            else
            {
                disabledValues.Remove(requestedUrl);
            }


            prefs.Edit().PutStringSet("AutoFillDisabledQueries", disabledValues).Commit();

            bool isManual = Intent.GetBooleanExtra(ChooseForAutofillActivityBase.ExtraIsManualRequest, false);
            
            Intent reply = new Intent();
            FillResponse.Builder builder = new FillResponse.Builder();
            AssistStructure structure = (AssistStructure)Intent.GetParcelableExtra(AutofillManager.ExtraAssistStructure);
            StructureParser parser = new StructureParser(this, structure);
            try
            {
                parser.ParseForFill(isManual);

            }
            catch (Java.Lang.SecurityException e)
            {
                Log.Warn(CommonUtil.Tag, "Security exception handling request");
                SetResult(Result.Canceled);
                return;
            }

            try
            {
                
                reply.PutExtra(AutofillManager.ExtraAuthenticationResult, (FillResponse)null);
            }
            catch (Exception e)
            {
                Kp2aLog.LogUnexpectedError(e);
                throw;
            }

            SetResult(Result.Ok, reply);
            

            Finish();
        }
    }
}