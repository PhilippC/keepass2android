/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
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
using Android.Widget;
using keepass2android.view;
using KeePassLib;
using Android.Support.V4.App;

namespace keepass2android.search
{
	/// <summary>
	/// Activity to show search results
	/// </summary>
	[Activity (Label = "@string/app_name", Theme="@style/NoTitleBar", LaunchMode=Android.Content.PM.LaunchMode.SingleTop)]
	[MetaData("android.app.searchable",Resource="@xml/searchable")]
	[IntentFilter(new[]{Intent.ActionSearch}, Categories=new[]{Intent.CategoryDefault})]
	public class SearchResults : GroupBaseActivity
	{
		private Database _db;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			if ( IsFinishing ) {
				return;
			}
			
			SetResult(KeePass.ExitNormal);

			ProcessIntent(Intent);
		}

		protected override void OnNewIntent(Intent intent)
		{
			ProcessIntent(intent);
		}

		private void ProcessIntent(Intent intent)
		{
			_db = App.Kp2a.GetDb();
			

			// Likely the app has been killed exit the activity 
			if ( ! _db.Open ) {
				Finish();
			}

			if (intent.Action == Intent.ActionView)
			{
				var entryIntent = new Intent(this, typeof(EntryActivity));
				entryIntent.PutExtra(EntryActivity.KeyEntry, intent.Data.LastPathSegment);

				Finish(); // Close this activity so that the entry activity is navigated to from the main activity, not this one.
				StartActivity(entryIntent);
			}
			else
			{
				// Action may either by ActionSearch (from search widget) or null (if called from SearchActivity directly)
				Query(getSearch(intent));
			}
		}

		private void Query (SearchParameters searchParams)
		{
			try {
				Group = _db.Search (searchParams);
			} catch (Exception e) {
				Toast.MakeText(this,e.Message, ToastLength.Long).Show();
				Finish();
				return;
			}


			
			if ( Group == null || (!Group.Entries.Any()) ) {
				SetContentView(new GroupEmptyView(this));
			} else {
				SetContentView(new GroupViewOnlyView(this));
			}
			
			SetGroupTitle();
			
			ListAdapter = new PwGroupListAdapter(this, Group);
		}

		private SearchParameters getSearch(Intent queryIntent) {
			// get and process search query here
			SearchParameters sp = new SearchParameters();
			sp.SearchString = queryIntent.GetStringExtra(SearchManager.Query);
			sp.SearchInTitles = queryIntent.GetBooleanExtra("SearchInTitles", sp.SearchInTitles);
			sp.SearchInUrls = queryIntent.GetBooleanExtra("SearchInUrls", sp.SearchInUrls);
			sp.SearchInPasswords = queryIntent.GetBooleanExtra("SearchInPasswords", sp.SearchInPasswords);
			sp.SearchInUserNames = queryIntent.GetBooleanExtra("SearchInUserNames", sp.SearchInUserNames);
			sp.SearchInNotes = queryIntent.GetBooleanExtra("SearchInNotes", sp.SearchInNotes);
			sp.SearchInGroupNames = queryIntent.GetBooleanExtra("SearchInGroupNames", sp.SearchInGroupNames);
			sp.SearchInOther = queryIntent.GetBooleanExtra("SearchInOther", sp.SearchInOther);
			sp.SearchInTags = queryIntent.GetBooleanExtra("SearchInTags", sp.SearchInTags);
			sp.RegularExpression = queryIntent.GetBooleanExtra("RegularExpression", sp.RegularExpression);
			sp.ExcludeExpired = queryIntent.GetBooleanExtra("ExcludeExpired", sp.ExcludeExpired);
			sp.ComparisonMode = queryIntent.GetBooleanExtra("CaseSensitive", false) ?
				StringComparison.InvariantCulture :
					StringComparison.InvariantCultureIgnoreCase;

			return sp;
			
		}


		public override bool OnSearchRequested()
		{
			if (base.OnSearchRequested())
			{
				Finish();
				return true;
			}
				return false;
			}
		}
}

