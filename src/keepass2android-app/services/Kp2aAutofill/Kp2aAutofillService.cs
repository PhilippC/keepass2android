// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using Android;
using Android.App;
using Android.Content;
using Android.Preferences;
using Android.Runtime;
using keepass2android.services.AutofillBase;
using keepass2android.services.AutofillBase.model;
using keepass2android.services.Kp2aAutofill;
using Keepass2android.Pluginsdk;
using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Utility;
using Kp2aAutofillParser;
using Org.Json;
using AutofillServiceBase = keepass2android.services.AutofillBase.AutofillServiceBase;

namespace keepass2android.services
{
  [Service(Label = AppNames.AppName, Permission = Manifest.Permission.BindAutofillService, Exported = true)]
  [IntentFilter(new[] { "android.service.autofill.AutofillService" })]
  [MetaData("android.autofill", Resource = "@xml/autofillservice")]
  [Register("keepass2android.services.Kp2aAutofillService")]
  public class Kp2aAutofillService : AutofillServiceBase
  {
    public Kp2aAutofillService()
    {

    }

    public Kp2aAutofillService(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override Dictionary<PwEntryOutput, FilledAutofillFieldCollection<ViewNodeInputField>> GetSuggestedEntries(string query)
    {
      if (!App.Kp2a.DatabaseIsUnlocked)
        return new Dictionary<PwEntryOutput, FilledAutofillFieldCollection<ViewNodeInputField>>();
      var foundEntries = (ShareUrlResults.GetSearchResultsForUrl(query)?.Entries ?? new PwObjectList<PwEntry>())
          .ToList();

      if (App.Kp2a.LastOpenedEntry?.SearchUrl == query)
      {
        foundEntries.Clear();
        foundEntries.Add(App.Kp2a.LastOpenedEntry?.Entry);
      }

      int numDisableDatasets = 0;
      if (!PreferenceManager.GetDefaultSharedPreferences(this)
              .GetBoolean(GetString(keepass2android.Resource.String.NoAutofillDisabling_key), false))
        numDisableDatasets = 1;

      //it seems like at least with Firefox we can have at most 3 datasets. Reserve space for the disable dataset and the "fill with KP2A" which allows to select another item
      return foundEntries.Take(2 - numDisableDatasets)
          .Select(e => new PwEntryOutput(e, App.Kp2a.FindDatabaseForElement(e)))
          .ToDictionary(e => e,
                      e => ChooseForAutofillActivity.GetFilledAutofillFieldCollectionFromEntry(e, this));
    }

    protected override void HandleSaveRequest(StructureParser parser, StructureParser.AutofillTargetId query)
    {
      var intent = new Intent(this, typeof(SelectCurrentDbActivity));
      intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);


      Dictionary<string, string> outputFields = new Dictionary<string, string>();
      foreach (var p in parser.ClientFormData.HintMap)
      {
        CommonUtil.logd(p.Key + " = " + p.Value.ValueToString());
        outputFields.TryAdd(ChooseForAutofillActivity.GetKp2aKeyFromHint(p.Key), p.Value.ValueToString());

      }
      if (query != null)
        outputFields.TryAdd(PwDefs.UrlField, query.WebDomain);

      JSONObject jsonOutput = new JSONObject(outputFields);
      var jsonOutputStr = jsonOutput.ToString();
      intent.PutExtra(Strings.ExtraEntryOutputData, jsonOutputStr);

      JSONArray jsonProtectedFields = new JSONArray(
          (System.Collections.ICollection)new string[] { });
      intent.PutExtra(Strings.ExtraProtectedFieldsList, jsonProtectedFields.ToString());

      intent.PutExtra(AppTask.AppTaskKey, "CreateEntryThenCloseTask");
      intent.PutExtra(CreateEntryThenCloseTask.ShowUserNotificationsKey, "false");

      StartActivity(intent);

    }

    public override IAutofillIntentBuilder IntentBuilder => new Kp2aAutofillIntentBuilder();
  }
}