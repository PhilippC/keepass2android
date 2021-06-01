/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2017 Dominik Reichl <dominik.reichl@t-online.de>

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

#if !KeePassUAP
using System.Drawing;
#endif

using KeePassLib.Collections;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.KeyDerivation;
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

		// Initializations: see Clear()
		private PwGroup m_pgRootGroup = null;
		private PwObjectList<PwDeletedObject> m_vDeletedObjects = new PwObjectList<PwDeletedObject>();

		private PwUuid m_uuidDataCipher = StandardAesEngine.AesUuid;
		private PwCompressionAlgorithm m_caCompression = PwCompressionAlgorithm.GZip;
		// private ulong m_uKeyEncryptionRounds = PwDefs.DefaultKeyEncryptionRounds;
		private KdfParameters m_kdfParams = KdfPool.GetDefaultParameters();

		private CompositeKey m_pwUserKey = null;
		private MemoryProtectionConfig m_memProtConfig = new MemoryProtectionConfig();

		private List<PwCustomIcon> m_vCustomIcons = new List<PwCustomIcon>();
		private bool m_bUINeedsIconUpdate = true;

		private DateTime m_dtSettingsChanged = PwDefs.DtDefaultNow;
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
		private bool m_bKeyChangeForceOnce = false;

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

		private StringDictionaryEx m_dCustomData = new StringDictionaryEx(true);
		private VariantDictionary m_dPublicCustomData = new VariantDictionary();

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

		public DateTime SettingsChanged
		{
			get { return m_dtSettingsChanged; }
			set { m_dtSettingsChanged = value; }
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

		public bool MasterKeyChangeForceOnce
		{
			get { return m_bKeyChangeForceOnce; }
			set { m_bKeyChangeForceOnce = value; }
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

		// /// <summary>
		// /// Number of key transformation rounds (KDF parameter).
		// /// </summary>
		// public ulong KeyEncryptionRounds
		// {
		//	get { return m_uKeyEncryptionRounds; }
		//	set { m_uKeyEncryptionRounds = value; }
		// }

		public KdfParameters KdfParameters
		{
			get { return m_kdfParams; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_kdfParams = value;
			}
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
		/// The data is stored in the encrypted part of encrypted
		/// database files.
		/// Use unique names for your items, e.g. "PluginName_ItemName".
		/// </summary>
		public StringDictionaryEx CustomData
		{
			get { return m_dCustomData; }
			internal set
			{
				if(value == null) { Debug.Assert(false); throw new ArgumentNullException("value"); }
				m_dCustomData = value;
			}
		}

		/// <summary>
		/// Custom data container that can be used by plugins to store
		/// own data in KeePass databases.
		/// The data is stored in the *unencrypted* part of database files,
		/// and it is not supported by all file formats (e.g. supported by KDBX,
		/// unsupported by XML).
		/// It is highly recommended to use <c>CustomData</c> instead,
		/// if possible.
		/// Use unique names for your items, e.g. "PluginName_ItemName".
		/// </summary>
		public VariantDictionary PublicCustomData
		{
			get { return m_dPublicCustomData; }
			internal set
			{
				if(value == null) { Debug.Assert(false); throw new ArgumentNullException("value"); }
				m_dPublicCustomData = value;
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
			set { m_pbHashOfFileOnDisk = value; }
		}

		public byte[] HashOfLastIO
		{
			get { return m_pbHashOfLastIO; }
			set { m_pbHashOfLastIO = value; }
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
			// m_uKeyEncryptionRounds = PwDefs.DefaultKeyEncryptionRounds;
			m_kdfParams = KdfPool.GetDefaultParameters();

			m_pwUserKey = null;
			m_memProtConfig = new MemoryProtectionConfig();

			m_vCustomIcons = new List<PwCustomIcon>();
			m_bUINeedsIconUpdate = true;

			DateTime dtNow = DateTime.UtcNow;

			m_dtSettingsChanged = dtNow;
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
			m_bKeyChangeForceOnce = false;

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

			m_dCustomData = new StringDictionaryEx();
			m_dPublicCustomData = new VariantDictionary();

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
		public void New(IOConnectionInfo ioConnection, CompositeKey pwKey, string filenameWithoutPathAndExt)
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

			m_pgRootGroup = new PwGroup(true, true, filenameWithoutPathAndExt,
				PwIcon.FolderOpen);
			m_pgRootGroup.IsExpanded = true;
		}


		/// <summary>
		/// Open a database. The URL may point to any supported data source.
		/// </summary>
		/// <param name="ioSource">IO connection to load the database from.</param>
		/// <param name="pwKey">Key used to open the specified database.</param>
		/// <param name="slLogger">Logger, which gets all status messages.</param>
		public void Open(Stream s, string fileNameWithoutPathAndExt, IOConnectionInfo ioSource, CompositeKey pwKey,
			IStatusLogger slLogger, IDatabaseFormat format)
		{
			Debug.Assert(s != null);
			if (s == null) throw new ArgumentNullException("s");
			Debug.Assert(fileNameWithoutPathAndExt != null);
			if (fileNameWithoutPathAndExt == null) throw new ArgumentException("fileNameWithoutPathAndExt");
			Debug.Assert(pwKey != null);
			Debug.Assert(ioSource != null);
			if(ioSource == null) throw new ArgumentNullException("ioSource");
			Debug.Assert(pwKey != null);
			if(pwKey == null) throw new ArgumentNullException("pwKey");

			Close();

			try
			{
				m_pgRootGroup = new PwGroup(true, true, fileNameWithoutPathAndExt, PwIcon.FolderOpen);
				m_pgRootGroup.IsExpanded = true;

				m_pwUserKey = pwKey;

				m_bModified = false;

				format.PopulateDatabaseFromStream(this, s, slLogger);

				m_pbHashOfLastIO = format.HashOfLastStream;
				m_pbHashOfFileOnDisk = format.HashOfLastStream;	

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
		/// Save the currently opened database. The file is written to the given stream which is expected to be the original location.
		/// </summary>
		/// This allows to save to cloud locations etc. 
		public void Save(Stream streamOfOriginalLocation, IStatusLogger slLogger)
		{
			Stream s = streamOfOriginalLocation;
			KdbxFile kdb = new KdbxFile(this);
			kdb.Save(s, null, KdbxFormat.Default, slLogger);

			m_pbHashOfLastIO = kdb.HashOfFileOnDisk;
			m_pbHashOfFileOnDisk = kdb.HashOfFileOnDisk;
			Debug.Assert(m_pbHashOfFileOnDisk != null);
			m_bModified = false;
		}


		/// <summary>
		/// Closes the currently opened database. No confirmation message is shown
		/// before closing. Unsaved changes will be lost.
		/// </summary>
		public void Close()
		{
			Clear();
		}

		public void MergeIn(PwDatabase pdSource, PwMergeMethod mm)
		{
			MergeIn(pdSource, mm, null);
		}

		public void MergeIn(PwDatabase pdSource, PwMergeMethod mm,
			IStatusLogger slStatus)
		{
			if (pdSource == null) throw new ArgumentNullException("pdSource");

			if (mm == PwMergeMethod.CreateNewUuids)
			{
				pdSource.RootGroup.Uuid = new PwUuid(true);
				pdSource.RootGroup.CreateNewItemUuids(true, true, true);
			}

			// PwGroup pgOrgStructure = m_pgRootGroup.CloneStructure();
			// PwGroup pgSrcStructure = pdSource.RootGroup.CloneStructure();
			// Later in case 'if(mm == PwMergeMethod.Synchronize)':
			// PwObjectPoolEx ppOrg = PwObjectPoolEx.FromGroup(pgOrgStructure);
			// PwObjectPoolEx ppSrc = PwObjectPoolEx.FromGroup(pgSrcStructure);

			PwObjectPoolEx ppOrg = PwObjectPoolEx.FromGroup(m_pgRootGroup);
			PwObjectPoolEx ppSrc = PwObjectPoolEx.FromGroup(pdSource.RootGroup);

			GroupHandler ghSrc = delegate (PwGroup pg)
			{
				// if(pg == pdSource.m_pgRootGroup) return true;

				// Do not use ppOrg for finding the group, because new groups
				// might have been added (which are not in the pool, and the
				// pool should not be modified)
				PwGroup pgLocal = m_pgRootGroup.FindGroup(pg.Uuid, true);

				if (pgLocal == null)
				{
					PwGroup pgSourceParent = pg.ParentGroup;
					PwGroup pgLocalContainer;
					if (pgSourceParent == null)
					{
						// pg is the root group of pdSource, and no corresponding
						// local group was found; create the group within the
						// local root group
						Debug.Assert(pg == pdSource.m_pgRootGroup);
						pgLocalContainer = m_pgRootGroup;
					}
					else if (pgSourceParent == pdSource.m_pgRootGroup)
						pgLocalContainer = m_pgRootGroup;
					else
						pgLocalContainer = m_pgRootGroup.FindGroup(pgSourceParent.Uuid, true);
					Debug.Assert(pgLocalContainer != null);
					if (pgLocalContainer == null) pgLocalContainer = m_pgRootGroup;

					PwGroup pgNew = new PwGroup(false, false);
					pgNew.Uuid = pg.Uuid;
					pgNew.AssignProperties(pg, false, true);

					if (!pgLocalContainer.CanAddGroup(pgNew))
					{
						Debug.Assert(false);
						pgLocalContainer = m_pgRootGroup;
						pgLocalContainer.CheckCanAddGroup(pgNew);
					}
					// pgLocalContainer.AddGroup(pgNew, true);
					InsertObjectAtBestPos<PwGroup>(pgLocalContainer.Groups, pgNew, ppSrc);
					pgNew.ParentGroup = pgLocalContainer;
				}
				else // pgLocal != null
				{
					Debug.Assert(mm != PwMergeMethod.CreateNewUuids);

					if (mm == PwMergeMethod.OverwriteExisting)
						pgLocal.AssignProperties(pg, false, false);
					else if ((mm == PwMergeMethod.OverwriteIfNewer) ||
						(mm == PwMergeMethod.Synchronize))
					{
						pgLocal.AssignProperties(pg, true, false);
					}
					// else if(mm == PwMergeMethod.KeepExisting) ...
				}

				return ((slStatus != null) ? slStatus.ContinueWork() : true);
			};

			EntryHandler ehSrc = delegate (PwEntry pe)
			{
				// PwEntry peLocal = m_pgRootGroup.FindEntry(pe.Uuid, true);
				PwEntry peLocal = (ppOrg.GetItemByUuid(pe.Uuid) as PwEntry);
				Debug.Assert(object.ReferenceEquals(peLocal,
					m_pgRootGroup.FindEntry(pe.Uuid, true)));

				if (peLocal == null)
				{
					PwGroup pgSourceParent = pe.ParentGroup;
					PwGroup pgLocalContainer;
					if (pgSourceParent == pdSource.m_pgRootGroup)
						pgLocalContainer = m_pgRootGroup;
					else
						pgLocalContainer = m_pgRootGroup.FindGroup(pgSourceParent.Uuid, true);
					Debug.Assert(pgLocalContainer != null);
					if (pgLocalContainer == null) pgLocalContainer = m_pgRootGroup;

					PwEntry peNew = new PwEntry(false, false);
					peNew.Uuid = pe.Uuid;
					peNew.AssignProperties(pe, false, true, true);

					// pgLocalContainer.AddEntry(peNew, true);
					InsertObjectAtBestPos<PwEntry>(pgLocalContainer.Entries, peNew, ppSrc);
					peNew.ParentGroup = pgLocalContainer;
				}
				else // peLocal != null
				{
					Debug.Assert(mm != PwMergeMethod.CreateNewUuids);

					const PwCompareOptions cmpOpt = (PwCompareOptions.IgnoreParentGroup |
						PwCompareOptions.IgnoreLastAccess | PwCompareOptions.IgnoreHistory |
						PwCompareOptions.NullEmptyEquivStd);
					bool bEquals = peLocal.EqualsEntry(pe, cmpOpt, MemProtCmpMode.None);

					bool bOrgBackup = !bEquals;
					if (mm != PwMergeMethod.OverwriteExisting)
						bOrgBackup &= (TimeUtil.CompareLastMod(pe, peLocal, true) > 0);
					bOrgBackup &= !pe.HasBackupOfData(peLocal, false, true);
					if (bOrgBackup) peLocal.CreateBackup(null); // Maintain at end

					bool bSrcBackup = !bEquals && (mm != PwMergeMethod.OverwriteExisting);
					bSrcBackup &= (TimeUtil.CompareLastMod(peLocal, pe, true) > 0);
					bSrcBackup &= !peLocal.HasBackupOfData(pe, false, true);
					if (bSrcBackup) pe.CreateBackup(null); // Maintain at end

					if (mm == PwMergeMethod.OverwriteExisting)
						peLocal.AssignProperties(pe, false, false, false);
					else if ((mm == PwMergeMethod.OverwriteIfNewer) ||
						(mm == PwMergeMethod.Synchronize))
					{
						peLocal.AssignProperties(pe, true, false, false);
					}
					// else if(mm == PwMergeMethod.KeepExisting) ...

					MergeEntryHistory(peLocal, pe, mm);
				}

				return ((slStatus != null) ? slStatus.ContinueWork() : true);
			};

			ghSrc(pdSource.RootGroup);
			if (!pdSource.RootGroup.TraverseTree(TraversalMethod.PreOrder, ghSrc, ehSrc))
				throw new InvalidOperationException();

			IStatusLogger slPrevStatus = m_slStatus;
			m_slStatus = slStatus;

			if (mm == PwMergeMethod.Synchronize)
			{
				RelocateGroups(ppOrg, ppSrc);
				RelocateEntries(ppOrg, ppSrc);
				ReorderObjects(m_pgRootGroup, ppOrg, ppSrc);

				// After all relocations and reorderings
				MergeInLocationChanged(m_pgRootGroup, ppOrg, ppSrc);
				ppOrg = null; // Pools are now invalid, because the location
				ppSrc = null; // changed times have been merged in
			}

			// Delete *after* relocating, because relocating might empty
			// some groups that are marked for deletion (and objects
			// that weren't relocated yet might prevent the deletion)
			Dictionary<PwUuid, PwDeletedObject> dDel = CreateDeletedObjectsPool();
			if (mm == PwMergeMethod.Synchronize)
				MergeInDeletionInfo(pdSource.m_vDeletedObjects, dDel);
			ApplyDeletions(m_pgRootGroup, dDel);
			// The list and the dictionary should be kept in sync
			Debug.Assert(m_vDeletedObjects.UCount == (uint)dDel.Count);

			// Must be called *after* merging groups, because group UUIDs
			// are required for recycle bin and entry template UUIDs
			MergeInDbProperties(pdSource, mm);

			MergeInCustomIcons(pdSource, dDel);
			Debug.Assert(m_vDeletedObjects.UCount == (uint)dDel.Count);

			MaintainBackups();

			Debug.Assert(!HasDuplicateUuids());
			m_slStatus = slPrevStatus;
		}


		private void MergeInCustomIcons(PwDatabase pdSource,
			Dictionary<PwUuid, PwDeletedObject> dDel)
		{
			bool bIconsMod = false;

			Dictionary<PwUuid, int> d = new Dictionary<PwUuid, int>();
			for (int i = m_vCustomIcons.Count - 1; i >= 0; --i)
				d[m_vCustomIcons[i].Uuid] = i;
			Debug.Assert(d.Count == m_vCustomIcons.Count); // UUIDs unique

			foreach (PwCustomIcon ciS in pdSource.m_vCustomIcons)
			{
				int iT;
				if (d.TryGetValue(ciS.Uuid, out iT))
				{
					PwCustomIcon ciT = m_vCustomIcons[iT];

					DateTime? odtT = ciT.LastModificationTime;
					DateTime? odtS = ciS.LastModificationTime;

					if (odtT.HasValue && odtS.HasValue)
					{
						if (odtT.Value >= odtS.Value) continue;
					}
					else if (odtT.HasValue) continue;
					else if (!odtS.HasValue) continue; // Both no time

					m_vCustomIcons[iT] = ciS.Clone();
				}
				else
				{
					d[ciS.Uuid] = m_vCustomIcons.Count;
					m_vCustomIcons.Add(ciS.Clone());
				}

				bIconsMod = true;
			}

			List<PwDeletedObject> lObsoleteDel = new List<PwDeletedObject>();
			foreach (KeyValuePair<PwUuid, PwDeletedObject> kvpDel in dDel)
			{
				int iT;
				if (d.TryGetValue(kvpDel.Key, out iT))
				{
					PwCustomIcon ci = m_vCustomIcons[iT];
					if (ci == null) { Debug.Assert(false); continue; } // Dup. del. obj.?

					DateTime? odt = ci.LastModificationTime;

					if (odt.HasValue && (odt.Value > kvpDel.Value.DeletionTime))
						lObsoleteDel.Add(kvpDel.Value);
					else
					{
						m_vCustomIcons[iT] = null; // Preserve indices, removed below
						bIconsMod = true;
					}
				}
			}

			Predicate<PwCustomIcon> f = delegate (PwCustomIcon ci) { return (ci == null); };
			m_vCustomIcons.RemoveAll(f);

			foreach (PwDeletedObject pdo in lObsoleteDel)
			{
				// Prevent future deletion attempts
				if (!m_vDeletedObjects.Remove(pdo)) { Debug.Assert(false); }
				if (!dDel.Remove(pdo.Uuid)) { Debug.Assert(false); }
			}

			if (bIconsMod) m_bUINeedsIconUpdate = true;

			FixCustomIconRefs();
		}

		private Dictionary<PwUuid, PwDeletedObject> CreateDeletedObjectsPool()
		{
			Dictionary<PwUuid, PwDeletedObject> d =
				new Dictionary<PwUuid, PwDeletedObject>();

			int n = (int)m_vDeletedObjects.UCount;
			for(int i = n - 1; i >= 0; --i)
			{
				PwDeletedObject pdo = m_vDeletedObjects.GetAt((uint)i);

				PwDeletedObject pdoEx;
				if(d.TryGetValue(pdo.Uuid, out pdoEx))
				{
					Debug.Assert(false); // Found duplicate, which should not happen

					if(pdo.DeletionTime > pdoEx.DeletionTime)
						pdoEx.DeletionTime = pdo.DeletionTime;

					m_vDeletedObjects.RemoveAt((uint)i);
				}
				else d[pdo.Uuid] = pdo;
			}

			return d;
		}

		private void MergeInDeletionInfo(PwObjectList<PwDeletedObject> lSrc,
			Dictionary<PwUuid, PwDeletedObject> dOrgDel)
		{
			foreach(PwDeletedObject pdoSrc in lSrc)
			{
				PwDeletedObject pdoOrg;
				if(dOrgDel.TryGetValue(pdoSrc.Uuid, out pdoOrg)) // Update
				{
					Debug.Assert(pdoOrg.Uuid.Equals(pdoSrc.Uuid));

					if(pdoSrc.DeletionTime > pdoOrg.DeletionTime)
						pdoOrg.DeletionTime = pdoSrc.DeletionTime;
				}
				else // Add
				{
					m_vDeletedObjects.Add(pdoSrc);
					dOrgDel[pdoSrc.Uuid] = pdoSrc;
				}
			}
		}

		private void ApplyDeletions<T>(PwObjectList<T> l, Predicate<T> fCanDelete,
			Dictionary<PwUuid, PwDeletedObject> dOrgDel)
			where T : class, ITimeLogger, IStructureItem, IDeepCloneable<T>
		{
			int n = (int)l.UCount;
			for(int i = n - 1; i >= 0; --i)
			{
				if((m_slStatus != null) && !m_slStatus.ContinueWork()) break;

				T t = l.GetAt((uint)i);

				PwDeletedObject pdo;
				if(dOrgDel.TryGetValue(t.Uuid, out pdo))
				{
					Debug.Assert(t.Uuid.Equals(pdo.Uuid));

					bool bDel = (TimeUtil.Compare(t.LastModificationTime,
						pdo.DeletionTime, true) < 0);
					bDel &= fCanDelete(t);

					if(bDel) l.RemoveAt((uint)i);
					else
					{
						// Prevent future deletion attempts; this also prevents
						// delayed deletions (emptying a group could cause a
						// group to be deleted, if the deletion was prevented
						// before due to the group not being empty)
						if(!m_vDeletedObjects.Remove(pdo)) { Debug.Assert(false); }
						if(!dOrgDel.Remove(pdo.Uuid)) { Debug.Assert(false); }
					}
				}
			}
		}

		private static bool SafeCanDeleteGroup(PwGroup pg)
		{
			if(pg == null) { Debug.Assert(false); return false; }

			if(pg.Groups.UCount > 0) return false;
			if(pg.Entries.UCount > 0) return false;
			return true;
		}

		private static bool SafeCanDeleteEntry(PwEntry pe)
		{
			if(pe == null) { Debug.Assert(false); return false; }

			return true;
		}

		// Apply deletions on all objects in the specified container
		// (but not the container itself), using post-order traversal
		// to avoid implicit deletions;
		// https://sourceforge.net/p/keepass/bugs/1499/
		private void ApplyDeletions(PwGroup pgContainer,
			Dictionary<PwUuid, PwDeletedObject> dOrgDel)
		{
			foreach(PwGroup pg in pgContainer.Groups) // Post-order traversal
			{
				ApplyDeletions(pg, dOrgDel);
			}

			ApplyDeletions<PwGroup>(pgContainer.Groups, PwDatabase.SafeCanDeleteGroup, dOrgDel);
			ApplyDeletions<PwEntry>(pgContainer.Entries, PwDatabase.SafeCanDeleteEntry, dOrgDel);
		}

		private void RelocateGroups(PwObjectPoolEx ppOrg, PwObjectPoolEx ppSrc)
		{
			PwObjectList<PwGroup> vGroups = m_pgRootGroup.GetGroups(true);

			foreach(PwGroup pg in vGroups)
			{
				if((m_slStatus != null) && !m_slStatus.ContinueWork()) break;

				// PwGroup pgOrg = pgOrgStructure.FindGroup(pg.Uuid, true);
				IStructureItem ptOrg = ppOrg.GetItemByUuid(pg.Uuid);
				if(ptOrg == null) continue;
				// PwGroup pgSrc = pgSrcStructure.FindGroup(pg.Uuid, true);
				IStructureItem ptSrc = ppSrc.GetItemByUuid(pg.Uuid);
				if(ptSrc == null) continue;

				PwGroup pgOrgParent = ptOrg.ParentGroup;
				// vGroups does not contain the root group, thus pgOrgParent
				// should not be null
				if(pgOrgParent == null) { Debug.Assert(false); continue; }

				PwGroup pgSrcParent = ptSrc.ParentGroup;
				// pgSrcParent may be null (for the source root group)
				if(pgSrcParent == null) continue;

				if(pgOrgParent.Uuid.Equals(pgSrcParent.Uuid))
				{
					// pg.LocationChanged = ((ptSrc.LocationChanged > ptOrg.LocationChanged) ?
					//	ptSrc.LocationChanged : ptOrg.LocationChanged);
					continue;
				}

				if(ptSrc.LocationChanged > ptOrg.LocationChanged)
				{
					PwGroup pgLocal = m_pgRootGroup.FindGroup(pgSrcParent.Uuid, true);
					if(pgLocal == null) { Debug.Assert(false); continue; }

					if(pgLocal.IsContainedIn(pg)) continue;

					pg.ParentGroup.Groups.Remove(pg);

					// pgLocal.AddGroup(pg, true);
					InsertObjectAtBestPos<PwGroup>(pgLocal.Groups, pg, ppSrc);
					pg.ParentGroup = pgLocal;

					// pg.LocationChanged = ptSrc.LocationChanged;
				}
				else
				{
					Debug.Assert(pg.ParentGroup.Uuid.Equals(pgOrgParent.Uuid));
					Debug.Assert(pg.LocationChanged == ptOrg.LocationChanged);
				}
			}

			Debug.Assert(m_pgRootGroup.GetGroups(true).UCount == vGroups.UCount);
		}

		private void RelocateEntries(PwObjectPoolEx ppOrg, PwObjectPoolEx ppSrc)
		{
			PwObjectList<PwEntry> vEntries = m_pgRootGroup.GetEntries(true);

			foreach(PwEntry pe in vEntries)
			{
				if((m_slStatus != null) && !m_slStatus.ContinueWork()) break;

				// PwEntry peOrg = pgOrgStructure.FindEntry(pe.Uuid, true);
				IStructureItem ptOrg = ppOrg.GetItemByUuid(pe.Uuid);
				if(ptOrg == null) continue;
				// PwEntry peSrc = pgSrcStructure.FindEntry(pe.Uuid, true);
				IStructureItem ptSrc = ppSrc.GetItemByUuid(pe.Uuid);
				if(ptSrc == null) continue;

				PwGroup pgOrg = ptOrg.ParentGroup;
				PwGroup pgSrc = ptSrc.ParentGroup;
				if(pgOrg.Uuid.Equals(pgSrc.Uuid))
				{
					// pe.LocationChanged = ((ptSrc.LocationChanged > ptOrg.LocationChanged) ?
					//	ptSrc.LocationChanged : ptOrg.LocationChanged);
					continue;
				}

				if(ptSrc.LocationChanged > ptOrg.LocationChanged)
				{
					PwGroup pgLocal = m_pgRootGroup.FindGroup(pgSrc.Uuid, true);
					if(pgLocal == null) { Debug.Assert(false); continue; }

					pe.ParentGroup.Entries.Remove(pe);

					// pgLocal.AddEntry(pe, true);
					InsertObjectAtBestPos<PwEntry>(pgLocal.Entries, pe, ppSrc);
					pe.ParentGroup = pgLocal;

					// pe.LocationChanged = ptSrc.LocationChanged;
				}
				else
				{
					Debug.Assert(pe.ParentGroup.Uuid.Equals(pgOrg.Uuid));
					Debug.Assert(pe.LocationChanged == ptOrg.LocationChanged);
				}
			}

			Debug.Assert(m_pgRootGroup.GetEntries(true).UCount == vEntries.UCount);
		}

		private void ReorderObjects(PwGroup pg, PwObjectPoolEx ppOrg,
			PwObjectPoolEx ppSrc)
		{
			ReorderObjectList<PwGroup>(pg.Groups, ppOrg, ppSrc);
			ReorderObjectList<PwEntry>(pg.Entries, ppOrg, ppSrc);

			foreach(PwGroup pgSub in pg.Groups)
			{
				ReorderObjects(pgSub, ppOrg, ppSrc);
			}
		}

		private void ReorderObjectList<T>(PwObjectList<T> lItems,
			PwObjectPoolEx ppOrg, PwObjectPoolEx ppSrc)
			where T : class, ITimeLogger, IStructureItem, IDeepCloneable<T>
		{
			List<PwObjectBlock<T>> lBlocks = PartitionConsec(lItems, ppOrg, ppSrc);
			if(lBlocks.Count <= 1) return;

#if DEBUG
			PwObjectList<T> lOrgItems = lItems.CloneShallow();
#endif

			Queue<KeyValuePair<int, int>> qToDo = new Queue<KeyValuePair<int, int>>();
			qToDo.Enqueue(new KeyValuePair<int, int>(0, lBlocks.Count - 1));

			while(qToDo.Count > 0)
			{
				if((m_slStatus != null) && !m_slStatus.ContinueWork()) break;

				KeyValuePair<int, int> kvp = qToDo.Dequeue();
				if(kvp.Key >= kvp.Value) { Debug.Assert(false); continue; }

				PwObjectPoolEx pPool;
				int iPivot = FindLocationChangedPivot(lBlocks, kvp, out pPool);
				PwObjectBlock<T> bPivot = lBlocks[iPivot];

				T tPivotPrimary = bPivot.PrimaryItem;
				if(tPivotPrimary == null) { Debug.Assert(false); continue; }
				ulong idPivot = pPool.GetIdByUuid(tPivotPrimary.Uuid);
				if(idPivot == 0) { Debug.Assert(false); continue; }

				Queue<PwObjectBlock<T>> qBefore = new Queue<PwObjectBlock<T>>();
				Queue<PwObjectBlock<T>> qAfter = new Queue<PwObjectBlock<T>>();
				bool bBefore = true;

				for(int i = kvp.Key; i <= kvp.Value; ++i)
				{
					if(i == iPivot) { bBefore = false; continue; }

					PwObjectBlock<T> b = lBlocks[i];
					Debug.Assert(b.LocationChanged <= bPivot.LocationChanged);

					T t = b.PrimaryItem;
					if(t != null)
					{
						ulong idBPri = pPool.GetIdByUuid(t.Uuid);
						if(idBPri > 0)
						{
							if(idBPri < idPivot) qBefore.Enqueue(b);
							else qAfter.Enqueue(b);

							continue;
						}
					}
					else { Debug.Assert(false); }

					if(bBefore) qBefore.Enqueue(b);
					else qAfter.Enqueue(b);
				}

				int j = kvp.Key;
				while(qBefore.Count > 0) { lBlocks[j] = qBefore.Dequeue(); ++j; }
				int iNewPivot = j;
				lBlocks[j] = bPivot;
				++j;
				while(qAfter.Count > 0) { lBlocks[j] = qAfter.Dequeue(); ++j; }
				Debug.Assert(j == (kvp.Value + 1));

				if((iNewPivot - 1) > kvp.Key)
					qToDo.Enqueue(new KeyValuePair<int, int>(kvp.Key, iNewPivot - 1));
				if((iNewPivot + 1) < kvp.Value)
					qToDo.Enqueue(new KeyValuePair<int, int>(iNewPivot + 1, kvp.Value));
			}

			uint u = 0;
			foreach(PwObjectBlock<T> b in lBlocks)
			{
				foreach(T t in b)
				{
					lItems.SetAt(u, t);
					++u;
				}
			}
			Debug.Assert(u == lItems.UCount);

#if DEBUG
			Debug.Assert(u == lOrgItems.UCount);
			foreach(T ptItem in lOrgItems)
			{
				Debug.Assert(lItems.IndexOf(ptItem) >= 0);
			}
#endif
		}

		private static List<PwObjectBlock<T>> PartitionConsec<T>(PwObjectList<T> lItems,
			PwObjectPoolEx ppOrg, PwObjectPoolEx ppSrc)
			where T : class, ITimeLogger, IStructureItem, IDeepCloneable<T>
		{
			List<PwObjectBlock<T>> lBlocks = new List<PwObjectBlock<T>>();

			Dictionary<PwUuid, bool> dItemUuids = new Dictionary<PwUuid, bool>();
			foreach(T t in lItems) { dItemUuids[t.Uuid] = true; }

			uint n = lItems.UCount;
			for(uint u = 0; u < n; ++u)
			{
				T t = lItems.GetAt(u);

				PwObjectBlock<T> b = new PwObjectBlock<T>();

				DateTime dtLoc;
                PwUuid puPrevParent;
                PwObjectPoolEx pPool = GetBestPool(t, ppOrg, ppSrc, out dtLoc,
                    out puPrevParent);
				b.Add(t, dtLoc, pPool);

				lBlocks.Add(b);

				ulong idOrg = ppOrg.GetIdByUuid(t.Uuid);
				ulong idSrc = ppSrc.GetIdByUuid(t.Uuid);
				if((idOrg == 0) || (idSrc == 0)) continue;

				for(uint x = u + 1; x < n; ++x)
				{
					T tNext = lItems.GetAt(x);

					ulong idOrgNext = idOrg + 1;
					while(true)
					{
						IStructureItem ptOrg = ppOrg.GetItemById(idOrgNext);
						if(ptOrg == null) { idOrgNext = 0; break; }
						if(ptOrg.Uuid.Equals(tNext.Uuid)) break; // Found it
						if(dItemUuids.ContainsKey(ptOrg.Uuid)) { idOrgNext = 0; break; }
						++idOrgNext;
					}
					if(idOrgNext == 0) break;

					ulong idSrcNext = idSrc + 1;
					while(true)
					{
						IStructureItem ptSrc = ppSrc.GetItemById(idSrcNext);
						if(ptSrc == null) { idSrcNext = 0; break; }
						if(ptSrc.Uuid.Equals(tNext.Uuid)) break; // Found it
						if(dItemUuids.ContainsKey(ptSrc.Uuid)) { idSrcNext = 0; break; }
						++idSrcNext;
					}
					if(idSrcNext == 0) break;

					pPool = GetBestPool(tNext, ppOrg, ppSrc, out dtLoc, out puPrevParent);
					b.Add(tNext, dtLoc, pPool);

					++u;
					idOrg = idOrgNext;
					idSrc = idSrcNext;
				}
			}

			return lBlocks;
		}

		private static PwObjectPoolEx GetBestPool<T>(T t, PwObjectPoolEx ppOrg,
			PwObjectPoolEx ppSrc, out DateTime dtLoc, out PwUuid puPrevParent)
			where T : class, ITimeLogger, IStructureItem, IDeepCloneable<T>
		{
            PwObjectPoolEx p = null;
            dtLoc = TimeUtil.SafeMinValueUtc;
            puPrevParent = PwUuid.Zero;

            IStructureItem ptOrg = ppOrg.GetItemByUuid(t.Uuid);
            if (ptOrg != null)
            {
                dtLoc = ptOrg.LocationChanged;
                puPrevParent = ptOrg.PreviousParentGroup;
                p = ppOrg;
            }

            IStructureItem ptSrc = ppSrc.GetItemByUuid(t.Uuid);
            if ((ptSrc != null) && (ptSrc.LocationChanged > dtLoc))
            {
                dtLoc = ptSrc.LocationChanged;
                puPrevParent = ptSrc.PreviousParentGroup;
                p = ppSrc;
            }

            Debug.Assert(p != null);
            return p;
		}

		private static int FindLocationChangedPivot<T>(List<PwObjectBlock<T>> lBlocks,
			KeyValuePair<int, int> kvpRange, out PwObjectPoolEx pPool)
			where T : class, ITimeLogger, IStructureItem, IDeepCloneable<T>
		{
			pPool = null;

			int iPosMax = kvpRange.Key;
			DateTime dtMax = TimeUtil.SafeMinValueUtc;

			for(int i = kvpRange.Key; i <= kvpRange.Value; ++i)
			{
				PwObjectBlock<T> b = lBlocks[i];
				if(b.LocationChanged > dtMax)
				{
					iPosMax = i;
					dtMax = b.LocationChanged;
					pPool = b.PoolAssoc;
				}
			}

			return iPosMax;
		}

        private static void MergeInLocationChanged(PwGroup pg,
            PwObjectPoolEx ppOrg, PwObjectPoolEx ppSrc)
        {
            GroupHandler gh = delegate (PwGroup pgSub)
            {
                DateTime dt;
                PwUuid puPrevParent;
                if (GetBestPool<PwGroup>(pgSub, ppOrg, ppSrc, out dt,
                    out puPrevParent) != null)
                {
                    pgSub.LocationChanged = dt;
                    pgSub.PreviousParentGroup = puPrevParent;
                }
                else { Debug.Assert(false); }
                return true;
            };

            EntryHandler eh = delegate (PwEntry pe)
            {
                DateTime dt;
                PwUuid puPrevParent;
                if (GetBestPool<PwEntry>(pe, ppOrg, ppSrc, out dt,
                    out puPrevParent) != null)
                {
                    pe.LocationChanged = dt;
                    pe.PreviousParentGroup = puPrevParent;
                }
                else { Debug.Assert(false); }
                return true;
            };

            gh(pg);
            pg.TraverseTree(TraversalMethod.PreOrder, gh, eh);
        }

		private static void InsertObjectAtBestPos<T>(PwObjectList<T> lItems,
			T tNew, PwObjectPoolEx ppSrc)
			where T : class, ITimeLogger, IStructureItem, IDeepCloneable<T>
		{
			if(tNew == null) { Debug.Assert(false); return; }

			ulong idSrc = ppSrc.GetIdByUuid(tNew.Uuid);
			if(idSrc == 0) { Debug.Assert(false); lItems.Add(tNew); return; }

			const uint uIdOffset = 2;
			Dictionary<PwUuid, uint> dOrg = new Dictionary<PwUuid, uint>();
			for(uint u = 0; u < lItems.UCount; ++u)
				dOrg[lItems.GetAt(u).Uuid] = uIdOffset + u;

			ulong idSrcNext = idSrc + 1;
			uint idOrgNext = 0;
			while(true)
			{
				IStructureItem pNext = ppSrc.GetItemById(idSrcNext);
				if(pNext == null) break;
				if(dOrg.TryGetValue(pNext.Uuid, out idOrgNext)) break;
				++idSrcNext;
			}

			if(idOrgNext != 0)
			{
				lItems.Insert(idOrgNext - uIdOffset, tNew);
				return;
			}

			ulong idSrcPrev = idSrc - 1;
			uint idOrgPrev = 0;
			while(true)
			{
				IStructureItem pPrev = ppSrc.GetItemById(idSrcPrev);
				if(pPrev == null) break;
				if(dOrg.TryGetValue(pPrev.Uuid, out idOrgPrev)) break;
				--idSrcPrev;
			}

			if(idOrgPrev != 0)
			{
				lItems.Insert(idOrgPrev + 1 - uIdOffset, tNew);
				return;
			}

			lItems.Add(tNew);
		}

		private void MergeInDbProperties(PwDatabase pdSource, PwMergeMethod mm)
		{
			if(pdSource == null) { Debug.Assert(false); return; }
			if((mm == PwMergeMethod.KeepExisting) || (mm == PwMergeMethod.None))
				return;

			bool bForce = (mm == PwMergeMethod.OverwriteExisting);
			bool bSourceNewer = (pdSource.m_dtSettingsChanged > m_dtSettingsChanged);

			if(bForce || bSourceNewer)
			{
				m_dtSettingsChanged = pdSource.m_dtSettingsChanged;

				m_clr = pdSource.m_clr;
			}

			if(bForce || (pdSource.m_dtNameChanged > m_dtNameChanged))
			{
				m_strName = pdSource.m_strName;
				m_dtNameChanged = pdSource.m_dtNameChanged;
			}

			if(bForce || (pdSource.m_dtDescChanged > m_dtDescChanged))
			{
				m_strDesc = pdSource.m_strDesc;
				m_dtDescChanged = pdSource.m_dtDescChanged;
			}

			if(bForce || (pdSource.m_dtDefaultUserChanged > m_dtDefaultUserChanged))
			{
				m_strDefaultUserName = pdSource.m_strDefaultUserName;
				m_dtDefaultUserChanged = pdSource.m_dtDefaultUserChanged;
			}

			PwUuid pwPrefBin = m_pwRecycleBin, pwAltBin = pdSource.m_pwRecycleBin;
			if(bForce || (pdSource.m_dtRecycleBinChanged > m_dtRecycleBinChanged))
			{
				pwPrefBin = pdSource.m_pwRecycleBin;
				pwAltBin = m_pwRecycleBin;
				m_bUseRecycleBin = pdSource.m_bUseRecycleBin;
				m_dtRecycleBinChanged = pdSource.m_dtRecycleBinChanged;
			}
			if(m_pgRootGroup.FindGroup(pwPrefBin, true) != null)
				m_pwRecycleBin = pwPrefBin;
			else if(m_pgRootGroup.FindGroup(pwAltBin, true) != null)
				m_pwRecycleBin = pwAltBin;
			else m_pwRecycleBin = PwUuid.Zero; // Debug.Assert(false);

			PwUuid pwPrefTmp = m_pwEntryTemplatesGroup, pwAltTmp = pdSource.m_pwEntryTemplatesGroup;
			if(bForce || (pdSource.m_dtEntryTemplatesChanged > m_dtEntryTemplatesChanged))
			{
				pwPrefTmp = pdSource.m_pwEntryTemplatesGroup;
				pwAltTmp = m_pwEntryTemplatesGroup;
				m_dtEntryTemplatesChanged = pdSource.m_dtEntryTemplatesChanged;
			}
			if(m_pgRootGroup.FindGroup(pwPrefTmp, true) != null)
				m_pwEntryTemplatesGroup = pwPrefTmp;
			else if(m_pgRootGroup.FindGroup(pwAltTmp, true) != null)
				m_pwEntryTemplatesGroup = pwAltTmp;
			else m_pwEntryTemplatesGroup = PwUuid.Zero; // Debug.Assert(false);

			foreach(KeyValuePair<string, string> kvp in pdSource.m_dCustomData)
			{
				if(bSourceNewer || !m_dCustomData.Exists(kvp.Key))
					m_dCustomData.Set(kvp.Key, kvp.Value, null);
			}

            // 'Clone' duplicates deep values (e.g. byte arrays)
            VariantDictionary vdS = (VariantDictionary)pdSource.m_dPublicCustomData.Clone();
            if (bForce || bSourceNewer)
                vdS.CopyTo(m_dPublicCustomData);
            else
            {
                m_dPublicCustomData.CopyTo(vdS);
                m_dPublicCustomData = vdS;
            }
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
			PwDatabase pdSource = new PwDatabase();

			IOConnectionInfo ioc = IOConnectionInfo.FromPath(strFile);
			pdSource.Open(ioc, m_pwUserKey, null);

			MergeIn(pdSource, PwMergeMethod.Synchronize);
		} */

		/// <summary>
		/// Get the index of a custom icon.
		/// </summary>
		/// <param name="pwIconId">ID of the icon.</param>
		/// <returns>Index of the icon.</returns>
		public int GetCustomIconIndex(PwUuid pwIconId)
		{
            for (int i = 0; i < m_vCustomIcons.Count; ++i)
            {
                PwCustomIcon pwci = m_vCustomIcons[i];
                if (pwci.Uuid.Equals(pwIconId))
                    return i;
            }

			// Debug.Assert(false); // Do not assert
			return -1;
		}

		public int GetCustomIconIndex(byte[] pbPngData)
		{
			if(pbPngData == null) { Debug.Assert(false); return -1; }

            for (int i = 0; i < m_vCustomIcons.Count; ++i)
            {
                PwCustomIcon pwci = m_vCustomIcons[i];
                byte[] pbEx = pwci.ImageDataPng;
                if (pbEx == null) { Debug.Assert(false); continue; }

                if (MemUtil.ArraysEqual(pbEx, pbPngData))
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
		public Android.Graphics.Bitmap GetCustomIcon(PwUuid pwIconId)
		{
			int nIndex = GetCustomIconIndex(pwIconId);

			if(nIndex >= 0) return m_vCustomIcons[nIndex].Image;
			else { Debug.Assert(false); return null; }
		}

        public bool DeleteCustomIcons(List<PwUuid> lUuids)
        {
            if (lUuids == null) { Debug.Assert(false); throw new ArgumentNullException("lUuids"); }
            if (lUuids.Count == 0) return false;

            Dictionary<PwUuid, bool> dToDel = new Dictionary<PwUuid, bool>();
            foreach (PwUuid pu in lUuids) { dToDel[pu] = true; }

            DateTime dt = DateTime.UtcNow;
            for (int i = m_vCustomIcons.Count - 1; i >= 0; --i)
            {
                PwUuid pu = m_vCustomIcons[i].Uuid;
                if (dToDel.ContainsKey(pu))
                {
                    m_vCustomIcons[i] = null; // Removed below
                    m_vDeletedObjects.Add(new PwDeletedObject(pu, dt));
                }
            }

            Predicate<PwCustomIcon> f = delegate (PwCustomIcon ci) { return (ci == null); };
            m_vCustomIcons.RemoveAll(f);

            FixCustomIconRefs();
            return true;
        }

        private void FixCustomIconRefs()
        {
            Dictionary<PwUuid, bool> d = new Dictionary<PwUuid, bool>();
            foreach (PwCustomIcon ci in m_vCustomIcons) { d[ci.Uuid] = true; }

            GroupHandler gh = delegate (PwGroup pg)
            {
                PwUuid pu = pg.CustomIconUuid;
                if (pu.Equals(PwUuid.Zero)) return true;
                if (!d.ContainsKey(pu)) pg.CustomIconUuid = PwUuid.Zero;
                return true;
            };

            EntryHandler eh = delegate (PwEntry pe)
            {
                FixCustomIconRefs(pe, d);
                return true;
            };

            gh(m_pgRootGroup);
            m_pgRootGroup.TraverseTree(TraversalMethod.PreOrder, gh, eh);
        }

        private void FixCustomIconRefs(PwEntry pe, Dictionary<PwUuid, bool> d)
        {
            PwUuid pu = pe.CustomIconUuid;
            if (pu.Equals(PwUuid.Zero)) return;
            if (!d.ContainsKey(pu)) pe.CustomIconUuid = PwUuid.Zero;

            foreach (PwEntry peH in pe.History) FixCustomIconRefs(peH, d);
        }


		private int GetTotalObjectUuidCount()
		{
			uint uGroups, uEntries;
			m_pgRootGroup.GetCounts(true, out uGroups, out uEntries);

			uint uTotal = uGroups + uEntries + 1; // 1 for root group
			if(uTotal > 0x7FFFFFFFU) { Debug.Assert(false); return 0x7FFFFFFF; }
			return (int)uTotal;
		}

		internal bool HasDuplicateUuids()
		{
			int nTotal = GetTotalObjectUuidCount();
			Dictionary<PwUuid, object> d = new Dictionary<PwUuid, object>(nTotal);
			bool bDupFound = false;

			GroupHandler gh = delegate(PwGroup pg)
			{
				PwUuid pu = pg.Uuid;
				if(d.ContainsKey(pu))
				{
					bDupFound = true;
					return false;
				}

				d.Add(pu, null);
				Debug.Assert(d.ContainsKey(pu));
				return true;
			};

			EntryHandler eh = delegate(PwEntry pe)
			{
				PwUuid pu = pe.Uuid;
				if(d.ContainsKey(pu))
				{
					bDupFound = true;
					return false;
				}

				d.Add(pu, null);
				Debug.Assert(d.ContainsKey(pu));
				return true;
			};

			gh(m_pgRootGroup);
			m_pgRootGroup.TraverseTree(TraversalMethod.PreOrder, gh, eh);

			Debug.Assert(bDupFound || (d.Count == nTotal));
			return bDupFound;
		}

		internal void FixDuplicateUuids()
		{
			int nTotal = GetTotalObjectUuidCount();
			Dictionary<PwUuid, object> d = new Dictionary<PwUuid, object>(nTotal);

			GroupHandler gh = delegate(PwGroup pg)
			{
				PwUuid pu = pg.Uuid;
				if(d.ContainsKey(pu))
				{
					pu = new PwUuid(true);
					while(d.ContainsKey(pu)) { Debug.Assert(false); pu = new PwUuid(true); }

					pg.Uuid = pu;
				}

				d.Add(pu, null);
				return true;
			};

			EntryHandler eh = delegate(PwEntry pe)
			{
				PwUuid pu = pe.Uuid;
				if(d.ContainsKey(pu))
				{
					pu = new PwUuid(true);
					while(d.ContainsKey(pu)) { Debug.Assert(false); pu = new PwUuid(true); }

					pe.SetUuid(pu, true);
				}

				d.Add(pu, null);
				return true;
			};

			gh(m_pgRootGroup);
			m_pgRootGroup.TraverseTree(TraversalMethod.PreOrder, gh, eh);

			Debug.Assert(d.Count == nTotal);
			Debug.Assert(!HasDuplicateUuids());
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

			DateTime dtNow = DateTime.UtcNow;
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
				m_vDeletedObjects.Add(new PwDeletedObject(pg.Uuid, DateTime.UtcNow));

				++uDeleted;
			}

			return uDeleted;
		}


		public uint DeleteUnusedCustomIcons()
		{
			Dictionary<PwUuid, bool> dToDel = new Dictionary<PwUuid, bool>();
			foreach (PwCustomIcon ci in m_vCustomIcons) { dToDel[ci.Uuid] = true; }

			GroupHandler gh = delegate (PwGroup pg)
			{
				PwUuid pu = pg.CustomIconUuid;
				if (!pu.Equals(PwUuid.Zero)) dToDel.Remove(pu);
				return true;
			};

			EntryHandler eh = delegate (PwEntry pe)
			{
				RemoveCustomIconsFromDict(dToDel, pe);
				return true;
			};

			gh(m_pgRootGroup);
			m_pgRootGroup.TraverseTree(TraversalMethod.PreOrder, gh, eh);

			uint cDel = (uint)dToDel.Count;
			if (cDel != 0)
			{
				DeleteCustomIcons(new List<PwUuid>(dToDel.Keys));
				m_bUINeedsIconUpdate = true;
			}

			return cDel;
		}

		private static void RemoveCustomIconsFromDict(Dictionary<PwUuid, bool> d,
			PwEntry pe)
		{
			PwUuid pu = pe.CustomIconUuid;
			if (!pu.Equals(PwUuid.Zero)) d.Remove(pu);

			foreach (PwEntry peH in pe.History) RemoveCustomIconsFromDict(d, peH);
		}

		internal static void CopyCustomIcons(PwDatabase pdFrom, PwDatabase pdTo,
			PwGroup pgSelect, bool bResetIfUnknown)
		{
			if (pgSelect == null) { Debug.Assert(false); return; }

			Dictionary<PwUuid, PwCustomIcon> dFrom = new Dictionary<PwUuid, PwCustomIcon>();
			if (pdFrom != null)
			{
				foreach (PwCustomIcon ci in pdFrom.m_vCustomIcons)
					dFrom[ci.Uuid] = ci;
			}

			Dictionary<PwUuid, int> dTo = new Dictionary<PwUuid, int>();
			if (pdTo != null)
			{
				for (int i = pdTo.m_vCustomIcons.Count - 1; i >= 0; --i)
					dTo[pdTo.m_vCustomIcons[i].Uuid] = i;
			}

    		Func<PwUuid, bool> fEnsureIcon = delegate (PwUuid puIcon)
			{
				if (puIcon.Equals(PwUuid.Zero)) return true;
				if (pdTo == null) { Debug.Assert(false); return false; }

				PwCustomIcon ciFrom;
				if (!dFrom.TryGetValue(puIcon, out ciFrom)) { Debug.Assert(false); return false; }

				int iTo;
				if (dTo.TryGetValue(puIcon, out iTo))
				{
					PwCustomIcon ciTo = pdTo.m_vCustomIcons[iTo];

					DateTime? odtFrom = ciFrom.LastModificationTime;
					DateTime? odtTo = ciTo.LastModificationTime;

					if (odtFrom.HasValue && odtTo.HasValue)
					{
						if (odtFrom.Value <= odtTo.Value) return true;
					}
					else if (odtTo.HasValue) return true;
					else if (!odtFrom.HasValue) return true; // Both no time

					pdTo.m_vCustomIcons[iTo] = ciFrom.Clone();
				}
				else
				{
					dTo[puIcon] = pdTo.m_vCustomIcons.Count;
					pdTo.m_vCustomIcons.Add(ciFrom.Clone());
				}

				pdTo.Modified = true;
				pdTo.UINeedsIconUpdate = true;
				return true;
			};

			GroupHandler gh = delegate (PwGroup pgCur)
			{
				bool bTo = fEnsureIcon(pgCur.CustomIconUuid);
				if (!bTo && bResetIfUnknown) pgCur.CustomIconUuid = PwUuid.Zero;
				return true;
			};

			EntryHandler eh = delegate (PwEntry peCur)
			{
				bool bTo = fEnsureIcon(peCur.CustomIconUuid);
				if (!bTo && bResetIfUnknown) peCur.CustomIconUuid = PwUuid.Zero;
				return true;
			};

			gh(pgSelect);
			pgSelect.TraverseTree(TraversalMethod.PreOrder, gh, eh);
		}
	}
}

