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
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Content.PM;
using Android.Preferences;
using KeePassLib;
using KeePassLib.Utility;

namespace keepass2android
{
    [Activity(Label = "@string/kp2a_findUrl", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden, Theme = "@style/MyTheme_ActionBar", Exported = true)]
#if NoNet
    [MetaData("android.app.searchable", Resource = "@xml/searchable_offline")]
#else
#if DEBUG
	[MetaData("android.app.searchable", Resource = "@xml/searchable_debug")]
#else
    [MetaData("android.app.searchable", Resource = "@xml/searchable")]
#endif
#endif
	[MetaData("android.app.default_searchable", Value = "keepass2android.search.SearchResults")]
	[IntentFilter(new[] { Intent.ActionSearch }, Categories = new[] { Intent.CategoryDefault })]
	public class ShareUrlResults : GroupBaseActivity
	{

		public ShareUrlResults (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		public ShareUrlResults()
		{
		}

		public static void Launch(Activity act, SearchUrlTask task, ActivityLaunchMode launchMode)
		{
			Intent i = new Intent(act, typeof(ShareUrlResults));
			task.ToIntent(i);
		    launchMode.Launch(act, i);
		}


        public override bool IsSearchResult
        {
            get { return true; }
        }

        protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			//if user presses back to leave this activity:
			SetResult(Result.Canceled);
            

		    UpdateBottomBarElementVisibility(Resource.Id.select_other_entry, true);
		    UpdateBottomBarElementVisibility(Resource.Id.add_url_entry, true);


            if (App.Kp2a.DatabaseIsUnlocked)
			{
			    var searchUrlTask = ((SearchUrlTask)AppTask);
			    String searchUrl = searchUrlTask.UrlToSearchFor;
				Query(searchUrl, searchUrlTask.AutoReturnFromQuery);	
			}
            // else: LockCloseListActivity.OnResume will trigger a broadcast (LockDatabase) which will cause the activity to be finished.



        }

        protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			AppTask.ToBundle(outState);
		}

		private void Query(string url, bool autoReturnFromQuery)
        {
            
            try
            {
                Group = GetSearchResultsForUrl(url);
            } catch (Exception e)
			{
				Toast.MakeText(this, e.Message, ToastLength.Long).Show();
				SetResult(Result.Canceled);
				Finish();
				return;
			}

			//if there is exactly one match: open the entry
			if ((Group.Entries.Count() == 1) && autoReturnFromQuery && PreferenceManager.GetDefaultSharedPreferences(this).GetBoolean(GetString(Resource.String.AutoReturnFromQuery_key),true))
			{
				LaunchActivityForEntry(Group.Entries.Single(),0);
				return;
			}

			//show results:
			if (Group == null || (!Group.Entries.Any()))
			{
				SetContentView(Resource.Layout.searchurlresults_empty);
			} 
			
			SetGroupTitle();

            FragmentManager.FindFragmentById<GroupListFragment>(Resource.Id.list_fragment).ListAdapter = new PwGroupListAdapter(this, Group);

			View selectOtherEntry = FindViewById (Resource.Id.select_other_entry);

            var newTask = new SearchUrlTask() {AutoReturnFromQuery = false, UrlToSearchFor = url};
		    if (AppTask is SelectEntryTask currentSelectTask)
		        newTask.ShowUserNotifications = currentSelectTask.ShowUserNotifications;
            
            selectOtherEntry.Click += (sender, e) => {
				GroupActivity.Launch (this, newTask, new ActivityLaunchModeRequestCode(0));

			};

			
			View createUrlEntry = FindViewById (Resource.Id.add_url_entry);

			if (App.Kp2a.OpenDatabases.Any(db => db.CanWrite))
			{
				createUrlEntry.Visibility = ViewStates.Visible;
				createUrlEntry.Click += (sender, e) =>
				{
					GroupActivity.Launch(this, new CreateEntryThenCloseTask { Url = url, ShowUserNotifications = (AppTask as SelectEntryTask)?.ShowUserNotifications ?? ShowUserNotificationsMode.Always }, new ActivityLaunchModeRequestCode(0));
					Toast.MakeText(this, GetString(Resource.String.select_group_then_add, new Java.Lang.Object[] { GetString(Resource.String.add_entry) }), ToastLength.Long).Show();
				};
			}
			else
			{
				createUrlEntry.Visibility = ViewStates.Gone;
			}

			Util.MoveBottomBarButtons(Resource.Id.select_other_entry, Resource.Id.add_url_entry, Resource.Id.bottom_bar, this);
		}

        public static PwGroup GetSearchResultsForUrl(string url)
        {
            PwGroup resultsGroup = null;
            foreach (var db in App.Kp2a.OpenDatabases)
            {
                //first: search for exact url
                var resultsForThisDb = db.SearchForExactUrl(url);
                if (!url.StartsWith(KeePass.AndroidAppScheme))
                {
                    //if no results, search for host (e.g. "accounts.google.com")
                    if (!resultsForThisDb.Entries.Any())
                        resultsForThisDb = db.SearchForHost(url, false);
                    //if still no results, search for host, allowing subdomains ("www.google.com" in entry is ok for "accounts.google.com" in search (but not the other way around)
                    if (!resultsForThisDb.Entries.Any())
                        resultsForThisDb = db.SearchForHost(url, true);
                }

                //if no results returned up to now, try to search through other fields as well:
                if (!resultsForThisDb.Entries.Any())
                    resultsForThisDb = db.SearchForText(url);
                //search for host as text
                if (!resultsForThisDb.Entries.Any())
                    resultsForThisDb = db.SearchForText(UrlUtil.GetHost(url.Trim()));

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
			get { return Resource.Layout.searchurlresults; }
	    }

	    public override bool EntriesBelongToCurrentDatabaseOnly
	    {
	        get { return false; }
	    }

	    public override ElementAndDatabaseId FullGroupId
	    {
	        get { return null; }
	    }
	}}

