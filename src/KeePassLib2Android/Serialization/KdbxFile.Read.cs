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
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Xml;

#if !KeePassLibSD
using System.IO.Compression;
#else
using KeePassLibSD;
#endif

using KeePassLib.Cryptography;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Resources;
using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
	/// <summary>
	/// Serialization to KeePass KDBX files.
	/// </summary>
	public sealed partial class KdbxFile
	{
		/// <summary>
		/// Load a KDB file from a file.
		/// </summary>
		/// <param name="strFilePath">File to load.</param>
		/// <param name="kdbFormat">Format specifier.</param>
		/// <param name="slLogger">Status logger (optional).</param>
		public void Load(string strFilePath, KdbxFormat kdbFormat, IStatusLogger slLogger)
		{
			IOConnectionInfo ioc = IOConnectionInfo.FromPath(strFilePath);
			Load(IOConnection.OpenRead(ioc), kdbFormat, slLogger);
		}

		/// <summary>
		/// Load a KDB file from a stream.
		/// </summary>
		/// <param name="sSource">Stream to read the data from. Must contain
		/// a KDBX stream.</param>
		/// <param name="kdbFormat">Format specifier.</param>
		/// <param name="slLogger">Status logger (optional).</param>
		public void Load(Stream sSource, KdbxFormat kdbFormat, IStatusLogger slLogger)
		{
			Debug.Assert(sSource != null);
			if(sSource == null) throw new ArgumentNullException("sSource");

			m_format = kdbFormat;
			m_slLogger = slLogger;

			HashingStreamEx hashedStream = new HashingStreamEx(sSource, false, null);

			UTF8Encoding encNoBom = StrUtil.Utf8;
			try
			{
				BinaryReaderEx br = null;
				BinaryReaderEx brDecrypted = null;
				Stream readerStream = null;

				if(kdbFormat == KdbxFormat.Default)
				{
					br = new BinaryReaderEx(hashedStream, encNoBom, KLRes.FileCorrupted);
					ReadHeader(br);

					Stream sDecrypted = AttachStreamDecryptor(hashedStream);
					if((sDecrypted == null) || (sDecrypted == hashedStream))
						throw new SecurityException(KLRes.CryptoStreamFailed);

					brDecrypted = new BinaryReaderEx(sDecrypted, encNoBom, KLRes.FileCorrupted);
					byte[] pbStoredStartBytes = brDecrypted.ReadBytes(32);

					if((m_pbStreamStartBytes == null) || (m_pbStreamStartBytes.Length != 32))
						throw new InvalidDataException();

					for(int iStart = 0; iStart < 32; ++iStart)
					{
						if(pbStoredStartBytes[iStart] != m_pbStreamStartBytes[iStart])
							throw new InvalidCompositeKeyException();
					}

					Stream sHashed = new HashedBlockStream(sDecrypted, false, 0,
						!m_bRepairMode);

					if(m_pwDatabase.Compression == PwCompressionAlgorithm.GZip)
						readerStream = new GZipStream(sHashed, CompressionMode.Decompress);
					else readerStream = sHashed;
				}
				else if(kdbFormat == KdbxFormat.PlainXml)
					readerStream = hashedStream;
				else { Debug.Assert(false); throw new FormatException("KdbFormat"); }

				if(kdbFormat != KdbxFormat.PlainXml) // Is an encrypted format
				{
					if(m_pbProtectedStreamKey == null)
					{
						Debug.Assert(false);
						throw new SecurityException("Invalid protected stream key!");
					}

					m_randomStream = new CryptoRandomStream(m_craInnerRandomStream,
						m_pbProtectedStreamKey);
				}
				else m_randomStream = null; // No random stream for plain-text files

				ReadXmlStreamed(readerStream, hashedStream);
				// ReadXmlDom(readerStream);

				readerStream.Close();
				// GC.KeepAlive(br);
				// GC.KeepAlive(brDecrypted);
			}
			catch(CryptographicException) // Thrown on invalid padding
			{
				throw new CryptographicException(KLRes.FileCorrupted);
			}
			finally { CommonCleanUpRead(sSource, hashedStream); }
		}

		private void CommonCleanUpRead(Stream sSource, HashingStreamEx hashedStream)
		{
			hashedStream.Close();
			m_pbHashOfFileOnDisk = hashedStream.Hash;

			sSource.Close();

			// Reset memory protection settings (to always use reasonable
			// defaults)
			m_pwDatabase.MemoryProtection = new MemoryProtectionConfig();

			// Remove old backups (this call is required here in order to apply
			// the default history maintenance settings for people upgrading from
			// KeePass <= 2.14 to >= 2.15; also it ensures history integrity in
			// case a different application has created the KDBX file and ignored
			// the history maintenance settings)
			m_pwDatabase.MaintainBackups(); // Don't mark database as modified

			m_pbHashOfHeader = null;
		}

		private void ReadHeader(BinaryReaderEx br)
		{
			MemoryStream msHeader = new MemoryStream();
			Debug.Assert(br.CopyDataTo == null);
			br.CopyDataTo = msHeader;

			byte[] pbSig1 = br.ReadBytes(4);
			uint uSig1 = MemUtil.BytesToUInt32(pbSig1);
			byte[] pbSig2 = br.ReadBytes(4);
			uint uSig2 = MemUtil.BytesToUInt32(pbSig2);

			if((uSig1 == FileSignatureOld1) && (uSig2 == FileSignatureOld2))
				throw new OldFormatException(PwDefs.ShortProductName + @" 1.x",
					OldFormatException.OldFormatType.KeePass1x);

			if((uSig1 == FileSignature1) && (uSig2 == FileSignature2)) { }
			else if((uSig1 == FileSignaturePreRelease1) && (uSig2 ==
				FileSignaturePreRelease2)) { }
			else throw new FormatException(KLRes.FileSigInvalid);

			byte[] pb = br.ReadBytes(4);
			uint uVersion = MemUtil.BytesToUInt32(pb);
			if((uVersion & FileVersionCriticalMask) > (FileVersion32 & FileVersionCriticalMask))
				throw new FormatException(KLRes.FileVersionUnsupported +
					MessageService.NewParagraph + KLRes.FileNewVerReq);

			while(true)
			{
				if(ReadHeaderField(br) == false)
					break;
			}

			br.CopyDataTo = null;
			byte[] pbHeader = msHeader.ToArray();
			msHeader.Close();
			SHA256Managed sha256 = new SHA256Managed();
			m_pbHashOfHeader = sha256.ComputeHash(pbHeader);
		}

		private bool ReadHeaderField(BinaryReaderEx brSource)
		{
			Debug.Assert(brSource != null);
			if(brSource == null) throw new ArgumentNullException("brSource");

			byte btFieldID = brSource.ReadByte();
			ushort uSize = MemUtil.BytesToUInt16(brSource.ReadBytes(2));

			byte[] pbData = null;
			if(uSize > 0)
			{
				string strPrevExcpText = brSource.ReadExceptionText;
				brSource.ReadExceptionText = KLRes.FileHeaderEndEarly;

				pbData = brSource.ReadBytes(uSize);

				brSource.ReadExceptionText = strPrevExcpText;
			}

			bool bResult = true;
			KdbxHeaderFieldID kdbID = (KdbxHeaderFieldID)btFieldID;
			switch(kdbID)
			{
				case KdbxHeaderFieldID.EndOfHeader:
					bResult = false; // Returning false indicates end of header
					break;

				case KdbxHeaderFieldID.CipherID:
					SetCipher(pbData);
					break;

				case KdbxHeaderFieldID.CompressionFlags:
					SetCompressionFlags(pbData);
					break;

				case KdbxHeaderFieldID.MasterSeed:
					m_pbMasterSeed = pbData;
					CryptoRandom.Instance.AddEntropy(pbData);
					break;

				case KdbxHeaderFieldID.TransformSeed:
					m_pbTransformSeed = pbData;
					CryptoRandom.Instance.AddEntropy(pbData);
					break;

				case KdbxHeaderFieldID.TransformRounds:
					m_pwDatabase.KeyEncryptionRounds = MemUtil.BytesToUInt64(pbData);
					break;

				case KdbxHeaderFieldID.EncryptionIV:
					m_pbEncryptionIV = pbData;
					break;

				case KdbxHeaderFieldID.ProtectedStreamKey:
					m_pbProtectedStreamKey = pbData;
					CryptoRandom.Instance.AddEntropy(pbData);
					break;

				case KdbxHeaderFieldID.StreamStartBytes:
					m_pbStreamStartBytes = pbData;
					break;

				case KdbxHeaderFieldID.InnerRandomStreamID:
					SetInnerRandomStreamID(pbData);
					break;

				default:
					Debug.Assert(false);
					if(m_slLogger != null)
						m_slLogger.SetText(KLRes.UnknownHeaderId + @": " +
							kdbID.ToString() + "!", LogStatusType.Warning);
					break;
			}

			return bResult;
		}

		private void SetCipher(byte[] pbID)
		{
			if((pbID == null) || (pbID.Length != 16))
				throw new FormatException(KLRes.FileUnknownCipher);

			m_pwDatabase.DataCipherUuid = new PwUuid(pbID);
		}

		private void SetCompressionFlags(byte[] pbFlags)
		{
			int nID = (int)MemUtil.BytesToUInt32(pbFlags);
			if((nID < 0) || (nID >= (int)PwCompressionAlgorithm.Count))
				throw new FormatException(KLRes.FileUnknownCompression);

			m_pwDatabase.Compression = (PwCompressionAlgorithm)nID;
		}

		private void SetInnerRandomStreamID(byte[] pbID)
		{
			uint uID = MemUtil.BytesToUInt32(pbID);
			if(uID >= (uint)CrsAlgorithm.Count)
				throw new FormatException(KLRes.FileUnknownCipher);

			m_craInnerRandomStream = (CrsAlgorithm)uID;
		}

		private Stream AttachStreamDecryptor(Stream s)
		{
			MemoryStream ms = new MemoryStream();

			Debug.Assert(m_pbMasterSeed.Length == 32);
			if(m_pbMasterSeed.Length != 32)
				throw new FormatException(KLRes.MasterSeedLengthInvalid);
			ms.Write(m_pbMasterSeed, 0, 32);

			byte[] pKey32 = m_pwDatabase.MasterKey.GenerateKey32(m_pbTransformSeed,
				m_pwDatabase.KeyEncryptionRounds).ReadData();
			if((pKey32 == null) || (pKey32.Length != 32))
				throw new SecurityException(KLRes.InvalidCompositeKey);
			ms.Write(pKey32, 0, 32);
			
			SHA256Managed sha256 = new SHA256Managed();
			byte[] aesKey = sha256.ComputeHash(ms.ToArray());

			ms.Close();
			Array.Clear(pKey32, 0, 32);

			if((aesKey == null) || (aesKey.Length != 32))
				throw new SecurityException(KLRes.FinalKeyCreationFailed);

			ICipherEngine iEngine = CipherPool.GlobalPool.GetCipher(m_pwDatabase.DataCipherUuid);
			if(iEngine == null) throw new SecurityException(KLRes.FileUnknownCipher);
			return iEngine.DecryptStream(s, aesKey, m_pbEncryptionIV);
		}

		[Obsolete]
		public static List<PwEntry> ReadEntries(PwDatabase pwDatabase, Stream msData)
		{
			return ReadEntries(msData);
		}

		/// <summary>
		/// Read entries from a stream.
		/// </summary>
		/// <param name="msData">Input stream to read the entries from.</param>
		/// <returns>Extracted entries.</returns>
		public static List<PwEntry> ReadEntries(Stream msData)
		{
			/* KdbxFile f = new KdbxFile(pwDatabase);
			f.m_format = KdbxFormat.PlainXml;

			XmlDocument doc = new XmlDocument();
			doc.Load(msData);

			XmlElement el = doc.DocumentElement;
			if(el.Name != ElemRoot) throw new FormatException();

			List<PwEntry> vEntries = new List<PwEntry>();

			foreach(XmlNode xmlChild in el.ChildNodes)
			{
				if(xmlChild.Name == ElemEntry)
				{
					PwEntry pe = f.ReadEntry(xmlChild);
					pe.Uuid = new PwUuid(true);

					foreach(PwEntry peHistory in pe.History)
						peHistory.Uuid = pe.Uuid;

					vEntries.Add(pe);
				}
				else { Debug.Assert(false); }
			}

			return vEntries; */

			PwDatabase pd = new PwDatabase();
			KdbxFile f = new KdbxFile(pd);
			f.Load(msData, KdbxFormat.PlainXml, null);

			List<PwEntry> vEntries = new List<PwEntry>();
			foreach(PwEntry pe in pd.RootGroup.Entries)
			{
				pe.SetUuid(new PwUuid(true), true);
				vEntries.Add(pe);
			}

			return vEntries;
		}
	}
}
