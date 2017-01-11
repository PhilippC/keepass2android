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
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

#if !KeePassUAP
using System.Security.Cryptography;
#endif

using KeePassLib.Collections;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Delegates;
using KeePassLib.Interfaces;
using KeePassLib.Resources;
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
		private const uint FileVersion32 = 0x00040000;

		internal const uint FileVersion32_4 = 0x00040000; // First of 4.x series
		internal const uint FileVersion32_3 = 0x00030001; // Old format 3.1

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
		private const string ElemSettingsChanged = "SettingsChanged";
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
		private const string ElemDbKeyChangeForceOnce = "MasterKeyChangeForceOnce";
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
		private bool m_bUsedOnce = false;

		private XmlWriter m_xmlWriter = null;
		private CryptoRandomStream m_randomStream = null;
		private KdbxFormat m_format = KdbxFormat.Default;
		private IStatusLogger m_slLogger = null;

		private uint m_uFileVersion = 0;
		private byte[] m_pbMasterSeed = null;
		// private byte[] m_pbTransformSeed = null;
		private byte[] m_pbEncryptionIV = null;
		private byte[] m_pbStreamStartBytes = null;

		// ArcFourVariant only for backward compatibility; KeePass defaults
		// to a more secure algorithm when *writing* databases
		private CrsAlgorithm m_craInnerRandomStream = CrsAlgorithm.ArcFourVariant;
		private byte[] m_pbInnerRandomStreamKey = null;

		private ProtectedBinarySet m_pbsBinaries = new ProtectedBinarySet();

		private byte[] m_pbHashOfHeader = null;
		private byte[] m_pbHashOfFileOnDisk = null;

		private readonly DateTime m_dtNow = DateTime.UtcNow; // Cache current time

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
			TransformSeed = 5, // KDBX 3.1, for backward compatibility only
			TransformRounds = 6, // KDBX 3.1, for backward compatibility only
			EncryptionIV = 7,
			InnerRandomStreamKey = 8, // KDBX 3.1, for backward compatibility only
			StreamStartBytes = 9, // KDBX 3.1, for backward compatibility only
			InnerRandomStreamID = 10, // KDBX 3.1, for backward compatibility only
			KdfParameters = 11, // KDBX 4, superseding Transform*
			PublicCustomData = 12 // KDBX 4
		}

		// Inner header in KDBX >= 4 files
		private enum KdbxInnerHeaderFieldID : byte
		{
			EndOfHeader = 0,
			InnerRandomStreamID = 1, // Supersedes KdbxHeaderFieldID.InnerRandomStreamID
			InnerRandomStreamKey = 2, // Supersedes KdbxHeaderFieldID.InnerRandomStreamKey
			Binary = 3
		}

		[Flags]
		private enum KdbxBinaryFlags : byte
		{
			None = 0,
			Protected = 1
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

		private uint m_uForceVersion = 0;
		internal uint ForceVersion
		{
			get { return m_uForceVersion; }
			set { m_uForceVersion = value; }
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

		private uint GetMinKdbxVersion()
		{
			if(m_uForceVersion != 0) return m_uForceVersion;

			// See also KeePassKdb2x3.Export (KDBX 3.1 export module)

			AesKdf kdfAes = new AesKdf();
			if(!kdfAes.Uuid.Equals(m_pwDatabase.KdfParameters.KdfUuid))
				return FileVersion32;

			if(m_pwDatabase.PublicCustomData.Count > 0)
				return FileVersion32;

			bool bCustomData = false;
			GroupHandler gh = delegate(PwGroup pg)
			{
				if(pg == null) { Debug.Assert(false); return true; }
				if(pg.CustomData.Count > 0) { bCustomData = true; return false; }
				return true;
			};
			EntryHandler eh = delegate(PwEntry pe)
			{
				if(pe == null) { Debug.Assert(false); return true; }
				if(pe.CustomData.Count > 0) { bCustomData = true; return false; }
				return true;
			};
			gh(m_pwDatabase.RootGroup);
			m_pwDatabase.RootGroup.TraverseTree(TraversalMethod.PreOrder, gh, eh);
			if(bCustomData) return FileVersion32;

			return FileVersion32_3; // KDBX 3.1 is sufficient
		}

		private void ComputeKeys(out byte[] pbCipherKey, int cbCipherKey,
			out byte[] pbHmacKey64)
		{
			byte[] pbCmp = new byte[32 + 32 + 1];
			try
			{
				Debug.Assert(m_pbMasterSeed != null);
				if(m_pbMasterSeed == null)
					throw new ArgumentNullException("m_pbMasterSeed");
				Debug.Assert(m_pbMasterSeed.Length == 32);
				if(m_pbMasterSeed.Length != 32)
					throw new FormatException(KLRes.MasterSeedLengthInvalid);
				Array.Copy(m_pbMasterSeed, 0, pbCmp, 0, 32);

				Debug.Assert(m_pwDatabase != null);
				Debug.Assert(m_pwDatabase.MasterKey != null);
				ProtectedBinary pbinUser = m_pwDatabase.MasterKey.GenerateKey32(
					m_pwDatabase.KdfParameters);
				Debug.Assert(pbinUser != null);
				if(pbinUser == null)
					throw new SecurityException(KLRes.InvalidCompositeKey);
				byte[] pUserKey32 = pbinUser.ReadData();
				if((pUserKey32 == null) || (pUserKey32.Length != 32))
					throw new SecurityException(KLRes.InvalidCompositeKey);
				Array.Copy(pUserKey32, 0, pbCmp, 32, 32);
				MemUtil.ZeroByteArray(pUserKey32);

				pbCipherKey = CryptoUtil.ResizeKey(pbCmp, 0, 64, cbCipherKey);

				pbCmp[64] = 1;
				using(SHA512Managed h = new SHA512Managed())
				{
					pbHmacKey64 = h.ComputeHash(pbCmp);
				}
			}
			finally { MemUtil.ZeroByteArray(pbCmp); }
		}

		private ICipherEngine GetCipher(out int cbEncKey, out int cbEncIV)
		{
			PwUuid pu = m_pwDatabase.DataCipherUuid;
			ICipherEngine iCipher = CipherPool.GlobalPool.GetCipher(pu);
			if(iCipher == null) // CryptographicExceptions are translated to "file corrupted"
				throw new Exception(KLRes.FileUnknownCipher +
					MessageService.NewParagraph + KLRes.FileNewVerOrPlgReq +
					MessageService.NewParagraph + "UUID: " + pu.ToHexString() + ".");

			ICipherEngine2 iCipher2 = (iCipher as ICipherEngine2);
			if(iCipher2 != null)
			{
				cbEncKey = iCipher2.KeyLength;
				if(cbEncKey < 0) throw new InvalidOperationException("EncKey.Length");

				cbEncIV = iCipher2.IVLength;
				if(cbEncIV < 0) throw new InvalidOperationException("EncIV.Length");
			}
			else
			{
				cbEncKey = 32;
				cbEncIV = 16;
			}

			return iCipher;
		}

		private Stream EncryptStream(Stream s, ICipherEngine iCipher,
			byte[] pbKey, int cbIV, bool bEncrypt)
		{
			byte[] pbIV = (m_pbEncryptionIV ?? MemUtil.EmptyByteArray);
			if(pbIV.Length != cbIV)
			{
				Debug.Assert(false);
				throw new Exception(KLRes.FileCorrupted);
			}

			if(bEncrypt)
				return iCipher.EncryptStream(s, pbKey, pbIV);
			return iCipher.DecryptStream(s, pbKey, pbIV);
		}

		private byte[] ComputeHeaderHmac(byte[] pbHeader, byte[] pbKey)
		{
			byte[] pbHeaderHmac;
			byte[] pbBlockKey = HmacBlockStream.GetHmacKey64(
				pbKey, ulong.MaxValue);
			using(HMACSHA256 h = new HMACSHA256(pbBlockKey))
			{
				pbHeaderHmac = h.ComputeHash(pbHeader);
			}
			MemUtil.ZeroByteArray(pbBlockKey);

			return pbHeaderHmac;
		}

		private void CloseStreams(List<Stream> lStreams)
		{
			if(lStreams == null) { Debug.Assert(false); return; }

			// Typically, closing a stream also closes its base
			// stream; however, there may be streams that do not
			// do this (e.g. some cipher plugin), thus for safety
			// we close all streams manually, from the innermost
			// to the outermost

			for(int i = lStreams.Count - 1; i >= 0; --i)
			{
				// Check for duplicates
				Debug.Assert((lStreams.IndexOf(lStreams[i]) == i) &&
					(lStreams.LastIndexOf(lStreams[i]) == i));

				try { lStreams[i].Close(); }
				catch(Exception) { Debug.Assert(false); }
			}

			// Do not clear the list
		}

		private void CleanUpInnerRandomStream()
		{
			if(m_randomStream != null) m_randomStream.Dispose();

			if(m_pbInnerRandomStreamKey != null)
				MemUtil.ZeroByteArray(m_pbInnerRandomStreamKey);
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
