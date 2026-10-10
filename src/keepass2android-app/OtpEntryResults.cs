/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;

namespace keepass2android
{
  /// <summary>
  /// Activity to display search results when adding OTP to an existing entry.
  /// Similar to ShareUrlResults but specialized for otpauth:// URI handling.
  /// </summary>
  [Activity(Label = "@string/otp_find_entry", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden, Theme = "@style/Kp2aTheme_ActionBar", Exported = false)]
  public class OtpEntryResults : GroupBaseActivity
  {
    public OtpEntryResults(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public OtpEntryResults()
    {
    }

    public static void Launch(Activity act, AddOtpToEntryTask task, ActivityLaunchMode launchMode)
    {
      Intent i = new Intent(act, typeof(OtpEntryResults));
      task.ToIntent(i);
      launchMode.Launch(act, i);
    }

    public override bool IsSearchResult
    {
      get { return true; }
    }

    protected override void OnCreate(Bundle savedInstanceState)
    {
      if (!App.Kp2a.DatabasesBackgroundModificationLock.TryEnterReadLock(TimeSpan.FromSeconds(5)))
      {
        App.Kp2a.ShowMessage(this, GetString(Resource.String.failed_to_access_database), MessageSeverity.Error);
        Finish();
        return;
      }

      base.OnCreate(savedInstanceState);

      SetResult(Result.Canceled);

      UpdateBottomBarElementVisibility(Resource.Id.select_other_entry, true);
      UpdateBottomBarElementVisibility(Resource.Id.add_url_entry, true);

      if (App.Kp2a.DatabaseIsUnlocked)
      {
        Query();
      }
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
      base.OnSaveInstanceState(outState);
      AppTask.ToBundle(outState);
    }

    private void Query()
    {
      try
      {
        var addOtpTask = AppTask as AddOtpToEntryTask;
        if (addOtpTask == null)
        {
          Finish();
          return;
        }

        string searchText = AddOtpToEntryTask.GetSearchTextFromOtpUri(addOtpTask.OtpUri);
        Group = GetSearchResultsForOtp(searchText);
      }
      catch (Exception e)
      {
        App.Kp2a.ShowMessage(this, Util.GetErrorMessage(e), MessageSeverity.Error);
        SetResult(Result.Canceled);
        Finish();
        return;
      }

      // If no results, show empty layout
      if (Group == null || !Group.Entries.Any())
      {
        SetContentView(Resource.Layout.otp_entry_results_empty);
      }

      SetGroupTitle();

      var listFragment = FragmentManager.FindFragmentById<GroupListFragment>(Resource.Id.list_fragment);
      if (listFragment != null)
      {
        listFragment.ListAdapter = new PwGroupListAdapter(this, Group);
      }

      View selectOtherEntry = FindViewById(Resource.Id.select_other_entry);
      View createNewEntry = FindViewById(Resource.Id.add_url_entry);

      var otpTask = AppTask as AddOtpToEntryTask;

      selectOtherEntry.Visibility = ViewStates.Visible;
      selectOtherEntry.Click += (sender, e) =>
      {
        // Launch GroupActivity with the same AddOtpToEntryTask so user can browse/search
        GroupActivity.Launch(this, otpTask, new ActivityLaunchModeRequestCode(0));
      };

      if (App.Kp2a.OpenDatabases.Any(db => db.CanWrite))
      {
        createNewEntry.Visibility = ViewStates.Visible;
        createNewEntry.Click += (sender, e) =>
        {
          // Create a new entry with OTP pre-filled (original behavior)
          GroupActivity.Launch(this,
              new CreateEntryThenCloseTask
              {
                AllFields = Newtonsoft.Json.JsonConvert.SerializeObject(
                    new Dictionary<string, string> { { "otp", otpTask.OtpUri } })
              },
              new ActivityLaunchModeRequestCode(0));
          App.Kp2a.ShowMessage(this,
              GetString(Resource.String.select_group_then_add,
                  new Java.Lang.Object[] { GetString(Resource.String.add_entry) }),
              MessageSeverity.Info);
        };
      }
      else
      {
        createNewEntry.Visibility = ViewStates.Gone;
      }

      Util.MoveBottomBarButtons(Resource.Id.select_other_entry, Resource.Id.add_url_entry, Resource.Id.bottom_bar, this);
    }

    /// <summary>
    /// Search for entries matching the OTP issuer/label text.
    /// </summary>
    public static PwGroup GetSearchResultsForOtp(string searchText)
    {
      PwGroup resultsGroup = null;
      foreach (var db in App.Kp2a.OpenDatabases)
      {
        PwGroup resultsForThisDb = null;

        if (!string.IsNullOrEmpty(searchText))
        {
          resultsForThisDb = db.SearchForText(searchText);
        }

        if (resultsForThisDb == null)
        {
          resultsForThisDb = new PwGroup(true, true) { Name = "Search Results" };
        }

        if (resultsGroup == null)
        {
          resultsGroup = resultsForThisDb;
        }
        else
        {
          foreach (var entry in resultsForThisDb.Entries)
          {
            resultsGroup.AddEntry(entry, false, false);
          }
        }
      }

      return resultsGroup;
    }

    public override bool OnSearchRequested()
    {
      Intent i = new Intent(this, typeof(SearchActivity));
      this.AppTask.ToIntent(i);
      i.SetFlags(ActivityFlags.ForwardResult);
      StartActivity(i);
      return true;
    }

    protected override int ContentResourceId
    {
      get { return Resource.Layout.otp_entry_results; }
    }

    public override bool EntriesBelongToCurrentDatabaseOnly
    {
      get { return false; }
    }

    public override ElementAndDatabaseId FullGroupId
    {
      get { return null; }
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();
      App.Kp2a.DatabasesBackgroundModificationLock.ExitReadLock();
    }
  }
}
