﻿using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.App.Slices;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Service.Autofill;
using Android.Util;
using Android.Views.Autofill;
using Android.Views.InputMethods;
using Android.Widget;
using Android.Widget.Inline;
using AndroidX.AutoFill.Inline;
using AndroidX.AutoFill.Inline.V1;
using Java.Util.Concurrent.Atomic;
using keepass2android.services.AutofillBase.model;
using Kp2aAutofillParser;

namespace keepass2android.services.AutofillBase
{
    public interface IAutofillIntentBuilder
    {
        PendingIntent GetAuthPendingIntentForResponse(Context context, string query, string queryDomain, string queryPackage,
            bool isManualRequest, bool autoReturnFromQuery, AutofillServiceBase.DisplayWarning warning);

        PendingIntent GetAuthPendingIntentForWarning(Context context, string query, string queryDomain, string queryPackage, AutofillServiceBase.DisplayWarning warning);

        PendingIntent GetDisablePendingIntentForResponse(Context context, string query, 
            bool isManualRequest, bool isDisable);
        Intent GetRestartAppIntent(Context context);

        int AppIconResource { get; }
    }

    public abstract class AutofillServiceBase: AutofillService
    {
        protected override void AttachBaseContext(Context baseContext)
        {
            base.AttachBaseContext(LocaleManager.setLocale(baseContext));
        }

        //use a lock to avoid returning a response several times in buggy Firefox during one connection: this avoids flickering 
        //and disappearing of the autofill prompt.
        //Instead of using a boolean lock, we use a "time-out lock" which is cleared after a few seconds
        private DateTime _lockTime = DateTime.MinValue;

        private TimeSpan _lockTimeout = TimeSpan.FromSeconds(2);

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
            var structure = request.FillContexts.Last().Structure;


            if (_lockTime + _lockTimeout < DateTime.Now)
            {
                _lockTime = DateTime.Now;

                //TODO support package signature verification as soon as this is supported in Keepass storage

                var clientState = request.ClientState;
                CommonUtil.logd("onFillRequest(): data=" + CommonUtil.BundleToString(clientState));


                cancellationSignal.CancelEvent += (sender, e) =>
                {
                    Kp2aLog.Log("Cancel autofill not implemented yet.");
                    _lockTime = DateTime.MinValue;
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

                
                InlineSuggestionsRequest inlineSuggestionsRequest = null;
                IList<InlinePresentationSpec> inlinePresentationSpecs = null;
                if (((int) Build.VERSION.SdkInt >= 30)
                    && (PreferenceManager.GetDefaultSharedPreferences(this).GetBoolean(GetString(Resource.String.InlineSuggestions_key), true)))
                {
                    inlineSuggestionsRequest = request.InlineSuggestionsRequest;

                    inlinePresentationSpecs = inlineSuggestionsRequest?.InlinePresentationSpecs;
                }


                var autofillIds = parser.AutofillFields.GetAutofillIds();
                if (autofillIds.Length != 0 && CanAutofill(query, isManual))
                {
                    var responseBuilder = new FillResponse.Builder();

                    bool hasEntryDataset = false;

                    IList<Dataset> entryDatasets = new List<Dataset>();
                    if (query.IncompatiblePackageAndDomain == false)
                    {
                        Kp2aLog.Log("AF: (query.IncompatiblePackageAndDomain == false)");
                        //domain and package are compatible. Use Domain if available and package otherwise. Can fill without warning.
                        entryDatasets = BuildEntryDatasets(query.DomainOrPackage, query.WebDomain,
                                                    query.PackageName,
                                                    autofillIds, parser, DisplayWarning.None,
                                                    inlinePresentationSpecs
                                                    ).Where(ds => ds != null).ToList();
                        if (entryDatasets.Count > inlineSuggestionsRequest?.MaxSuggestionCount - 2 /*disable dataset and query*/)
                        {
                            //we have too many elements. disable inline suggestions
                            inlinePresentationSpecs = null;
                            entryDatasets = BuildEntryDatasets(query.DomainOrPackage, query.WebDomain,
                                                    query.PackageName,
                                                    autofillIds, parser, DisplayWarning.None,
                                                    null
                                                    ).Where(ds => ds != null).ToList();
                        }
                        foreach (var entryDataset in entryDatasets
                        )
                        {
                            Kp2aLog.Log("AF: Got EntryDataset " + (entryDataset == null));
                            responseBuilder.AddDataset(entryDataset);
                            hasEntryDataset = true;
                        }
                    }
                   

                    
                    {
                        if (query.WebDomain != null)
                            AddQueryDataset(query.WebDomain,
                                query.WebDomain, query.PackageName,
                                isManual, autofillIds, responseBuilder, !hasEntryDataset,
                                query.IncompatiblePackageAndDomain
                                    ? DisplayWarning.FillDomainInUntrustedApp
                                    : DisplayWarning.None,
                                AutofillHelper.ExtractSpec(inlinePresentationSpecs, entryDatasets.Count));
                        else
                            AddQueryDataset(query.PackageNameWithPseudoSchema,
                                query.WebDomain, query.PackageName,
                                isManual, autofillIds, responseBuilder, !hasEntryDataset, DisplayWarning.None,
                                AutofillHelper.ExtractSpec(inlinePresentationSpecs, entryDatasets.Count));
                    }

                    if (!PreferenceManager.GetDefaultSharedPreferences(this)
                        .GetBoolean(GetString(Resource.String.NoAutofillDisabling_key), false))
                        AddDisableDataset(query.DomainOrPackage, autofillIds, responseBuilder, isManual, AutofillHelper.ExtractSpec(inlinePresentationSpecs, entryDatasets.Count));

                    if (PreferenceManager.GetDefaultSharedPreferences(this)
                        .GetBoolean(GetString(Resource.String.OfferSaveCredentials_key), true))
                    {
                        if (!CompatBrowsers.Contains(parser.PackageId))
                        {
                            responseBuilder.SetSaveInfo(new SaveInfo.Builder(parser.AutofillFields.SaveType,
                                parser.AutofillFields.GetAutofillIds()).Build());
                        }

                    }

                    Kp2aLog.Log("return autofill success");
                    callback.OnSuccess(responseBuilder.Build());
                }
                else
                {
                    Kp2aLog.Log("cannot autofill");
                    callback.OnSuccess(null);
                }
            }
            else
            {
                Kp2aLog.Log("Ignoring onFillRequest as there is another request going on.");
            }
        }
        
        
        

