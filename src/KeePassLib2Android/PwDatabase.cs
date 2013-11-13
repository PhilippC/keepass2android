/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2013 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.IO;
using System.Drawing;

using KeePassLib.Collections;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Delegates;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Resources;
using KeePassLib.Security;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace KeePassLib
{
	/// <summary>
	/// The core password manager class. It contains a number of groups, which
	/// contain the actual entries.
	/// </summary>
	public sealed class PwDatabase
	{
		internal const int DefaultHistoryMaxItems = 10; // -1 = unlimited
		internal const long DefaultHistoryMaxSize = 6 * 1024 * 1024; // -1 = unlimited

		private static bool m_bPrimaryCreated = false;

		// Initializations see Clear()
		private PwGroup m_pgRootGroup = null;
		private PwObjectList<PwDeletedObject> m_vDeletedObjects = new PwObjectList<PwDeletedObject>();

		private PwUuid m_uuidDataCipher = StandardAesEngine.AesUuid;
		private PwCompressionAlgorithm m_caCompression = PwCompressionAlgorithm.GZip;
		private ulong m_uKeyEncryptionRounds = PwDefs.DefaultKeyEncryptionRounds;

		private CompositeKey m_pwUserKey = null;
		private MemoryProtectionConfig m_memProtConfig = new MemoryProtectionConfig();

		private List<PwCustomIcon> m_vCustomIcons = new List<PwCustomIcon>();
		private bool m_bUINeedsIconUpdate = true;

		private string m_strName = string.Empty;
		private DateTime m_dtNameChanged = PwDefs.DtDefaultNow;
		private string m_strDesc = string.Empty;
		private DateTime m_dtDescChanged = PwDefs.DtDefaultNow;
		private string m_strDefaultUserName = string.Empty;
		private DateTime m_dtDefaultUserChanged = PwDefs.DtDefaultNow;
		private uint m_uMntncHistoryDays = 365;
		private Color m_clr = Color.Empty;

		private DateTime m_dtKeyLastChanged = PwDefs.DtDefaultNow;
		private long m_lKeyChangeRecDays = -1;
		private long m_lKeyChangeForceDays = -1;

		private IOConnectionInfo m_ioSource = new IOConnectionInfo();
		private bool m_bDatabaseOpened = false;
		private bool m_bModified = false;

		private PwUuid m_pwLastSelectedGroup = PwUuid.Zero;
		private PwUuid m_pwLastTopVisibleGroup = PwUuid.Zero;

		private bool m_bUseRecycleBin = true;
		private PwUuid m_pwRecycleBin = PwUuid.Zero;
		private DateTime m_dtRecycleBinChanged = PwDefs.DtDefaultNow;
		private PwUuid m_pwEntryTemplatesGroup = PwUuid.Zero;
		private DateTime m_dtEntryTemplatesChanged = PwDefs.DtDefaultNow;

		private int m_nHistoryMaxItems = DefaultHistoryMaxItems;
		private long m_lHistoryMaxSize = DefaultHistoryMaxSize; // In bytes

		private StringDictionaryEx m_vCustomData = new StringDictionaryEx();

		private byte[] m_pbHashOfFileOnDisk = null;
		private byte[] m_pbHashOfLastIO = null;

		private bool m_bUseFileTransactions = false;
		private bool m_bUseFileLocks = false;

		private IStatusLogger m_slStatus = null;

		private static string m_strLocalizedAppName = string.Empty;

		// private const string StrBackupExtension = ".bak";

		/// <summary>
		/// Get the root group that contains all groups and entries stored in the
		/// database.
		/// </summary>
		/// <returns>Root group. The return value is <c>null</c>, if no database
		/// has been opened.</returns>
		public PwGroup RootGroup
		{
			get { return m_pgRootGroup; }
			set
			{
				Debug.Assert(value != null);
				if(value == null) throw new ArgumentNullException("value");

				m_pgRootGroup = value;
			}
		}

		/// <summary>
		/// <c>IOConnection</c> of the currently opened database file.
		/// Is never <c>null</c>.
		/// </summary>
		public IOConnectionInfo IOConnectionInfo
		{
			get { return m_ioSource; }
		}

		/// <summary>
		/// If this is <c>true</c>, a database is currently open.
		/// </summary>
		public bool IsOpen
		{
			get { return m_bDatabaseOpened; }
		}

		/// <summary>
		/// Modification flag. If true, the class has been modified and the
		/// user interface should prompt the user to save the changes before
		/// closing the database for example.
		/// </summary>
		public bool Modified
		{
			get { return m_bModified; }
			set { m_bModified = value; }
		}

		/// <summary>
		/// The user key used for database encryption. This key must be created
		/// and set before using any of the database load/save functions.
		/// </summary>
		public CompositeKey MasterKey
		{
			get { return m_pwUserKey; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");

				m_pwUserKey = value;
			}
		}

		/// <summary>
		/// Name of the database.
		/// </summary>
		public string Name
		{
			get { return m_strName; }
			set
			{
				Debug.Assert(value != null);
				if(value != null) m_strName = value;
			}
		}

		public DateTime NameChanged
		{
			get { return m_dtNameChanged; }
			set { m_dtNameChanged = value; }
		}

		/// <summary>
		/// Database description.
		/// </summary>
		public string Description
		{
			get { return m_strDesc; }
			set
			{
				Debug.Assert(value != null);
				if(value != null) m_strDesc = value;
			}
		}

		public DateTime DescriptionChanged
		{
			get { return m_dtDescChanged; }
			set { m_dtDescChanged = value; }
		}

		/// <summary>
		/// Default user name used for new entries.
		/// </summary>
		public string DefaultUserName
		{
			get { return m_strDefaultUserName; }
			set
			{
				Debug.Assert(value != null);
				if(value != null) m_strDefaultUserName = value;
			}
		}

		public DateTime DefaultUserNameChanged
		{
			get { return m_dtDefaultUserChanged; }
			set { m_dtDefaultUserChanged = value; }
		}

		/// <summary>
		/// Number of days until history entries are being deleted
		/// in a database maintenance operation.
		/// </summary>
		public uint MaintenanceHistoryDays
		{
			get { return m_uMntncHistoryDays; }
			set { m_uMntncHistoryDays = value; }
		}

		public Color Color
		{
			get { return m_clr; }
			set { m_clr = value; }
		}

		public DateTime MasterKeyChanged
		{
			get { return m_dtKeyLastChanged; }
			set { m_dtKeyLastChanged = value; }
		}

		public long MasterKeyChangeRec
		{
			get { return m_lKeyChangeRecDays; }
			set { m_lKeyChangeRecDays = value; }
		}

		public long MasterKeyChangeForce
		{
			get { return m_lKeyChangeForceDays; }
			set { m_lKeyChangeForceDays = value; }
		}

		/// <summary>
		/// The encryption algorithm used to encrypt the data part of the database.
		/// </summary>
		public PwUuid DataCipherUuid
		{
			get { return m_uuidDataCipher; }
			set
			{
				Debug.Assert(value != null);
				if(value != null) m_uuidDataCipher = value;
			}
		}

		/// <summary>
		/// Compression algorithm used to encrypt the data part of the database.
		/// </summary>
		public PwCompressionAlgorithm Compression
		{
			get { return m_caCompression; }
			set { m_caCompression = value; }
		}

		/// <summary>
		/// Number of key transformation rounds (in order to make dictionary
		/// attacks harder).
		/// </summary>
		public ulong KeyEncryptionRounds
		{
			get { return m_uKeyEncryptionRounds; }
			set { m_uKeyEncryptionRounds = value; }
		}

		/// <summary>
		/// Memory protection configuration (for default fields).
		/// </summary>
		public MemoryProtectionConfig MemoryProtection
		{
			get { return m_memProtConfig; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				
				m_memProtConfig = value;
			}
		}

		/// <summary>
		/// Get a list of all deleted objects.
		/// </summary>
		public PwObjectList<PwDeletedObject> DeletedObjects
		{
			get { return m_vDeletedObjects; }
		}

		/// <summary>
		/// Get all custom icons stored in this database.
		/// </summary>
		public List<PwCustomIcon> CustomIcons
		{
			get { return m_vCustomIcons; }
		}

		/// <summary>
		/// This is a dirty-flag for the UI. It is used to indicate when an
		/// icon list update is required.
		/// </summary>
		public bool UINeedsIconUpdate
		{
			get { return m_bUINeedsIconUpdate; }
			set { m_bUINeedsIconUpdate = value; }
		}

		public PwUuid LastSelectedGroup
		{
			get { return m_pwLastSelectedGroup; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_pwLastSelectedGroup = value;
			}
		}

		public PwUuid LastTopVisibleGroup
		{
			get { return m_pwLastTopVisibleGroup; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_pwLastTopVisibleGroup = value;
			}
		}

		public bool RecycleBinEnabled
		{
			get { return m_bUseRecycleBin; }
			set { m_bUseRecycleBin = value; }
		}

		public PwUuid RecycleBinUuid
		{
			get { return m_pwRecycleBin; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_pwRecycleBin = value;
			}
		}

		public DateTime RecycleBinChanged
		{
			get { return m_dtRecycleBinChanged; }
			set { m_dtRecycleBinChanged = value; }
		}

		/// <summary>
		/// UUID of the group containing template entries. May be
		/// <c>PwUuid.Zero</c>, if no entry templates group has been specified.
		/// </summary>
		public PwUuid EntryTemplatesGroup
		{
			get { return m_pwEntryTemplatesGroup; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_pwEntryTemplatesGroup = value;
			}
		}

		public DateTime EntryTemplatesGroupChanged
		{
			get { return m_dtEntryTemplatesChanged; }
			set { m_dtEntryTemplatesChanged = value; }
		}

		public int HistoryMaxItems
		{
			get { return m_nHistoryMaxItems; }
			set { m_nHistoryMaxItems = value; }
		}

		public long HistoryMaxSize
		{
			get { return m_lHistoryMaxSize; }
			set { m_lHistoryMaxSize = value; }
		}

		/// <summary>
		/// Custom data container that can be used by plugins to store
		/// own data in KeePass databases.
		/// </summary>
		public StringDictionaryEx CustomData
		{
			get { return m_vCustomData; }
			set
			{
				Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");
				m_vCustomData = value;
			}
		}

		/// <summary>
		/// Hash value of the primary file on disk (last read or last write).
		/// A call to <c>SaveAs</c> without making the saved file primary will
		/// not change this hash. May be <c>null</c>.
		/// </summary>
		public byte[] HashOfFileOnDisk
		{
			get { return m_pbHashOfFileOnDisk; }
		}

		public byte[] HashOfLastIO
		{
			get { return m_pbHashOfLastIO; }
		}

		public bool UseFileTransactions
		{
			get { return m_bUseFileTransactions; }
			set { m_bUseFileTransactions = value; }
		}

		public bool UseFileLocks
		{
			get { return m_bUseFileLocks; }
			set { m_bUseFileLocks = value; }
		}

		private string m_strDetachBins = null;
		/// <summary>
		/// Detach binaries when opening a file. If this isn't <c>null</c>,
		/// all binaries are saved to the specified path and are removed
		/// from the database.
		/// </summary>
		public string DetachBinaries
		{
			get { return m_strDetachBins; }
			set { m_strDetachBins = value; }
		}

		/// <summary>
		/// Localized application name.
		/// </summary>
		public static string LocalizedAppName
		{
			get { return m_strLocalizedAppName; }
			set { Debug.Assert(value != null); m_strLocalizedAppName = value; }
		}

		/// <summary>
		/// Constructs an empty password manager object.
		/// </summary>
		public PwDatabase()
		{
			if(m_bPrimaryCreated == false) m_bPrimaryCreated = true;

			Clear();
		}

		private void Clear()
		{
			m_pgRootGroup = null;
			m_vDeletedObjects = new PwObjectList<PwDeletedObject>();

			m_uuidDataCipher = StandardAesEngine.AesUuid;
			m_caCompression = PwCompressionAlgorithm.GZip;
			m_uKeyEncryptionRounds = PwDefs.DefaultKeyEncryptionRounds;

			m_pwUserKey = null;
			m_memProtConfig = new MemoryProtectionConfig();

			m_vCustomIcons = new List<PwCustomIcon>();
			m_bUINeedsIconUpdate = true;

			DateTime dtNow = DateTime.Now;

			m_strName = string.Empty;
			m_dtNameChanged = dtNow;
			m_strDesc = string.Empty;
			m_dtDescChanged = dtNow;
			m_strDefaultUserName = string.Empty;
			m_dtDefaultUserChanged = dtNow;
			m_uMntncHistoryDays = 365;
			m_clr = Color.Empty;

			m_dtKeyLastChanged = dtNow;
			m_lKeyChangeRecDays = -1;
			m_lKeyChangeForceDays = -1;

			m_ioSource = new IOConnectionInfo();
			m_bDatabaseOpened = false;
			m_bModified = false;

			m_pwLastSelectedGroup = PwUuid.Zero;
			m_pwLastTopVisibleGroup = PwUuid.Zero;

			m_bUseRecycleBin = true;
			m_pwRecycleBin = PwUuid.Zero;
			m_dtRecycleBinChanged = dtNow;
			m_pwEntryTemplatesGroup = PwUuid.Zero;
			m_dtEntryTemplatesChanged = dtNow;

			m_nHistoryMaxItems = DefaultHistoryMaxItems;
			m_lHistoryMaxSize = DefaultHistoryMaxSize;

			m_vCustomData = new StringDictionaryEx();

			m_pbHashOfFileOnDisk = null;
			m_pbHashOfLastIO = null;

			m_bUseFileTransactions = false;
			m_bUseFileLocks = false;
		}

		/// <summary>
		/// Initialize the class for managing a new database. Previously loaded
		/// data is deleted.
		/// </summary>
		/// <param name="ioConnection">IO connection of the new database.</param>
		/// <param name="pwKey">Key to open the database.</param>
		public void New(IOConnectionInfo ioConnection, CompositeKey pwKey)
		{
			Debug.Assert(ioConnection != null);
			if(ioConnection == null) throw new ArgumentNullException("ioConnection");
			Debug.Assert(pwKey != null);
			if(pwKey == null) throw new ArgumentNullException("pwKey");

			Close();

			m_ioSource = ioConnection;
			m_pwUserKey = pwKey;

			m_bDatabaseOpened = true;
			m_bModified = true;

			m_pgRootGroup = new PwGroup(true, true,
				UrlUtil.StripExtension(UrlUtil.GetFileName(ioConnection.Path)),
				PwIcon.FolderOpen);
			m_pgRootGroup.IsExpanded = true;
		}

		/// <summary>
		/// Open a database. The URL may point to any supported data source.
		/// </summary>
		/// <param name="ioSource">IO connection to load the database from.</param>
		/// <param name="pwKey">Key used to open the specified database.</param>
		/// <param name="slLogger">Logger, which gets all status messages.</param>
		public void Open(IOConnectionInfo ioSource, CompositeKey pwKey,
			IStatusLogger slLogger)
		{
			Debug.Assert(ioSource != null);
			if(ioSource == null) throw new ArgumentNullException("ioSource");
			Debug.Assert(pwKey != null);
			if(pwKey == null) throw new ArgumentNullException("pwKey");

			Close();

			try
			{
				m_pgRootGroup = new PwGroup(true, true, UrlUtil.StripExtension(
					UrlUtil.GetFileName(ioSource.Path)), PwIcon.FolderOpen);
				m_pgRootGroup.IsExpanded = true;

				m_pwUserKey = pwKey;

				m_bModified = false;

				KdbxFile kdbx = new KdbxFile(this);
				kdbx.DetachBinaries = m_strDetachBins;

				Stream s = IOConnection.OpenRead(ioSource);
				kdbx.Load(s, KdbxFormat.Default, slLogger);
				s.Close();

				m_pbHashOfLastIO = kdbx.HashOfFileOnDisk;
				m_pbHashOfFileOnDisk = kdbx.HashOfFileOnDisk;
				Debug.Assert(m_pbHashOfFileOnDisk != null);

				m_bDatabaseOpened = true;
				m_ioSource = ioSource;
			}
			catch(Exception)
			{
				Clear();
				throw;
			}
		}

		/// <summary>
		/// Save the currently opened database. The file is written to the location
		/// it has been opened from.
		/// </summary>
		/// <param name="slLogger">Logger that recieves status information.</param>
		public void Save(IStatusLogger slLogger)
		{
			Debug.Assert(ValidateUuidUniqueness());

			FileLock fl = null;
			if(m_bUseFileLocks) fl = new FileLock(m_ioSource);
			try
			{
				FileTransactionEx ft = new FileTransactionEx(m_ioSource,
					m_bUseFileTransactions);
				Stream s = ft.OpenWrite();

				KdbxFile kdb = new KdbxFile(this);
				kdb.Save(s, null, KdbxFormat.Default, slLogger);

				ft.CommitWrite();

				m_pbHashOfLastIO = kdb.HashOfFileOnDisk;
				m_pbHashOfFileOnDisk = kdb.HashOfFileOnDisk;
				Debug.Assert(m_pbHashOfFileOnDisk != null);
			}
			finally { if(fl != null) fl.Dispose(); }

			m_bModified = false;
		}

		/// <summary>
		/// Save the currently opened database to a different location. If
		/// <paramref name="bIsPrimaryNow" /> is <c>true</c>, the specified
		/// location is made the default location for future saves
		/// using <c>SaveDatabase</c>.
		/// </summary>
		/// <param name="ioConnection">New location to serialize the database to.</param>
		/// <param name="bIsPrimaryNow">If <c>true</c>, the new location is made the
		/// standard location for the database. If <c>false</c>, a copy of the currently
		/// opened database is saved to the specified location, but it isn't
		/// made the default location (i.e. no lock files will be moved for
		/// example).</param>
		/// <param name="slLogger">Logger that recieves status information.</param>
		public void SaveAs(IOConnectionInfo ioConnection, bool bIsPrimaryNow,
			IStatusLogger slLogger)
		{
			Debug.Assert(ioConnection != null);
			if(ioConnection == null) throw new ArgumentNullException("ioConnection");

			IOConnectionInfo ioCurrent = m_ioSource; // Remember current
			m_ioSource = ioConnection;

			byte[] pbHashCopy = m_pbHashOfFileOnDisk;

			try { this.Save(slLogger); }
			catch(Exception)
			{
				m_ioSource = ioCurrent; // Restore
				m_pbHashOfFileOnDisk = pbHashCopy;

				m_pbHashOfLastIO = null;
				throw;
			}

			if(!bIsPrimaryNow)
			{
				m_ioSource = ioCurrent; // Restore
				m_pbHashOfFileOnDisk = pbHashCopy;
			}
		}

		/// <summary>
		/// Closes the currently opened database. No confirmation message is shown
		/// before closing. Unsaved changes will be lost.
		/// </summary>
		public void Close()
		{
			Clear();
		}

		public void MergeIn(PwDatabase pwSource, PwMergeMethod mm)
		{
			MergeIn(pwSource, mm, null);
		}

		/// <summary>
		/// Synchronize the current database with another one.
		/// </summary>
		/// <param name="pwSource">Input database to synchronize with. This input
		/// database is used to update the current one, but is not modified! You
		/// must copy the current object if you want a second instance of the
		/// synchronized database. The input database must not be seen as valid
		/// database any more after calling <c>Synchronize</c>.</param>
		/// <param name="mm">Merge method.</param>
		/// <param name="slStatus">Logger to report status messages to.
		/// May be <c>null</c>.</param>
		public void MergeIn(PwDatabase pwSource, PwMergeMethod mm,
			IStatusLogger slStatus)
		{
			if(pwSource == null) throw new ArgumentNullException("pwSource");

			PwGroup pgOrgStructure = m_pgRootGroup.CloneStructure();
			PwGroup pgSrcStructure = pwSource.m_pgRootGroup.CloneStructure();

			if(mm == PwMergeMethod.CreateNewUuids)
				pwSource.RootGroup.CreateNewItemUuids(true, true, true);

			GroupHandler gh = delegate(PwGroup pg)
			{
				if(pg == pwSource.m_pgRootGroup) return true;

				PwGroup pgLocal = m_pgRootGroup.FindGroup(pg.Uuid, true);
				if(pgLocal == null)
				{
					PwGroup pgSourceParent = pg.ParentGroup;
					PwGroup pgLocalContainer;
					if(pgSourceParent == pwSource.m_pgRootGroup)
						pgLocalContainer = m_pgRootGroup;
					else
						pgLocalContainer = m_pgRootGroup.FindGroup(pgSourceParent.Uuid, true);
					Debug.Assert(pgLocalContainer != null);
					if(pgLocalContainer == null) pgLocalContainer = m_pgRootGroup;

					PwGroup pgNew = new PwGroup(false, false);
					pgNew.Uuid = pg.Uuid;
					pgNew.AssignProperties(pg, false, true);
					pgLocalContainer.AddGroup(pgNew, true);
				}
				else // pgLocal != null
				{
					Debug.Assert(mm != PwMergeMethod.CreateNewUuids);

					if(mm == PwMergeMethod.OverwriteExisting)
						pgLocal.AssignProperties(pg, false, false);
					else if((mm == PwMergeMethod.OverwriteIfNewer) ||
						(mm == PwMergeMethod.Synchronize))
					{
						pgLocal.AssignProperties(pg, true, false);
					}
					// else if(mm == PwMergeMethod.KeepExisting) ...
				}

				return ((slStatus != null) ? slStatus.ContinueWork() : true);
			};

			EntryHandler eh = delegate(PwEntry pe)
			{
				PwEntry peLocal = m_pgRootGroup.FindEntry(pe.Uuid, true);
				if(peLocal == null)
				{
					PwGroup pgSourceParent = pe.ParentGroup;
					PwGroup pgLocalContainer;
					if(pgSourceParent == pwSource.m_pgRootGroup)
						pgLocalContainer = m_pgRootGroup;
					else
						pgLocalContainer = m_pgRootGroup.FindGroup(pgSourceParent.Uuid, true);
					Debug.Assert(pgLocalContainer != null);
					if(pgLocalContainer == null) pgLocalContainer = m_pgRootGroup;

					PwEntry peNew = new PwEntry(false, false);
					peNew.Uuid = pe.Uuid;
					peNew.AssignProperties(pe, false, true, true);
					pgLocalContainer.AddEntry(peNew, true);
				}
				else // peLocal != null
				{
					Debug.Assert(mm != PwMergeMethod.CreateNewUuids);

					const PwCompareOptions cmpOpt = (PwCompareOptions.IgnoreParentGroup |
						PwCompareOptions.IgnoreLastAccess | PwCompareOptions.IgnoreHistory |
						PwCompareOptions.NullEmptyEquivStd);
					bool bEquals = peLocal.EqualsEntry(pe, cmpOpt, MemProtCmpMode.None);

					bool bOrgBackup = !bEquals;
					if(mm != PwMergeMethod.OverwriteExisting)
						bOrgBackup &= (TimeUtil.CompareLastMod(pe, peLocal, true) > 0);
					bOrgBackup &= !pe.HasBackupOfData(peLocal, false, true);
					if(bOrgBackup) peLocal.CreateBackup(null); // Maintain at end

					bool bSrcBackup = !bEquals && (mm != PwMergeMethod.OverwriteExisting);
					bSrcBackup &= (TimeUtil.CompareLastMod(peLocal, pe, true) > 0);
					bSrcBackup &= !peLocal.HasBackupOfData(pe, false, true);
					if(bSrcBackup) pe.CreateBackup(null); // Maintain at end

					if(mm == PwMergeMethod.OverwriteExisting)
						peLocal.AssignProperties(pe, false, false, false);
					else if((mm == PwMergeMethod.OverwriteIfNewer) ||
						(mm == PwMergeMethod.Synchronize))
					{
						peLocal.AssignProperties(pe, true, false, false);
					}
					// else if(mm == PwMergeMethod.KeepExisting) ...

					MergeEntryHistory(peLocal, pe, mm);
				}

				return ((slStatus != null) ? slStatus.ContinueWork() : true);
			};

			if(!pwSource.RootGroup.TraverseTree(TraversalMethod.PreOrder, gh, eh))
				throw new InvalidOperationException();

			IStatusLogger slPrevStatus = m_slStatus;
			m_slStatus = slStatus;

			if(mm == PwMergeMethod.Synchronize)
			{
				ApplyDeletions(pwSource.m_vDeletedObjects, true);
				ApplyDeletions(m_vDeletedObjects, false);

				PwObjectPool ppOrgGroups = PwObjectPool.FromGroupRecursive(
					pgOrgStructure, false);
				PwObjectPool ppSrcGroups = PwObjectPool.FromGroupRecursive(
					pgSrcStructure, false);
				PwObjectPool ppOrgEntries = PwObjectPool.FromGroupRecursive(
					pgOrgStructure, true);
				PwObjectPool ppSrcEntries = PwObjectPool.FromGroupRecursive(
					pgSrcStructure, true);

				RelocateGroups(ppOrgGroups, ppSrcGroups);
				ReorderGroups(ppOrgGroups, ppSrcGroups);
				RelocateEntries(ppOrgEntries, ppSrcEntries);
				ReorderEntries(ppOrgEntries, ppSrcEntries);
				Debug.Assert(ValidateUuidUniqueness());
			}

			// Must be called *after* merging groups, because group UUIDs
			// are required for recycle bin and entry template UUIDs
			MergeInDbProperties(pwSource, mm);

			MergeInCustomIcons(pwSource);

			MaintainBackups();

			m_slStatus = slPrevStatus;
		}

		private void MergeInCustomIcons(PwDatabase pwSource)
		{
			foreach(PwCustomIcon pwci in pwSource.CustomIcons)
			{
				if(GetCustomIconIndex(pwci.Uuid) >= 0) continue;

				m_vCustomIcons.Add(pwci); // PwCustomIcon is immutable
				m_bUINeedsIconUpdate = true;
			}
		}

		/// <summary>
		/// Apply a list of deleted objects.
		/// </summary>
		/// <param name="listDelObjects">List of deleted objects.</param>
		private void ApplyDeletions(PwObjectList<PwDeletedObject> listDelObjects,
			bool bCopyDeletionInfoToLocal)
		{
			Debug.Assert(listDelObjects != null); if(listDelObjects == null) throw new ArgumentNullException("listDelObjects");

			LinkedList<PwGroup> listGroupsToDelete = new LinkedList<PwGroup>();
			LinkedList<PwEntry> listEntriesToDelete = new LinkedList<PwEntry>();

			GroupHandler gh = delegate(PwGroup pg)
			{
				if(pg == m_pgRootGroup) return true;

				foreach(PwDeletedObject pdo in listDelObjects)
				{
					if(pg.Uuid.Equals(pdo.Uuid))
					{
						if(TimeUtil.Compare(pg.LastModificationTime,
							pdo.DeletionTime, true) < 0)
							listGroupsToDelete.AddLast(pg);
					}
				}

				return ((m_slStatus != null) ? m_slStatus.ContinueWork() : true);
			};

			EntryHandler eh = delegate(PwEntry pe)
			{
				foreach(PwDeletedObject pdo in listDelObjects)
				{
					if(pe.Uuid.Equals(pdo.Uuid))
					{
						if(TimeUtil.Compare(pe.LastModificationTime,
							pdo.DeletionTime, true) < 0)
							listEntriesToDelete.AddLast(pe);
					}
				}

				return ((m_slStatus != null) ? m_slStatus.ContinueWork() : true);
			};

			m_pgRootGroup.TraverseTree(TraversalMethod.PreOrder, gh, eh);

			foreach(PwGroup pg in listGroupsToDelete)
				pg.ParentGroup.Groups.Remove(pg);
			foreach(PwEntry pe in listEntriesToDelete)
				pe.ParentGroup.Entries.Remove(pe);

			if(bCopyDeletionInfoToLocal)
			{
				foreach(PwDeletedObject pdoNew in listDelObjects)
				{
					bool bCopy = true;

					foreach(PwDeletedObject pdoLocal in m_vDeletedObjects)
					{
						if(pdoNew.Uuid.Equals(pdoLocal.Uuid))
						{
							bCopy = false;

							if(pdoNew.DeletionTime > pdoLocal.DeletionTime)
								pdoLocal.DeletionTime = pdoNew.DeletionTime;

							break;
						}
					}

					if(bCopy) m_vDeletedObjects.Add(pdoNew);
				}
			}
		}

		private void RelocateGroups(PwObjectPool ppOrgStructure,
			PwObjectPool ppSrcStructure)
		{
			PwObjectList<PwGroup> vGroups = m_pgRootGroup.GetGroups(true);

			foreach(PwGroup pg in vGroups)
			{
				if((m_slStatus != null) && !m_slStatus.ContinueWork()) break;

				// PwGroup pgOrg = pgOrgStructure.FindGroup(pg.Uuid, true);
				IStructureItem ptOrg = ppOrgStructure.Get(pg.Uuid);
				if(ptOrg == null) continue;
				// PwGroup pgSrc = pgSrcStructure.FindGroup(pg.Uuid, true);
				IStructureItem ptSrc = ppSrcStructure.Get(pg.Uuid);
				if(ptSrc == null) continue;

				PwGroup pgOrgParent = ptOrg.ParentGroup;
				PwGroup pgSrcParent = ptSrc.ParentGroup;
				if(pgOrgParent.Uuid.Equals(pgSrcParent.Uuid))
				{
					pg.LocationChanged = ((ptSrc.LocationChanged > ptOrg.LocationChanged) ?
						ptSrc.LocationChanged : ptOrg.LocationChanged);
					continue;
				}

				if(ptSrc.LocationChanged > ptOrg.LocationChanged)
				{
					PwGroup pgLocal = m_pgRootGroup.FindGroup(pgSrcParent.Uuid, true);
					if(pgLocal == null) { Debug.Assert(false); continue; }

					if(pgLocal.IsContainedIn(pg)) continue;

					pg.ParentGroup.Groups.Remove(pg);
					pgLocal.AddGroup(pg, true);
					pg.LocationChanged = ptSrc.LocationChanged;
				}
				else
				{
					Debug.Assert(pg.ParentGroup.Uuid.Equals(pgOrgParent.Uuid));
					Debug.Assert(pg.LocationChanged == ptOrg.LocationChanged);
				}
			}

			Debug.Assert(m_pgRootGroup.GetGroups(true).UCount == vGroups.UCount);
		}

		private void RelocateEntries(PwObjectPool ppOrgStructure,
			PwObjectPool ppSrcStructure)
		{
			PwObjectList<PwEntry> vEntries = m_pgRootGroup.GetEntries(true);

			foreach(PwEntry pe in vEntries)
			{
				if((m_slStatus != null) && !m_slStatus.ContinueWork()) break;

				// PwEntry peOrg = pgOrgStructure.FindEntry(pe.Uuid, true);
				IStructureItem ptOrg = ppOrgStructure.Get(pe.Uuid);
				if(ptOrg == null) continue;
				// PwEntry peSrc = pgSrcStructure.FindEntry(pe.Uuid, true);
				IStructureItem ptSrc = ppSrcStructure.Get(pe.Uuid);
				if(ptSrc == null) continue;

				PwGroup pgOrg = ptOrg.ParentGroup;
				PwGroup pgSrc = ptSrc.ParentGroup;
				if(pgOrg.Uuid.Equals(pgSrc.Uuid))
				{
					pe.LocationChanged = ((ptSrc.LocationChanged > ptOrg.LocationChanged) ?
						ptSrc.LocationChanged : ptOrg.LocationChanged);
					continue;
				}

				if(ptSrc.LocationChanged > ptOrg.LocationChanged)
				{
					PwGroup pgLocal = m_pgRootGroup.FindGroup(pgSrc.Uuid, true);
					if(pgLocal == null) { Debug.Assert(false); continue; }

					pe.ParentGroup.Entries.Remove(pe);
					pgLocal.AddEntry(pe, true);
					pe.LocationChanged = ptSrc.LocationChanged;
				}
				else
				{
					Debug.Assert(pe.ParentGroup.Uuid.Equals(pgOrg.Uuid));
					Debug.Assert(pe.LocationChanged == ptOrg.LocationChanged);
				}
			}

			Debug.Assert(m_pgRootGroup.GetEntries(true).UCount == vEntries.UCount);
		}

		private void ReorderGroups(PwObjectPool ppOrgStructure,
			PwObjectPool ppSrcStructure)
		{
			GroupHandler gh = delegate(PwGroup pg)
			{
				ReorderObjectList<PwGroup>(pg.Groups, ppOrgStructure,
					ppSrcStructure, false);
				return true;
			};

			ReorderObjectList<PwGroup>(m_pgRootGroup.Groups, ppOrgStructure,
				ppSrcStructure, false);
			m_pgRootGroup.TraverseTree(TraversalMethod.PreOrder, gh, null);
		}

		private void ReorderEntries(PwObjectPool ppOrgStructure,
			PwObjectPool ppSrcStructure)
		{
			GroupHandler gh = delegate(PwGroup pg)
			{
				ReorderObjectList<PwEntry>(pg.Entries, ppOrgStructure,
					ppSrcStructure, true);
				return true;
			};

			ReorderObjectList<PwEntry>(m_pgRootGroup.Entries, ppOrgStructure,
				ppSrcStructure, true);
			m_pgRootGroup.TraverseTree(TraversalMethod.PreOrder, gh, null);
		}

		private void ReorderObjectList<T>(PwObjectList<T> vItems,
			PwObjectPool ppOrgStructure, PwObjectPool ppSrcStructure, bool bEntries)
			where T : class, IStructureItem, IDeepCloneable<T>
		{
			if(!ObjectListRequiresReorder<T>(vItems, ppOrgStructure, ppSrcStructure,
				bEntries)) return;

#if DEBUG
			PwObjectList<T> vOrgListItems = vItems.CloneShallow();
#endif

			Queue<KeyValuePair<uint, uint>> qToDo = new Queue<KeyValuePair<uint, uint>>();
			qToDo.Enqueue(new KeyValuePair<uint, uint>(0, vItems.UCount - 1));

			while(qToDo.Count > 0)
			{
				if((m_slStatus != null) && !m_slStatus.ContinueWork()) break;

				KeyValuePair<uint, uint> kvp = qToDo.Dequeue();
				if(kvp.Value <= kvp.Key) { Debug.Assert(false); continue; }

				Queue<PwUuid> qRelBefore = new Queue<PwUuid>();
				Queue<PwUuid> qRelAfter = new Queue<PwUuid>();
				uint uPivot = FindLocationChangedPivot<T>(vItems, kvp, ppOrgStructure,
					ppSrcStructure, qRelBefore, qRelAfter, bEntries);
				T ptPivot = vItems.GetAt(uPivot);

				List<T> vToSort = vItems.GetRange(kvp.Key, kvp.Value);
				Queue<T> qBefore = new Queue<T>();
				Queue<T> qAfter = new Queue<T>();
				bool bBefore = true;

				foreach(T pt in vToSort)
				{
					if(pt == ptPivot) { bBefore = false; continue; }

					bool bAdded = false;
					foreach(PwUuid puBefore in qRelBefore)
					{
						if(puBefore.Equals(pt.Uuid))
						{
							qBefore.Enqueue(pt);
							bAdded = true;
							break;
						}
					}
					if(bAdded) continue;

					foreach(PwUuid puAfter in qRelAfter)
					{
						if(puAfter.Equals(pt.Uuid))
						{
							qAfter.Enqueue(pt);
							bAdded = true;
							break;
						}
					}
					if(bAdded) continue;

					if(bBefore) qBefore.Enqueue(pt);
					else qAfter.Enqueue(pt);
				}
				Debug.Assert(bBefore == false);

				uint uPos = kvp.Key;
				while(qBefore.Count > 0) vItems.SetAt(uPos++, qBefore.Dequeue());
				vItems.SetAt(uPos++, ptPivot);
				while(qAfter.Count > 0) vItems.SetAt(uPos++, qAfter.Dequeue());
				Debug.Assert(uPos == (kvp.Value + 1));

				int iNewPivot = vItems.IndexOf(ptPivot);
				if((iNewPivot < (int)kvp.Key) || (iNewPivot > (int)kvp.Value))
				{
					Debug.Assert(false);
					continue;
				}

				if((iNewPivot - 1) > (int)kvp.Key)
					qToDo.Enqueue(new KeyValuePair<uint, uint>(kvp.Key,
						(uint)(iNewPivot - 1)));

				if((iNewPivot + 1) < (int)kvp.Value)
					qToDo.Enqueue(new KeyValuePair<uint, uint>((uint)(iNewPivot + 1),
						kvp.Value));
			}

#if DEBUG
			foreach(T ptItem in vOrgListItems)
			{
				Debug.Assert(vItems.IndexOf(ptItem) >= 0);
			}
#endif
		}

		private static uint FindLocationChangedPivot<T>(PwObjectList<T> vItems,
			KeyValuePair<uint, uint> kvpRange, PwObjectPool ppOrgStructure,
			PwObjectPool ppSrcStructure, Queue<PwUuid> qBefore, Queue<PwUuid> qAfter,
			bool bEntries)
			where T : class, IStructureItem, IDeepCloneable<T>
		{
			uint uPosMax = kvpRange.Key;
			DateTime dtMax = DateTime.MinValue;
			List<IStructureItem> vNeighborSrc = null;

			for(uint u = kvpRange.Key; u <= kvpRange.Value; ++u)
			{
				T pt = vItems.GetAt(u);

				// IStructureItem ptOrg = pgOrgStructure.FindObject(pt.Uuid, true, bEntries);
				IStructureItem ptOrg = ppOrgStructure.Get(pt.Uuid);
				if((ptOrg != null) && (ptOrg.LocationChanged > dtMax))
				{
					uPosMax = u;
					dtMax = ptOrg.LocationChanged; // No 'continue'
					vNeighborSrc = ptOrg.ParentGroup.GetObjects(false, bEntries);
				}

				// IStructureItem ptSrc = pgSrcStructure.FindObject(pt.Uuid, true, bEntries);
				IStructureItem ptSrc = ppSrcStructure.Get(pt.Uuid);
				if((ptSrc != null) && (ptSrc.LocationChanged > dtMax))
				{
					uPosMax = u;
					dtMax = ptSrc.LocationChanged; // No 'continue'
					vNeighborSrc = ptSrc.ParentGroup.GetObjects(false, bEntries);
				}
			}

			GetNeighborItems(vNeighborSrc, vItems.GetAt(uPosMax).Uuid, qBefore, qAfter);
			return uPosMax;
		}

		private static void GetNeighborItems(List<IStructureItem> vItems,
			PwUuid pwPivot, Queue<PwUuid> qBefore, Queue<PwUuid> qAfter)
		{
			qBefore.Clear();
			qAfter.Clear();

			// Checks after clearing the queues
			if(vItems == null) { Debug.Assert(false); return; } // No throw

			bool bBefore = true;
			for(int i = 0; i < vItems.Count; ++i)
			{
				PwUuid pw = vItems[i].Uuid;

				if(pw.Equals(pwPivot)) bBefore = false;
				else if(bBefore) qBefore.Enqueue(pw);
				else qAfter.Enqueue(pw);
			}
			Debug.Assert(bBefore == false);
		}

		/// <summary>
		/// Method to check whether a reordering is required. This fast test
		/// allows to skip the reordering routine, resulting in a large
		/// performance increase.
		/// </summary>
		private bool ObjectListRequiresReorder<T>(PwObjectList<T> vItems,
			PwObjectPool ppOrgStructure, PwObjectPool ppSrcStructure, bool bEntries)
			where T : class, IStructureItem, IDeepCloneable<T>
		{
			Debug.Assert(ppOrgStructure.ContainsOnlyType(bEntries ? typeof(PwEntry) : typeof(PwGroup)));
			Debug.Assert(ppSrcStructure.ContainsOnlyType(bEntries ? typeof(PwEntry) : typeof(PwGroup)));
			if(vItems.UCount <= 1) return false;

			if((m_slStatus != null) && !m_slStatus.ContinueWork()) return false;

			T ptFirst = vItems.GetAt(0);
			// IStructureItem ptOrg = pgOrgStructure.FindObject(ptFirst.Uuid, true, bEntries);
			IStructureItem ptOrg = ppOrgStructure.Get(ptFirst.Uuid);
			if(ptOrg == null) return true;
			// IStructureItem ptSrc = pgSrcStructure.FindObject(ptFirst.Uuid, true, bEntries);
			IStructureItem ptSrc = ppSrcStructure.Get(ptFirst.Uuid);
			if(ptSrc == null) return true;

			if(ptFirst.ParentGroup == null) { Debug.Assert(false); return true; }
			PwGroup pgOrgParent = ptOrg.ParentGroup;
			if(pgOrgParent == null) return true; // Root might be in tree
			PwGroup pgSrcParent = ptSrc.ParentGroup;
			if(pgSrcParent == null) return true; // Root might be in tree

			if(!ptFirst.ParentGroup.Uuid.Equals(pgOrgParent.Uuid)) return true;
			if(!pgOrgParent.Uuid.Equals(pgSrcParent.Uuid)) return true;

			List<IStructureItem> lOrg = pgOrgParent.GetObjects(false, bEntries);
			List<IStructureItem> lSrc = pgSrcParent.GetObjects(false, bEntries);
			if(vItems.UCount != (uint)lOrg.Count) return true;
			if(lOrg.Count != lSrc.Count) return true;

			for(uint u = 0; u < vItems.UCount; ++u)
			{
				IStructureItem pt = vItems.GetAt(u);
				Debug.Assert(pt.ParentGroup == ptFirst.ParentGroup);

				if(!pt.Uuid.Equals(lOrg[(int)u].Uuid)) return true;
				if(!pt.Uuid.Equals(lSrc[(int)u].Uuid)) return true;
				if(pt.LocationChanged != lOrg[(int)u].LocationChanged) return true;
				if(pt.LocationChanged != lSrc[(int)u].LocationChanged) return true;
			}

			return false;
		}

		private void MergeInDbProperties(PwDatabase pwSource, PwMergeMethod mm)
		{
			if(pwSource == null) { Debug.Assert(false); return; }
			if((mm == PwMergeMethod.KeepExisting) || (mm == PwMergeMethod.None))
				return;

			bool bForce = (mm == PwMergeMethod.OverwriteExisting);

			if(bForce || (pwSource.m_dtNameChanged > m_dtNameChanged))
			{
				m_strName = pwSource.m_strName;
				m_dtNameChanged = pwSource.m_dtNameChanged;
			}

			if(bForce || (pwSource.m_dtDescChanged > m_dtDescChanged))
			{
				m_strDesc = pwSource.m_strDesc;
				m_dtDescChanged = pwSource.m_dtDescChanged;
			}

			if(bForce || (pwSource.m_dtDefaultUserChanged > m_dtDefaultUserChanged))
			{
				m_strDefaultUserName = pwSource.m_strDefaultUserName;
				m_dtDefaultUserChanged = pwSource.m_dtDefaultUserChanged;
			}

			if(bForce) m_clr = pwSource.m_clr;

			PwUuid pwPrefBin = m_pwRecycleBin, pwAltBin = pwSource.m_pwRecycleBin;
			if(bForce || (pwSource.m_dtRecycleBinChanged > m_dtRecycleBinChanged))
			{
				pwPrefBin = pwSource.m_pwRecycleBin;
				pwAltBin = m_pwRecycleBin;
				m_bUseRecycleBin = pwSource.m_bUseRecycleBin;
				m_dtRecycleBinChanged = pwSource.m_dtRecycleBinChanged;
			}
			if(m_pgRootGroup.FindGroup(pwPrefBin, true) != null)
				m_pwRecycleBin = pwPrefBin;
			else if(m_pgRootGroup.FindGroup(pwAltBin, true) != null)
				m_pwRecycleBin = pwAltBin;
			else m_pwRecycleBin = PwUuid.Zero; // Debug.Assert(false);

			PwUuid pwPrefTmp = m_pwEntryTemplatesGroup, pwAltTmp = pwSource.m_pwEntryTemplatesGroup;
			if(bForce || (pwSource.m_dtEntryTemplatesChanged > m_dtEntryTemplatesChanged))
			{
				pwPrefTmp = pwSource.m_pwEntryTemplatesGroup;
				pwAltTmp = m_pwEntryTemplatesGroup;
				m_dtEntryTemplatesChanged = pwSource.m_dtEntryTemplatesChanged;
			}
			if(m_pgRootGroup.FindGroup(pwPrefTmp, true) != null)
				m_pwEntryTemplatesGroup = pwPrefTmp;
			else if(m_pgRootGroup.FindGroup(pwAltTmp, true) != null)
				m_pwEntryTemplatesGroup = pwAltTmp;
			else m_pwEntryTemplatesGroup = PwUuid.Zero; // Debug.Assert(false);
		}

		private void MergeEntryHistory(PwEntry pe, PwEntry peSource,
			PwMergeMethod mm)
		{
			if(!pe.Uuid.Equals(peSource.Uuid)) { Debug.Assert(false); return; }

			if(pe.History.UCount == peSource.History.UCount)
			{
				bool bEqual = true;
				for(uint uEnum = 0; uEnum < pe.History.UCount; ++uEnum)
				{
					if(pe.History.GetAt(uEnum).LastModificationTime !=
						peSource.History.GetAt(uEnum).LastModificationTime)
					{
						bEqual = false;
						break;
					}
				}

				if(bEqual) return;
			}

			if((m_slStatus != null) && !m_slStatus.ContinueWork()) return;

			IDictionary<DateTime, PwEntry> dict =
#if KeePassLibSD
				new SortedList<DateTime, PwEntry>();
#else
				new SortedDictionary<DateTime, PwEntry>();
#endif
			foreach(PwEntry peOrg in pe.History)
			{
				dict[peOrg.LastModificationTime] = peOrg;
			}

			foreach(PwEntry peSrc in peSource.History)
			{
				DateTime dt = peSrc.LastModificationTime;
				if(dict.ContainsKey(dt))
				{
					if(mm == PwMergeMethod.OverwriteExisting)
						dict[dt] = peSrc.CloneDeep();
				}
				else dict[dt] = peSrc.CloneDeep();
			}

			pe.History.Clear();
			foreach(KeyValuePair<DateTime, PwEntry> kvpCur in dict)
			{
				Debug.Assert(kvpCur.Value.Uuid.Equals(pe.Uuid));
				Debug.Assert(kvpCur.Value.History.UCount == 0);
				pe.History.Add(kvpCur.Value);
			}
		}

		public bool MaintainBackups()
		{
			if(m_pgRootGroup == null) { Debug.Assert(false); return false; }

			bool bDeleted = false;
			EntryHandler eh = delegate(PwEntry pe)
			{
				if(pe.MaintainBackups(this)) bDeleted = true;
				return true;
			};

			m_pgRootGroup.TraverseTree(TraversalMethod.PreOrder, null, eh);
			return bDeleted;
		}

		/* /// <summary>
		/// Synchronize current database with another one.
		/// </summary>
		/// <param name="strFile">Source file.</param>
		public void Synchronize(string strFile)
		{
			PwDatabase pwSource = new PwDatabase();

			IOConnectionInfo ioc = IOConnectionInfo.FromPath(strFile);
			pwSource.Open(ioc, m_pwUserKey, null);

			MergeIn(pwSource, PwMergeMethod.Synchronize);
		} */

		/// <summary>
		/// Get the index of a custom icon.
		/// </summary>
		/// <param name="pwIconId">ID of the icon.</param>
		/// <returns>Index of the icon.</returns>
		public int GetCustomIconIndex(PwUuid pwIconId)
		{
			for(int i = 0; i < m_vCustomIcons.Count; ++i)
			{
				PwCustomIcon pwci = m_vCustomIcons[i];
				if(pwci.Uuid.Equals(pwIconId))
					return i;
			}

			// Debug.Assert(false); // Do not assert
			return -1;
		}

		public int GetCustomIconIndex(byte[] pbPngData)
		{
			if(pbPngData == null) { Debug.Assert(false); return -1; }

			for(int i = 0; i < m_vCustomIcons.Count; ++i)
			{
				PwCustomIcon pwci = m_vCustomIcons[i];
				byte[] pbEx = pwci.ImageDataPng;
				if(pbEx == null) { Debug.Assert(false); continue; }

				if(MemUtil.ArraysEqual(pbEx, pbPngData))
					return i;
			}

			return -1;
		}

		/// <summary>
		/// Get a custom icon. This function can return <c>null</c>, if
		/// no cached image of the icon is available.
		/// </summary>
		/// <param name="pwIconId">ID of the icon.</param>
		/// <returns>Image data.</returns>
		public Image GetCustomIcon(PwUuid pwIconId)
		{
			int nIndex = GetCustomIconIndex(pwIconId);

			if(nIndex >= 0) return m_vCustomIcons[nIndex].Image;
			else { Debug.Assert(false); return null; }
		}

		public bool DeleteCustomIcons(List<PwUuid> vUuidsToDelete)
		{
			Debug.Assert(vUuidsToDelete != null);
			if(vUuidsToDelete == null) throw new ArgumentNullException("vUuidsToDelete");
			if(vUuidsToDelete.Count <= 0) return true;

			GroupHandler gh = delegate(PwGroup pg)
			{
				PwUuid uuidThis = pg.CustomIconUuid;
				if(uuidThis.Equals(PwUuid.Zero)) return true;

				foreach(PwUuid uuidDelete in vUuidsToDelete)
				{
					if(uuidThis.Equals(uuidDelete))
					{
						pg.CustomIconUuid = PwUuid.Zero;
						break;
					}
				}

				return true;
			};

			EntryHandler eh = delegate(PwEntry pe)
			{
				RemoveCustomIconUuid(pe, vUuidsToDelete);
				return true;
			};

			gh(m_pgRootGroup);
			if(!m_pgRootGroup.TraverseTree(TraversalMethod.PreOrder, gh, eh))
			{
				Debug.Assert(false);
				return false;
			}

			foreach(PwUuid pwUuid in vUuidsToDelete)
			{
				int nIndex = GetCustomIconIndex(pwUuid);
				if(nIndex >= 0) m_vCustomIcons.RemoveAt(nIndex);
			}

			return true;
		}

		private static void RemoveCustomIconUuid(PwEntry pe, List<PwUuid> vToDelete)
		{
			PwUuid uuidThis = pe.CustomIconUuid;
			if(uuidThis.Equals(PwUuid.Zero)) return;

			foreach(PwUuid uuidDelete in vToDelete)
			{
				if(uuidThis.Equals(uuidDelete))
				{
					pe.CustomIconUuid = PwUuid.Zero;
					break;
				}
			}

			foreach(PwEntry peHistory in pe.History)
				RemoveCustomIconUuid(peHistory, vToDelete);
		}

		private bool ValidateUuidUniqueness()
		{
#if DEBUG
			List<PwUuid> l = new List<PwUuid>();
			bool bAllUnique = true;

			GroupHandler gh = delegate(PwGroup pg)
			{
				foreach(PwUuid u in l)
					bAllUnique &= !pg.Uuid.Equals(u);
				l.Add(pg.Uuid);
				return bAllUnique;
			};

			EntryHandler eh = delegate(PwEntry pe)
			{
				foreach(PwUuid u in l)
					bAllUnique &= !pe.Uuid.Equals(u);
				l.Add(pe.Uuid);
				return bAllUnique;
			};

			m_pgRootGroup.TraverseTree(TraversalMethod.PreOrder, gh, eh);
			return bAllUnique;
#else
			return true;
#endif
		}

		/* public void CreateBackupFile(IStatusLogger sl)
		{
			if(sl != null) sl.SetText(KLRes.CreatingBackupFile, LogStatusType.Info);

			IOConnectionInfo iocBk = m_ioSource.CloneDeep();
			iocBk.Path += StrBackupExtension;

			bool bMadeUnhidden = UrlUtil.UnhideFile(iocBk.Path);

			bool bFastCopySuccess = false;
			if(m_ioSource.IsLocalFile() && (m_ioSource.UserName.Length == 0) &&
				(m_ioSource.Password.Length == 0))
			{
				try
				{
					string strFile = m_ioSource.Path + StrBackupExtension;
					File.Copy(m_ioSource.Path, strFile, true);
					bFastCopySuccess = true;
				}
				catch(Exception) { Debug.Assert(false); }
			}

			if(bFastCopySuccess == false)
			{
				using(Stream sIn = IOConnection.OpenRead(m_ioSource))
				{
					using(Stream sOut = IOConnection.OpenWrite(iocBk))
					{
						MemUtil.CopyStream(sIn, sOut);
						sOut.Close();
					}

					sIn.Close();
				}
			}

			if(bMadeUnhidden) UrlUtil.HideFile(iocBk.Path, true); // Hide again
		} */

		/* private static void RemoveData(PwGroup pg)
		{
			EntryHandler eh = delegate(PwEntry pe)
			{
				pe.AutoType.Clear();
				pe.Binaries.Clear();
				pe.History.Clear();
				pe.Strings.Clear();
				return true;
			};

			pg.TraverseTree(TraversalMethod.PreOrder, null, eh);
		} */

		public uint DeleteDuplicateEntries(IStatusLogger sl)
		{
			uint uDeleted = 0;

			PwGroup pgRecycleBin = null;
			if(m_bUseRecycleBin)
				pgRecycleBin = m_pgRootGroup.FindGroup(m_pwRecycleBin, true);

			DateTime dtNow = DateTime.Now;
			PwObjectList<PwEntry> l = m_pgRootGroup.GetEntries(true);
			int i = 0;
			while(true)
			{
				if(i >= ((int)l.UCount - 1)) break;

				if(sl != null)
				{
					long lCnt = (long)l.UCount, li = (long)i;
					long nArTotal = (lCnt * lCnt) / 2L;
					long nArCur = li * lCnt - ((li * li) / 2L);
					long nArPct = (nArCur * 100L) / nArTotal;
					if(nArPct < 0) nArPct = 0;
					if(nArPct > 100) nArPct = 100;
					if(!sl.SetProgress((uint)nArPct)) break;
				}

				PwEntry peA = l.GetAt((uint)i);

				for(uint j = (uint)i + 1; j < l.UCount; ++j)
				{
					PwEntry peB = l.GetAt(j);
					if(!DupEntriesEqual(peA, peB)) continue;

					bool bDeleteA = (TimeUtil.CompareLastMod(peA, peB, true) <= 0);
					if(pgRecycleBin != null)
					{
						bool bAInBin = peA.IsContainedIn(pgRecycleBin);
						bool bBInBin = peB.IsContainedIn(pgRecycleBin);

						if(bAInBin && !bBInBin) bDeleteA = true;
						else if(bBInBin && !bAInBin) bDeleteA = false;
					}

					if(bDeleteA)
					{
						peA.ParentGroup.Entries.Remove(peA);
						m_vDeletedObjects.Add(new PwDeletedObject(peA.Uuid, dtNow));

						l.RemoveAt((uint)i);
						--i;
					}
					else
					{
						peB.ParentGroup.Entries.Remove(peB);
						m_vDeletedObjects.Add(new PwDeletedObject(peB.Uuid, dtNow));

						l.RemoveAt(j);
					}

					++uDeleted;
					break;
				}

				++i;
			}

			return uDeleted;
		}

		private static List<string> m_lStdFields = null;
		private static bool DupEntriesEqual(PwEntry a, PwEntry b)
		{
			if(m_lStdFields == null) m_lStdFields = PwDefs.GetStandardFields();

			foreach(string strStdKey in m_lStdFields)
			{
				string strA = a.Strings.ReadSafe(strStdKey);
				string strB = b.Strings.ReadSafe(strStdKey);
				if(!strA.Equals(strB)) return false;
			}

			foreach(KeyValuePair<string, ProtectedString> kvpA in a.Strings)
			{
				if(PwDefs.IsStandardField(kvpA.Key)) continue;

				ProtectedString psB = b.Strings.Get(kvpA.Key);
				if(psB == null) return false;

				// Ignore protection setting, compare values only
				if(!kvpA.Value.ReadString().Equals(psB.ReadString())) return false;
			}

			foreach(KeyValuePair<string, ProtectedString> kvpB in b.Strings)
			{
				if(PwDefs.IsStandardField(kvpB.Key)) continue;

				ProtectedString psA = a.Strings.Get(kvpB.Key);
				if(psA == null) return false;

				// Must be equal by logic
				Debug.Assert(kvpB.Value.ReadString().Equals(psA.ReadString()));
			}

			if(a.Binaries.UCount != b.Binaries.UCount) return false;
			foreach(KeyValuePair<string, ProtectedBinary> kvpBin in a.Binaries)
			{
				ProtectedBinary pbB = b.Binaries.Get(kvpBin.Key);
				if(pbB == null) return false;

				// Ignore protection setting, compare values only
				byte[] pbDataA = kvpBin.Value.ReadData();
				byte[] pbDataB = pbB.ReadData();
				bool bBinEq = MemUtil.ArraysEqual(pbDataA, pbDataB);
				MemUtil.ZeroByteArray(pbDataA);
				MemUtil.ZeroByteArray(pbDataB);
				if(!bBinEq) return false;
			}

			return true;
		}

		public uint DeleteEmptyGroups()
		{
			uint uDeleted = 0;

			PwObjectList<PwGroup> l = m_pgRootGroup.GetGroups(true);
			int iStart = (int)l.UCount - 1;
			for(int i = iStart; i >= 0; --i)
			{
				PwGroup pg = l.GetAt((uint)i);
				if((pg.Groups.UCount > 0) || (pg.Entries.UCount > 0)) continue;

				pg.ParentGroup.Groups.Remove(pg);
				m_vDeletedObjects.Add(new PwDeletedObject(pg.Uuid, DateTime.Now));

				++uDeleted;
			}

			return uDeleted;
		}

		public uint DeleteUnusedCustomIcons()
		{
			List<PwUuid> lToDelete = new List<PwUuid>();
			foreach(PwCustomIcon pwci in m_vCustomIcons)
				lToDelete.Add(pwci.Uuid);

			GroupHandler gh = delegate(PwGroup pg)
			{
				PwUuid pwUuid = pg.CustomIconUuid;
				if((pwUuid == null) || pwUuid.Equals(PwUuid.Zero)) return true;

				for(int i = 0; i < lToDelete.Count; ++i)
				{
					if(lToDelete[i].Equals(pwUuid))
					{
						lToDelete.RemoveAt(i);
						break;
					}
				}

				return true;
			};

			EntryHandler eh = delegate(PwEntry pe)
			{
				PwUuid pwUuid = pe.CustomIconUuid;
				if((pwUuid == null) || pwUuid.Equals(PwUuid.Zero)) return true;

				for(int i = 0; i < lToDelete.Count; ++i)
				{
					if(lToDelete[i].Equals(pwUuid))
					{
						lToDelete.RemoveAt(i);
						break;
					}
				}

				return true;
			};

			gh(m_pgRootGroup);
			m_pgRootGroup.TraverseTree(TraversalMethod.PreOrder, gh, eh);

			uint uDeleted = 0;
			foreach(PwUuid pwDel in lToDelete)
			{
				int nIndex = GetCustomIconIndex(pwDel);
				if(nIndex < 0) { Debug.Assert(false); continue; }

				m_vCustomIcons.RemoveAt(nIndex);
				++uDeleted;
			}

			if(uDeleted > 0) m_bUINeedsIconUpdate = true;
			return uDeleted;
		}
	}
}
