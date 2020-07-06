using System;
using System.Collections.Generic;
using System.Linq;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Service.Autofill;
using Android.Util;
using Android.Views.Autofill;
using Android.Widget;
using keepass2android.services.AutofillBase.model;

namespace keepass2android.services.AutofillBase
{
    public interface IAutofillIntentBuilder
    {
        IntentSender GetAuthIntentSenderForResponse(Context context, string query, string queryDomain, string queryPackage,
            bool isManualRequest, bool autoReturnFromQuery, AutofillServiceBase.DisplayWarning warning);

        IntentSender GetAuthIntentSenderForWarning(Context context, string query, string queryDomain, string queryPackage, AutofillServiceBase.DisplayWarning warning);

        IntentSender GetDisableIntentSenderForResponse(Context context, string query, 
            bool isManualRequest, bool isDisable);
        Intent GetRestartAppIntent(Context context);

        int AppIconResource { get; }
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

        public static HashSet<string> CompatBrowsers = new HashSet<string>
        {
            "org.mozilla.firefox",
            "org.mozilla.firefox_beta",
            "com.microsoft.emmx",
            "com.android.chrome",
            "com.chrome.beta",
            "com.android.browser",
            "com.brave.browser",
            "com.opera.browser",
            "com.opera.browser.beta",
            "com.opera.mini.native",
            "com.chrome.dev",
            "com.chrome.canary",
            "com.google.android.apps.chrome",
            "com.google.android.apps.chrome_dev",
            "com.yandex.browser",
            "com.sec.android.app.sbrowser",
            "com.sec.android.app.sbrowser.beta",
            "org.codeaurora.swe.browser",
            "com.amazon.cloud9",
            "mark.via.gp",
            "org.bromite.bromite",
            "org.chromium.chrome",
            "com.kiwibrowser.browser",
            "com.ecosia.android",
            "com.opera.mini.native.beta",
            "org.mozilla.fennec_aurora",
            "org.mozilla.fennec_fdroid",
            "com.qwant.liberty",
            "com.opera.touch",
            "org.mozilla.fenix",
            "org.mozilla.fenix.nightly",
            "org.mozilla.reference.browser",
            "org.mozilla.rocket",
            "org.torproject.torbrowser",
            "com.vivaldi.browser",
        };

        public override void OnFillRequest(FillRequest request, CancellationSignal cancellationSignal, FillCallback callback)
        {
            bool isManual = (request.Flags & FillRequest.FlagManualRequest) != 0;
            CommonUtil.logd( "onFillRequest " + (isManual ? "manual" : "auto"));
            var structure = request.FillContexts[request.FillContexts.Count - 1].Structure;

            //TODO support package signature verification as soon as this is supported in Keepass storage

            var clientState = request.ClientState;
            CommonUtil.logd( "onFillRequest(): data=" + CommonUtil.BundleToString(clientState));


            cancellationSignal.CancelEvent += (sender, e) => {
                Log.Warn(CommonUtil.Tag, "Cancel autofill not implemented yet.");
            };
            // Parse AutoFill data in Activity
            StructureParser.AutofillTargetId query = null;
            var parser = new StructureParser(this, structure);
            try
            {
                query = parser.ParseForFill(isManual);
                
            }
            catch (Java.Lang.SecurityException e)
            {
                Log.Warn(CommonUtil.Tag, "Security exception handling request");
                callback.OnFailure(e.Message);
                return;
            }
            
            AutofillFieldMetadataCollection autofillFields = parser.AutofillFields;
            
            
            var autofillIds = autofillFields.GetAutofillIds();
            if (autofillIds.Length != 0 && CanAutofill(query, isManual))
            {
                var responseBuilder = new FillResponse.Builder();

                Dataset entryDataset = null;
                if (query.IncompatiblePackageAndDomain == false)
                {
                    //domain and package are compatible. Use Domain if available and package otherwise. Can fill without warning.
                    entryDataset = BuildEntryDataset(query.DomainOrPackage, query.WebDomain, query.PackageName, autofillIds, parser, DisplayWarning.None);
                }
                else
                {

                    //domain or package are incompatible. Don't show the entry. (Tried to do so first but behavior was not consistent)
                    //entryDataset = BuildEntryDataset(query.WebDomain, query.WebDomain, query.PackageName, autofillIds, parser, DisplayWarning.FillDomainInUntrustedApp);
                }
                bool hasEntryDataset = entryDataset != null;
                if (entryDataset != null)
                    responseBuilder.AddDataset(entryDataset);

                if (query.WebDomain != null)
                    AddQueryDataset(query.WebDomain, 
                        query.WebDomain, query.PackageName,
                        isManual, autofillIds, responseBuilder, !hasEntryDataset, query.IncompatiblePackageAndDomain ? DisplayWarning.FillDomainInUntrustedApp : DisplayWarning.None);
                else
                    AddQueryDataset(query.PackageNameWithPseudoSchema,
                        query.WebDomain, query.PackageName,
                        isManual, autofillIds, responseBuilder, !hasEntryDataset, DisplayWarning.None);


                AddDisableDataset(query.DomainOrPackage, autofillIds, responseBuilder, isManual);
                
                if (PreferenceManager.GetDefaultSharedPreferences(this)
                    .GetBoolean(GetString(Resource.String.OfferSaveCredentials_key), true))
                {
                    if (!CompatBrowsers.Contains(parser.PackageId))
                    {
                        responseBuilder.SetSaveInfo(new SaveInfo.Builder(parser.AutofillFields.SaveType,
                            parser.AutofillFields.GetAutofillIds()).Build());
                    }
                    
                }

                callback.OnSuccess(responseBuilder.Build());
            }
            else
            {
                callback.OnSuccess(null);
            }
        }

