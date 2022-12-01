/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2021 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

using KeePassLib.Collections;
using KeePassLib.Delegates;
using KeePassLib.Interfaces;
using KeePassLib.Security;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace KeePassLib
{
    public sealed partial class PwGroup
    {
        private const int SearchContextStringMaxLength = 50; // Note, doesn't include elipsis, if added
        public const string SearchContextUuid = "Uuid";
        public const string SearchContextParentGroup = "Parent Group";
        public const string SearchContextTags = "Tags";

        /// <summary>
        /// Search this group and all subgroups for entries.
        /// </summary>
        /// <param name="sp">Specifies the search method.</param>
        /// <param name="listStorage">Entry list in which the search results will
        /// be stored.</param>
        public void SearchEntries(SearchParameters sp, PwObjectList<PwEntry> listStorage)
        {
            SearchEntries(sp, listStorage, null);
        }

        /// <summary>
        /// Search this group and all subgroups for entries.
        /// </summary>
        /// <param name="sp">Specifies the search method.</param>
        /// <param name="listStorage">Entry list in which the search results will
        /// be stored.</param>
        /// <param name="slStatus">Optional status reporting object.</param>
        public void SearchEntries(SearchParameters sp, PwObjectList<PwEntry> listStorage,
            IStatusLogger slStatus)
        {
            SearchEntries(sp, listStorage, null, slStatus);
        }

        /// <summary>
        /// Search this group and all subgroups for entries.
        /// </summary>
        /// <param name="sp">Specifies the search method.</param>
        /// <param name="listStorage">Entry list in which the search results will
        /// be stored.</param>
        /// <param name="resultContexts">Dictionary that will be populated with text fragments indicating the context of why each entry (keyed by Uuid) was returned</param>
        public void SearchEntries(SearchParameters sp, PwObjectList<PwEntry> listStorage,
            IDictionary<PwUuid, KeyValuePair<string, string>> resultContexts,
            IStatusLogger slStatus)
        {

            if (sp == null)
            {
                Debug.Assert(false);
                return;
            }

            if (listStorage == null)
            {
                Debug.Assert(false);
                return;
            }

            ulong uCurEntries = 0, uTotalEntries = 0;

            List<string> lTerms = StrUtil.SplitSearchTerms(sp.SearchString);
            if ((lTerms.Count <= 1) || sp.RegularExpression)
            {
                if (slStatus != null) uTotalEntries = GetEntriesCount(true);
                SearchEntriesSingle(sp, listStorage, resultContexts, slStatus, ref uCurEntries,
                    uTotalEntries);
                return;
            }

            // Search longer strings first (for improved performance)
            lTerms.Sort(StrUtil.CompareLengthGt);

            string strFullSearch = sp.SearchString; // Backup

            PwGroup pg = this;
            for (int iTerm = 0; iTerm < lTerms.Count; ++iTerm)
            {
                // Update counters for a better state guess
                if (slStatus != null)
                {
                    ulong uRemRounds = (ulong) (lTerms.Count - iTerm);
                    uTotalEntries = uCurEntries + (uRemRounds *
                                                   pg.GetEntriesCount(true));
                }

                PwGroup pgNew = new PwGroup();

                sp.SearchString = lTerms[iTerm];

                bool bNegate = false;
                if (sp.SearchString.StartsWith("-"))
                {
                    sp.SearchString = sp.SearchString.Substring(1);
                    bNegate = (sp.SearchString.Length > 0);
                }

                if (!pg.SearchEntriesSingle(sp, pgNew.Entries, resultContexts, slStatus,
                    ref uCurEntries, uTotalEntries))
                {
                    pg = null;
                    break;
                }

                if (bNegate)
                {
                    PwObjectList<PwEntry> lCand = pg.GetEntries(true);

                    pg = new PwGroup();
                    foreach (PwEntry peCand in lCand)
                    {
                        if (pgNew.Entries.IndexOf(peCand) < 0) pg.Entries.Add(peCand);
                    }
                }
                else pg = pgNew;
            }

            if (pg != null) listStorage.Add(pg.Entries);
            sp.SearchString = strFullSearch; // Restore
        }

        private bool SearchEntriesSingle(SearchParameters spIn,
            PwObjectList<PwEntry> listStorage, IDictionary<PwUuid, KeyValuePair<string, string>> resultContexts,
            IStatusLogger slStatus,
            ref ulong uCurEntries, ulong uTotalEntries)
        {
            SearchParameters sp = spIn.Clone();
            if (sp.SearchString == null)
            {
                Debug.Assert(false);
                return true;
            }

            sp.SearchString = sp.SearchString.Trim();

            bool bTitle = sp.SearchInTitles;
            bool bUserName = sp.SearchInUserNames;
            bool bPassword = sp.SearchInPasswords;
            bool bUrl = sp.SearchInUrls;
            bool bNotes = sp.SearchInNotes;
            bool bOther = sp.SearchInOther;
            bool bUuids = sp.SearchInUuids;
            bool bGroupName = sp.SearchInGroupNames;
            bool bTags = sp.SearchInTags;
            bool bExcludeExpired = sp.ExcludeExpired;
            bool bRespectEntrySearchingDisabled = sp.RespectEntrySearchingDisabled;

            DateTime dtNow = DateTime.Now;

            Regex rx = null;
            if (sp.RegularExpression)
            {
                RegexOptions ro = RegexOptions.None; // RegexOptions.Compiled
                if ((sp.ComparisonMode == StringComparison.CurrentCultureIgnoreCase) ||
#if !KeePassUAP
                    (sp.ComparisonMode == StringComparison.InvariantCultureIgnoreCase) ||
#endif
                    (sp.ComparisonMode == StringComparison.OrdinalIgnoreCase))
                {
                    ro |= RegexOptions.IgnoreCase;
                }

                rx = new Regex(sp.SearchString, ro);
            }

            ulong uLocalCurEntries = uCurEntries;

            EntryHandler eh = null;
            if (sp.SearchString.Length <= 0) // Report all
            {
                eh = delegate(PwEntry pe)
                {
                    if (slStatus != null)
                    {
                        if (!slStatus.SetProgress((uint) ((uLocalCurEntries *
                                                           100UL) / uTotalEntries))) return false;
                        ++uLocalCurEntries;
                    }

                    if (bRespectEntrySearchingDisabled && !pe.GetSearchingEnabled())
                        return true; // Skip
                    if (bExcludeExpired && pe.Expires && (dtNow > pe.ExpiryTime))
                        return true; // Skip

                    listStorage.Add(pe);
                    return true;
                };
            }
            else
            {
                eh = delegate(PwEntry pe)
                {
                    if (slStatus != null)
                    {
                        if (!slStatus.SetProgress((uint) ((uLocalCurEntries *
                                                           100UL) / uTotalEntries))) return false;
                        ++uLocalCurEntries;
                    }

                    if (bRespectEntrySearchingDisabled && !pe.GetSearchingEnabled())
                        return true; // Skip
                    if (bExcludeExpired && pe.Expires && (dtNow > pe.ExpiryTime))
                        return true; // Skip

                    uint uInitialResults = listStorage.UCount;

                    foreach (KeyValuePair<string, ProtectedString> kvp in pe.Strings)
                    {
                        string strKey = kvp.Key;

                        if (strKey == PwDefs.TitleField)
                        {
                            if (bTitle)
                                SearchEvalAdd(sp, kvp.Value.ReadString(),
                                    rx, pe, listStorage, resultContexts, strKey);
                        }
                        else if (strKey == PwDefs.UserNameField)
                        {
                            if (bUserName)
                                SearchEvalAdd(sp, kvp.Value.ReadString(),
                                    rx, pe, listStorage, resultContexts, strKey);
                        }
                        else if (strKey == PwDefs.PasswordField)
                        {
                            if (bPassword)
                                SearchEvalAdd(sp, kvp.Value.ReadString(),
                                    rx, pe, listStorage, resultContexts, strKey);
                        }
                        else if (strKey == PwDefs.UrlField)
                        {
                            if (bUrl)
                                SearchEvalAdd(sp, kvp.Value.ReadString(),
                                    rx, pe, listStorage, resultContexts, strKey);
                        }
                        else if (strKey == PwDefs.NotesField)
                        {
                            if (bNotes)
                                SearchEvalAdd(sp, kvp.Value.ReadString(),
                                    rx, pe, listStorage, resultContexts, strKey);
                        }
                        else if (bOther)
                            SearchEvalAdd(sp, kvp.Value.ReadString(),
                                rx, pe, listStorage, resultContexts, strKey);

                        // An entry can match only once => break if we have added it
                        if (listStorage.UCount > uInitialResults) break;
                    }

                    if (bUuids && (listStorage.UCount == uInitialResults))
                        SearchEvalAdd(sp, pe.Uuid.ToHexString(), rx, pe, listStorage, resultContexts,
                            SearchContextTags);

                    if (bGroupName && (listStorage.UCount == uInitialResults) &&
                        (pe.ParentGroup != null))
                        SearchEvalAdd(sp, pe.ParentGroup.Name, rx, pe, listStorage, resultContexts,
                            SearchContextParentGroup);

                    if (bTags)
                    {
                        foreach (string strTag in pe.Tags)
                        {
                            if (listStorage.UCount != uInitialResults) break; // Match

                            SearchEvalAdd(sp, strTag, rx, pe, listStorage, resultContexts, SearchContextTags);
                        }
                    }

                    return true;
                };
            }

            if (!PreOrderTraverseTree(null, eh)) return false;
            uCurEntries = uLocalCurEntries;
            return true;
        }

        private static void SearchEvalAdd(SearchParameters sp, string strDataField,
            Regex rx, PwEntry pe, PwObjectList<PwEntry> lResults,
            IDictionary<PwUuid, KeyValuePair<string, string>> resultContexts, string contextFieldName)
        {
            bool bMatch = false;
            int matchPos;
            if (rx == null)
            {
                matchPos = strDataField.IndexOf(sp.SearchString, sp.ComparisonMode);
                bMatch = matchPos >= 0;
            }
            else
            {
                var match = rx.Match(strDataField);
                bMatch = match.Success;
                matchPos = match.Index;
            }

            if (!bMatch && (sp.DataTransformationFn != null))
            {
                string strCmp = sp.DataTransformationFn(strDataField, pe);
                if (!object.ReferenceEquals(strCmp, strDataField))
                {
                    if (rx == null)
                    {
                        matchPos = strCmp.IndexOf(sp.SearchString, sp.ComparisonMode);
                        bMatch = matchPos >= 0;
                    }
                    else
                    {
                        var match = rx.Match(strCmp);
                        bMatch = match.Success;
                        matchPos = match.Index;
                    }
                }
            }

            if (bMatch)
            {
                lResults.Add(pe);

                if (resultContexts != null)
                {
                    // Trim the value if necessary
                    var contextString = strDataField;
                    if (contextString.Length > SearchContextStringMaxLength)
                    {
                        // Start 10% before actual data, and don't run over
                        var startPos = Math.Max(0,
                            Math.Min(matchPos - (SearchContextStringMaxLength / 10),
                                contextString.Length - SearchContextStringMaxLength));
                        contextString = "… " + contextString.Substring(startPos, SearchContextStringMaxLength) +
                                        ((startPos + SearchContextStringMaxLength < contextString.Length)
                                            ? " …"
                                            : null);
                    }

                    resultContexts[pe.Uuid] = new KeyValuePair<string, string>(contextFieldName, contextString);
                }
            }
        }

    }
}
