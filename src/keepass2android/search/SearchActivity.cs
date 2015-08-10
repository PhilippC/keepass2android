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
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using KeePassLib;
using keepass2android.search;

namespace keepass2android
{
	/// <summary>
	/// Activity to display search options
	/// </summary>
    [Activity(Label = "@string/app_name", Theme = "@style/MyTheme_ActionBar")]			
	public class SearchActivity : LockCloseActivity
	{
		bool GetCheckBoxValue(int resId)
		{
			return ((CheckBox)FindViewById(resId)).Checked;
		}

		private AppTask _appTask;

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);
			_appTask = AppTask.GetTaskInOnCreate(bundle, Intent);
			SetContentView(Resource.Layout.search);
			SearchParameters sp = new SearchParameters();
			PopulateCheckBox(Resource.Id.cbSearchInTitle, sp.SearchInTitles);
			PopulateCheckBox(Resource.Id.cbSearchInUsername, sp.SearchInUserNames);
			PopulateCheckBox(Resource.Id.cbSearchInNotes, sp.SearchInNotes);
			PopulateCheckBox(Resource.Id.cbSearchInPassword, sp.SearchInPasswords);
			PopulateCheckBox(Resource.Id.cbSearchInTags, sp.SearchInTags);
			PopulateCheckBox(Resource.Id.cbSearchInGroupName, sp.SearchInGroupNames);
			PopulateCheckBox(Resource.Id.cbSearchInUrl, sp.SearchInUrls);
			PopulateCheckBox(Resource.Id.cbSearchInOtherStrings, sp.SearchInOther);
			PopulateCheckBox(Resource.Id.cbRegEx, sp.RegularExpression);

			StringComparison sc = sp.ComparisonMode;
			bool caseSensitive = ((sc != StringComparison.CurrentCultureIgnoreCase) &&
			                             (sc != StringComparison.InvariantCultureIgnoreCase) &&
			                             (sc != StringComparison.OrdinalIgnoreCase));
			PopulateCheckBox(Resource.Id.cbCaseSensitive, caseSensitive);
			PopulateCheckBox(Resource.Id.cbExcludeExpiredEntries, sp.ExcludeExpired);

			ImageButton btnSearch = (ImageButton)FindViewById(Resource.Id.search_button);

			btnSearch.Click += (sender, e) => PerformSearch();

			FindViewById<EditText>(Resource.Id.searchEditText).EditorAction += (sender, e) => 
			{
				if (e.ActionId == Android.Views.InputMethods.ImeAction.Search) {
					PerformSearch();
				}
			};
		}
		void PopulateCheckBox(int resId, bool value)
		{
			((CheckBox) FindViewById(resId)).Checked = value;
		}

		void PerformSearch()
		{
			String searchString = ((EditText)FindViewById(Resource.Id.searchEditText)).Text;
			if (String.IsNullOrWhiteSpace(searchString))
				return;
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
			//forward appTask:
			_appTask.ToIntent(searchIntent);

			Util.FinishAndForward(this, searchIntent);

		}
	}
}