        private List<Dataset> BuildEntryDatasets(string query, string queryDomain, string queryPackage, AutofillId[] autofillIds, StructureParser parser,
            DisplayWarning warning, IList<InlinePresentationSpec> inlinePresentationSpecs)
        {
            List<Dataset> result = new List<Dataset>();
            Kp2aLog.Log("AF: BuildEntryDatasets");
            var suggestedEntries = GetSuggestedEntries(query).ToDictionary(e => e.DatasetName, e => e);
            Kp2aLog.Log("AF: BuildEntryDatasets found " + suggestedEntries.Count + " entries");
            int count = 0;
            foreach (var filledAutofillFieldCollection in suggestedEntries.Values)
            {

                if (filledAutofillFieldCollection == null)
                    continue;

                var inlinePresentationSpec = AutofillHelper.ExtractSpec(inlinePresentationSpecs, count);

                if (warning == DisplayWarning.None)
                {
          
                    FilledAutofillFieldCollection<ViewNodeInputField> partitionData =
                        AutofillHintsHelper.FilterForPartition(filledAutofillFieldCollection, parser.AutofillFields.FocusedAutofillCanonicalHints);

                    Kp2aLog.Log("AF: Add dataset");

                    result.Add(AutofillHelper.NewDataset(this, parser.AutofillFields, partitionData, IntentBuilder, 
                        inlinePresentationSpec));
                }
                else
                {
                    //return an "auth" dataset (actually for just warning the user in case domain/package dont match)
                    PendingIntent pendingIntent =
                        IntentBuilder.GetAuthPendingIntentForWarning(this, query, queryDomain, queryPackage, warning);
                    var datasetName = filledAutofillFieldCollection.DatasetName;
                    if (datasetName == null)
                    {
                        Kp2aLog.Log("AF: dataset name is null");
                        continue;
                    }

                    RemoteViews presentation =
                        AutofillHelper.NewRemoteViews(PackageName, datasetName, AppNames.LauncherIcon);

                    var datasetBuilder = new Dataset.Builder(presentation);
                    datasetBuilder.SetAuthentication(pendingIntent?.IntentSender);

                    AutofillHelper.AddInlinePresentation(this, inlinePresentationSpec, datasetName, datasetBuilder, AppNames.LauncherIcon, null);

                    //need to add placeholders so we can directly fill after ChooseActivity
                    foreach (var autofillId in autofillIds)
                    {
                        datasetBuilder.SetValue(autofillId, AutofillValue.ForText("PLACEHOLDER"));
                    }
                    Kp2aLog.Log("AF: Add auth dataset");
                    result.Add(datasetBuilder.Build());
                }
                count++;
            }

            return result;


        }

