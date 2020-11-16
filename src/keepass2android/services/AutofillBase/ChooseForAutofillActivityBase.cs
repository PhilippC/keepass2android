using System;
using Android.App;
using Android.App.Assist;
using Android.Content;
using Android.OS;
using Android.Service.Autofill;
using Android.Support.V7.App;
using Android.Util;
using Android.Views.Autofill;
using Android.Widget;
using Java.Util;
using keepass2android.services.AutofillBase.model;
using System.Linq;
using Android.Content.PM;
using AlertDialog = Android.App.AlertDialog;

namespace keepass2android.services.AutofillBase
{
    public abstract class ChooseForAutofillActivityBase : AndroidX.AppCompat.App.AppCompatActivity
    {
        protected Intent ReplyIntent;


        public static string ExtraQueryString => "EXTRA_QUERY_STRING";
        public static string ExtraQueryPackageString => "EXTRA_QUERY_PACKAGE_STRING";
        public static string ExtraQueryDomainString => "EXTRA_QUERY_DOMAIN_STRING";
        public static string ExtraUseLastOpenedEntry => "EXTRA_USE_LAST_OPENED_ENTRY"; //if set to true, no query UI is displayed. Can be used to just show a warning
        public static string ExtraIsManualRequest => "EXTRA_IS_MANUAL_REQUEST";
        public static string ExtraAutoReturnFromQuery => "EXTRA_AUTO_RETURN_FROM_QUERY";
        public static string ExtraDisplayWarning => "EXTRA_DISPLAY_WARNING";

        public int RequestCodeQuery => 6245;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            Kp2aLog.Log("ChooseForAutofillActivityBase.OnCreate");
            base.OnCreate(savedInstanceState);
            
            //if launched from history, don't re-use the task. Proceed to FileSelect instead.
            if (Intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory))
            {
                Kp2aLog.Log("ChooseForAutofillActivityBase: started from history");
                Kp2aLog.Log("Forwarding to FileSelect. QueryCredentialsActivity started from history.");
                RestartApp();
                return;
            }

            string requestedUrl = Intent.GetStringExtra(ExtraQueryString);
            if (requestedUrl == null)
            {
                Kp2aLog.Log("ChooseForAutofillActivityBase: no requestedUrl ");
                Toast.MakeText(this, "Cannot execute query for null.", ToastLength.Long).Show();
                RestartApp();
                return;
            }
            
            if (Intent.HasExtra(ExtraDisplayWarning))
            {
                
                AutofillServiceBase.DisplayWarning warning =
                    (AutofillServiceBase.DisplayWarning)Intent.GetIntExtra(ExtraDisplayWarning, (int)AutofillServiceBase.DisplayWarning.None);
                Kp2aLog.Log("ChooseForAutofillActivityBase: ExtraDisplayWarning = " + warning);
                if (warning != AutofillServiceBase.DisplayWarning.None)
                {
                    AlertDialog.Builder builder = new AlertDialog.Builder(this);
                    builder.SetTitle(this.GetString(Resource.String.AutofillWarning_title));

                    string appName = Intent.GetStringExtra(ExtraQueryPackageString);
                    string appNameWithPackage = appName;
                    try
                    {
                        var appInfo = PackageManager.GetApplicationInfo(appName, 0);
                        if (appInfo != null)
                        {
                            appName = PackageManager.GetApplicationLabel(appInfo);
                            appNameWithPackage = appName + " (" + Intent.GetStringExtra(ExtraQueryPackageString) + ")";
                        }


                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    builder.SetMessage(
                            GetString(Resource.String.AutofillWarning_Intro, new Java.Lang.Object[]
                            {
                                Intent.GetStringExtra(ExtraQueryDomainString), appNameWithPackage
                            }) 
                            + " " + 
                            this.GetString(Resource.String.AutofillWarning_FillDomainInUntrustedApp, new Java.Lang.Object[]
                            {
                                Intent.GetStringExtra(ExtraQueryDomainString), appName
                            }));

                    builder.SetPositiveButton(this.GetString(Resource.String.Continue),
                        (dlgSender, dlgEvt) =>
                        {
                            new Kp2aDigitalAssetLinksDataSource(this).RememberTrustedLink(Intent.GetStringExtra(ExtraQueryDomainString),
                                Intent.GetStringExtra(ExtraQueryPackageString));
                            Proceed();

                        });
                    builder.SetNeutralButton(this.GetString(Resource.String.AutofillWarning_trustAsBrowser, new Java.Lang.Object[]
                    {appName}),
                        (sender, args) =>
                        {
                            new Kp2aDigitalAssetLinksDataSource(this).RememberAsTrustedApp(Intent.GetStringExtra(ExtraQueryPackageString));
                            Proceed();
                        });

                    builder.SetNegativeButton(this.GetString(Resource.String.cancel), (dlgSender, dlgEvt) =>
                    {
                        Finish();
                    });
                    

                    Dialog dialog = builder.Create();
                    dialog.Show();
                    return;
                }

            }
            else Kp2aLog.Log("ChooseForAutofillActivityBase: No ExtraDisplayWarning");
            Proceed();
        }


