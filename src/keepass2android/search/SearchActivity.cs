/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

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
using KeePassLib;
using keepass2android.search;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", Theme="@style/Base")]			
	public class SearchActivity : LifecycleDebugActivity
	{
		bool GetCheckBoxValue(int resId)
		{
			return ((CheckBox)FindViewById(resId)).Checked;
		}

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);
			SetContentView(Resource.Layout.search);
			SearchParameters sp = new SearchParameters();
			populateCheckBox(Resource.Id.cbSearchInTitle, sp.SearchInTitles);
			populateCheckBox(Resource.Id.cbSearchInUsername, sp.SearchInUserNames);
			populateCheckBox(Resource.Id.cbSearchInNotes, sp.SearchInNotes);
			populateCheckBox(Resource.Id.cbSearchInPassword, sp.SearchInPasswords);
			populateCheckBox(Resource.Id.cbSearchInTags, sp.SearchInTags);
			populateCheckBox(Resource.Id.cbSearchInGroupName, sp.SearchInGroupNames);
			populateCheckBox(Resource.Id.cbSearchInUrl, sp.SearchInUrls);
			populateCheckBox(Resource.Id.cbSearchInOtherStrings, sp.SearchInOther);
			populateCheckBox(Resource.Id.cbRegEx, sp.RegularExpression);

			StringComparison sc = sp.ComparisonMode;
			bool caseSensitive = ((sc != StringComparison.CurrentCultureIgnoreCase) &&
			                             (sc != StringComparison.InvariantCultureIgnoreCase) &&
			                             (sc != StringComparison.OrdinalIgnoreCase));
			populateCheckBox(Resource.Id.cbCaseSensitive, caseSensitive);
			populateCheckBox(Resource.Id.cbExcludeExpiredEntries, sp.ExcludeExpired);

			ImageButton btnSearch = (ImageButton)FindViewById(Resource.Id.search_button);

			btnSearch.Click += (object sender, EventArgs e) => 
			{
				PerformSearch();
			};

			FindViewById<EditText>(Resource.Id.searchEditText).EditorAction += (object sender, TextView.EditorActionEventArgs e) => 
			{
				if (e.ActionId == Android.Views.InputMethods.ImeAction.Search) {
					PerformSearch();
				}
			};
		}
		void populateCheckBox(int resId, bool value)
		{
			((CheckBox) FindViewById(resId)).Checked = value;
		}

		void PerformSearch()
		{
			String searchString = ((EditText)FindViewById(Resource.Id.searchEditText)).Text;
			if (String.IsNullOrWhiteSpace(searchString))
				return;
			SearchParameters spNew = new SearchParameters();
			Intent searchIntent = new Intent(this, typeof(SearchResults));
			searchIntent.PutExtra("SearchInTitles", GetCheckBoxValue(Resource.Id.cbSearchInTitle));
			searchIntent.PutExtra("SearchInUrls", GetCheckBoxValue(Resource.Id.cbSearchInUrl));
			searchIntent.PutExtra("SearchInPasswords", GetCheckBoxValue(Resource.Id.cbSearchInPassword));
			searchIntent.PutExtra("SearchInUserNames", GetCheckBoxValue(Resource.Id.cbSearchInUsername));
			searchIntent.PutExtra("SearchInNotes", GetCheckBoxValue(Resource.Id.cbSearchInNotes));
			searchIntent.PutExtra("SearchInGroupNames", GetCheckBoxValue(Resource.Id.cbSearchInGroupName));
			searchIntent.PutExtra("SearchInOther", GetCheckBoxValue(Resource.Id.cbSearchInOtherStrings));
			searchIntent.PutExtra("SearchInTags", GetCheckBoxValue(Resource.Id.cbSearchInTags));
			searchIntent.PutExtra("RegularExpression", GetCheckBoxValue(Resource.Id.cbRegEx));
			searchIntent.PutExtra("CaseSensitive", GetCheckBoxValue(Resource.Id.cbCaseSensitive));
			searchIntent.PutExtra("ExcludeExpired", GetCheckBoxValue(Resource.Id.cbExcludeExpiredEntries));
			searchIntent.PutExtra(SearchManager.Query, searchString);
			StartActivityForResult(searchIntent, 0);
			Finish();
		}
	}
}

