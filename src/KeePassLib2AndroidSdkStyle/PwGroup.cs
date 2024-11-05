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

using KeePassLib.Collections;
using KeePassLib.Delegates;
using KeePassLib.Interfaces;
using KeePassLib.Resources;
using KeePassLib.Utility;

namespace KeePassLib
{
	/// <summary>
	/// A group containing subgroups and entries.
	/// </summary>
	public sealed partial class PwGroup : ITimeLogger, IStructureItem, IDeepCloneable<PwGroup>
	{
		public const bool DefaultAutoTypeEnabled = true;
		public const bool DefaultSearchingEnabled = true;

		// In the tree view of Windows 10, the X coordinate is reset
		// to 0 after 256 nested nodes
		private const uint MaxDepth = 126; // Depth 126 = level 127 < 256/2

		private PwUuid m_uuid = PwUuid.Zero;
		private PwGroup m_pParentGroup = null;
		private DateTime m_tParentGroupLastMod = PwDefs.DtDefaultNow;
		private PwUuid m_puPrevParentGroup = PwUuid.Zero;

		private PwObjectList<PwGroup> m_listGroups = new PwObjectList<PwGroup>();
		private PwObjectList<PwEntry> m_listEntries = new PwObjectList<PwEntry>();

		private string m_strName = string.Empty;
		private string m_strNotes = string.Empty;

		private PwIcon m_pwIcon = PwIcon.Folder;
		private PwUuid m_pwCustomIconID = PwUuid.Zero;

		private DateTime m_tCreation = PwDefs.DtDefaultNow;
		private DateTime m_tLastMod = PwDefs.DtDefaultNow;
		private DateTime m_tLastAccess = PwDefs.DtDefaultNow;
		private DateTime m_tExpire = PwDefs.DtDefaultNow;
		private bool m_bExpires = false;
		private ulong m_uUsageCount = 0;

		private bool m_bIsExpanded = true;
		private bool m_bVirtual = false;

		private string m_strDefaultAutoTypeSequence = string.Empty;

		private bool? m_bEnableAutoType = null;
		private bool? m_bEnableSearching = null;

		private PwUuid m_pwLastTopVisibleEntry = PwUuid.Zero;

		private List<string> m_lTags = new List<string>();

		private StringDictionaryEx m_dCustomData = new StringDictionaryEx();

		/// <summary>
		/// UUID of this group.
		/// </summary>
		public PwUuid Uuid
		{
			get { return m_uuid; }
			set
			{
				if (value == null) { Debug.Assert(false); throw new ArgumentNullException("value"); }
				m_uuid = value;
			}
		}

		/// <summary>
		/// Reference to the group to which this group belongs. May be <c>null</c>.
		/// </summary>
		public PwGroup ParentGroup
		{
			get { return m_pParentGroup; }

			// Plugins: use the PwGroup.AddGroup method instead.
			// Internal: check depth using CanAddGroup/CheckCanAddGroup.
			internal set { Debug.Assert(value != this); m_pParentGroup = value; }
		}

		/// <summary>
		/// The date/time when the location of the object was last changed.
		/// </summary>
		public DateTime LocationChanged
		{
			get { return m_tParentGroupLastMod; }
			set { m_tParentGroupLastMod = value; }
		}

		public PwUuid PreviousParentGroup
		{
			get { return m_puPrevParentGroup; }
			set
			{
				if (value == null) { Debug.Assert(false); throw new ArgumentNullException("value"); }
				m_puPrevParentGroup = value;
			}
		}

		/// <summary>
		/// The name of this group. Cannot be <c>null</c>.
		/// </summary>
		public string Name
		{
			get { return m_strName; }
			set
			{
				if (value == null) { Debug.Assert(false); throw new ArgumentNullException("value"); }
				m_strName = value;
			}
		}

		/// <summary>
		/// Comments about this group. Cannot be <c>null</c>.
		/// </summary>
		public string Notes
		{
			get { return m_strNotes; }
			set
			{
				if (value == null) { Debug.Assert(false); throw new ArgumentNullException("value"); }
				m_strNotes = value;
			}
		}

		/// <summary>
		/// Icon of the group.
		/// </summary>
		public PwIcon IconId
		{
			get { return m_pwIcon; }
			set { m_pwIcon = value; }
		}

		/// <summary>
		/// Get the custom icon ID. This value is 0, if no custom icon is
		/// being used (i.e. the icon specified by the <c>IconID</c> property
		/// should be displayed).
		/// </summary>
		public PwUuid CustomIconUuid
		{
			get { return m_pwCustomIconID; }
			set
			{
				if (value == null) { Debug.Assert(false); throw new ArgumentNullException("value"); }
				m_pwCustomIconID = value;
			}
		}

		/// <summary>
		/// A flag that specifies if the group is shown as expanded or
		/// collapsed in the user interface.
		/// </summary>
		public bool IsExpanded
		{
			get { return m_bIsExpanded; }
			set { m_bIsExpanded = value; }
		}

		/// <summary>
		/// The date/time when this group was created.
		/// </summary>
		public DateTime CreationTime
		{
			get { return m_tCreation; }
			set { m_tCreation = value; }
		}

		/// <summary>
		/// The date/time when this group was last modified.
		/// </summary>
		public DateTime LastModificationTime
		{
			get { return m_tLastMod; }
			set { m_tLastMod = value; }
		}

		/// <summary>
		/// The date/time when this group was last accessed (read).
		/// </summary>
		public DateTime LastAccessTime
		{
			get { return m_tLastAccess; }
			set { m_tLastAccess = value; }
		}

		/// <summary>
		/// The date/time when this group expires.
		/// </summary>
		public DateTime ExpiryTime
		{
			get { return m_tExpire; }
			set { m_tExpire = value; }
		}

		/// <summary>
		/// Flag that determines if the group expires.
		/// </summary>
		public bool Expires
		{
			get { return m_bExpires; }
			set { m_bExpires = value; }
		}

		/// <summary>
		/// Get or set the usage count of the group. To increase the usage
		/// count by one, use the <c>Touch</c> function.
		/// </summary>
		public ulong UsageCount
		{
			get { return m_uUsageCount; }
			set { m_uUsageCount = value; }
		}

