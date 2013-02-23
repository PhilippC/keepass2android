/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2012 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Xml;
using System.Drawing;

using KeePassLib.Collections;
using KeePassLib.Interfaces;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePassLib
{
	/// <summary>
	/// A class representing a password entry. A password entry consists of several
	/// fields like title, user name, password, etc. Each password entry has a
	/// unique ID (UUID).
	/// </summary>
	public sealed class PwEntry : ITimeLogger, IStructureItem, IDeepCloneable<PwEntry>
	{
		private PwUuid m_uuid = PwUuid.Zero;
		private PwGroup m_pParentGroup = null;
		private DateTime m_tParentGroupLastMod = PwDefs.DtDefaultNow;

		private ProtectedStringDictionary m_listStrings = new ProtectedStringDictionary();
		private ProtectedBinaryDictionary m_listBinaries = new ProtectedBinaryDictionary();
		private AutoTypeConfig m_listAutoType = new AutoTypeConfig();
		private PwObjectList<PwEntry> m_listHistory = new PwObjectList<PwEntry>();

		private PwIcon m_pwIcon = PwIcon.Key;
		private PwUuid m_pwCustomIconID = PwUuid.Zero;

		private Color m_clrForeground = Color.Empty;
		private Color m_clrBackground = Color.Empty;

		private DateTime m_tCreation = PwDefs.DtDefaultNow;
		private DateTime m_tLastMod = PwDefs.DtDefaultNow;
		private DateTime m_tLastAccess = PwDefs.DtDefaultNow;
		private DateTime m_tExpire = PwDefs.DtDefaultNow;
		private bool m_bExpires = false;
		private ulong m_uUsageCount = 0;

		private string m_strOverrideUrl = string.Empty;

		private List<string> m_vTags = new List<string>();

		/// <summary>
		/// UUID of this entry.
		/// </summary>
		public PwUuid Uuid
		{
			get { return m_uuid; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_uuid = value;
			}
		}

		/// <summary>
		/// Reference to a group which contains the current entry.
		/// </summary>
		public PwGroup ParentGroup
		{
			get { return m_pParentGroup; }

			/// Plugins: use <c>PwGroup.AddEntry</c> instead.
			internal set { m_pParentGroup = value; }
		}

		/// <summary>
		/// The date/time when the location of the object was last changed.
		/// </summary>
		public DateTime LocationChanged
		{
			get { return m_tParentGroupLastMod; }
			set { m_tParentGroupLastMod = value; }
		}

		/// <summary>
		/// Get or set all entry strings.
		/// </summary>
		public ProtectedStringDictionary Strings
		{
			get { return m_listStrings; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_listStrings = value;
			}
		}

		/// <summary>
		/// Get or set all entry binaries.
		/// </summary>
		public ProtectedBinaryDictionary Binaries
		{
			get { return m_listBinaries; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_listBinaries = value;
			}
		}

		/// <summary>
		/// Get or set all auto-type window/keystroke sequence associations.
		/// </summary>
		public AutoTypeConfig AutoType
		{
			get { return m_listAutoType; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_listAutoType = value;
			}
		}

		/// <summary>
		/// Get all previous versions of this entry (backups).
		/// </summary>
		public PwObjectList<PwEntry> History
		{
			get { return m_listHistory; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_listHistory = value;
			}
		}

		/// <summary>
		/// Image ID specifying the icon that will be used for this entry.
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
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_pwCustomIconID = value;
			}
		}

		/// <summary>
		/// Get or set the foreground color of this entry.
		/// </summary>
		public Color ForegroundColor
		{
			get { return m_clrForeground; }
			set { m_clrForeground = value; }
		}

		/// <summary>
		/// Get or set the background color of this entry.
		/// </summary>
		public Color BackgroundColor
		{
			get { return m_clrBackground; }
			set { m_clrBackground = value; }
		}

		/// <summary>
		/// The date/time when this entry was created.
		/// </summary>
		public DateTime CreationTime
		{
			get { return m_tCreation; }
			set { m_tCreation = value; }
		}

		/// <summary>
		/// The date/time when this entry was last accessed (read).
		/// </summary>
		public DateTime LastAccessTime
		{
			get { return m_tLastAccess; }
			set { m_tLastAccess = value; }
		}

		/// <summary>
		/// The date/time when this entry was last modified.
		/// </summary>
		public DateTime LastModificationTime
		{
			get { return m_tLastMod; }
			set { m_tLastMod = value; }
		}

		/// <summary>
		/// The date/time when this entry expires. Use the <c>Expires</c> property
		/// to specify if the entry does actually expire or not.
		/// </summary>
		public DateTime ExpiryTime
		{
			get { return m_tExpire; }
			set { m_tExpire = value; }
		}

		/// <summary>
		/// Specifies whether the entry expires or not.
		/// </summary>
		public bool Expires
		{
			get { return m_bExpires; }
			set { m_bExpires = value; }
		}

		/// <summary>
		/// Get or set the usage count of the entry. To increase the usage
		/// count by one, use the <c>Touch</c> function.
		/// </summary>
		public ulong UsageCount
		{
			get { return m_uUsageCount; }
			set { m_uUsageCount = value; }
		}

		/// <summary>
		/// Entry-specific override URL. If this string is non-empty,
		/// </summary>
		public string OverrideUrl
		{
			get { return m_strOverrideUrl; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strOverrideUrl = value;
			}
		}

		/// <summary>
		/// List of tags associated with this entry.
		/// </summary>
		public List<string> Tags
		{
			get { return m_vTags; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_vTags = value;
			}
		}

		public static EventHandler<ObjectTouchedEventArgs> EntryTouched;
		public EventHandler<ObjectTouchedEventArgs> Touched;

		/// <summary>
		/// Construct a new, empty password entry. Member variables will be initialized
		/// to their default values.
		/// </summary>
		/// <param name="bCreateNewUuid">If <c>true</c>, a new UUID will be created
		/// for this entry. If <c>false</c>, the UUID is zero and you must set it
		/// manually later.</param>
		/// <param name="bSetTimes">If <c>true</c>, the creation, last modification
		/// and last access times will be set to the current system time.</param>
		public PwEntry(bool bCreateNewUuid, bool bSetTimes)
		{
			if(bCreateNewUuid) m_uuid = new PwUuid(true);

			if(bSetTimes)
			{
				m_tCreation = m_tLastMod = m_tLastAccess =
					m_tParentGroupLastMod = DateTime.Now;
			}
		}

		/// <summary>
		/// Construct a new, empty password entry. Member variables will be initialized
		/// to their default values.
		/// </summary>
		/// <param name="pwParentGroup">Reference to the containing group, this
		/// parameter may be <c>null</c> and set later manually.</param>
		/// <param name="bCreateNewUuid">If <c>true</c>, a new UUID will be created
		/// for this entry. If <c>false</c>, the UUID is zero and you must set it
		/// manually later.</param>
		/// <param name="bSetTimes">If <c>true</c>, the creation, last modification
		/// and last access times will be set to the current system time.</param>
		[Obsolete("Use a different constructor. To add an entry to a group, use AddEntry of PwGroup.")]
		public PwEntry(PwGroup pwParentGroup, bool bCreateNewUuid, bool bSetTimes)
		{
			m_pParentGroup = pwParentGroup;

			if(bCreateNewUuid) m_uuid = new PwUuid(true);

			if(bSetTimes)
			{
				m_tCreation = m_tLastMod = m_tLastAccess =
					m_tParentGroupLastMod = DateTime.Now;
			}
		}

		/// <summary>
		/// Clone the current entry. The returned entry is an exact value copy
		/// of the current entry (including UUID and parent group reference).
		/// All mutable members are cloned.
		/// </summary>
		/// <returns>Exact value clone. All references to mutable values changed.</returns>
		public PwEntry CloneDeep()
		{
			PwEntry peNew = new PwEntry(false, false);

			peNew.m_uuid = m_uuid; // PwUuid is immutable
			peNew.m_pParentGroup = m_pParentGroup;
			peNew.m_tParentGroupLastMod = m_tParentGroupLastMod;

			peNew.m_listStrings = m_listStrings.CloneDeep();
			peNew.m_listBinaries = m_listBinaries.CloneDeep();
			peNew.m_listAutoType = m_listAutoType.CloneDeep();
			peNew.m_listHistory = m_listHistory.CloneDeep();

			peNew.m_pwIcon = m_pwIcon;
			peNew.m_pwCustomIconID = m_pwCustomIconID;

			peNew.m_clrForeground = m_clrForeground;
			peNew.m_clrBackground = m_clrBackground;

			peNew.m_tCreation = m_tCreation;
			peNew.m_tLastMod = m_tLastMod;
			peNew.m_tLastAccess = m_tLastAccess;
			peNew.m_tExpire = m_tExpire;
			peNew.m_bExpires = m_bExpires;
			peNew.m_uUsageCount = m_uUsageCount;

			peNew.m_strOverrideUrl = m_strOverrideUrl;

			peNew.m_vTags = new List<string>(m_vTags);

			return peNew;
		}

		public PwEntry CloneStructure()
		{
			PwEntry peNew = new PwEntry(false, false);

			peNew.m_uuid = m_uuid; // PwUuid is immutable
			peNew.m_tParentGroupLastMod = m_tParentGroupLastMod;
			// Do not assign m_pParentGroup

			return peNew;
		}

		private static PwCompareOptions BuildCmpOpt(bool bIgnoreParentGroup,
			bool bIgnoreLastMod, bool bIgnoreLastAccess, bool bIgnoreHistory,
			bool bIgnoreThisLastBackup)
		{
			PwCompareOptions pwOpt = PwCompareOptions.None;
			if(bIgnoreParentGroup) pwOpt |= PwCompareOptions.IgnoreParentGroup;
			if(bIgnoreLastMod) pwOpt |= PwCompareOptions.IgnoreLastMod;
			if(bIgnoreLastAccess) pwOpt |= PwCompareOptions.IgnoreLastAccess;
			if(bIgnoreHistory) pwOpt |= PwCompareOptions.IgnoreHistory;
			if(bIgnoreThisLastBackup) pwOpt |= PwCompareOptions.IgnoreLastBackup;
			return pwOpt;
		}

		[Obsolete]
		public bool EqualsEntry(PwEntry pe, bool bIgnoreParentGroup, bool bIgnoreLastMod,
			bool bIgnoreLastAccess, bool bIgnoreHistory, bool bIgnoreThisLastBackup)
		{
			return EqualsEntry(pe, BuildCmpOpt(bIgnoreParentGroup, bIgnoreLastMod,
				bIgnoreLastAccess, bIgnoreHistory, bIgnoreThisLastBackup),
				MemProtCmpMode.None);
		}

		[Obsolete]
		public bool EqualsEntry(PwEntry pe, bool bIgnoreParentGroup, bool bIgnoreLastMod,
			bool bIgnoreLastAccess, bool bIgnoreHistory, bool bIgnoreThisLastBackup,
			MemProtCmpMode mpCmpStr)
		{
			return EqualsEntry(pe, BuildCmpOpt(bIgnoreParentGroup, bIgnoreLastMod,
				bIgnoreLastAccess, bIgnoreHistory, bIgnoreThisLastBackup), mpCmpStr);
		}

		public bool EqualsEntry(PwEntry pe, PwCompareOptions pwOpt,
			MemProtCmpMode mpCmpStr)
		{
			if(pe == null) { Debug.Assert(false); return false; }

			bool bNeEqStd = ((pwOpt & PwCompareOptions.NullEmptyEquivStd) !=
				PwCompareOptions.None);
			bool bIgnoreLastAccess = ((pwOpt & PwCompareOptions.IgnoreLastAccess) !=
				PwCompareOptions.None);
			bool bIgnoreLastMod = ((pwOpt & PwCompareOptions.IgnoreLastMod) !=
				PwCompareOptions.None);

			if(!m_uuid.EqualsValue(pe.m_uuid)) return false;
			if((pwOpt & PwCompareOptions.IgnoreParentGroup) == PwCompareOptions.None)
			{
				if(m_pParentGroup != pe.m_pParentGroup) return false;
				if(!bIgnoreLastMod && (m_tParentGroupLastMod != pe.m_tParentGroupLastMod))
					return false;
			}

			if(!m_listStrings.EqualsDictionary(pe.m_listStrings, pwOpt, mpCmpStr))
				return false;
			if(!m_listBinaries.EqualsDictionary(pe.m_listBinaries)) return false;

			if(!m_listAutoType.Equals(pe.m_listAutoType)) return false;

			if((pwOpt & PwCompareOptions.IgnoreHistory) == PwCompareOptions.None)
			{
				bool bIgnoreLastBackup = ((pwOpt & PwCompareOptions.IgnoreLastBackup) !=
					PwCompareOptions.None);

				if(!bIgnoreLastBackup && (m_listHistory.UCount != pe.m_listHistory.UCount))
					return false;
				if(bIgnoreLastBackup && (m_listHistory.UCount == 0))
				{
					Debug.Assert(false);
					return false;
				}
				if(bIgnoreLastBackup && ((m_listHistory.UCount - 1) != pe.m_listHistory.UCount))
					return false;

				PwCompareOptions cmpSub = PwCompareOptions.IgnoreParentGroup;
				if(bNeEqStd) cmpSub |= PwCompareOptions.NullEmptyEquivStd;
				if(bIgnoreLastMod) cmpSub |= PwCompareOptions.IgnoreLastMod;
				if(bIgnoreLastAccess) cmpSub |= PwCompareOptions.IgnoreLastAccess;

				for(uint uHist = 0; uHist < pe.m_listHistory.UCount; ++uHist)
				{
					if(!m_listHistory.GetAt(uHist).EqualsEntry(pe.m_listHistory.GetAt(
						uHist), cmpSub, MemProtCmpMode.None))
						return false;
				}
			}

			if(m_pwIcon != pe.m_pwIcon) return false;
			if(!m_pwCustomIconID.EqualsValue(pe.m_pwCustomIconID)) return false;

			if(m_clrForeground != pe.m_clrForeground) return false;
			if(m_clrBackground != pe.m_clrBackground) return false;

			if(m_tCreation != pe.m_tCreation) return false;
			if(!bIgnoreLastMod && (m_tLastMod != pe.m_tLastMod)) return false;
			if(!bIgnoreLastAccess && (m_tLastAccess != pe.m_tLastAccess)) return false;
			if(m_tExpire != pe.m_tExpire) return false;
			if(m_bExpires != pe.m_bExpires) return false;
			if(!bIgnoreLastAccess && (m_uUsageCount != pe.m_uUsageCount)) return false;

			if(m_strOverrideUrl != pe.m_strOverrideUrl) return false;

			if(m_vTags.Count != pe.m_vTags.Count) return false;
			for(int iTag = 0; iTag < m_vTags.Count; ++iTag)
			{
				if(m_vTags[iTag] != pe.m_vTags[iTag]) return false;
			}

			return true;
		}

		/// <summary>
		/// Assign properties to the current entry based on a template entry.
		/// </summary>
		/// <param name="peTemplate">Template entry. Must not be <c>null</c>.</param>
		/// <param name="bOnlyIfNewer">Only set the properties of the template entry
		/// if it is newer than the current one.</param>
		/// <param name="bIncludeHistory">If <c>true</c>, the history will be
		/// copied, too.</param>
		/// <param name="bAssignLocationChanged">If <c>true</c>, the
		/// <c>LocationChanged</c> property is copied, otherwise not.</param>
		public void AssignProperties(PwEntry peTemplate, bool bOnlyIfNewer,
			bool bIncludeHistory, bool bAssignLocationChanged)
		{
			Debug.Assert(peTemplate != null); if(peTemplate == null) throw new ArgumentNullException("peTemplate");

			if(bOnlyIfNewer && (peTemplate.m_tLastMod < m_tLastMod)) return;

			// Template UUID should be the same as the current one
			Debug.Assert(m_uuid.EqualsValue(peTemplate.m_uuid));
			m_uuid = peTemplate.m_uuid;

			if(bAssignLocationChanged)
				m_tParentGroupLastMod = peTemplate.m_tParentGroupLastMod;

			m_listStrings = peTemplate.m_listStrings;
			m_listBinaries = peTemplate.m_listBinaries;
			m_listAutoType = peTemplate.m_listAutoType;
			if(bIncludeHistory) m_listHistory = peTemplate.m_listHistory;

			m_pwIcon = peTemplate.m_pwIcon;
			m_pwCustomIconID = peTemplate.m_pwCustomIconID; // Immutable

			m_clrForeground = peTemplate.m_clrForeground;
			m_clrBackground = peTemplate.m_clrBackground;

			m_tCreation = peTemplate.m_tCreation;
			m_tLastMod = peTemplate.m_tLastMod;
			m_tLastAccess = peTemplate.m_tLastAccess;
			m_tExpire = peTemplate.m_tExpire;
			m_bExpires = peTemplate.m_bExpires;
			m_uUsageCount = peTemplate.m_uUsageCount;

			m_strOverrideUrl = peTemplate.m_strOverrideUrl;

			m_vTags = new List<string>(peTemplate.m_vTags);
		}

		/// <summary>
		/// Touch the entry. This function updates the internal last access
		/// time. If the <paramref name="bModified" /> parameter is <c>true</c>,
		/// the last modification time gets updated, too.
		/// </summary>
		/// <param name="bModified">Modify last modification time.</param>
		public void Touch(bool bModified)
		{
			Touch(bModified, true);
		}

		/// <summary>
		/// Touch the entry. This function updates the internal last access
		/// time. If the <paramref name="bModified" /> parameter is <c>true</c>,
		/// the last modification time gets updated, too.
		/// </summary>
		/// <param name="bModified">Modify last modification time.</param>
		/// <param name="bTouchParents">If <c>true</c>, all parent objects
		/// get touched, too.</param>
		public void Touch(bool bModified, bool bTouchParents)
		{
			m_tLastAccess = DateTime.Now;
			++m_uUsageCount;

			if(bModified) m_tLastMod = m_tLastAccess;

			if(this.Touched != null)
				this.Touched(this, new ObjectTouchedEventArgs(this,
					bModified, bTouchParents));
			if(PwEntry.EntryTouched != null)
				PwEntry.EntryTouched(this, new ObjectTouchedEventArgs(this,
					bModified, bTouchParents));

			if(bTouchParents && (m_pParentGroup != null))
				m_pParentGroup.Touch(bModified, true);
		}

		/// <summary>
		/// Create a backup of this entry. The backup item doesn't contain any
		/// history items.
		/// </summary>
		[Obsolete]
		public void CreateBackup()
		{
			CreateBackup(null);
		}

		/// <summary>
		/// Create a backup of this entry. The backup item doesn't contain any
		/// history items.
		/// <param name="pwHistMntcSettings">If this parameter isn't <c>null</c>,
		/// the history list is maintained automatically (i.e. old backups are
		/// deleted if there are too many or the history size is too large).
		/// This parameter may be <c>null</c> (no maintenance then).</param>
		/// </summary>
		public void CreateBackup(PwDatabase pwHistMntcSettings)
		{
			PwEntry peCopy = CloneDeep();
			peCopy.History = new PwObjectList<PwEntry>(); // Remove history

			m_listHistory.Add(peCopy); // Must be added at end, see EqualsEntry

			if(pwHistMntcSettings != null) MaintainBackups(pwHistMntcSettings);
		}

		/// <summary>
		/// Restore an entry snapshot from backups.
		/// </summary>
		/// <param name="uBackupIndex">Index of the backup item, to which
		/// should be reverted.</param>
		[Obsolete]
		public void RestoreFromBackup(uint uBackupIndex)
		{
			RestoreFromBackup(uBackupIndex, null);
		}

		/// <summary>
		/// Restore an entry snapshot from backups.
		/// </summary>
		/// <param name="uBackupIndex">Index of the backup item, to which
		/// should be reverted.</param>
		/// <param name="pwHistMntcSettings">If this parameter isn't <c>null</c>,
		/// the history list is maintained automatically (i.e. old backups are
		/// deleted if there are too many or the history size is too large).
		/// This parameter may be <c>null</c> (no maintenance then).</param>
		public void RestoreFromBackup(uint uBackupIndex, PwDatabase pwHistMntcSettings)
		{
			Debug.Assert(uBackupIndex < m_listHistory.UCount);
			if(uBackupIndex >= m_listHistory.UCount)
				throw new ArgumentOutOfRangeException("uBackupIndex");

			PwEntry pe = m_listHistory.GetAt(uBackupIndex);
			Debug.Assert(pe != null); if(pe == null) throw new InvalidOperationException();

			CreateBackup(pwHistMntcSettings); // Backup current data before restoring
			AssignProperties(pe, false, false, false);
		}

		public bool HasBackupOfData(PwEntry peData, bool bIgnoreLastMod,
			bool bIgnoreLastAccess)
		{
			if(peData == null) { Debug.Assert(false); return false; }

			PwCompareOptions cmpOpt = (PwCompareOptions.IgnoreParentGroup |
				PwCompareOptions.IgnoreHistory | PwCompareOptions.NullEmptyEquivStd);
			if(bIgnoreLastMod) cmpOpt |= PwCompareOptions.IgnoreLastMod;
			if(bIgnoreLastAccess) cmpOpt |= PwCompareOptions.IgnoreLastAccess;

			foreach(PwEntry pe in m_listHistory)
			{
				if(pe.EqualsEntry(peData, cmpOpt, MemProtCmpMode.None)) return true;
			}

			return false;
		}

		/// <summary>
		/// Delete old history items if there are too many or the history
		/// size is too large.
		/// <returns>If one or more history items have been deleted, <c>true</c>
		/// is returned. Otherwise <c>false</c>.</returns>
		/// </summary>
		public bool MaintainBackups(PwDatabase pwSettings)
		{
			if(pwSettings == null) { Debug.Assert(false); return false; }

			bool bDeleted = false;

			int nMaxItems = pwSettings.HistoryMaxItems;
			if(nMaxItems >= 0)
			{
				while(m_listHistory.UCount > (uint)nMaxItems)
				{
					RemoveOldestBackup();
					bDeleted = true;
				}
			}

			long lMaxSize = pwSettings.HistoryMaxSize;
			if(lMaxSize >= 0)
			{
				while(true)
				{
					ulong uHistSize = 0;
					foreach(PwEntry pe in m_listHistory) { uHistSize += pe.GetSize(); }

					if(uHistSize > (ulong)lMaxSize)
					{
						RemoveOldestBackup();
						bDeleted = true;
					}
					else break;
				}
			}

			return bDeleted;
		}

		private void RemoveOldestBackup()
		{
			DateTime dtMin = DateTime.MaxValue;
			uint idxRemove = uint.MaxValue;

			for(uint u = 0; u < m_listHistory.UCount; ++u)
			{
				PwEntry pe = m_listHistory.GetAt(u);
				if(pe.LastModificationTime < dtMin)
				{
					idxRemove = u;
					dtMin = pe.LastModificationTime;
				}
			}

			if(idxRemove != uint.MaxValue) m_listHistory.RemoveAt(idxRemove);
		}

		public bool GetAutoTypeEnabled()
		{
			if(!m_listAutoType.Enabled) return false;

			if(m_pParentGroup != null)
				return m_pParentGroup.GetAutoTypeEnabledInherited();

			return PwGroup.DefaultAutoTypeEnabled;
		}

		public string GetAutoTypeSequence()
		{
			string strSeq = m_listAutoType.DefaultSequence;

			PwGroup pg = m_pParentGroup;
			while(pg != null)
			{
				if(strSeq.Length != 0) break;

				strSeq = pg.DefaultAutoTypeSequence;
				pg = pg.ParentGroup;
			}

			if(strSeq.Length != 0) return strSeq;

			if(PwDefs.IsTanEntry(this)) return PwDefs.DefaultAutoTypeSequenceTan;
			return PwDefs.DefaultAutoTypeSequence;
		}

		public bool GetSearchingEnabled()
		{
			if(m_pParentGroup != null)
				return m_pParentGroup.GetSearchingEnabledInherited();

			return PwGroup.DefaultSearchingEnabled;
		}

		/// <summary>
		/// Approximate the total size of this entry in bytes (including
		/// strings, binaries and history entries).
		/// </summary>
		/// <returns>Size in bytes.</returns>
		public ulong GetSize()
		{
			ulong uSize = 128; // Approx fixed length data

			foreach(KeyValuePair<string, ProtectedString> kvpStr in m_listStrings)
			{
				uSize += (ulong)kvpStr.Key.Length;
				uSize += (ulong)kvpStr.Value.Length;
			}

			foreach(KeyValuePair<string, ProtectedBinary> kvpBin in m_listBinaries)
			{
				uSize += (ulong)kvpBin.Key.Length;
				uSize += kvpBin.Value.Length;
			}

			uSize += (ulong)m_listAutoType.DefaultSequence.Length;
			foreach(AutoTypeAssociation a in m_listAutoType.Associations)
			{
				uSize += (ulong)a.WindowName.Length;
				uSize += (ulong)a.Sequence.Length;
			}

			foreach(PwEntry peHistory in m_listHistory)
				uSize += peHistory.GetSize();

			uSize += (ulong)m_strOverrideUrl.Length;

			foreach(string strTag in m_vTags)
				uSize += (ulong)strTag.Length;

			return uSize;
		}

		public bool HasTag(string strTag)
		{
			if(string.IsNullOrEmpty(strTag)) { Debug.Assert(false); return false; }

			for(int i = 0; i < m_vTags.Count; ++i)
			{
				if(m_vTags[i].Equals(strTag, StrUtil.CaseIgnoreCmp)) return true;
			}

			return false;
		}

		public bool AddTag(string strTag)
		{
			if(string.IsNullOrEmpty(strTag)) { Debug.Assert(false); return false; }

			for(int i = 0; i < m_vTags.Count; ++i)
			{
				if(m_vTags[i].Equals(strTag, StrUtil.CaseIgnoreCmp)) return false;
			}

			m_vTags.Add(strTag);
			return true;
		}

		public bool RemoveTag(string strTag)
		{
			if(string.IsNullOrEmpty(strTag)) { Debug.Assert(false); return false; }

			for(int i = 0; i < m_vTags.Count; ++i)
			{
				if(m_vTags[i].Equals(strTag, StrUtil.CaseIgnoreCmp))
				{
					m_vTags.RemoveAt(i);
					return true;
				}
			}

			return false;
		}

		public bool IsContainedIn(PwGroup pgContainer)
		{
			PwGroup pgCur = m_pParentGroup;
			while(pgCur != null)
			{
				if(pgCur == pgContainer) return true;

				pgCur = pgCur.ParentGroup;
			}

			return false;
		}

		public void SetUuid(PwUuid pwNewUuid, bool bAlsoChangeHistoryUuids)
		{
			this.Uuid = pwNewUuid;

			if(bAlsoChangeHistoryUuids)
			{
				foreach(PwEntry peHist in m_listHistory)
				{
					peHist.Uuid = pwNewUuid;
				}
			}
		}
	}

	public sealed class PwEntryComparer : IComparer<PwEntry>
	{
		private string m_strFieldName;
		private bool m_bCaseInsensitive;
		private bool m_bCompareNaturally;

		public PwEntryComparer(string strFieldName, bool bCaseInsensitive,
			bool bCompareNaturally)
		{
			if(strFieldName == null) throw new ArgumentNullException("strFieldName");

			m_strFieldName = strFieldName;
			m_bCaseInsensitive = bCaseInsensitive;
			m_bCompareNaturally = bCompareNaturally;
		}

		public int Compare(PwEntry a, PwEntry b)
		{
			string strA = a.Strings.ReadSafe(m_strFieldName);
			string strB = b.Strings.ReadSafe(m_strFieldName);

			if(m_bCompareNaturally) return StrUtil.CompareNaturally(strA, strB);
			return string.Compare(strA, strB, m_bCaseInsensitive);
		}
	}
}
