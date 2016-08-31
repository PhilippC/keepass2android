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
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

#if !KeePassUAP
using System.Security.Cryptography;
#endif

#if !KeePassLibSD
using System.IO.Compression;
#else
using KeePassLibSD;
#endif

using KeePassLib.Collections;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Resources;
using KeePassLib.Utility;

using keepass2android;

namespace KeePassLib.Serialization
{
	/// <summary>
	/// Serialization to KeePass KDBX files.
	/// </summary>
	public sealed partial class KdbxFile
	{
		/// <summary>
		/// Load a KDBX file.
		/// </summary>
		/// <param name="strFilePath">File to load.</param>
		/// <param name="fmt">Format.</param>
		/// <param name="slLogger">Status logger (optional).</param>
		public void Load(string strFilePath, KdbxFormat fmt, IStatusLogger slLogger)
		{
			IOConnectionInfo ioc = IOConnectionInfo.FromPath(strFilePath);
			Load(IOConnection.OpenRead(ioc), fmt, slLogger);
		}

		/// <summary>
		/// Load a KDBX file from a stream.
		/// </summary>
		/// <param name="sSource">Stream to read the data from. Must contain
		/// a KDBX stream.</param>
		/// <param name="fmt">Format.</param>
		/// <param name="slLogger">Status logger (optional).</param>
		public void Load(Stream sSource, KdbxFormat fmt, IStatusLogger slLogger)
		{
			Debug.Assert(sSource != null);
			if(sSource == null) throw new ArgumentNullException("sSource");

			m_format = fmt;
			m_slLogger = slLogger;

			UTF8Encoding encNoBom = StrUtil.Utf8;
			byte[] pbCipherKey = null;
			byte[] pbHmacKey64 = null;

			List<Stream> lStreams = new List<Stream>();
			lStreams.Add(sSource);

			HashingStreamEx sHashing = new HashingStreamEx(sSource, false, null);
			lStreams.Add(sHashing);

			try
			{
				Stream sXml;
				if (fmt == KdbxFormat.Default || fmt == KdbxFormat.ProtocolBuffers)
				{
					BinaryReaderEx br = new BinaryReaderEx(sHashing,
						encNoBom, KLRes.FileCorrupted);
					byte[] pbHeader = LoadHeader(br);

					int cbEncKey, cbEncIV;
					ICipherEngine iCipher = GetCipher(out cbEncKey, out cbEncIV);
			
					if (m_slLogger != null)
						m_slLogger.SetText("KP2AKEY_TransformingKey", LogStatusType.AdditionalInfo);
			
					ComputeKeys(out pbCipherKey, cbEncKey, out pbHmacKey64);

					Stream sPlain;
					if(m_uFileVersion <= FileVersion32_3)
					{
						Stream sDecrypted = EncryptStream(sHashing, iCipher,
							pbCipherKey, cbEncIV, false);
						if((sDecrypted == null) || (sDecrypted == sHashing))
							throw new SecurityException(KLRes.CryptoStreamFailed);

						if (m_slLogger != null)
							m_slLogger.SetText("KP2AKEY_DecodingDatabase", LogStatusType.AdditionalInfo);

						lStreams.Add(sDecrypted);

						BinaryReaderEx brDecrypted = new BinaryReaderEx(sDecrypted,
							encNoBom, KLRes.FileCorrupted);
						byte[] pbStoredStartBytes = brDecrypted.ReadBytes(32);

						if((m_pbStreamStartBytes == null) || (m_pbStreamStartBytes.Length != 32))
							throw new InvalidDataException();
						if(!MemUtil.ArraysEqual(pbStoredStartBytes, m_pbStreamStartBytes))
							throw new InvalidCompositeKeyException();

						if (m_slLogger != null)
							m_slLogger.SetText("KP2AKEY_DecodingDatabase", LogStatusType.AdditionalInfo);


						sPlain = new HashedBlockStream(sDecrypted, false, 0, !m_bRepairMode);
					}
					else // KDBX >= 4
					{
						byte[] pbHeaderHmac = ComputeHeaderHmac(pbHeader, pbHmacKey64);
						byte[] pbStoredHmac = MemUtil.Read(sHashing, 32);
						if((pbStoredHmac == null) || (pbStoredHmac.Length != 32))
							throw new InvalidDataException();
						if(!MemUtil.ArraysEqual(pbHeaderHmac, pbStoredHmac))
							throw new InvalidCompositeKeyException();

						HmacBlockStream sBlocks = new HmacBlockStream(sHashing,
							false, !m_bRepairMode, pbHmacKey64);
						lStreams.Add(sBlocks);

						sPlain = EncryptStream(sBlocks, iCipher, pbCipherKey,
							cbEncIV, false);
						if((sPlain == null) || (sPlain == sBlocks))
							throw new SecurityException(KLRes.CryptoStreamFailed);
					}
					lStreams.Add(sPlain);

					if(m_pwDatabase.Compression == PwCompressionAlgorithm.GZip)
					{
						sXml = new GZipStream(sPlain, CompressionMode.Decompress);
						lStreams.Add(sXml);
					}
					else sXml = sPlain;
				}
				else if(fmt == KdbxFormat.PlainXml)
					sXml = sHashing;
				else { Debug.Assert(false); throw new ArgumentOutOfRangeException("fmt"); }

				if(fmt == KdbxFormat.Default)
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
				if (m_slLogger != null)
					m_slLogger.SetText("KP2AKEY_ParsingDatabase", LogStatusType.AdditionalInfo);
				
#if KeePassDebug_WriteXml
				// FileStream fsOut = new FileStream("Raw.xml", FileMode.Create,
				//	FileAccess.Write, FileShare.None);
				// try
				// {
				//	while(true)
				//	{
				//		int b = sXml.ReadByte();
				//		if(b == -1) break;
				//		fsOut.WriteByte((byte)b);
				//	}
				// }
				// catch(Exception) { }
				// fsOut.Close();
#endif
				var stopWatch = Stopwatch.StartNew();
				
				if (fmt == KdbxFormat.ProtocolBuffers)
				{
					KdbpFile.ReadDocument(m_pwDatabase, sXml, m_pbProtectedStreamKey, m_pbHashOfHeader);

					Kp2aLog.Log(String.Format("KdbpFile.ReadDocument: {0}ms", stopWatch.ElapsedMilliseconds));

				}
				else
				{

					ReadXmlStreamed(sXml, sHashing);

					Kp2aLog.Log(String.Format("ReadXmlStreamed: {0}ms", stopWatch.ElapsedMilliseconds));
				}
				// ReadXmlDom(sXml);
			}
			catch(CryptographicException) // Thrown on invalid padding
			{
				throw new CryptographicException(KLRes.FileCorrupted);
			}
			finally
			{
				if(pbCipherKey != null) MemUtil.ZeroByteArray(pbCipherKey);
				if(pbHmacKey64 != null) MemUtil.ZeroByteArray(pbHmacKey64);

				CommonCleanUpRead(lStreams, sHashing);
			}
		}