		/// <summary>
		/// Get a list of subgroups in this group.
		/// </summary>
		public PwObjectList<PwGroup> Groups
		{
			get { return m_listGroups; }
		}

		/// <summary>
		/// Get a list of entries in this group.
		/// </summary>
		public PwObjectList<PwEntry> Entries
		{
			get { return m_listEntries; }
		}

		/// <summary>
		/// A flag specifying whether this group is virtual or not. Virtual
		/// groups can contain links to entries stored in other groups.
		/// Note that this flag has to be interpreted and set by the calling
		/// code; it won't prevent you from accessing and modifying the list
		/// of entries in this group in any way.
		/// </summary>
		public bool IsVirtual
		{
			get { return m_bVirtual; }
			set { m_bVirtual = value; }
		}

		/// <summary>
		/// Default auto-type keystroke sequence for all entries in
		/// this group. This property can be an empty string, which
		/// means that the value should be inherited from the parent.
		/// </summary>
		public string DefaultAutoTypeSequence
		{
			get { return m_strDefaultAutoTypeSequence; }
			set
			{
				if (value == null) { Debug.Assert(false); throw new ArgumentNullException("value"); }
				m_strDefaultAutoTypeSequence = value;
			}
		}

		public bool? EnableAutoType
		{
			get { return m_bEnableAutoType; }
			set { m_bEnableAutoType = value; }
		}

		public bool? EnableSearching
		{
			get { return m_bEnableSearching; }
			set { m_bEnableSearching = value; }
		}

		public PwUuid LastTopVisibleEntry
		{
			get { return m_pwLastTopVisibleEntry; }
			set
			{
				if (value == null) { Debug.Assert(false); throw new ArgumentNullException("value"); }
				m_pwLastTopVisibleEntry = value;
			}
		}

		public List<string> Tags
		{
			get { StrUtil.NormalizeTags(m_lTags); return m_lTags; }
			set
			{
				if (value == null) { Debug.Assert(false); throw new ArgumentNullException("value"); }
				m_lTags = value;
			}
		}

		/// <summary>
		/// Custom data container that can be used by plugins to store
		/// own data in KeePass groups.
		/// The data is stored in the encrypted part of encrypted
		/// database files.
		/// Use unique names for your items, e.g. "PluginName_ItemName".
		/// </summary>
		public StringDictionaryEx CustomData
		{
			get { return m_dCustomData; }
			internal set
			{
				if (value == null) { Debug.Assert(false); throw new ArgumentNullException("value"); }
				m_dCustomData = value;
			}
		}

		public static EventHandler<ObjectTouchedEventArgs> GroupTouched;
		public EventHandler<ObjectTouchedEventArgs> Touched;

		/// <summary>
		/// Construct a new, empty group.
		/// </summary>
		public PwGroup()
		{
		}

		/// <summary>
		/// Construct a new, empty group.
		/// </summary>
		/// <param name="bCreateNewUuid">Create a new UUID for this group.</param>
		/// <param name="bSetTimes">Set creation, last access and last modification times to the current time.</param>
		public PwGroup(bool bCreateNewUuid, bool bSetTimes)
		{
			if (bCreateNewUuid) m_uuid = new PwUuid(true);

			if (bSetTimes)
			{
				DateTime dtNow = DateTime.UtcNow;
				m_tCreation = dtNow;
				m_tLastMod = dtNow;
				m_tLastAccess = dtNow;
				m_tParentGroupLastMod = dtNow;
			}
		}

		/// <summary>
		/// Construct a new group.
		/// </summary>
		/// <param name="bCreateNewUuid">Create a new UUID for this group.</param>
		/// <param name="bSetTimes">Set creation, last access and last modification times to the current time.</param>
		/// <param name="strName">Name of the new group.</param>
		/// <param name="pwIcon">Icon of the new group.</param>
		public PwGroup(bool bCreateNewUuid, bool bSetTimes, string strName, PwIcon pwIcon)
		{
			if (bCreateNewUuid) m_uuid = new PwUuid(true);

			if (bSetTimes)
			{
				DateTime dtNow = DateTime.UtcNow;
				m_tCreation = dtNow;
				m_tLastMod = dtNow;
				m_tLastAccess = dtNow;
				m_tParentGroupLastMod = dtNow;
			}

			if (strName != null) m_strName = strName;

			m_pwIcon = pwIcon;
		}

#if DEBUG
		// For display in debugger
		public override string ToString()
		{
			return (@"PwGroup '" + m_strName + @"'");
		}
#endif

		/// <summary>
		/// Deeply clone the current group. The returned group will be an exact
		/// value copy of the current object (including UUID, etc.).
		/// </summary>
		/// <returns>Exact value copy of the current <c>PwGroup</c> object.</returns>
		public PwGroup CloneDeep()
		{
			PwGroup pg = new PwGroup(false, false);

			pg.m_uuid = m_uuid; // PwUuid is immutable

			pg.m_listGroups = m_listGroups.CloneDeep();
			pg.m_listEntries = m_listEntries.CloneDeep();
			pg.TakeOwnership(true, true, false);

			pg.m_pParentGroup = m_pParentGroup;
			pg.m_tParentGroupLastMod = m_tParentGroupLastMod;
			pg.m_puPrevParentGroup = m_puPrevParentGroup;

			pg.m_strName = m_strName;
			pg.m_strNotes = m_strNotes;

			pg.m_pwIcon = m_pwIcon;
			pg.m_pwCustomIconID = m_pwCustomIconID;

			pg.m_tCreation = m_tCreation;
			pg.m_tLastMod = m_tLastMod;
			pg.m_tLastAccess = m_tLastAccess;
			pg.m_tExpire = m_tExpire;
			pg.m_bExpires = m_bExpires;
			pg.m_uUsageCount = m_uUsageCount;

			pg.m_bIsExpanded = m_bIsExpanded;
			pg.m_bVirtual = m_bVirtual;

			pg.m_strDefaultAutoTypeSequence = m_strDefaultAutoTypeSequence;

			pg.m_bEnableAutoType = m_bEnableAutoType;
			pg.m_bEnableSearching = m_bEnableSearching;

			pg.m_pwLastTopVisibleEntry = m_pwLastTopVisibleEntry;

			pg.m_lTags.AddRange(m_lTags);

			pg.m_dCustomData = m_dCustomData.CloneDeep();

			return pg;
		}

