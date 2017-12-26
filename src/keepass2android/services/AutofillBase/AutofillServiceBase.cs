using System;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Service.Autofill;
using Android.Util;

namespace keepass2android.services.AutofillBase
{
    public interface IAutofillIntentBuilder
    {
        IntentSender GetAuthIntentSenderForResponse(Context context, string query);
        IntentSender GetAuthIntentSenderForDataset(Context context, string dataset);
        Intent GetRestartAppIntent(Context context);
    }

    public abstract class AutofillServiceBase: AutofillService
    {
        public AutofillServiceBase()
        {
            
        }

        public AutofillServiceBase(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }


        public override void OnFillRequest(FillRequest request, CancellationSignal cancellationSignal, FillCallback callback)
        {
            Log.Debug(CommonUtil.Tag, "onFillRequest");
            var structure = request.FillContexts[request.FillContexts.Count - 1].Structure;

            //TODO package signature verification?

            var clientState = request.ClientState;
            Log.Debug(CommonUtil.Tag, "onFillRequest(): data=" + CommonUtil.BundleToString(clientState));


            cancellationSignal.CancelEvent += (sender, e) => {
                Log.Warn(CommonUtil.Tag, "Cancel autofill not implemented yet.");
            };
            // Parse AutoFill data in Activity
            string query = null;
            var parser = new StructureParser(this, structure);
            try
            {
                query = parser.ParseForFill();
            }
            catch (Java.Lang.SecurityException e)
            {
                Log.Warn(CommonUtil.Tag, "Security exception handling request");
                callback.OnFailure(e.Message);
                return;
            }
            
            AutofillFieldMetadataCollection autofillFields = parser.AutofillFields;
            var responseBuilder = new FillResponse.Builder();
            // Check user's settings for authenticating Responses and Datasets.
            bool responseAuth = true;
            var autofillIds = autofillFields.GetAutofillIds();
            if (responseAuth && autofillIds.Length != 0)
            {
                // If the entire Autofill Response is authenticated, AuthActivity is used
                // to generate Response.
                var sender = IntentBuilder.GetAuthIntentSenderForResponse(this, query);
                var presentation = keepass2android.services.AutofillBase.AutofillHelper
                    .NewRemoteViews(PackageName, GetString(Resource.String.autofill_sign_in_prompt),
                        Resource.Drawable.ic_launcher);
                responseBuilder
                    .SetAuthentication(autofillIds, sender, presentation);
                callback.OnSuccess(responseBuilder.Build());
            }
            else
            {
                var datasetAuth = true;
                var response = AutofillHelper.NewResponse(this, datasetAuth, autofillFields, null, IntentBuilder);
                callback.OnSuccess(response);
            }
        }

        public override void OnSaveRequest(SaveRequest request, SaveCallback callback)
        {
            //TODO implement
            callback.OnFailure("not implemented");
        }


        public override void OnConnected()
        {
            Log.Debug(CommonUtil.Tag, "onConnected");
        }

        public override void OnDisconnected()
        {
            Log.Debug(CommonUtil.Tag, "onDisconnected");
        }

        public abstract IAutofillIntentBuilder IntentBuilder{get;}
    }
}
