/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

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
using System.Linq;
using System.Text.RegularExpressions;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Preferences;
using Android.Views;
using Android.Widget;
using keepass2android.view;
using keepass2android;
using KeePassLib;

namespace keepass2android.search
{
  /// <summary>
  /// Activity to show search results
  /// </summary>
  [Activity(Label = "@string/app_name", Theme = "@style/Kp2aTheme_ActionBar", LaunchMode = Android.Content.PM.LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
  public class SearchTotpResults : GroupBaseActivity
  {

    public static void Launch(Activity act, AppTask appTask, ActivityFlags? flags = null)
    {
      Intent i = new Intent(act, typeof(SearchTotpResults));


      if (flags != null)
        i.SetFlags((ActivityFlags)flags);

      appTask.ToIntent(i);
      if (flags != null && (((ActivityFlags)flags) | ActivityFlags.ForwardResult) == ActivityFlags.ForwardResult)
        act.StartActivity(i);
      else
        act.StartActivityForResult(i, 0);
    }

    public override bool MayPreviewTotp
    {
      get
      {
        return true;
      }
    }

    protected override void OnCreate(Bundle bundle)
    {
      base.OnCreate(bundle);

      if (IsFinishing)
      {
        return;
      }

      SetResult(KeePass.ExitNormal);

      // Likely the app has been killed exit the activity 
      if (!App.Kp2a.DatabaseIsUnlocked)
      {
        Finish();
      }

      Group = new PwGroup()
      {
        Name = GetString(Resource.String.TOTP)
      };
      try
      {
        foreach (var db in App.Kp2a.OpenDatabases)
        {
          foreach (var entry in db.EntriesById.Values)
          {
            var totpData = new Kp2aTotp().TryGetTotpData(new PwEntryOutput(entry, db));
            if (totpData?.IsTotpEntry == true)
              Group.AddEntry(entry, false);
          }

        }
      }
      catch (Exception e)
      {
        Kp2aLog.LogUnexpectedError(e);
        App.Kp2a.ShowMessage(this, Util.GetErrorMessage(e), MessageSeverity.Error);
        Finish();
        return;
      }

      if (Group == null || (!Group.Entries.Any()))
      {
        SetContentView(Resource.Layout.group_empty);
      }

      SetGroupTitle();

      FragmentManager.FindFragmentById<GroupListFragment>(Resource.Id.list_fragment).ListAdapter = new PwGroupListAdapter(this, Group);

    }

    public override bool EntriesBelongToCurrentDatabaseOnly
    {
      get { return false; }
    }

    public override ElementAndDatabaseId FullGroupId
    {
      get { return null; }
    }





    public override void OnCreateContextMenu(IContextMenu menu, View v,
        IContextMenuContextMenuInfo menuInfo)
    {

      AdapterView.AdapterContextMenuInfo acmi = (AdapterView.AdapterContextMenuInfo)menuInfo;
      ClickView cv = (ClickView)acmi.TargetView;
      cv.OnCreateMenu(menu, menuInfo);
    }

    public override bool OnContextItemSelected(IMenuItem item)
    {
      AdapterView.AdapterContextMenuInfo acmi = (AdapterView.AdapterContextMenuInfo)item.MenuInfo;
      ClickView cv = (ClickView)acmi.TargetView;

      bool result;

      return cv.OnContextItemSelected(item);
    }


    public override bool OnSearchRequested()
    {
      Intent i = new Intent(this, typeof(SearchActivity));
      this.AppTask.ToIntent(i);
      i.SetFlags(ActivityFlags.ForwardResult);
      StartActivity(i);
      return true;
    }

    public override bool IsSearchResult
    {
      get { return true; }
    }
  }
}

