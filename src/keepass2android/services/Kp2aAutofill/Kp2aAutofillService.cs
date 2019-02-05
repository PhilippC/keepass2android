using System;
using System.Collections.Generic;
using Android;
using Android.App;
using Android.Content;
using Android.Runtime;
using keepass2android.services.AutofillBase;
using keepass2android.services.AutofillBase.model;
using keepass2android.services.Kp2aAutofill;
using Keepass2android.Pluginsdk;
using KeePassLib;
using KeePassLib.Utility;
using Org.Json;
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
            if (App.Kp2a.LastOpenedEntry?.SearchUrl == query)
                return ChooseForAutofillActivity.GetFilledAutofillFieldCollectionFromEntry(
                    App.Kp2a.LastOpenedEntry, this);
            return null;
        }

        protected override void HandleSaveRequest(StructureParser parser, string query)
        {
            

            var intent = new Intent(this, typeof(SelectCurrentDbActivity));

            Dictionary<string, string> outputFields = new Dictionary<string, string>();
            foreach (var p in parser.ClientFormData.HintMap)
            {
                CommonUtil.logd(p.Key + " = " + p.Value.ValueToString());
                outputFields.TryAdd(ChooseForAutofillActivity.GetKp2aKeyFromHint(p.Key), p.Value.ValueToString());

            }
            if (query != null)
                outputFields.TryAdd(PwDefs.UrlField, query);

            JSONObject jsonOutput = new JSONObject(outputFields);
            var jsonOutputStr = jsonOutput.ToString();
            intent.PutExtra(Strings.ExtraEntryOutputData, jsonOutputStr);

            JSONArray jsonProtectedFields = new JSONArray(
                (System.Collections.ICollection)new string[]{});
            intent.PutExtra(Strings.ExtraProtectedFieldsList, jsonProtectedFields.ToString());

            intent.PutExtra(AppTask.AppTaskKey, "CreateEntryThenCloseTask");
            intent.PutExtra(CreateEntryThenCloseTask.ShowUserNotificationsKey, "false");

            StartActivity(intent);

        }

        public override IAutofillIntentBuilder IntentBuilder => new Kp2aAutofillIntentBuilder();
    }
}