		public PwGroup CloneStructure()
		{
			PwGroup pg = new PwGroup(false, false);

			pg.m_uuid = m_uuid; // PwUuid is immutable
			pg.m_tParentGroupLastMod = m_tParentGroupLastMod;
			// Do not assign m_pParentGroup

			foreach (PwGroup pgSub in m_listGroups)
				pg.AddGroup(pgSub.CloneStructure(), true);

			foreach (PwEntry peSub in m_listEntries)
				pg.AddEntry(peSub.CloneStructure(), true);

			return pg;
		}

		public bool EqualsGroup(PwGroup pg, PwCompareOptions pwOpt,
			MemProtCmpMode mpCmpStr)
		{
			if (pg == null) { Debug.Assert(false); return false; }

			bool bIgnoreLastAccess = ((pwOpt & PwCompareOptions.IgnoreLastAccess) !=
				PwCompareOptions.None);
			bool bIgnoreLastMod = ((pwOpt & PwCompareOptions.IgnoreLastMod) !=
				PwCompareOptions.None);

			if (!m_uuid.Equals(pg.m_uuid)) return false;
			if ((pwOpt & PwCompareOptions.IgnoreParentGroup) == PwCompareOptions.None)
			{
				if (m_pParentGroup != pg.m_pParentGroup) return false;
				if (!bIgnoreLastMod && (m_tParentGroupLastMod != pg.m_tParentGroupLastMod))
					return false;
				if (!m_puPrevParentGroup.Equals(pg.m_puPrevParentGroup))
					return false;
			}

			if (m_strName != pg.m_strName) return false;
			if (m_strNotes != pg.m_strNotes) return false;

			if (m_pwIcon != pg.m_pwIcon) return false;
			if (!m_pwCustomIconID.Equals(pg.m_pwCustomIconID)) return false;

			if (m_tCreation != pg.m_tCreation) return false;
			if (!bIgnoreLastMod && (m_tLastMod != pg.m_tLastMod)) return false;
			if (!bIgnoreLastAccess && (m_tLastAccess != pg.m_tLastAccess)) return false;
			if (m_tExpire != pg.m_tExpire) return false;
			if (m_bExpires != pg.m_bExpires) return false;
			if (!bIgnoreLastAccess && (m_uUsageCount != pg.m_uUsageCount)) return false;

			// if(m_bIsExpanded != pg.m_bIsExpanded) return false;

			if (m_strDefaultAutoTypeSequence != pg.m_strDefaultAutoTypeSequence) return false;

			if (m_bEnableAutoType.HasValue != pg.m_bEnableAutoType.HasValue) return false;
			if (m_bEnableAutoType.HasValue)
			{
				if (m_bEnableAutoType.Value != pg.m_bEnableAutoType.Value) return false;
			}
			if (m_bEnableSearching.HasValue != pg.m_bEnableSearching.HasValue) return false;
			if (m_bEnableSearching.HasValue)
			{
				if (m_bEnableSearching.Value != pg.m_bEnableSearching.Value) return false;
			}

			if (!m_pwLastTopVisibleEntry.Equals(pg.m_pwLastTopVisibleEntry)) return false;

			// The Tags property normalizes
			if (!MemUtil.ListsEqual<string>(this.Tags, pg.Tags)) return false;

			if (!m_dCustomData.Equals(pg.m_dCustomData)) return false;

			if ((pwOpt & PwCompareOptions.PropertiesOnly) == PwCompareOptions.None)
			{
				if (m_listEntries.UCount != pg.m_listEntries.UCount) return false;
				for (uint u = 0; u < m_listEntries.UCount; ++u)
				{
					PwEntry peA = m_listEntries.GetAt(u);
					PwEntry peB = pg.m_listEntries.GetAt(u);
					if (!peA.EqualsEntry(peB, pwOpt, mpCmpStr)) return false;
				}

				if (m_listGroups.UCount != pg.m_listGroups.UCount) return false;
				for (uint u = 0; u < m_listGroups.UCount; ++u)
				{
					PwGroup pgA = m_listGroups.GetAt(u);
					PwGroup pgB = pg.m_listGroups.GetAt(u);
					if (!pgA.EqualsGroup(pgB, pwOpt, mpCmpStr)) return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Assign properties to the current group based on a template group.
		/// </summary>
		/// <param name="pgTemplate">Template group. Must not be <c>null</c>.</param>
		/// <param name="bOnlyIfNewer">Only set the properties of the template group
		/// if it is newer than the current one.</param>
		/// <param name="bAssignLocationChanged">If <c>true</c>, the
		/// <c>LocationChanged</c> property is copied, otherwise not.</param>
		public void AssignProperties(PwGroup pgTemplate, bool bOnlyIfNewer,
			bool bAssignLocationChanged)
		{
			Debug.Assert(pgTemplate != null); if (pgTemplate == null) throw new ArgumentNullException("pgTemplate");

			if (bOnlyIfNewer && (TimeUtil.Compare(pgTemplate.m_tLastMod, m_tLastMod,
				true) < 0))
				return;

			// Template UUID should be the same as the current one
			Debug.Assert(m_uuid.Equals(pgTemplate.m_uuid));
			m_uuid = pgTemplate.m_uuid;

			if (bAssignLocationChanged)
			{
				m_tParentGroupLastMod = pgTemplate.m_tParentGroupLastMod;
				m_puPrevParentGroup = pgTemplate.m_puPrevParentGroup;
			}

			m_strName = pgTemplate.m_strName;
			m_strNotes = pgTemplate.m_strNotes;

			m_pwIcon = pgTemplate.m_pwIcon;
			m_pwCustomIconID = pgTemplate.m_pwCustomIconID;

			m_tCreation = pgTemplate.m_tCreation;
			m_tLastMod = pgTemplate.m_tLastMod;
			m_tLastAccess = pgTemplate.m_tLastAccess;
			m_tExpire = pgTemplate.m_tExpire;
			m_bExpires = pgTemplate.m_bExpires;
			m_uUsageCount = pgTemplate.m_uUsageCount;

			m_strDefaultAutoTypeSequence = pgTemplate.m_strDefaultAutoTypeSequence;

			m_bEnableAutoType = pgTemplate.m_bEnableAutoType;
			m_bEnableSearching = pgTemplate.m_bEnableSearching;

			m_pwLastTopVisibleEntry = pgTemplate.m_pwLastTopVisibleEntry;

			m_lTags = new List<string>(pgTemplate.m_lTags);

			m_dCustomData = pgTemplate.m_dCustomData.CloneDeep();
		}

		/// <summary>
		/// Touch the group. This function updates the internal last access
		/// time. If the <paramref name="bModified" /> parameter is <c>true</c>,
		/// the last modification time gets updated, too.
		/// </summary>
		/// <param name="bModified">Modify last modification time.</param>
		public void Touch(bool bModified)
		{
			Touch(bModified, true);
		}

		/// <summary>
		/// Touch the group. This function updates the internal last access
		/// time. If the <paramref name="bModified" /> parameter is <c>true</c>,
		/// the last modification time gets updated, too.
		/// </summary>
		/// <param name="bModified">Modify last modification time.</param>
		/// <param name="bTouchParents">If <c>true</c>, all parent objects
		/// get touched, too.</param>
		public void Touch(bool bModified, bool bTouchParents)
		{
			m_tLastAccess = DateTime.UtcNow;
			++m_uUsageCount;

			if (bModified) m_tLastMod = m_tLastAccess;

			if (this.Touched != null)
				this.Touched(this, new ObjectTouchedEventArgs(this,
					bModified, bTouchParents));
			if (PwGroup.GroupTouched != null)
				PwGroup.GroupTouched(this, new ObjectTouchedEventArgs(this,
					bModified, bTouchParents));

			if (bTouchParents && (m_pParentGroup != null))
				m_pParentGroup.Touch(bModified, true);
		}

		/// <summary>
		/// Get number of groups and entries in the current group. This function
		/// can also traverse through all subgroups and accumulate their counts
		/// (recursive mode).
		/// </summary>
		/// <param name="bRecursive">If this parameter is <c>true</c>, all
		/// subgroups and entries in subgroups will be counted and added to
		/// the returned value. If it is <c>false</c>, only the number of
		/// subgroups and entries of the current group is returned.</param>
		/// <param name="uNumGroups">Number of subgroups.</param>
		/// <param name="uNumEntries">Number of entries.</param>
		public void GetCounts(bool bRecursive, out uint uNumGroups, out uint uNumEntries)
		{
			if (bRecursive)
			{
				uint uTotalGroups = m_listGroups.UCount;
				uint uTotalEntries = m_listEntries.UCount;
				uint uSubGroupCount, uSubEntryCount;

				foreach (PwGroup pg in m_listGroups)
				{
					pg.GetCounts(true, out uSubGroupCount, out uSubEntryCount);

					uTotalGroups += uSubGroupCount;
					uTotalEntries += uSubEntryCount;
				}

				uNumGroups = uTotalGroups;
				uNumEntries = uTotalEntries;
			}
			else // !bRecursive
			{
				uNumGroups = m_listGroups.UCount;
				uNumEntries = m_listEntries.UCount;
			}
		}

		public uint GetEntriesCount(bool bRecursive)
		{
			uint uGroups, uEntries;
			GetCounts(bRecursive, out uGroups, out uEntries);
			return uEntries;
		}

		/// <summary>
		/// Traverse the group/entry tree in the current group. Various traversal
		/// methods are available.
		/// </summary>
		/// <param name="tm">Specifies the traversal method.</param>
		/// <param name="groupHandler">Function that performs an action on
		/// the currently visited group (see <c>GroupHandler</c> for more).
		/// This parameter may be <c>null</c>, in this case the tree is traversed but
		/// you don't get notifications for each visited group.</param>
		/// <param name="entryHandler">Function that performs an action on
		/// the currently visited entry (see <c>EntryHandler</c> for more).
		/// This parameter may be <c>null</c>.</param>
		/// <returns>Returns <c>true</c> if all entries and groups have been
		/// traversed. If the traversal has been canceled by one of the two
		/// handlers, the return value is <c>false</c>.</returns>
		public bool TraverseTree(TraversalMethod tm, GroupHandler groupHandler, EntryHandler entryHandler)
		{
			bool bRet = false;

			switch (tm)
			{
				case TraversalMethod.None:
					bRet = true;
					break;
				case TraversalMethod.PreOrder:
					bRet = PreOrderTraverseTree(groupHandler, entryHandler);
					break;
				default:
					Debug.Assert(false);
					break;
			}

			return bRet;
		}

		private bool PreOrderTraverseTree(GroupHandler groupHandler, EntryHandler entryHandler)
		{
			if (entryHandler != null)
			{
				foreach (PwEntry pe in m_listEntries)
				{
					if (!entryHandler(pe)) return false;
				}
			}

			foreach (PwGroup pg in m_listGroups)
			{
				if (groupHandler != null)
				{
					if (!groupHandler(pg)) return false;
				}

				if (!pg.PreOrderTraverseTree(groupHandler, entryHandler))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Pack all groups into one flat linked list of references (recursively).
		/// </summary>
		/// <returns>Flat list of all groups.</returns>
		public LinkedList<PwGroup> GetFlatGroupList()
		{
			LinkedList<PwGroup> list = new LinkedList<PwGroup>();

			foreach (PwGroup pg in m_listGroups)
			{
				list.AddLast(pg);

				if (pg.Groups.UCount != 0)
					LinearizeGroupRecursive(list, pg, 1);
			}

			return list;
		}

		private void LinearizeGroupRecursive(LinkedList<PwGroup> list, PwGroup pg, ushort uLevel)
		{
			Debug.Assert(pg != null); if (pg == null) return;

			foreach (PwGroup pwg in pg.Groups)
			{
				list.AddLast(pwg);

				if (pwg.Groups.UCount != 0)
					LinearizeGroupRecursive(list, pwg, (ushort)(uLevel + 1));
			}
		}

		/// <summary>
		/// Pack all entries into one flat linked list of references. Temporary
		/// group IDs are assigned automatically.
		/// </summary>
		/// <param name="flatGroupList">A flat group list created by
		/// <c>GetFlatGroupList</c>.</param>
		/// <returns>Flat list of all entries.</returns>
		public static LinkedList<PwEntry> GetFlatEntryList(LinkedList<PwGroup> flatGroupList)
		{
			Debug.Assert(flatGroupList != null); if (flatGroupList == null) return null;

			LinkedList<PwEntry> list = new LinkedList<PwEntry>();
			foreach (PwGroup pg in flatGroupList)
			{
				foreach (PwEntry pe in pg.Entries)
					list.AddLast(pe);
			}

			return list;
		}

		/// <summary>
		/// Enable protection of a specific string field type.
		/// </summary>
		/// <param name="strFieldName">Name of the string field to protect or unprotect.</param>
		/// <param name="bEnable">Enable protection or not.</param>
		/// <returns>Returns <c>true</c>, if the operation completed successfully,
		/// otherwise <c>false</c>.</returns>
		public bool EnableStringFieldProtection(string strFieldName, bool bEnable)
		{
			Debug.Assert(strFieldName != null);

			EntryHandler eh = delegate (PwEntry pe)
			{
				// Enable protection of current string
				pe.Strings.EnableProtection(strFieldName, bEnable);

				// Do the same for all history items
				foreach (PwEntry peHistory in pe.History)
				{
					peHistory.Strings.EnableProtection(strFieldName, bEnable);
				}

				return true;
			};

			return PreOrderTraverseTree(null, eh);
		}

		internal List<string> GetTagsInherited(bool bNormalize)
		{
			List<string> l = new List<string>();

			PwGroup pg = this;
			while (pg != null)
			{
				l.AddRange(pg.Tags);
				pg = pg.m_pParentGroup;
			}

			if (bNormalize) StrUtil.NormalizeTags(l);
			return l;
		}

		public List<string> BuildEntryTagsList()
		{
			return BuildEntryTagsList(false, false);
		}

		public List<string> BuildEntryTagsList(bool bSort)
		{
			return BuildEntryTagsList(bSort, false);
		}

		internal List<string> BuildEntryTagsList(bool bSort, bool bGroupTags)
		{
			Dictionary<string, bool> d = new Dictionary<string, bool>();

			GroupHandler gh = null;
			if (bGroupTags)
			{
				gh = delegate (PwGroup pg)
				{
					foreach (string strTag in pg.Tags) d[strTag] = true;
					return true;
				};
			}

			EntryHandler eh = delegate (PwEntry pe)
			{
				foreach (string strTag in pe.Tags) d[strTag] = true;
				return true;
			};

			if (gh != null) gh(this);
			TraverseTree(TraversalMethod.PreOrder, gh, eh);

			List<string> l = new List<string>(d.Keys);
			if (bSort) l.Sort(StrUtil.CompareNaturally);

			return l;
		}

#if !KeePassLibSD
		public IDictionary<string, uint> BuildEntryTagsDict(bool bSort)
		{
			Debug.Assert(!bSort); // Obsolete

			IDictionary<string, uint> d;
			if (!bSort) d = new Dictionary<string, uint>();
			else d = new SortedDictionary<string, uint>();

			GroupHandler gh = delegate (PwGroup pg)
			{
				foreach (string strTag in pg.Tags)
				{
					// For groups without entries
					if (!d.ContainsKey(strTag)) d[strTag] = 0;
				}

				return true;
			};

			EntryHandler eh = delegate (PwEntry pe)
			{
				foreach (string strTag in pe.GetTagsInherited())
				{
					uint u;
					d.TryGetValue(strTag, out u);
					d[strTag] = u + 1;
				}

				return true;
			};

			gh(this);
			TraverseTree(TraversalMethod.PreOrder, gh, eh);

			return d;
		}
#endif

		public void FindEntriesByTag(string strTag, PwObjectList<PwEntry> listStorage,
			bool bSearchRecursive)
		{
			if (strTag == null) throw new ArgumentNullException("strTag");

			strTag = StrUtil.NormalizeTag(strTag);
			if (string.IsNullOrEmpty(strTag)) return;

			EntryHandler eh = delegate (PwEntry pe)
			{
				foreach (string strEntryTag in pe.GetTagsInherited())
				{
					if (strEntryTag == strTag)
					{
						listStorage.Add(pe);
						break;
					}
				}

				return true;
			};

			if (bSearchRecursive)
				TraverseTree(TraversalMethod.PreOrder, null, eh);
			else
			{
				foreach (PwEntry pe in m_listEntries) eh(pe);
			}
		}

		/// <summary>
		/// Find a group.
		/// </summary>
		/// <param name="uuid">UUID identifying the group the caller is looking for.</param>
		/// <param name="bSearchRecursive">If <c>true</c>, the search is recursive.</param>
		/// <returns>Returns reference to found group, otherwise <c>null</c>.</returns>
		public PwGroup FindGroup(PwUuid uuid, bool bSearchRecursive)
		{
			// Do not assert on PwUuid.Zero
			if (m_uuid.Equals(uuid)) return this;

			if (bSearchRecursive)
			{
				PwGroup pgRec;
				foreach (PwGroup pg in m_listGroups)
				{
					pgRec = pg.FindGroup(uuid, true);
					if (pgRec != null) return pgRec;
				}
			}
			else // Not recursive
			{
				foreach (PwGroup pg in m_listGroups)
				{
					if (pg.m_uuid.Equals(uuid))
						return pg;
				}
			}

			return null;
		}

		/// <summary>
		/// Find an object.
		/// </summary>
		/// <param name="uuid">UUID of the object to find.</param>
		/// <param name="bRecursive">Specifies whether to search recursively.</param>
		/// <param name="bEntries">If <c>null</c>, groups and entries are
		/// searched. If <c>true</c>, only entries are searched. If <c>false</c>,
		/// only groups are searched.</param>
		/// <returns>Reference to the object, if found. Otherwise <c>null</c>.</returns>
		public IStructureItem FindObject(PwUuid uuid, bool bRecursive,
			bool? bEntries)
		{
			if (bEntries.HasValue)
			{
				if (bEntries.Value) return FindEntry(uuid, bRecursive);
				else return FindGroup(uuid, bRecursive);
			}

			PwGroup pg = FindGroup(uuid, bRecursive);
			if (pg != null) return pg;
			return FindEntry(uuid, bRecursive);
		}

		/// <summary>
		/// Try to find a subgroup and create it, if it doesn't exist yet.
		/// </summary>
		/// <param name="strName">Name of the subgroup.</param>
		/// <param name="bCreateIfNotFound">If the group isn't found: create it.</param>
		/// <returns>Returns a reference to the requested group or <c>null</c> if
		/// it doesn't exist and shouldn't be created.</returns>
		public PwGroup FindCreateGroup(string strName, bool bCreateIfNotFound)
		{
			Debug.Assert(strName != null); if (strName == null) throw new ArgumentNullException("strName");

			foreach (PwGroup pg in m_listGroups)
			{
				if (pg.Name == strName) return pg;
			}

			if (!bCreateIfNotFound) return null;

			PwGroup pgNew = new PwGroup(true, true, strName, PwIcon.Folder);
			AddGroup(pgNew, true);
			return pgNew;
		}

		/// <summary>
		/// Find an entry.
		/// </summary>
		/// <param name="uuid">UUID identifying the entry the caller is looking for.</param>
		/// <param name="bSearchRecursive">If <c>true</c>, the search is recursive.</param>
		/// <returns>Returns reference to found entry, otherwise <c>null</c>.</returns>
		public PwEntry FindEntry(PwUuid uuid, bool bSearchRecursive)
		{
			foreach (PwEntry pe in m_listEntries)
			{
				if (pe.Uuid.Equals(uuid)) return pe;
			}

			if (bSearchRecursive)
			{
				PwEntry peSub;
				foreach (PwGroup pg in m_listGroups)
				{
					peSub = pg.FindEntry(uuid, true);
					if (peSub != null) return peSub;
				}
			}

			return null;
		}

		/// <summary>
		/// Get the full path of a group.
		/// </summary>
		/// <returns>Full path of the group.</returns>
		public string GetFullPath()
		{
			return GetFullPath(".", false);
		}

		/// <summary>
		/// Get the full path of a group.
		/// </summary>
		/// <param name="strSeparator">String that separates the group
		/// names.</param>
		/// <param name="bIncludeTopMostGroup">Specifies whether the returned
		/// path starts with the topmost group.</param>
		/// <returns>Full path of the group.</returns>
		public string GetFullPath(string strSeparator, bool bIncludeTopMostGroup)
		{
			Debug.Assert(strSeparator != null);
			if (strSeparator == null) throw new ArgumentNullException("strSeparator");

			string strPath = m_strName;

			PwGroup pg = m_pParentGroup;
			while (pg != null)
			{
				if (!bIncludeTopMostGroup && (pg.m_pParentGroup == null))
					break;

				strPath = pg.Name + strSeparator + strPath;

				pg = pg.m_pParentGroup;
			}

			return strPath;
		}

		/// <summary>
		/// Assign new UUIDs to groups and entries.
		/// </summary>
		/// <param name="bNewGroups">Create new UUIDs for subgroups.</param>
		/// <param name="bNewEntries">Create new UUIDs for entries.</param>
		/// <param name="bRecursive">Recursive tree traversal.</param>
		public void CreateNewItemUuids(bool bNewGroups, bool bNewEntries, bool bRecursive)
		{
			if (bNewGroups)
			{
				foreach (PwGroup pg in m_listGroups)
					pg.Uuid = new PwUuid(true);
			}

			if (bNewEntries)
			{
				foreach (PwEntry pe in m_listEntries)
					pe.SetUuid(new PwUuid(true), true);
			}

			if (bRecursive)
			{
				foreach (PwGroup pg in m_listGroups)
					pg.CreateNewItemUuids(bNewGroups, bNewEntries, true);
			}
		}

		public void TakeOwnership(bool bTakeSubGroups, bool bTakeEntries, bool bRecursive)
		{
			if (bTakeSubGroups)
			{
				foreach (PwGroup pg in m_listGroups)
					pg.ParentGroup = this;
			}

			if (bTakeEntries)
			{
				foreach (PwEntry pe in m_listEntries)
					pe.ParentGroup = this;
			}

			if (bRecursive)
			{
				foreach (PwGroup pg in m_listGroups)
					pg.TakeOwnership(bTakeSubGroups, bTakeEntries, true);
			}
		}

#if !KeePassLibSD
		/// <summary>
		/// Find/create a subtree of groups.
		/// </summary>
		/// <param name="strTree">Tree string.</param>
		/// <param name="vSeparators">Separators that delimit groups in the
		/// <c>strTree</c> parameter.</param>
		public PwGroup FindCreateSubTree(string strTree, char[] vSeparators)
		{
			return FindCreateSubTree(strTree, vSeparators, true);
		}

		public PwGroup FindCreateSubTree(string strTree, char[] vSeparators,
			bool bAllowCreate)
		{
			if (vSeparators == null) { Debug.Assert(false); vSeparators = new char[0]; }

			string[] v = new string[vSeparators.Length];
			for (int i = 0; i < vSeparators.Length; ++i)
				v[i] = new string(vSeparators[i], 1);

			return FindCreateSubTree(strTree, v, bAllowCreate);
		}

		public PwGroup FindCreateSubTree(string strTree, string[] vSeparators,
			bool bAllowCreate)
		{
			Debug.Assert(strTree != null); if (strTree == null) return this;
			if (strTree.Length == 0) return this;

			string[] vGroups = strTree.Split(vSeparators, StringSplitOptions.None);
			if ((vGroups == null) || (vGroups.Length == 0)) return this;

			PwGroup pgContainer = this;
			for (int nGroup = 0; nGroup < vGroups.Length; ++nGroup)
			{
				if (string.IsNullOrEmpty(vGroups[nGroup])) continue;

				bool bFound = false;
				foreach (PwGroup pg in pgContainer.Groups)
				{
					if (pg.Name == vGroups[nGroup])
					{
						pgContainer = pg;
						bFound = true;
						break;
					}
				}

				if (!bFound)
				{
					if (!bAllowCreate) return null;

					PwGroup pg = new PwGroup(true, true, vGroups[nGroup], PwIcon.Folder);
					pgContainer.AddGroup(pg, true);
					pgContainer = pg;
				}
			}

			return pgContainer;
		}
#endif

		/// <summary>
		/// Get the depth of this group (i.e. the number of ancestors).
		/// </summary>
		/// <returns>Depth of this group.</returns>
		public uint GetDepth()
		{
			PwGroup pg = m_pParentGroup;
			uint d = 0;

			while (pg != null)
			{
				pg = pg.m_pParentGroup;
				++d;
			}

			return d;
		}

		private uint GetHeight()
		{
			if (m_listGroups.UCount == 0) return 0;

			uint h = 0;
			foreach (PwGroup pgSub in m_listGroups)
			{
				h = Math.Max(h, pgSub.GetHeight());
			}

			return (h + 1);
		}

		public string GetAutoTypeSequenceInherited()
		{
			if (m_strDefaultAutoTypeSequence.Length > 0)
				return m_strDefaultAutoTypeSequence;

			if (m_pParentGroup != null)
				return m_pParentGroup.GetAutoTypeSequenceInherited();

			return string.Empty;
		}

		public bool GetAutoTypeEnabledInherited()
		{
			if (m_bEnableAutoType.HasValue) return m_bEnableAutoType.Value;

			if (m_pParentGroup != null)
				return m_pParentGroup.GetAutoTypeEnabledInherited();

			return DefaultAutoTypeEnabled;
		}

		public bool GetSearchingEnabledInherited()
		{
			if (m_bEnableSearching.HasValue) return m_bEnableSearching.Value;

			if (m_pParentGroup != null)
				return m_pParentGroup.GetSearchingEnabledInherited();

			return DefaultSearchingEnabled;
		}

		/// <summary>
		/// Get a list of subgroups (not including this one).
		/// </summary>
		/// <param name="bRecursive">If <c>true</c>, subgroups are added
		/// recursively, i.e. all child groups are returned, too.</param>
		/// <returns>List of subgroups. If <paramref name="bRecursive" /> is
		/// <c>true</c>, it is guaranteed that subsubgroups appear after
		/// subgroups.</returns>
		public PwObjectList<PwGroup> GetGroups(bool bRecursive)
		{
			if (!bRecursive) return m_listGroups;

			PwObjectList<PwGroup> list = m_listGroups.CloneShallow();
			foreach (PwGroup pgSub in m_listGroups)
			{
				list.Add(pgSub.GetGroups(true));
			}

			return list;
		}

		public PwObjectList<PwEntry> GetEntries(bool bIncludeSubGroupEntries)
		{
			PwObjectList<PwEntry> l = new PwObjectList<PwEntry>();

			GroupHandler gh = delegate (PwGroup pg)
			{
				l.Add(pg.Entries);
				return true;
			};

			gh(this);
			if (bIncludeSubGroupEntries)
				PreOrderTraverseTree(gh, null);

			Debug.Assert(l.UCount == GetEntriesCount(bIncludeSubGroupEntries));
			return l;
		}

		/// <summary>
		/// Get objects contained in this group.
		/// </summary>
		/// <param name="bRecursive">Specifies whether to search recursively.</param>
		/// <param name="bEntries">If <c>null</c>, the returned list contains
		/// groups and entries. If <c>true</c>, the returned list contains only
		/// entries. If <c>false</c>, the returned list contains only groups.</param>
		/// <returns>List of objects.</returns>
		public List<IStructureItem> GetObjects(bool bRecursive, bool? bEntries)
		{
			List<IStructureItem> list = new List<IStructureItem>();

			if (!bEntries.HasValue || !bEntries.Value)
			{
				PwObjectList<PwGroup> lGroups = GetGroups(bRecursive);
				foreach (PwGroup pg in lGroups) list.Add(pg);
			}

			if (!bEntries.HasValue || bEntries.Value)
			{
				PwObjectList<PwEntry> lEntries = GetEntries(bRecursive);
				foreach (PwEntry pe in lEntries) list.Add(pe);
			}

			return list;
		}

		public bool IsContainedIn(PwGroup pgContainer)
		{
			PwGroup pgCur = m_pParentGroup;
			while (pgCur != null)
			{
				if (pgCur == pgContainer) return true;

				pgCur = pgCur.m_pParentGroup;
			}

			return false;
		}

		/// <summary>
		/// Add a subgroup to this group.
		/// </summary>
		/// <param name="subGroup">Group to be added. Must not be <c>null</c>.</param>
		/// <param name="bTakeOwnership">If this parameter is <c>true</c>, the
		/// parent group reference of the subgroup will be set to the current
		/// group (i.e. the current group takes ownership of the subgroup).</param>
		public void AddGroup(PwGroup subGroup, bool bTakeOwnership)
		{
			AddGroup(subGroup, bTakeOwnership, false);
		}

		/// <summary>
		/// Add a subgroup to this group.
		/// </summary>
		/// <param name="subGroup">Group to be added. Must not be <c>null</c>.</param>
		/// <param name="bTakeOwnership">If this parameter is <c>true</c>, the
		/// parent group reference of the subgroup will be set to the current
		/// group (i.e. the current group takes ownership of the subgroup).</param>
		/// <param name="bUpdateLocationChangedOfSub">If <c>true</c>, the
		/// <c>LocationChanged</c> property of the subgroup is updated.</param>
		public void AddGroup(PwGroup subGroup, bool bTakeOwnership,
			bool bUpdateLocationChangedOfSub)
		{
			if (subGroup == null) throw new ArgumentNullException("subGroup");

			CheckCanAddGroup(subGroup);
			m_listGroups.Add(subGroup);

			if (bTakeOwnership) subGroup.ParentGroup = this;

			if (bUpdateLocationChangedOfSub) subGroup.LocationChanged = DateTime.UtcNow;
		}

		internal bool CanAddGroup(PwGroup pgSub)
		{
			if (pgSub == null) { Debug.Assert(false); return false; }

			uint dCur = GetDepth(), hSub = pgSub.GetHeight();
			return ((dCur + hSub + 1) <= MaxDepth);
		}

		internal void CheckCanAddGroup(PwGroup pgSub)
		{
			if (!CanAddGroup(pgSub))
			{
				Debug.Assert(false);
				throw new InvalidOperationException(KLRes.StructsTooDeep);
			}
		}

		/// <summary>
		/// Add an entry to this group.
		/// </summary>
		/// <param name="pe">Entry to be added. Must not be <c>null</c>.</param>
		/// <param name="bTakeOwnership">If this parameter is <c>true</c>, the
		/// parent group reference of the entry will be set to the current
		/// group (i.e. the current group takes ownership of the entry).</param>
		public void AddEntry(PwEntry pe, bool bTakeOwnership)
		{
			AddEntry(pe, bTakeOwnership, false);
		}

		/// <summary>
		/// Add an entry to this group.
		/// </summary>
		/// <param name="pe">Entry to be added. Must not be <c>null</c>.</param>
		/// <param name="bTakeOwnership">If this parameter is <c>true</c>, the
		/// parent group reference of the entry will be set to the current
		/// group (i.e. the current group takes ownership of the entry).</param>
		/// <param name="bUpdateLocationChangedOfEntry">If <c>true</c>, the
		/// <c>LocationChanged</c> property of the entry is updated.</param>
		public void AddEntry(PwEntry pe, bool bTakeOwnership,
			bool bUpdateLocationChangedOfEntry)
		{
			if (pe == null) throw new ArgumentNullException("pe");

			m_listEntries.Add(pe);

			// Do not remove the entry from its previous parent group,
			// only assign it to the new one
			if (bTakeOwnership) pe.ParentGroup = this;

			if (bUpdateLocationChangedOfEntry) pe.LocationChanged = DateTime.UtcNow;
		}

		public void SortSubGroups(bool bRecursive)
		{
			m_listGroups.Sort(new PwGroupComparer());

			if (bRecursive)
			{
				foreach (PwGroup pgSub in m_listGroups)
					pgSub.SortSubGroups(true);
			}
		}

		public void DeleteAllObjects(PwDatabase pdContext)
		{
			DateTime dtNow = DateTime.UtcNow;

			foreach (PwEntry pe in m_listEntries)
			{
				PwDeletedObject pdo = new PwDeletedObject(pe.Uuid, dtNow);
				pdContext.DeletedObjects.Add(pdo);
			}
			m_listEntries.Clear();

			foreach (PwGroup pg in m_listGroups)
			{
				pg.DeleteAllObjects(pdContext);

				PwDeletedObject pdo = new PwDeletedObject(pg.Uuid, dtNow);
				pdContext.DeletedObjects.Add(pdo);
			}
			m_listGroups.Clear();
		}

		internal List<PwGroup> GetTopSearchSkippedGroups()
		{
			List<PwGroup> l = new List<PwGroup>();

			if (!GetSearchingEnabledInherited()) l.Add(this);
			else GetTopSearchSkippedGroupsRec(l);

			return l;
		}

		private void GetTopSearchSkippedGroupsRec(List<PwGroup> l)
		{
			if (m_bEnableSearching.HasValue && !m_bEnableSearching.Value)
			{
				l.Add(this);
				return;
			}
			else { Debug.Assert(GetSearchingEnabledInherited()); }

			foreach (PwGroup pgSub in m_listGroups)
				pgSub.GetTopSearchSkippedGroupsRec(l);
		}

		public void SetCreatedNow(bool bRecursive)
		{
			DateTime dt = DateTime.UtcNow;

			m_tCreation = dt;
			m_tLastAccess = dt;

			if (!bRecursive) return;

			GroupHandler gh = delegate (PwGroup pg)
			{
				pg.m_tCreation = dt;
				pg.m_tLastAccess = dt;
				return true;
			};

			EntryHandler eh = delegate (PwEntry pe)
			{
				pe.CreationTime = dt;
				pe.LastAccessTime = dt;
				return true;
			};

			TraverseTree(TraversalMethod.PreOrder, gh, eh);
		}

		public PwGroup Duplicate()
		{
			PwGroup pg = CloneDeep();

			pg.Uuid = new PwUuid(true);
			pg.CreateNewItemUuids(true, true, true);

			pg.SetCreatedNow(true);

			return pg;
		}

		

		internal string[] GetAutoTypeSequences(bool bWithStd)
		{
			try
			{
				Dictionary<string, bool> d = new Dictionary<string, bool>();

				Action<string> fAdd = delegate (string str)
				{
					if (!string.IsNullOrEmpty(str)) d[str] = true;
				};

				if (bWithStd)
				{
					fAdd(PwDefs.DefaultAutoTypeSequence);
					fAdd(PwDefs.DefaultAutoTypeSequenceTan);
				}

				GroupHandler gh = delegate (PwGroup pg)
				{
					fAdd(pg.DefaultAutoTypeSequence);
					return true;
				};

				EntryHandler eh = delegate (PwEntry pe)
				{
					AutoTypeConfig c = pe.AutoType;

					fAdd(c.DefaultSequence);
					foreach (AutoTypeAssociation a in c.Associations)
					{
						fAdd(a.Sequence);
					}

					return true;
				};

				gh(this);
				TraverseTree(TraversalMethod.PreOrder, gh, eh);

				string[] v = new string[d.Count];
				if (d.Count != 0)
				{
					d.Keys.CopyTo(v, 0);
					Array.Sort<string>(v, StrUtil.CaseIgnoreComparer);
				}

				return v;
			}
			catch (Exception) { Debug.Assert(false); }

			return new string[0];
		}
	}

	public sealed class PwGroupComparer : IComparer<PwGroup>
	{
		public PwGroupComparer()
		{
		}

		public int Compare(PwGroup a, PwGroup b)
		{
			return StrUtil.CompareNaturally(a.Name, b.Name);
		}
	}
}