		private void CommonCleanUpRead(List<Stream> lStreams, HashingStreamEx sHashing)
		{
			CloseStreams(lStreams);

			Debug.Assert(lStreams.Contains(sHashing)); // sHashing must be closed
			m_pbHashOfFileOnDisk = sHashing.Hash;
			Debug.Assert(m_pbHashOfFileOnDisk != null);

			// Reset memory protection settings (to always use reasonable
			// defaults)
			m_pwDatabase.MemoryProtection = new MemoryProtectionConfig();

			// Remove old backups (this call is required here in order to apply
			// the default history maintenance settings for people upgrading from
			// KeePass <= 2.14 to >= 2.15; also it ensures history integrity in
			// case a different application has created the KDBX file and ignored
			// the history maintenance settings)
			m_pwDatabase.MaintainBackups(); // Don't mark database as modified

			// Expand the root group, such that in case the user accidently
			// collapses the root group he can simply reopen the database
			PwGroup pgRoot = m_pwDatabase.RootGroup;
			if(pgRoot != null) pgRoot.IsExpanded = true;
			else { Debug.Assert(false); }

			m_pbHashOfHeader = null;
		}

		private byte[] LoadHeader(BinaryReaderEx br)
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
			m_uFileVersion = uVersion;

			while(true)
			{
				if(!ReadHeaderField(br)) break;
			}