        private Dataset BuildEntryDataset(string query, string queryDomain, string queryPackage, AutofillId[] autofillIds, StructureParser parser,
            DisplayWarning warning)
        {
            var filledAutofillFieldCollection = GetSuggestedEntry(query);
            if (filledAutofillFieldCollection == null)
                return null;

            if (warning == DisplayWarning.None)
            {
                //can return an actual dataset
                int partitionIndex = AutofillHintsHelper.GetPartitionIndex(parser.AutofillFields.FocusedAutofillCanonicalHints.FirstOrDefault());
                FilledAutofillFieldCollection partitionData = AutofillHintsHelper.FilterForPartition(filledAutofillFieldCollection, partitionIndex);

                return AutofillHelper.NewDataset(this, parser.AutofillFields, partitionData, IntentBuilder);
            }
            else
            {
                //return an "auth" dataset (actually for just warning the user in case domain/package dont match)
                var sender = IntentBuilder.GetAuthIntentSenderForWarning(this, query, queryDomain, queryPackage, warning);
                var datasetName = filledAutofillFieldCollection.DatasetName;
                if (datasetName == null)
                    return null;

                RemoteViews presentation = AutofillHelper.NewRemoteViews(PackageName, datasetName, AppNames.LauncherIcon);

                var datasetBuilder = new Dataset.Builder(presentation);
                datasetBuilder.SetAuthentication(sender);
                //need to add placeholders so we can directly fill after ChooseActivity
                foreach (var autofillId in autofillIds)
                {
                    datasetBuilder.SetValue(autofillId, AutofillValue.ForText("PLACEHOLDER"));
                }

                return datasetBuilder.Build();
            }

            
        }

        protected abstract FilledAutofillFieldCollection GetSuggestedEntry(string query);

        public enum DisplayWarning
        {
            None,
            FillDomainInUntrustedApp, //display a warning that the user is filling credentials for a domain inside an app not marked as trusted browser
            
        }