        private void Proceed()
        {
            string requestedUrl = Intent.GetStringExtra(ExtraQueryString);

            var i = GetQueryIntent(requestedUrl, Intent.GetBooleanExtra(ExtraAutoReturnFromQuery, true), Intent.GetBooleanExtra(ExtraUseLastOpenedEntry, false));
            if (i == null)
            {
                //GetQueryIntent returns null if no query is required
                ReturnSuccess();
            }
            else
                StartActivityForResult(i, RequestCodeQuery);
        }

        protected abstract Intent GetQueryIntent(string requestedUrl, bool autoReturnFromQuery, bool useLastOpenedEntry);

        protected void RestartApp()
        {
            Intent intent = IntentBuilder.GetRestartAppIntent(this);
            StartActivity(intent);
            Finish();
        }

        
        public override void Finish()
        {
            if (ReplyIntent != null)
            {
                SetResult(Result.Ok, ReplyIntent);
            }
            else
            {
                SetResult(Result.Canceled);
            }
            base.Finish();
        }

        void OnFailure()
        {
            Log.Warn(CommonUtil.Tag, "Failed auth.");
            ReplyIntent = null;
        }

        protected void OnSuccess(FilledAutofillFieldCollection clientFormDataMap, bool isManual)
        {
            var intent = Intent;
            AssistStructure structure = (AssistStructure)intent.GetParcelableExtra(AutofillManager.ExtraAssistStructure);
            StructureParser parser = new StructureParser(this, structure);
            parser.ParseForFill(isManual);
            AutofillFieldMetadataCollection autofillFields = parser.AutofillFields;
            int partitionIndex = AutofillHintsHelper.GetPartitionIndex(autofillFields.FocusedAutofillCanonicalHints.FirstOrDefault());
            FilledAutofillFieldCollection partitionData = AutofillHintsHelper.FilterForPartition(clientFormDataMap, partitionIndex);
            ReplyIntent = new Intent();
            SetDatasetIntent(AutofillHelper.NewDataset(this, autofillFields, partitionData, IntentBuilder));
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == RequestCodeQuery)
            {
                if (resultCode == ExpectedActivityResult)
                    ReturnSuccess();
                else
                {
                    OnFailure();
                    Finish();
                }


            }

        }

        private void ReturnSuccess()
        {
            OnSuccess(GetDataset(), Intent.GetBooleanExtra(ExtraIsManualRequest, false));
            Finish();
        }

        protected virtual Result ExpectedActivityResult
        {
            get { return Result.Ok; }
        }

        /// <summary>
        /// Creates the FilledAutofillFieldCollection from the intent returned from the query activity
        /// </summary>
        protected abstract FilledAutofillFieldCollection GetDataset();

        public abstract IAutofillIntentBuilder IntentBuilder { get; }

        
        protected void SetResponseIntent(FillResponse fillResponse)
        {
            ReplyIntent.PutExtra(AutofillManager.ExtraAuthenticationResult, fillResponse);
        }

        protected void SetDatasetIntent(Dataset dataset)
        {
            ReplyIntent.PutExtra(AutofillManager.ExtraAuthenticationResult, dataset);
        }
    }
}