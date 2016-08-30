/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2016 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

using KeePassLib.Collections;
using KeePassLib.Cryptography;
using KeePassLib.Delegates;
using KeePassLib.Interfaces;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
	/// <summary>
	/// The <c>KdbxFile</c> class supports saving the data to various
	/// formats.
	/// </summary>
	public enum KdbxFormat
	{
		/// <summary>
		/// The default, encrypted file format.
		/// </summary>
		Default = 0,

		/// <summary>
		/// Use this flag when exporting data to a plain-text XML file.
		/// </summary>
		PlainXml
	}

	/// <summary>
	/// Serialization to KeePass KDBX files.
	/// </summary>
	public sealed partial class KdbxFile
	{
		/// <summary>
		/// File identifier, first 32-bit value.
		/// </summary>
		internal const uint FileSignature1 = 0x9AA2D903;

		/// <summary>
		/// File identifier, second 32-bit value.
		/// </summary>
		internal const uint FileSignature2 = 0xB54BFB67;

		/// <summary>
		/// File version of files saved by the current <c>KdbxFile</c> class.
		/// KeePass 2.07 has version 1.01, 2.08 has 1.02, 2.09 has 2.00,
		/// 2.10 has 2.02, 2.11 has 2.04, 2.15 has 3.00, 2.20 has 3.01.
		/// The first 2 bytes are critical (i.e. loading will fail, if the
		/// file version is too high), the last 2 bytes are informational.
		/// </summary>
		private const uint FileVersion32 = 0x00030001;

		private const uint FileVersionCriticalMask = 0xFFFF0000;

		// KeePass 1.x signature
		internal const uint FileSignatureOld1 = 0x9AA2D903;
		internal const uint FileSignatureOld2 = 0xB54BFB65;
		// KeePass 2.x pre-release (alpha and beta) signature
		internal const uint FileSignaturePreRelease1 = 0x9AA2D903;
		internal const uint FileSignaturePreRelease2 = 0xB54BFB66;

		private const string ElemDocNode = "KeePassFile";
		private const string ElemMeta = "Meta";
		private const string ElemRoot = "Root";
		private const string ElemGroup = "Group";
		private const string ElemEntry = "Entry";

		private const string ElemGenerator = "Generator";
		private const string ElemHeaderHash = "HeaderHash";
		private const string ElemDbName = "DatabaseName";
		private const string ElemDbNameChanged = "DatabaseNameChanged";
		private const string ElemDbDesc = "DatabaseDescription";
		private const string ElemDbDescChanged = "DatabaseDescriptionChanged";
		private const string ElemDbDefaultUser = "DefaultUserName";
		private const string ElemDbDefaultUserChanged = "DefaultUserNameChanged";
		private const string ElemDbMntncHistoryDays = "MaintenanceHistoryDays";
		private const string ElemDbColor = "Color";
		private const string ElemDbKeyChanged = "MasterKeyChanged";
		private const string ElemDbKeyChangeRec = "MasterKeyChangeRec";
		private const string ElemDbKeyChangeForce = "MasterKeyChangeForce";
		private const string ElemRecycleBinEnabled = "RecycleBinEnabled";
		private const string ElemRecycleBinUuid = "RecycleBinUUID";
		private const string ElemRecycleBinChanged = "RecycleBinChanged";
		private const string ElemEntryTemplatesGroup = "EntryTemplatesGroup";
		private const string ElemEntryTemplatesGroupChanged = "EntryTemplatesGroupChanged";
		private const string ElemHistoryMaxItems = "HistoryMaxItems";
		private const string ElemHistoryMaxSize = "HistoryMaxSize";
		private const string ElemLastSelectedGroup = "LastSelectedGroup";
		private const string ElemLastTopVisibleGroup = "LastTopVisibleGroup";

		private const string ElemMemoryProt = "MemoryProtection";
		private const string ElemProtTitle = "ProtectTitle";
		private const string ElemProtUserName = "ProtectUserName";
		private const string ElemProtPassword = "ProtectPassword";
		private const string ElemProtUrl = "ProtectURL";
		private const string ElemProtNotes = "ProtectNotes";
		// private const string ElemProtAutoHide = "AutoEnableVisualHiding";

		private const string ElemCustomIcons = "CustomIcons";
		private const string ElemCustomIconItem = "Icon";
		private const string ElemCustomIconItemID = "UUID";
		private const string ElemCustomIconItemData = "Data";

		private const string ElemAutoType = "AutoType";
		private const string ElemHistory = "History";

		private const string ElemName = "Name";
		private const string ElemNotes = "Notes";
		private const string ElemUuid = "UUID";
		private const string ElemIcon = "IconID";
		private const string ElemCustomIconID = "CustomIconUUID";
		private const string ElemFgColor = "ForegroundColor";
		private const string ElemBgColor = "BackgroundColor";
		private const string ElemOverrideUrl = "OverrideURL";
		private const string ElemTimes = "Times";
		private const string ElemTags = "Tags";

		private const string ElemCreationTime = "CreationTime";
		private const string ElemLastModTime = "LastModificationTime";
		private const string ElemLastAccessTime = "LastAccessTime";
		private const string ElemExpiryTime = "ExpiryTime";
		private const string ElemExpires = "Expires";
		private const string ElemUsageCount = "UsageCount";
		private const string ElemLocationChanged = "LocationChanged";

		private const string ElemGroupDefaultAutoTypeSeq = "DefaultAutoTypeSequence";
		private const string ElemEnableAutoType = "EnableAutoType";
		private const string ElemEnableSearching = "EnableSearching";

		private const string ElemString = "String";
		private const string ElemBinary = "Binary";
		private const string ElemKey = "Key";
		private const string ElemValue = "Value";

		private const string ElemAutoTypeEnabled = "Enabled";
		private const string ElemAutoTypeObfuscation = "DataTransferObfuscation";
		private const string ElemAutoTypeDefaultSeq = "DefaultSequence";
		private const string ElemAutoTypeItem = "Association";
		private const string ElemWindow = "Window";
		private const string ElemKeystrokeSequence = "KeystrokeSequence";

		private const string ElemBinaries = "Binaries";

		private const string AttrId = "ID";
		private const string AttrRef = "Ref";
		private const string AttrProtected = "Protected";
		private const string AttrProtectedInMemPlainXml = "ProtectInMemory";
		private const string AttrCompressed = "Compressed";

		private const string ElemIsExpanded = "IsExpanded";
		private const string ElemLastTopVisibleEntry = "LastTopVisibleEntry";

		private const string ElemDeletedObjects = "DeletedObjects";
		private const string ElemDeletedObject = "DeletedObject";
		private const string ElemDeletionTime = "DeletionTime";

		private const string ValFalse = "False";
		private const string ValTrue = "True";

		private const string ElemCustomData = "CustomData";
		private const string ElemStringDictExItem = "Item";

		private PwDatabase m_pwDatabase; // Not null, see constructor

		private XmlWriter m_xmlWriter = null;
		private CryptoRandomStream m_randomStream = null;
		private KdbxFormat m_format = KdbxFormat.Default;
		private IStatusLogger m_slLogger = null;

		private byte[] m_pbMasterSeed = null;
		private byte[] m_pbTransformSeed = null;
		private byte[] m_pbEncryptionIV = null;
		private byte[] m_pbProtectedStreamKey = null;
		private byte[] m_pbStreamStartBytes = null;

		// ArcFourVariant only for compatibility; KeePass will default to a
		// different (more secure) algorithm when *writing* databases
		private CrsAlgorithm m_craInnerRandomStream = CrsAlgorithm.ArcFourVariant;

		private Dictionary<string, ProtectedBinary> m_dictBinPool =
			new Dictionary<string, ProtectedBinary>();

		private byte[] m_pbHashOfHeader = null;
		private byte[] m_pbHashOfFileOnDisk = null;

		private readonly DateTime m_dtNow = DateTime.Now; // Cache current time

		private const uint NeutralLanguageOffset = 0x100000; // 2^20, see 32-bit Unicode specs
		private const uint NeutralLanguageIDSec = 0x7DC5C; // See 32-bit Unicode specs
		private const uint NeutralLanguageID = NeutralLanguageOffset + NeutralLanguageIDSec;
		private static bool m_bLocalizedNames = false;

		private enum KdbxHeaderFieldID : byte
		{
			EndOfHeader = 0,
			Comment = 1,
			CipherID = 2,
			CompressionFlags = 3,
			MasterSeed = 4,
			TransformSeed = 5,
			TransformRounds = 6,
			EncryptionIV = 7,
			ProtectedStreamKey = 8,
			StreamStartBytes = 9,
			InnerRandomStreamID = 10
		}

		public byte[] HashOfFileOnDisk
		{
			get { return m_pbHashOfFileOnDisk; }
		}

		private bool m_bRepairMode = false;
		public bool RepairMode
		{
			get { return m_bRepairMode; }
			set { m_bRepairMode = value; }
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
		/// Default constructor.
		/// </summary>
		/// <param name="pwDataStore">The <c>PwDatabase</c> instance that the
		/// class will load file data into or use to create a KDBX file.</param>
		public KdbxFile(PwDatabase pwDataStore)
		{
			Debug.Assert(pwDataStore != null);
			if(pwDataStore == null) throw new ArgumentNullException("pwDataStore");

			m_pwDatabase = pwDataStore;
		}

		/// <summary>
		/// Call this once to determine the current localization settings.
		/// </summary>
		public static void DetermineLanguageId()
		{
			// Test if localized names should be used. If localized names are used,
			// the m_bLocalizedNames value must be set to true. By default, localized
			// names should be used! (Otherwise characters could be corrupted
			// because of different code pages).
			unchecked
			{
				uint uTest = 0;
				foreach(char ch in PwDatabase.LocalizedAppName)
					uTest = uTest * 5 + ch;

				m_bLocalizedNames = (uTest != NeutralLanguageID);
			}
		}

		private void BinPoolBuild(PwGroup pgDataSource)
		{
			m_dictBinPool = new Dictionary<string, ProtectedBinary>();

			if(pgDataSource == null) { Debug.Assert(false); return; }

			EntryHandler eh = delegate(PwEntry pe)
			{
				foreach(PwEntry peHistory in pe.History)
				{
					BinPoolAdd(peHistory.Binaries);
				}

				BinPoolAdd(pe.Binaries);
				return true;
			};

			pgDataSource.TraverseTree(TraversalMethod.PreOrder, null, eh);
		}

		private void BinPoolAdd(ProtectedBinaryDictionary dict)
		{
			foreach(KeyValuePair<string, ProtectedBinary> kvp in dict)
			{
				BinPoolAdd(kvp.Value);
			}
		}

		private void BinPoolAdd(ProtectedBinary pb)
		{
			if(pb == null) { Debug.Assert(false); return; }

			if(BinPoolFind(pb) != null) return; // Exists already

			m_dictBinPool.Add(m_dictBinPool.Count.ToString(
				NumberFormatInfo.InvariantInfo), pb);
		}

		private string BinPoolFind(ProtectedBinary pb)
		{
			if(pb == null) { Debug.Assert(false); return null; }

			foreach(KeyValuePair<string, ProtectedBinary> kvp in m_dictBinPool)
			{
				if(pb.Equals(kvp.Value)) return kvp.Key;
			}

			return null;
		}

		private ProtectedBinary BinPoolGet(string strKey)
		{
			if(strKey == null) { Debug.Assert(false); return null; }

			ProtectedBinary pb;
			if(m_dictBinPool.TryGetValue(strKey, out pb)) return pb;

			return null;
		}

		private static void SaveBinary(string strName, ProtectedBinary pb,
			string strSaveDir)
		{
			if(pb == null) { Debug.Assert(false); return; }

			if(string.IsNullOrEmpty(strName)) strName = "File.bin";

			string strPath;
			int iTry = 1;
			do
			{
				strPath = UrlUtil.EnsureTerminatingSeparator(strSaveDir, false);

				string strExt = UrlUtil.GetExtension(strName);
				string strDesc = UrlUtil.StripExtension(strName);

				strPath += strDesc;
				if(iTry > 1)
					strPath += " (" + iTry.ToString(NumberFormatInfo.InvariantInfo) +
						")";

				if(!string.IsNullOrEmpty(strExt)) strPath += "." + strExt;

				++iTry;
			}
			while(File.Exists(strPath));

#if !KeePassLibSD
			byte[] pbData = pb.ReadData();
			File.WriteAllBytes(strPath, pbData);
			MemUtil.ZeroByteArray(pbData);
#else
			FileStream fs = new FileStream(strPath, FileMode.Create,
				FileAccess.Write, FileShare.None);
			byte[] pbData = pb.ReadData();
			fs.Write(pbData, 0, pbData.Length);
			fs.Close();
#endif
		}
	}
}
