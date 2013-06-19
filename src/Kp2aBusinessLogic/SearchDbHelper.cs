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
using KeePassLib;
using System.Text.RegularExpressions;
using KeePassLib.Collections;
using KeePassLib.Interfaces;
using KeePassLib.Utility;

namespace keepass2android
{
	/// <summary>
	/// Helper class providing methods to search a given database for specific things
	/// </summary>
	public class SearchDbHelper
	{
        private readonly IKp2aApp _app;

		
		public SearchDbHelper(IKp2aApp app) {
			_app = app;
		}


		public PwGroup SearchForText (Database database, string str)
		{
			SearchParameters sp = new SearchParameters {SearchString = str};

			return Search(database, sp, null);
		}
		public PwGroup Search(Database database, SearchParameters sp, IDictionary<PwUuid, String> resultContexts)
		{
			
			if(sp.RegularExpression) // Validate regular expression
			{
				new Regex(sp.SearchString); 
			}
			
			string strGroupName = _app.GetResourceString(UiStringKey.search_results) + " (\"" + sp.SearchString + "\")";
			PwGroup pgResults = new PwGroup(true, true, strGroupName, PwIcon.EMailSearch) {IsVirtual = true};

			PwObjectList<PwEntry> listResults = pgResults.Entries;
			
			
			database.Root.SearchEntries(sp, listResults, resultContexts, new NullStatusLogger());
			
			
			return pgResults;
			

		}

		
		public PwGroup SearchForExactUrl (Database database, string url)
		{
			SearchParameters sp = SearchParameters.None;
			sp.SearchInUrls = true;
			sp.SearchString = url;
		
			if(sp.RegularExpression) // Validate regular expression
			{
				new Regex(sp.SearchString); 
			}
			
			string strGroupName = _app.GetResourceString(UiStringKey.search_results) + " (\"" + sp.SearchString + "\")";
			PwGroup pgResults = new PwGroup(true, true, strGroupName, PwIcon.EMailSearch) {IsVirtual = true};

			PwObjectList<PwEntry> listResults = pgResults.Entries;
			
			
			database.Root.SearchEntries(sp, listResults, new NullStatusLogger());
			
			
			return pgResults;
			
		}

		private static String ExtractHost(String url)
		{
			return UrlUtil.GetHost(url.Trim());
		}

		public PwGroup SearchForHost(Database database, String url, bool allowSubdomains)
		{
			String host = ExtractHost(url);
			string strGroupName = _app.GetResourceString(UiStringKey.search_results) + " (\"" + host + "\")";
			PwGroup pgResults = new PwGroup(true, true, strGroupName, PwIcon.EMailSearch) {IsVirtual = true};
			if (String.IsNullOrWhiteSpace(host))
				return pgResults;
			foreach (PwEntry entry in database.Entries.Values)
			{
				String otherHost = ExtractHost(entry.Strings.ReadSafe(PwDefs.UrlField));
				if ((allowSubdomains) && (otherHost.StartsWith("www.")))
					otherHost = otherHost.Substring(4); //remove "www."
				if (String.IsNullOrWhiteSpace(otherHost))
				{
					continue;
				}
				if (host.IndexOf(otherHost, StringComparison.InvariantCultureIgnoreCase) > -1)
				{
					pgResults.AddEntry(entry, false);
				}
			}
			return pgResults;
		}

	}
}