        protected abstract List<FilledAutofillFieldCollection<ViewNodeInputField>> GetSuggestedEntries(string query);

        public enum DisplayWarning
        {
            None,
            FillDomainInUntrustedApp, //display a warning that the user is filling credentials for a domain inside an app not marked as trusted browser
            
        }

        private void AddQueryDataset(string query, string queryDomain, string queryPackage, bool isManual, AutofillId[] autofillIds, FillResponse.Builder responseBuilder, bool autoReturnFromQuery, DisplayWarning warning, InlinePresentationSpec inlinePresentationSpec)
        {
            PendingIntent pendingIntent = IntentBuilder.GetAuthPendingIntentForResponse(this, query, queryDomain, queryPackage, isManual, autoReturnFromQuery, warning);
            string text = GetString(Resource.String.autofill_sign_in_prompt);
            RemoteViews overlayPresentation = AutofillHelper.NewRemoteViews(base.PackageName,
                text, AppNames.LauncherIcon);

            var datasetBuilder = new Dataset.Builder(overlayPresentation);
            datasetBuilder.SetAuthentication(pendingIntent?.IntentSender);
            //need to add placeholders so we can directly fill after ChooseActivity
            foreach (var autofillId in autofillIds)
            {
                datasetBuilder.SetValue(autofillId, AutofillValue.ForText("PLACEHOLDER"));
            }

            AutofillHelper.AddInlinePresentation(this, inlinePresentationSpec, text, datasetBuilder, AppNames.LauncherIcon, pendingIntent);


            responseBuilder.AddDataset(datasetBuilder.Build());
        }
        public static string GetDisplayNameForQuery(string str, Context Context)
        {
            string displayName = str;
            try
            {
                string appPrefix = KeePass.AndroidAppScheme;
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

        private void AddDisableDataset(string query, AutofillId[] autofillIds, FillResponse.Builder responseBuilder, bool isManual, InlinePresentationSpec inlinePresentationSpec)
        {
            bool isQueryDisabled = IsQueryDisabled(query);
            if (isQueryDisabled && !isManual)
                return;
            bool isForDisable = !isQueryDisabled;
            var pendingIntent = IntentBuilder.GetDisablePendingIntentForResponse(this, query, isManual, isForDisable);

            string text = GetString(isForDisable ? Resource.String.autofill_disable : Resource.String.autofill_enable_for, new Java.Lang.Object[] { GetDisplayNameForQuery(query, this) });
            RemoteViews presentation = AutofillHelper.NewRemoteViews(base.PackageName,
                text, Resource.Drawable.ic_menu_close_grey);

            var datasetBuilder = new Dataset.Builder(presentation);
            datasetBuilder.SetAuthentication(pendingIntent?.IntentSender);

            AutofillHelper.AddInlinePresentation(this, inlinePresentationSpec, text, datasetBuilder, Resource.Drawable.ic_menu_close_grey, null);

            foreach (var autofillId in autofillIds)
            {
                datasetBuilder.SetValue(autofillId, AutofillValue.ForText("PLACEHOLDER"));
            }

            responseBuilder.AddDataset(datasetBuilder.Build());
        }

        private bool CanAutofill(StructureParser.AutofillTargetId query, bool isManual)
        {
            if (query.PackageNameWithPseudoSchema == KeePass.AndroidAppScheme+"android" || query.PackageNameWithPseudoSchema == KeePass.AndroidAppScheme + this.PackageName)
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

            _lockTime = DateTime.MinValue;
            CommonUtil.logd( "onDisconnected");
        }

        public abstract IAutofillIntentBuilder IntentBuilder{get;}
    }
}
