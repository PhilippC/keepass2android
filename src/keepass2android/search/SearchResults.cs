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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using keepass2android.view;
using KeePassLib;

namespace keepass2android.search
{
	[Activity (Label = "@string/app_name", Theme="@style/NoTitleBar")]
	[MetaData("android.app.searchable",Resource="@xml/searchable")]
	[IntentFilter(new[]{Intent.ActionSearch}, Categories=new[]{Intent.CategoryDefault})]
	public class SearchResults : GroupBaseActivity
	{
		private Database mDb;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			if ( IsFinishing ) {
				return;
			}
			
			SetResult(KeePass.EXIT_NORMAL);
			
			mDb = App.getDB();
			
			// Likely the app has been killed exit the activity 
			if ( ! mDb.Open ) {
				Finish();
			}

			query(getSearch(Intent));


		}


		private void query (SearchParameters searchParams)
		{
			try {
				mGroup = mDb.Search (searchParams);
			} catch (Exception e) {
				Toast.MakeText(this,e.Message, ToastLength.Long).Show();
				Finish();
				return;
			}


			
			if ( mGroup == null || (mGroup.Entries.Count() < 1) ) {
				SetContentView(new GroupEmptyView(this));
			} else {
				SetContentView(new GroupViewOnlyView(this));
			}
			
			setGroupTitle();
			
			ListAdapter = new PwGroupListAdapter(this, mGroup);
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
			} else
			{
				return false;
			}
		}
	}
}