			br.CopyDataTo = null;
			byte[] pbHeader = msHeader.ToArray();
			msHeader.Close();

			m_pbHashOfHeader = CryptoUtil.HashSha256(pbHeader);
			return pbHeader;
		}

		private bool ReadHeaderField(BinaryReaderEx brSource)
		{
			Debug.Assert(brSource != null);
			if(brSource == null) throw new ArgumentNullException("brSource");

			byte btFieldID = brSource.ReadByte();

			int cbSize;
			Debug.Assert(m_uFileVersion > 0);
			if(m_uFileVersion <= FileVersion32_3)
				cbSize = (int)MemUtil.BytesToUInt16(brSource.ReadBytes(2));
			else cbSize = MemUtil.BytesToInt32(brSource.ReadBytes(4));
			if(cbSize < 0) throw new FormatException(KLRes.FileCorrupted);

			byte[] pbData = MemUtil.EmptyByteArray;
			if(cbSize > 0)
			{
				string strPrevExcpText = brSource.ReadExceptionText;
				brSource.ReadExceptionText = KLRes.FileHeaderEndEarly;

				pbData = brSource.ReadBytes(cbSize);

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

				// Obsolete; for backward compatibility only
				case KdbxHeaderFieldID.TransformSeed:
					AesKdf kdfS = new AesKdf();
					if(!m_pwDatabase.KdfParameters.KdfUuid.Equals(kdfS.Uuid))
						m_pwDatabase.KdfParameters = kdfS.GetDefaultParameters();

					// m_pbTransformSeed = pbData;
					m_pwDatabase.KdfParameters.SetByteArray(AesKdf.ParamSeed, pbData);

					CryptoRandom.Instance.AddEntropy(pbData);
					break;

				// Obsolete; for backward compatibility only
				case KdbxHeaderFieldID.TransformRounds:
					AesKdf kdfR = new AesKdf();
					if(!m_pwDatabase.KdfParameters.KdfUuid.Equals(kdfR.Uuid))
						m_pwDatabase.KdfParameters = kdfR.GetDefaultParameters();

					// m_pwDatabase.KeyEncryptionRounds = MemUtil.BytesToUInt64(pbData);
					m_pwDatabase.KdfParameters.SetUInt64(AesKdf.ParamRounds,
						MemUtil.BytesToUInt64(pbData));
					break;

				case KdbxHeaderFieldID.EncryptionIV:
					m_pbEncryptionIV = pbData;
					break;

				case KdbxHeaderFieldID.ProtectedStreamKey:
					m_pbProtectedStreamKey = pbData;
					CryptoRandom.Instance.AddEntropy(pbData);
					break;

				case KdbxHeaderFieldID.StreamStartBytes:
					Debug.Assert(m_uFileVersion <= FileVersion32_3);
					m_pbStreamStartBytes = pbData;
					break;

				case KdbxHeaderFieldID.InnerRandomStreamID:
					SetInnerRandomStreamID(pbData);
					break;

				case KdbxHeaderFieldID.KdfParameters:
					m_pwDatabase.KdfParameters = KdfParameters.DeserializeExt(pbData);
					break;

				case KdbxHeaderFieldID.PublicCustomData:
					Debug.Assert(m_pwDatabase.PublicCustomData.Count == 0);
					m_pwDatabase.PublicCustomData = VariantDictionary.Deserialize(pbData);
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
			if((pbID == null) || (pbID.Length != (int)PwUuid.UuidSize))
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
