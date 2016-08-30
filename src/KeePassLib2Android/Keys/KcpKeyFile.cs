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
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

#if !KeePassUAP
using System.Security.Cryptography;
#endif

using KeePassLib.Cryptography;
using KeePassLib.Resources;
using KeePassLib.Security;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace KeePassLib.Keys
{
	/// <summary>
	/// Key files as provided by the user.
	/// </summary>
	public sealed class KcpKeyFile : IUserKey
	{
		private string m_strPath;
		private ProtectedBinary m_pbKeyData;

		/// <summary>
		/// Path to the key file.
		/// </summary>
		public string Path
		{
			get { return m_strPath; }
		}

		/// <summary>
		/// Get key data. Querying this property is fast (it returns a
		/// reference to a cached <c>ProtectedBinary</c> object).
		/// If no key data is available, <c>null</c> is returned.
		/// </summary>
		public ProtectedBinary KeyData
		{
			get { return m_pbKeyData; }
		}

		public KcpKeyFile(string strKeyFile)
		{
			Construct(IOConnectionInfo.FromPath(strKeyFile), false);
		}

		public KcpKeyFile(string strKeyFile, bool bThrowIfDbFile)
		{
			Construct(IOConnectionInfo.FromPath(strKeyFile), bThrowIfDbFile);
		}

		public KcpKeyFile(IOConnectionInfo iocKeyFile)
		{
			Construct(iocKeyFile, false);
		}

		public KcpKeyFile(IOConnectionInfo iocKeyFile, bool bThrowIfDbFile)
		{
			Construct(iocKeyFile, bThrowIfDbFile);
		}

		private void Construct(IOConnectionInfo iocFile, bool bThrowIfDbFile)
		{
			byte[] pbFileData = IOConnection.ReadFile(iocFile);
			if(pbFileData == null) throw new FileNotFoundException();

			if(bThrowIfDbFile && (pbFileData.Length >= 8))
			{
				uint uSig1 = MemUtil.BytesToUInt32(MemUtil.Mid(pbFileData, 0, 4));
				uint uSig2 = MemUtil.BytesToUInt32(MemUtil.Mid(pbFileData, 4, 4));

				if(((uSig1 == KdbxFile.FileSignature1) &&
					(uSig2 == KdbxFile.FileSignature2)) ||
					((uSig1 == KdbxFile.FileSignaturePreRelease1) &&
					(uSig2 == KdbxFile.FileSignaturePreRelease2)) ||
					((uSig1 == KdbxFile.FileSignatureOld1) &&
					(uSig2 == KdbxFile.FileSignatureOld2)))
#if KeePassLibSD
					throw new Exception(KLRes.KeyFileDbSel);
#else
					throw new InvalidDataException(KLRes.KeyFileDbSel);
#endif
			}

			byte[] pbKey = LoadXmlKeyFile(pbFileData);
			if(pbKey == null) pbKey = LoadKeyFile(pbFileData);

			if(pbKey == null) throw new InvalidOperationException();

			m_strPath = iocFile.Path;
			m_pbKeyData = new ProtectedBinary(true, pbKey);

			MemUtil.ZeroByteArray(pbKey);
		}

		// public void Clear()
		// {
		//	m_strPath = string.Empty;
		//	m_pbKeyData = null;
		// }

		private static byte[] LoadKeyFile(byte[] pbFileData)
		{
			if(pbFileData == null) { Debug.Assert(false); return null; }

			int iLength = pbFileData.Length;

			byte[] pbKey = null;
			if(iLength == 32) pbKey = LoadBinaryKey32(pbFileData);
			else if(iLength == 64) pbKey = LoadHexKey32(pbFileData);

			if(pbKey == null)
			{
				SHA256Managed sha256 = new SHA256Managed();
				pbKey = sha256.ComputeHash(pbFileData);
			}

			return pbKey;
		}

		private static byte[] LoadBinaryKey32(byte[] pbFileData)
		{
			if(pbFileData == null) { Debug.Assert(false); return null; }
			if(pbFileData.Length != 32) { Debug.Assert(false); return null; }

			return pbFileData;
		}

		private static byte[] LoadHexKey32(byte[] pbFileData)
		{
			if(pbFileData == null) { Debug.Assert(false); return null; }
			if(pbFileData.Length != 64) { Debug.Assert(false); return null; }

			try
			{
				string strHex = StrUtil.Utf8.GetString(pbFileData, 0, 64);
				if(!StrUtil.IsHexString(strHex, true)) return null;

				byte[] pbKey = MemUtil.HexStringToByteArray(strHex);
				if((pbKey == null) || (pbKey.Length != 32))
					return null;

				return pbKey;
			}
			catch(Exception) { Debug.Assert(false); }

			return null;
		}

		/// <summary>
		/// Create a new, random key-file.
		/// </summary>
		/// <param name="strFilePath">Path where the key-file should be saved to.
		/// If the file exists already, it will be overwritten.</param>
		/// <param name="pbAdditionalEntropy">Additional entropy used to generate
		/// the random key. May be <c>null</c> (in this case only the KeePass-internal
		/// random number generator is used).</param>
		/// <returns>Returns a <c>FileSaveResult</c> error code.</returns>
		public static void Create(string strFilePath, byte[] pbAdditionalEntropy)
		{
			byte[] pbKey32 = CryptoRandom.Instance.GetRandomBytes(32);
			if(pbKey32 == null) throw new SecurityException();

			byte[] pbFinalKey32;
			if((pbAdditionalEntropy == null) || (pbAdditionalEntropy.Length == 0))
				pbFinalKey32 = pbKey32;
			else
			{
				MemoryStream ms = new MemoryStream();
				ms.Write(pbAdditionalEntropy, 0, pbAdditionalEntropy.Length);
				ms.Write(pbKey32, 0, 32);

				SHA256Managed sha256 = new SHA256Managed();
				pbFinalKey32 = sha256.ComputeHash(ms.ToArray());
				ms.Close();
			}

			CreateXmlKeyFile(strFilePath, pbFinalKey32);
		}

		// ================================================================
		// XML Key Files
		// ================================================================

		// Sample XML file:
		// <?xml version="1.0" encoding="utf-8"?>
		// <KeyFile>
		//     <Meta>
		//         <Version>1.00</Version>
		//     </Meta>
		//     <Key>
		//         <Data>ySFoKuCcJblw8ie6RkMBdVCnAf4EedSch7ItujK6bmI=</Data>
		//     </Key>
		// </KeyFile>

		private const string RootElementName = "KeyFile";
		private const string MetaElementName = "Meta";
		private const string VersionElementName = "Version";
		private const string KeyElementName = "Key";
		private const string KeyDataElementName = "Data";

		private static byte[] LoadXmlKeyFile(byte[] pbFileData)
		{
			if(pbFileData == null) { Debug.Assert(false); return null; }

			MemoryStream ms = new MemoryStream(pbFileData, false);
			byte[] pbKeyData = null;

			try
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(ms);

				XmlElement el = doc.DocumentElement;
				if((el == null) || !el.Name.Equals(RootElementName)) return null;
				if(el.ChildNodes.Count < 2) return null;

				foreach(XmlNode xmlChild in el.ChildNodes)
				{
					if(xmlChild.Name.Equals(MetaElementName)) { } // Ignore Meta
					else if(xmlChild.Name == KeyElementName)
					{
						foreach(XmlNode xmlKeyChild in xmlChild.ChildNodes)
						{
							if(xmlKeyChild.Name == KeyDataElementName)
							{
								if(pbKeyData == null)
									pbKeyData = Convert.FromBase64String(xmlKeyChild.InnerText);
							}
						}
					}
				}
			}
			catch(Exception) { pbKeyData = null; }
			finally { ms.Close(); }

			return pbKeyData;
		}

		private static void CreateXmlKeyFile(string strFile, byte[] pbKeyData)
		{
			Debug.Assert(strFile != null);
			if(strFile == null) throw new ArgumentNullException("strFile");
			Debug.Assert(pbKeyData != null);
			if(pbKeyData == null) throw new ArgumentNullException("pbKeyData");

			IOConnectionInfo ioc = IOConnectionInfo.FromPath(strFile);
			Stream sOut = IOConnection.OpenWrite(ioc);

#if KeePassUAP
			XmlWriterSettings xws = new XmlWriterSettings();
			xws.Encoding = StrUtil.Utf8;
			xws.Indent = false;

			XmlWriter xtw = XmlWriter.Create(sOut, xws);
#else
			XmlTextWriter xtw = new XmlTextWriter(sOut, StrUtil.Utf8);
#endif

			xtw.WriteStartDocument();
			xtw.WriteWhitespace("\r\n");
			xtw.WriteStartElement(RootElementName); // KeyFile
			xtw.WriteWhitespace("\r\n\t");

			xtw.WriteStartElement(MetaElementName); // Meta
			xtw.WriteWhitespace("\r\n\t\t");
			xtw.WriteStartElement(VersionElementName); // Version
			xtw.WriteString("1.00");
			xtw.WriteEndElement(); // End Version
			xtw.WriteWhitespace("\r\n\t");
			xtw.WriteEndElement(); // End Meta
			xtw.WriteWhitespace("\r\n\t");

			xtw.WriteStartElement(KeyElementName); // Key
			xtw.WriteWhitespace("\r\n\t\t");

			xtw.WriteStartElement(KeyDataElementName); // Data
			xtw.WriteString(Convert.ToBase64String(pbKeyData));
			xtw.WriteEndElement(); // End Data
			xtw.WriteWhitespace("\r\n\t");

			xtw.WriteEndElement(); // End Key
			xtw.WriteWhitespace("\r\n");

			xtw.WriteEndElement(); // RootElementName
			xtw.WriteWhitespace("\r\n");
			xtw.WriteEndDocument(); // End KeyFile
			xtw.Close();

			sOut.Close();
		}
	}
}
