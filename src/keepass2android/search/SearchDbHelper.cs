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
using KeePassLib;
using Android.Preferences;
using System.Text.RegularExpressions;
using KeePassLib.Collections;
using KeePassLib.Interfaces;
using KeePassLib.Utility;
using Android.Util;

namespace keepass2android
{
	public class SearchDbHelper
	{

		private readonly Context mCtx;
		
		public SearchDbHelper(Context ctx) {
			mCtx = ctx;
		}


		public PwGroup searchForText (Database database, string str)
		{
			SearchParameters sp = new SearchParameters();
			sp.SearchString = str;

			return search(database, sp, null);
		}
		public PwGroup search(Database database, SearchParameters sp, IDictionary<PwUuid, String> resultContexts)
		{
			
			if(sp.RegularExpression) // Validate regular expression
			{
				new Regex(sp.SearchString); 
			}
			
			string strGroupName = mCtx.GetString(Resource.String.search_results) + " (\"" + sp.SearchString + "\")";
			PwGroup pgResults = new PwGroup(true, true, strGroupName, PwIcon.EMailSearch);
			pgResults.IsVirtual = true;
			
			PwObjectList<PwEntry> listResults = pgResults.Entries;
			
			
			database.root.SearchEntries(sp, listResults, resultContexts, new NullStatusLogger());
			
			
			return pgResults;
			

		}

		
		public PwGroup searchForExactUrl (Database database, string url)
		{
			SearchParameters sp = SearchParameters.None;
			sp.SearchInUrls = true;
			sp.SearchString = url;
		
			if(sp.RegularExpression) // Validate regular expression
			{
				new Regex(sp.SearchString); 
			}
			
			string strGroupName = mCtx.GetString(Resource.String.search_results) + " (\"" + sp.SearchString + "\")";
			PwGroup pgResults = new PwGroup(true, true, strGroupName, PwIcon.EMailSearch);
			pgResults.IsVirtual = true;
			
			PwObjectList<PwEntry> listResults = pgResults.Entries;
			
			
			database.root.SearchEntries(sp, listResults, new NullStatusLogger());
			
			
			return pgResults;
			
		}

		private String extractHost(String url)
		{
			return UrlUtil.GetHost(url.Trim());
		}

		public PwGroup searchForHost(Database database, String url, bool allowSubdomains)
		{
			String host = extractHost(url);
			string strGroupName = mCtx.GetString(Resource.String.search_results) + " (\"" + host + "\")";
			PwGroup pgResults = new PwGroup(true, true, strGroupName, PwIcon.EMailSearch);
			pgResults.IsVirtual = true;
			if (String.IsNullOrWhiteSpace(host))
				return pgResults;
			foreach (PwEntry entry in database.entries.Values)
			{
				String otherHost = extractHost(entry.Strings.ReadSafe(PwDefs.UrlField));
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

