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
using System.Drawing;
using System.Security.Cryptography;
#endif

#if KeePassLibSD
using KeePassLibSD;
#else
using System.IO.Compression;
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
using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
	/// <summary>
	/// Serialization to KeePass KDBX files.
	/// </summary>
	public sealed partial class KdbxFile
	{
		// public void Save(string strFile, PwGroup pgDataSource, KdbxFormat fmt,
		//	IStatusLogger slLogger)
		// {
		//	bool bMadeUnhidden = UrlUtil.UnhideFile(strFile);
		//
		//	IOConnectionInfo ioc = IOConnectionInfo.FromPath(strFile);
		//	this.Save(IOConnection.OpenWrite(ioc), pgDataSource, format, slLogger);
		//
		//	if(bMadeUnhidden) UrlUtil.HideFile(strFile, true); // Hide again
		// }

		/// <summary>
		/// Save the contents of the current <c>PwDatabase</c> to a KDBX file.
		/// </summary>
		/// <param name="sSaveTo">Stream to write the KDBX file into.</param>
		/// <param name="pgDataSource">Group containing all groups and
		/// entries to write. If <c>null</c>, the complete database will
		/// be written.</param>
		/// <param name="fmt">Format of the file to create.</param>
		/// <param name="slLogger">Logger that recieves status information.</param>
		public void Save(Stream sSaveTo, PwGroup pgDataSource, KdbxFormat fmt,
			IStatusLogger slLogger)
		{
			Debug.Assert(sSaveTo != null);
			if(sSaveTo == null) throw new ArgumentNullException("sSaveTo");

			if(m_bUsedOnce)
				throw new InvalidOperationException("Do not reuse KdbxFile objects!");
			m_bUsedOnce = true;

			m_format = fmt;
			m_slLogger = slLogger;

			PwGroup pgRoot = (pgDataSource ?? m_pwDatabase.RootGroup);
			UTF8Encoding encNoBom = StrUtil.Utf8;
			CryptoRandom cr = CryptoRandom.Instance;
			byte[] pbCipherKey = null;
			byte[] pbHmacKey64 = null;

			m_pbsBinaries.Clear();
			m_pbsBinaries.AddFrom(pgRoot);

			List<Stream> lStreams = new List<Stream>();
			lStreams.Add(sSaveTo);

			HashingStreamEx sHashing = new HashingStreamEx(sSaveTo, true, null);
			lStreams.Add(sHashing);

			try
			{
				m_uFileVersion = GetMinKdbxVersion();

				int cbEncKey, cbEncIV;
				ICipherEngine iCipher = GetCipher(out cbEncKey, out cbEncIV);

				m_pbMasterSeed = cr.GetRandomBytes(32);
				m_pbEncryptionIV = cr.GetRandomBytes((uint)cbEncIV);

				// m_pbTransformSeed = cr.GetRandomBytes(32);
				PwUuid puKdf = m_pwDatabase.KdfParameters.KdfUuid;
				KdfEngine kdf = KdfPool.Get(puKdf);
				if(kdf == null)
					throw new Exception(KLRes.UnknownKdf + MessageService.NewParagraph +
						// KLRes.FileNewVerOrPlgReq + MessageService.NewParagraph +
						"UUID: " + puKdf.ToHexString() + ".");
				kdf.Randomize(m_pwDatabase.KdfParameters);

				if(m_format == KdbxFormat.Default)
				{
					if(m_uFileVersion < FileVersion32_4)
					{
						m_craInnerRandomStream = CrsAlgorithm.Salsa20;
						m_pbInnerRandomStreamKey = cr.GetRandomBytes(32);
					}
					else // KDBX >= 4
					{
						m_craInnerRandomStream = CrsAlgorithm.ChaCha20;
						m_pbInnerRandomStreamKey = cr.GetRandomBytes(64);
					}

					m_randomStream = new CryptoRandomStream(m_craInnerRandomStream,
						m_pbInnerRandomStreamKey);
				}

				if(m_uFileVersion < FileVersion32_4)
					m_pbStreamStartBytes = cr.GetRandomBytes(32);

				Stream sXml;
				if(m_format == KdbxFormat.Default)
				{
					byte[] pbHeader = GenerateHeader();
					m_pbHashOfHeader = CryptoUtil.HashSha256(pbHeader);

					MemUtil.Write(sHashing, pbHeader);
					sHashing.Flush();

					ComputeKeys(out pbCipherKey, cbEncKey, out pbHmacKey64);

					Stream sPlain;
					if(m_uFileVersion < FileVersion32_4)
					{
						Stream sEncrypted = EncryptStream(sHashing, iCipher,
							pbCipherKey, cbEncIV, true);
						if((sEncrypted == null) || (sEncrypted == sHashing))
							throw new SecurityException(KLRes.CryptoStreamFailed);
						lStreams.Add(sEncrypted);

						MemUtil.Write(sEncrypted, m_pbStreamStartBytes);

						sPlain = new HashedBlockStream(sEncrypted, true);
					}
					else // KDBX >= 4
					{
						// For integrity checking (without knowing the master key)
						MemUtil.Write(sHashing, m_pbHashOfHeader);

						byte[] pbHeaderHmac = ComputeHeaderHmac(pbHeader, pbHmacKey64);
						MemUtil.Write(sHashing, pbHeaderHmac);

						Stream sBlocks = new HmacBlockStream(sHashing, true,
							true, pbHmacKey64);
						lStreams.Add(sBlocks);

						sPlain = EncryptStream(sBlocks, iCipher, pbCipherKey,
							cbEncIV, true);
						if((sPlain == null) || (sPlain == sBlocks))
							throw new SecurityException(KLRes.CryptoStreamFailed);
					}
					lStreams.Add(sPlain);

					if(m_pwDatabase.Compression == PwCompressionAlgorithm.GZip)
					{
						sXml = new GZipStream(sPlain, CompressionMode.Compress);
						lStreams.Add(sXml);
					}
					else sXml = sPlain;

					if(m_uFileVersion >= FileVersion32_4)
						WriteInnerHeader(sXml); // Binary header before XML
				}
				else if(m_format == KdbxFormat.PlainXml)
					sXml = sHashing;
				else
				{
					Debug.Assert(false);
					throw new ArgumentOutOfRangeException("fmt");
				}

#if KeePassUAP
				XmlWriterSettings xws = new XmlWriterSettings();
				xws.Encoding = encNoBom;
				xws.Indent = true;
				xws.IndentChars = "\t";
				xws.NewLineOnAttributes = false;

				XmlWriter xw = XmlWriter.Create(sXml, xws);
#else
				XmlTextWriter xw = new XmlTextWriter(sXml, encNoBom);

				xw.Formatting = Formatting.Indented;
				xw.IndentChar = '\t';
				xw.Indentation = 1;
#endif
				m_xmlWriter = xw;

				WriteDocument(pgRoot);

				m_xmlWriter.Flush();
				m_xmlWriter.Close();
			}
			finally
			{
				if(pbCipherKey != null) MemUtil.ZeroByteArray(pbCipherKey);
				if(pbHmacKey64 != null) MemUtil.ZeroByteArray(pbHmacKey64);

				CommonCleanUpWrite(lStreams, sHashing);
			}
		}

		private void CommonCleanUpWrite(List<Stream> lStreams, HashingStreamEx sHashing)
		{
			CloseStreams(lStreams);

			Debug.Assert(lStreams.Contains(sHashing)); // sHashing must be closed
			m_pbHashOfFileOnDisk = sHashing.Hash;
			Debug.Assert(m_pbHashOfFileOnDisk != null);

			CleanUpInnerRandomStream();

			m_xmlWriter = null;
			m_pbHashOfHeader = null;
		}

		private byte[] GenerateHeader()
		{
			byte[] pbHeader;
			using(MemoryStream ms = new MemoryStream())
			{
				MemUtil.Write(ms, MemUtil.UInt32ToBytes(FileSignature1));
				MemUtil.Write(ms, MemUtil.UInt32ToBytes(FileSignature2));
				MemUtil.Write(ms, MemUtil.UInt32ToBytes(m_uFileVersion));

				WriteHeaderField(ms, KdbxHeaderFieldID.CipherID,
					m_pwDatabase.DataCipherUuid.UuidBytes);

				int nCprID = (int)m_pwDatabase.Compression;
				WriteHeaderField(ms, KdbxHeaderFieldID.CompressionFlags,
					MemUtil.UInt32ToBytes((uint)nCprID));

				WriteHeaderField(ms, KdbxHeaderFieldID.MasterSeed, m_pbMasterSeed);

				if(m_uFileVersion < FileVersion32_4)
				{
					Debug.Assert(m_pwDatabase.KdfParameters.KdfUuid.Equals(
						(new AesKdf()).Uuid));
					WriteHeaderField(ms, KdbxHeaderFieldID.TransformSeed,
						m_pwDatabase.KdfParameters.GetByteArray(AesKdf.ParamSeed));
					WriteHeaderField(ms, KdbxHeaderFieldID.TransformRounds,
						MemUtil.UInt64ToBytes(m_pwDatabase.KdfParameters.GetUInt64(
						AesKdf.ParamRounds, PwDefs.DefaultKeyEncryptionRounds)));
				}
				else
					WriteHeaderField(ms, KdbxHeaderFieldID.KdfParameters,
						KdfParameters.SerializeExt(m_pwDatabase.KdfParameters));

				if(m_pbEncryptionIV.Length > 0)
					WriteHeaderField(ms, KdbxHeaderFieldID.EncryptionIV, m_pbEncryptionIV);

				if(m_uFileVersion < FileVersion32_4)
				{
					WriteHeaderField(ms, KdbxHeaderFieldID.InnerRandomStreamKey,
						m_pbInnerRandomStreamKey);

					WriteHeaderField(ms, KdbxHeaderFieldID.StreamStartBytes,
						m_pbStreamStartBytes);

					int nIrsID = (int)m_craInnerRandomStream;
					WriteHeaderField(ms, KdbxHeaderFieldID.InnerRandomStreamID,
						MemUtil.Int32ToBytes(nIrsID));
				}

				// Write public custom data only when there is at least one item,
				// because KDBX 3.1 didn't support this field yet
				if(m_pwDatabase.PublicCustomData.Count > 0)
					WriteHeaderField(ms, KdbxHeaderFieldID.PublicCustomData,
						VariantDictionary.Serialize(m_pwDatabase.PublicCustomData));

				WriteHeaderField(ms, KdbxHeaderFieldID.EndOfHeader, new byte[] {
					(byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' });

				pbHeader = ms.ToArray();
			}

			return pbHeader;
		}

		private void WriteHeaderField(Stream s, KdbxHeaderFieldID kdbID,
			byte[] pbData)
		{
			s.WriteByte((byte)kdbID);

			byte[] pb = (pbData ?? MemUtil.EmptyByteArray);
			int cb = pb.Length;
			if(cb < 0) { Debug.Assert(false); throw new OutOfMemoryException(); }

			Debug.Assert(m_uFileVersion > 0);
			if(m_uFileVersion < FileVersion32_4)
			{
				if(cb > (int)ushort.MaxValue)
				{
					Debug.Assert(false);
					throw new ArgumentOutOfRangeException("pbData");
				}

				MemUtil.Write(s, MemUtil.UInt16ToBytes((ushort)cb));
			}
			else MemUtil.Write(s, MemUtil.Int32ToBytes(cb));

			MemUtil.Write(s, pb);
		}

		private void WriteInnerHeader(Stream s)
		{
			int nIrsID = (int)m_craInnerRandomStream;
			WriteInnerHeaderField(s, KdbxInnerHeaderFieldID.InnerRandomStreamID,
				MemUtil.Int32ToBytes(nIrsID), null);

			WriteInnerHeaderField(s, KdbxInnerHeaderFieldID.InnerRandomStreamKey,
				m_pbInnerRandomStreamKey, null);

			ProtectedBinary[] vBin = m_pbsBinaries.ToArray();
			for(int i = 0; i < vBin.Length; ++i)
			{
				ProtectedBinary pb = vBin[i];
				if(pb == null) throw new InvalidOperationException();

				KdbxBinaryFlags f = KdbxBinaryFlags.None;
				if(pb.IsProtected) f |= KdbxBinaryFlags.Protected;

				byte[] pbFlags = new byte[1] { (byte)f };
				byte[] pbData = pb.ReadData();

				WriteInnerHeaderField(s, KdbxInnerHeaderFieldID.Binary,
					pbFlags, pbData);

				if(pb.IsProtected) MemUtil.ZeroByteArray(pbData);
			}

			WriteInnerHeaderField(s, KdbxInnerHeaderFieldID.EndOfHeader,
				null, null);
		}

		private void WriteInnerHeaderField(Stream s, KdbxInnerHeaderFieldID kdbID,
			byte[] pbData1, byte[] pbData2)
		{
			s.WriteByte((byte)kdbID);

			byte[] pb1 = (pbData1 ?? MemUtil.EmptyByteArray);
			byte[] pb2 = (pbData2 ?? MemUtil.EmptyByteArray);

			int cb = pb1.Length + pb2.Length;
			if(cb < 0) { Debug.Assert(false); throw new OutOfMemoryException(); }

			MemUtil.Write(s, MemUtil.Int32ToBytes(cb));
			MemUtil.Write(s, pb1);
			MemUtil.Write(s, pb2);
		}

		private void WriteDocument(PwGroup pgRoot)
		{
			Debug.Assert(m_xmlWriter != null);
			if(m_xmlWriter == null) throw new InvalidOperationException();

			uint uNumGroups, uNumEntries, uCurEntry = 0;
			pgRoot.GetCounts(true, out uNumGroups, out uNumEntries);

			m_xmlWriter.WriteStartDocument(true);
			m_xmlWriter.WriteStartElement(ElemDocNode);

			WriteMeta();

			m_xmlWriter.WriteStartElement(ElemRoot);
			StartGroup(pgRoot);

			Stack<PwGroup> groupStack = new Stack<PwGroup>();
			groupStack.Push(pgRoot);

			GroupHandler gh = delegate(PwGroup pg)
			{
				Debug.Assert(pg != null);
				if(pg == null) throw new ArgumentNullException("pg");

				while(true)
				{
					if(pg.ParentGroup == groupStack.Peek())
					{
						groupStack.Push(pg);
						StartGroup(pg);
						break;
					}
					else
					{
						groupStack.Pop();
						if(groupStack.Count <= 0) return false;

						EndGroup();
					}
				}

				return true;
			};

			EntryHandler eh = delegate(PwEntry pe)
			{
				Debug.Assert(pe != null);
				WriteEntry(pe, false);

				++uCurEntry;
				if(m_slLogger != null)
					if(!m_slLogger.SetProgress((100 * uCurEntry) / uNumEntries))
						return false;

				return true;
			};

			if(!pgRoot.TraverseTree(TraversalMethod.PreOrder, gh, eh))
				throw new InvalidOperationException();

			while(groupStack.Count > 1)
			{
				m_xmlWriter.WriteEndElement();
				groupStack.Pop();
			}

			EndGroup();

			WriteList(ElemDeletedObjects, m_pwDatabase.DeletedObjects);
			m_xmlWriter.WriteEndElement(); // Root

			m_xmlWriter.WriteEndElement(); // ElemDocNode
			m_xmlWriter.WriteEndDocument();
		}

		private void WriteMeta()
		{
			m_xmlWriter.WriteStartElement(ElemMeta);

			WriteObject(ElemGenerator, PwDatabase.LocalizedAppName, false);

			if((m_pbHashOfHeader != null) && (m_uFileVersion < FileVersion32_4))
				WriteObject(ElemHeaderHash, Convert.ToBase64String(
					m_pbHashOfHeader), false);

			if(m_uFileVersion >= FileVersion32_4)
				WriteObject(ElemSettingsChanged, m_pwDatabase.SettingsChanged);

			WriteObject(ElemDbName, m_pwDatabase.Name, true);
			WriteObject(ElemDbNameChanged, m_pwDatabase.NameChanged);
			WriteObject(ElemDbDesc, m_pwDatabase.Description, true);
			WriteObject(ElemDbDescChanged, m_pwDatabase.DescriptionChanged);
			WriteObject(ElemDbDefaultUser, m_pwDatabase.DefaultUserName, true);
			WriteObject(ElemDbDefaultUserChanged, m_pwDatabase.DefaultUserNameChanged);
			WriteObject(ElemDbMntncHistoryDays, m_pwDatabase.MaintenanceHistoryDays);
			WriteObject(ElemDbColor, StrUtil.ColorToUnnamedHtml(m_pwDatabase.Color, true), false);
			WriteObject(ElemDbKeyChanged, m_pwDatabase.MasterKeyChanged);
			WriteObject(ElemDbKeyChangeRec, m_pwDatabase.MasterKeyChangeRec);
			WriteObject(ElemDbKeyChangeForce, m_pwDatabase.MasterKeyChangeForce);
			if(m_pwDatabase.MasterKeyChangeForceOnce)
				WriteObject(ElemDbKeyChangeForceOnce, true);

			WriteList(ElemMemoryProt, m_pwDatabase.MemoryProtection);

			WriteCustomIconList();

			WriteObject(ElemRecycleBinEnabled, m_pwDatabase.RecycleBinEnabled);
			WriteObject(ElemRecycleBinUuid, m_pwDatabase.RecycleBinUuid);
			WriteObject(ElemRecycleBinChanged, m_pwDatabase.RecycleBinChanged);
			WriteObject(ElemEntryTemplatesGroup, m_pwDatabase.EntryTemplatesGroup);
			WriteObject(ElemEntryTemplatesGroupChanged, m_pwDatabase.EntryTemplatesGroupChanged);
			WriteObject(ElemHistoryMaxItems, m_pwDatabase.HistoryMaxItems);
			WriteObject(ElemHistoryMaxSize, m_pwDatabase.HistoryMaxSize);

			WriteObject(ElemLastSelectedGroup, m_pwDatabase.LastSelectedGroup);
			WriteObject(ElemLastTopVisibleGroup, m_pwDatabase.LastTopVisibleGroup);

			if((m_format != KdbxFormat.Default) || (m_uFileVersion < FileVersion32_4))
				WriteBinPool();

			WriteList(ElemCustomData, m_pwDatabase.CustomData);

			m_xmlWriter.WriteEndElement();
		}

		private void StartGroup(PwGroup pg)
		{
			m_xmlWriter.WriteStartElement(ElemGroup);
			WriteObject(ElemUuid, pg.Uuid);
			WriteObject(ElemName, pg.Name, true);
			WriteObject(ElemNotes, pg.Notes, true);
			WriteObject(ElemIcon, (int)pg.IconId);
			
			if(!pg.CustomIconUuid.Equals(PwUuid.Zero))
				WriteObject(ElemCustomIconID, pg.CustomIconUuid);
			
			WriteList(ElemTimes, pg);
			WriteObject(ElemIsExpanded, pg.IsExpanded);
			WriteObject(ElemGroupDefaultAutoTypeSeq, pg.DefaultAutoTypeSequence, true);
			WriteObject(ElemEnableAutoType, StrUtil.BoolToStringEx(pg.EnableAutoType), false);
			WriteObject(ElemEnableSearching, StrUtil.BoolToStringEx(pg.EnableSearching), false);
			WriteObject(ElemLastTopVisibleEntry, pg.LastTopVisibleEntry);

			if(pg.CustomData.Count > 0)
				WriteList(ElemCustomData, pg.CustomData);
		}

		private void EndGroup()
		{
			m_xmlWriter.WriteEndElement(); // Close group element
		}

		private void WriteEntry(PwEntry pe, bool bIsHistory)
		{
			Debug.Assert(pe != null); if(pe == null) throw new ArgumentNullException("pe");

			m_xmlWriter.WriteStartElement(ElemEntry);

			WriteObject(ElemUuid, pe.Uuid);
			WriteObject(ElemIcon, (int)pe.IconId);

			if(!pe.CustomIconUuid.Equals(PwUuid.Zero))
				WriteObject(ElemCustomIconID, pe.CustomIconUuid);

			WriteObject(ElemFgColor, StrUtil.ColorToUnnamedHtml(pe.ForegroundColor, true), false);
			WriteObject(ElemBgColor, StrUtil.ColorToUnnamedHtml(pe.BackgroundColor, true), false);
			WriteObject(ElemOverrideUrl, pe.OverrideUrl, true);
			WriteObject(ElemTags, StrUtil.TagsToString(pe.Tags, false), true);

			WriteList(ElemTimes, pe);

			WriteList(pe.Strings, true);
			WriteList(pe.Binaries);
			WriteList(ElemAutoType, pe.AutoType);

			if(pe.CustomData.Count > 0)
				WriteList(ElemCustomData, pe.CustomData);

			if(!bIsHistory) WriteList(ElemHistory, pe.History, true);
			else { Debug.Assert(pe.History.UCount == 0); }

			m_xmlWriter.WriteEndElement();
		}

		private void WriteList(ProtectedStringDictionary dictStrings, bool bEntryStrings)
		{
			Debug.Assert(dictStrings != null);
			if(dictStrings == null) throw new ArgumentNullException("dictStrings");

			foreach(KeyValuePair<string, ProtectedString> kvp in dictStrings)
				WriteObject(kvp.Key, kvp.Value, bEntryStrings);
		}

		private void WriteList(ProtectedBinaryDictionary dictBinaries)
		{
			Debug.Assert(dictBinaries != null);
			if(dictBinaries == null) throw new ArgumentNullException("dictBinaries");

			foreach(KeyValuePair<string, ProtectedBinary> kvp in dictBinaries)
				WriteObject(kvp.Key, kvp.Value, true);
		}

		private void WriteList(string name, AutoTypeConfig cfgAutoType)
		{
			Debug.Assert(name != null);
			Debug.Assert(cfgAutoType != null);
			if(cfgAutoType == null) throw new ArgumentNullException("cfgAutoType");

			m_xmlWriter.WriteStartElement(name);

			WriteObject(ElemAutoTypeEnabled, cfgAutoType.Enabled);
			WriteObject(ElemAutoTypeObfuscation, (int)cfgAutoType.ObfuscationOptions);

			if(cfgAutoType.DefaultSequence.Length > 0)
				WriteObject(ElemAutoTypeDefaultSeq, cfgAutoType.DefaultSequence, true);

			foreach(AutoTypeAssociation a in cfgAutoType.Associations)
				WriteObject(ElemAutoTypeItem, ElemWindow, ElemKeystrokeSequence,
					new KeyValuePair<string, string>(a.WindowName, a.Sequence));

			m_xmlWriter.WriteEndElement();
		}

		private void WriteList(string name, ITimeLogger times)
		{
			Debug.Assert(name != null);
			Debug.Assert(times != null); if(times == null) throw new ArgumentNullException("times");

			m_xmlWriter.WriteStartElement(name);

			WriteObject(ElemCreationTime, times.CreationTime);
			WriteObject(ElemLastModTime, times.LastModificationTime);
			WriteObject(ElemLastAccessTime, times.LastAccessTime);
			WriteObject(ElemExpiryTime, times.ExpiryTime);
			WriteObject(ElemExpires, times.Expires);
			WriteObject(ElemUsageCount, times.UsageCount);
			WriteObject(ElemLocationChanged, times.LocationChanged);

			m_xmlWriter.WriteEndElement(); // Name
		}

		private void WriteList(string name, PwObjectList<PwEntry> value, bool bIsHistory)
		{
			Debug.Assert(name != null);
			Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");

			m_xmlWriter.WriteStartElement(name);

			foreach(PwEntry pe in value)
				WriteEntry(pe, bIsHistory);

			m_xmlWriter.WriteEndElement();
		}

		private void WriteList(string name, PwObjectList<PwDeletedObject> value)
		{
			Debug.Assert(name != null);
			Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");

			m_xmlWriter.WriteStartElement(name);

			foreach(PwDeletedObject pdo in value)
				WriteObject(ElemDeletedObject, pdo);

			m_xmlWriter.WriteEndElement();
		}

		private void WriteList(string name, MemoryProtectionConfig value)
		{
			Debug.Assert(name != null);
			Debug.Assert(value != null);

			m_xmlWriter.WriteStartElement(name);

			WriteObject(ElemProtTitle, value.ProtectTitle);
			WriteObject(ElemProtUserName, value.ProtectUserName);
			WriteObject(ElemProtPassword, value.ProtectPassword);
			WriteObject(ElemProtUrl, value.ProtectUrl);
			WriteObject(ElemProtNotes, value.ProtectNotes);
			// WriteObject(ElemProtAutoHide, value.AutoEnableVisualHiding);

			m_xmlWriter.WriteEndElement();
		}

		private void WriteList(string name, StringDictionaryEx value)
		{
			Debug.Assert(name != null);
			Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");

			m_xmlWriter.WriteStartElement(name);

			foreach(KeyValuePair<string, string> kvp in value)
				WriteObject(ElemStringDictExItem, ElemKey, ElemValue, kvp);

			m_xmlWriter.WriteEndElement();
		}

		private void WriteCustomIconList()
		{
			if(m_pwDatabase.CustomIcons.Count == 0) return;

			m_xmlWriter.WriteStartElement(ElemCustomIcons);

			foreach(PwCustomIcon pwci in m_pwDatabase.CustomIcons)
			{
				m_xmlWriter.WriteStartElement(ElemCustomIconItem);

				WriteObject(ElemCustomIconItemID, pwci.Uuid);

				string strData = Convert.ToBase64String(pwci.ImageDataPng);
				WriteObject(ElemCustomIconItemData, strData, false);

				m_xmlWriter.WriteEndElement();
			}

			m_xmlWriter.WriteEndElement();
		}

		private void WriteObject(string name, string value,
			bool bFilterValueXmlChars)
		{
			Debug.Assert(name != null);
			Debug.Assert(value != null);

			m_xmlWriter.WriteStartElement(name);

			if(bFilterValueXmlChars)
				m_xmlWriter.WriteString(StrUtil.SafeXmlString(value));
			else m_xmlWriter.WriteString(value);

			m_xmlWriter.WriteEndElement();
		}

		private void WriteObject(string name, bool value)
		{
			Debug.Assert(name != null);

			WriteObject(name, value ? ValTrue : ValFalse, false);
		}

		private void WriteObject(string name, PwUuid value)
		{
			Debug.Assert(name != null);
			Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");

			WriteObject(name, Convert.ToBase64String(value.UuidBytes), false);
		}

		private void WriteObject(string name, int value)
		{
			Debug.Assert(name != null);

			m_xmlWriter.WriteStartElement(name);
			m_xmlWriter.WriteString(value.ToString(NumberFormatInfo.InvariantInfo));
			m_xmlWriter.WriteEndElement();
		}

		private void WriteObject(string name, uint value)
		{
			Debug.Assert(name != null);

			m_xmlWriter.WriteStartElement(name);
			m_xmlWriter.WriteString(value.ToString(NumberFormatInfo.InvariantInfo));
			m_xmlWriter.WriteEndElement();
		}

		private void WriteObject(string name, long value)
		{
			Debug.Assert(name != null);

			m_xmlWriter.WriteStartElement(name);
			m_xmlWriter.WriteString(value.ToString(NumberFormatInfo.InvariantInfo));
			m_xmlWriter.WriteEndElement();
		}

		private void WriteObject(string name, ulong value)
		{
			Debug.Assert(name != null);

			m_xmlWriter.WriteStartElement(name);
			m_xmlWriter.WriteString(value.ToString(NumberFormatInfo.InvariantInfo));
			m_xmlWriter.WriteEndElement();
		}

		private void WriteObject(string name, DateTime value)
		{
			Debug.Assert(name != null);
			Debug.Assert(value.Kind == DateTimeKind.Utc);

			// Cf. ReadTime
			if((m_format == KdbxFormat.Default) && (m_uFileVersion >= FileVersion32_4))
			{
				DateTime dt = TimeUtil.ToUtc(value, false);

				// DateTime dtBase = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
				// dt -= new TimeSpan(dtBase.Ticks);

				// WriteObject(name, dt.ToBinary());

				// dt = TimeUtil.RoundToMultOf2PowLess1s(dt);
				// long lBin = dt.ToBinary();

				long lSec = dt.Ticks / TimeSpan.TicksPerSecond;
				// WriteObject(name, lSec);

				byte[] pb = MemUtil.Int64ToBytes(lSec);
				WriteObject(name, Convert.ToBase64String(pb), false);
			}
			else WriteObject(name, TimeUtil.SerializeUtc(value), false);
		}

		private void WriteObject(string name, string strKeyName,
			string strValueName, KeyValuePair<string, string> kvp)
		{
			m_xmlWriter.WriteStartElement(name);

			m_xmlWriter.WriteStartElement(strKeyName);
			m_xmlWriter.WriteString(StrUtil.SafeXmlString(kvp.Key));
			m_xmlWriter.WriteEndElement();
			m_xmlWriter.WriteStartElement(strValueName);
			m_xmlWriter.WriteString(StrUtil.SafeXmlString(kvp.Value));
			m_xmlWriter.WriteEndElement();

			m_xmlWriter.WriteEndElement();
		}

		private void WriteObject(string name, ProtectedString value, bool bIsEntryString)
		{
			Debug.Assert(name != null);
			Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");

			m_xmlWriter.WriteStartElement(ElemString);
			m_xmlWriter.WriteStartElement(ElemKey);
			m_xmlWriter.WriteString(StrUtil.SafeXmlString(name));
			m_xmlWriter.WriteEndElement();
			m_xmlWriter.WriteStartElement(ElemValue);

			bool bProtected = value.IsProtected;
			if(bIsEntryString)
			{
				// Adjust memory protection setting (which might be different
				// from the database default, e.g. due to an import which
				// didn't specify the correct setting)
				if(name == PwDefs.TitleField)
					bProtected = m_pwDatabase.MemoryProtection.ProtectTitle;
				else if(name == PwDefs.UserNameField)
					bProtected = m_pwDatabase.MemoryProtection.ProtectUserName;
				else if(name == PwDefs.PasswordField)
					bProtected = m_pwDatabase.MemoryProtection.ProtectPassword;
				else if(name == PwDefs.UrlField)
					bProtected = m_pwDatabase.MemoryProtection.ProtectUrl;
				else if(name == PwDefs.NotesField)
					bProtected = m_pwDatabase.MemoryProtection.ProtectNotes;
			}

			if(bProtected && (m_format == KdbxFormat.Default))
			{
				m_xmlWriter.WriteAttributeString(AttrProtected, ValTrue);

				byte[] pbEncoded = value.ReadXorredString(m_randomStream);
				if(pbEncoded.Length > 0)
					m_xmlWriter.WriteBase64(pbEncoded, 0, pbEncoded.Length);
			}
			else
			{
				string strValue = value.ReadString();

				// If names should be localized, we need to apply the language-dependent
				// string transformation here. By default, language-dependent conversions
				// should be applied, otherwise characters could be rendered incorrectly
				// (code page problems).
				if(m_bLocalizedNames)
				{
					StringBuilder sb = new StringBuilder();
					foreach(char ch in strValue)
					{
						char chMapped = ch;

						// Symbols and surrogates must be moved into the correct code
						// page area
						if(char.IsSymbol(ch) || char.IsSurrogate(ch))
						{
							System.Globalization.UnicodeCategory cat =
								CharUnicodeInfo.GetUnicodeCategory(ch);
							// Map character to correct position in code page
							chMapped = (char)((int)cat * 32 + ch);
						}
						else if(char.IsControl(ch))
						{
							if(ch >= 256) // Control character in high ANSI code page
							{
								// Some of the control characters map to corresponding ones
								// in the low ANSI range (up to 255) when calling
								// ToLower on them with invariant culture (see
								// http://lists.ximian.com/pipermail/mono-patches/2002-February/086106.html )
#if !KeePassLibSD
								chMapped = char.ToLowerInvariant(ch);
#else
								chMapped = char.ToLower(ch);
#endif
							}
						}

						sb.Append(chMapped);
					}

					strValue = sb.ToString(); // Correct string for current code page
				}

				if((m_format == KdbxFormat.PlainXml) && bProtected)
					m_xmlWriter.WriteAttributeString(AttrProtectedInMemPlainXml, ValTrue);

				m_xmlWriter.WriteString(StrUtil.SafeXmlString(strValue));
			}

			m_xmlWriter.WriteEndElement(); // ElemValue
			m_xmlWriter.WriteEndElement(); // ElemString
		}

		private void WriteObject(string name, ProtectedBinary value, bool bAllowRef)
		{
			Debug.Assert(name != null);
			Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");

			m_xmlWriter.WriteStartElement(ElemBinary);
			m_xmlWriter.WriteStartElement(ElemKey);
			m_xmlWriter.WriteString(StrUtil.SafeXmlString(name));
			m_xmlWriter.WriteEndElement();
			m_xmlWriter.WriteStartElement(ElemValue);

			string strRef = null;
			if(bAllowRef)
			{
				int iRef = m_pbsBinaries.Find(value);
				if(iRef >= 0) strRef = iRef.ToString(NumberFormatInfo.InvariantInfo);
				else { Debug.Assert(false); }
			}
			if(strRef != null)
				m_xmlWriter.WriteAttributeString(AttrRef, strRef);
			else SubWriteValue(value);

			m_xmlWriter.WriteEndElement(); // ElemValue
			m_xmlWriter.WriteEndElement(); // ElemBinary
		}

		private void SubWriteValue(ProtectedBinary value)
		{
			if(value.IsProtected && (m_format == KdbxFormat.Default))
			{
				m_xmlWriter.WriteAttributeString(AttrProtected, ValTrue);

				byte[] pbEncoded = value.ReadXorredData(m_randomStream);
				if(pbEncoded.Length > 0)
					m_xmlWriter.WriteBase64(pbEncoded, 0, pbEncoded.Length);
			}
			else
			{
				if(m_pwDatabase.Compression != PwCompressionAlgorithm.None)
				{
					m_xmlWriter.WriteAttributeString(AttrCompressed, ValTrue);

					byte[] pbRaw = value.ReadData();
					byte[] pbCmp = MemUtil.Compress(pbRaw);
					m_xmlWriter.WriteBase64(pbCmp, 0, pbCmp.Length);

					if(value.IsProtected)
					{
						MemUtil.ZeroByteArray(pbRaw);
						MemUtil.ZeroByteArray(pbCmp);
					}
				}
				else
				{
					byte[] pbRaw = value.ReadData();
					m_xmlWriter.WriteBase64(pbRaw, 0, pbRaw.Length);

					if(value.IsProtected) MemUtil.ZeroByteArray(pbRaw);
				}
			}
		}

		private void WriteObject(string name, PwDeletedObject value)
		{
			Debug.Assert(name != null);
			Debug.Assert(value != null); if(value == null) throw new ArgumentNullException("value");

			m_xmlWriter.WriteStartElement(name);
			WriteObject(ElemUuid, value.Uuid);
			WriteObject(ElemDeletionTime, value.DeletionTime);
			m_xmlWriter.WriteEndElement();
		}

		private void WriteBinPool()
		{
			m_xmlWriter.WriteStartElement(ElemBinaries);

			ProtectedBinary[] v = m_pbsBinaries.ToArray();
			for(int i = 0; i < v.Length; ++i)
			{
				m_xmlWriter.WriteStartElement(ElemBinary);
				m_xmlWriter.WriteAttributeString(AttrId,
					i.ToString(NumberFormatInfo.InvariantInfo));
				SubWriteValue(v[i]);
				m_xmlWriter.WriteEndElement();
			}

			m_xmlWriter.WriteEndElement();
		}

		[Obsolete]
		public static bool WriteEntries(Stream msOutput, PwEntry[] vEntries)
		{
			return WriteEntries(msOutput, null, vEntries);
		}

		public static bool WriteEntries(Stream msOutput, PwDatabase pdContext,
			PwEntry[] vEntries)
		{
			if(msOutput == null) { Debug.Assert(false); return false; }
			// pdContext may be null
			if(vEntries == null) { Debug.Assert(false); return false; }

			/* KdbxFile f = new KdbxFile(pwDatabase);
			f.m_format = KdbxFormat.PlainXml;

			XmlTextWriter xtw = null;
			try { xtw = new XmlTextWriter(msOutput, StrUtil.Utf8); }
			catch(Exception) { Debug.Assert(false); return false; }
			if(xtw == null) { Debug.Assert(false); return false; }

			f.m_xmlWriter = xtw;

			xtw.Formatting = Formatting.Indented;
			xtw.IndentChar = '\t';
			xtw.Indentation = 1;

			xtw.WriteStartDocument(true);
			xtw.WriteStartElement(ElemRoot);

			foreach(PwEntry pe in vEntries)
				f.WriteEntry(pe, false);

			xtw.WriteEndElement();
			xtw.WriteEndDocument();

			xtw.Flush();
			xtw.Close();
			return true; */

			PwDatabase pd = new PwDatabase();
			pd.New(new IOConnectionInfo(), new CompositeKey());

			PwGroup pg = pd.RootGroup;
			if(pg == null) { Debug.Assert(false); return false; }

			foreach(PwEntry pe in vEntries)
			{
				PwUuid pu = pe.CustomIconUuid;
				if(!pu.Equals(PwUuid.Zero) && (pd.GetCustomIconIndex(pu) < 0))
				{
					int i = -1;
					if(pdContext != null) i = pdContext.GetCustomIconIndex(pu);
					if(i >= 0)
					{
						PwCustomIcon ci = pdContext.CustomIcons[i];
						pd.CustomIcons.Add(ci);
					}
					else { Debug.Assert(pdContext == null); }
				}

				PwEntry peCopy = pe.CloneDeep();
				pg.AddEntry(peCopy, true);
			}

			KdbxFile f = new KdbxFile(pd);
			f.Save(msOutput, null, KdbxFormat.PlainXml, null);
			return true;
		}
	}
}