        private void AddQueryDataset(string query, string queryDomain, string queryPackage, bool isManual, AutofillId[] autofillIds, FillResponse.Builder responseBuilder, bool autoReturnFromQuery, DisplayWarning warning)
        {
            var sender = IntentBuilder.GetAuthIntentSenderForResponse(this, query, queryDomain, queryPackage, isManual, autoReturnFromQuery, warning);
            RemoteViews presentation = AutofillHelper.NewRemoteViews(PackageName,
                GetString(Resource.String.autofill_sign_in_prompt), AppNames.LauncherIcon);

            var datasetBuilder = new Dataset.Builder(presentation);
            datasetBuilder.SetAuthentication(sender);
            //need to add placeholders so we can directly fill after ChooseActivity
            foreach (var autofillId in autofillIds)
            {
                datasetBuilder.SetValue(autofillId, AutofillValue.ForText("PLACEHOLDER"));
            }

            responseBuilder.AddDataset(datasetBuilder.Build());
        }
        public static string GetDisplayNameForQuery(string str, Context Context)
        {
            string displayName = str;
            try
            {
                string appPrefix = "androidapp://";
                if (str.StartsWith(appPrefix))
                {
                    str = str.Substring(appPrefix.Length);
                    PackageManager pm = Context.PackageManager;
                    ApplicationInfo ai;
                    try
                    {
                        ai = pm.GetApplicationInfo(str, 0);
                    }
                    catch (PackageManager.NameNotFoundException e)
                    {
                        ai = null;
                    }
                    displayName = ai != null ? pm.GetApplicationLabel(ai) : str;
                }
            }
            catch (Exception e)
            {
                Kp2aLog.LogUnexpectedError(e);
            }
           
            return displayName;
        }

        private void AddDisableDataset(string query, AutofillId[] autofillIds, FillResponse.Builder responseBuilder, bool isManual)
        {
            bool isQueryDisabled = IsQueryDisabled(query);
            if (isQueryDisabled && !isManual)
                return;
            bool isForDisable = !isQueryDisabled;
            var sender = IntentBuilder.GetDisableIntentSenderForResponse(this, query, isManual, isForDisable);
            
            RemoteViews presentation = AutofillHelper.NewRemoteViews(PackageName,
                GetString(isForDisable ? Resource.String.autofill_disable : Resource.String.autofill_enable_for, new Java.Lang.Object[] { GetDisplayNameForQuery(query, this)}), Resource.Drawable.ic_menu_close_grey);

            var datasetBuilder = new Dataset.Builder(presentation);
            datasetBuilder.SetAuthentication(sender);

            foreach (var autofillId in autofillIds)
            {
                datasetBuilder.SetValue(autofillId, AutofillValue.ForText("PLACEHOLDER"));
            }

            responseBuilder.AddDataset(datasetBuilder.Build());
        }

        private bool CanAutofill(StructureParser.AutofillTargetId query, bool isManual)
        {
            if (query.PackageNameWithPseudoSchema == "androidapp://android" || query.PackageNameWithPseudoSchema == "androidapp://" + this.PackageName)
                return false;
            if (!isManual)
            {
                var isQueryDisabled = IsQueryDisabled(query.DomainOrPackage);
                if (isQueryDisabled)
                    return false;
            }
            return true;
        }

        private bool IsQueryDisabled(string query)
        {
            var prefs = PreferenceManager.GetDefaultSharedPreferences(this);
            var disabledValues = prefs.GetStringSet("AutoFillDisabledQueries", new List<string>());

            bool isQueryDisabled = disabledValues.Contains(query);
            return isQueryDisabled;
        }

        public override void OnSaveRequest(SaveRequest request, SaveCallback callback)
        {

            var structure = request.FillContexts?.LastOrDefault()?.Structure;
            if (structure == null)
            {
                return;
            }

            var parser = new StructureParser(this, structure);
            var query = parser.ParseForSave();
            try
            {
                HandleSaveRequest(parser, query);
                callback.OnSuccess();
            }
            catch (Exception e)
            {
                callback.OnFailure(e.Message);   
            }
            
        }

        protected abstract void HandleSaveRequest(StructureParser parser, StructureParser.AutofillTargetId query);


        public override void OnConnected()
        {
            CommonUtil.logd( "onConnected");
        }

        public override void OnDisconnected()
        {
            CommonUtil.logd( "onDisconnected");
        }

        public abstract IAutofillIntentBuilder IntentBuilder{get;}
    }
}
