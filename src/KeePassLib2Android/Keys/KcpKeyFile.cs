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
using InvalidDataException = KeePassLib.Serialization.InvalidDataException;

namespace KeePassLib.Keys
{
	/// <summary>
	/// Key files as provided by the user.
	/// </summary>
	public sealed class KcpKeyFile : IUserKey
	{
		private IOConnectionInfo m_ioc;
		private ProtectedBinary m_pbKeyData;
		private ProtectedBinary m_pbFileData;

		/// <summary>
		/// Path to the key file.
		/// </summary>
		public string Path
		{
			get { return m_ioc.Path; }
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

	    public uint GetMinKdbxVersion()
	    {
	        return 0;
	    }

	    public IOConnectionInfo Ioc
		{
			get { return m_ioc; }
		}

		public ProtectedBinary RawFileData
		{
			get { return m_pbFileData; }
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

		public KcpKeyFile(byte[] keyFileContents, IOConnectionInfo iocKeyFile, bool bThrowIfDbFile)
		{
			Construct(keyFileContents, iocKeyFile, bThrowIfDbFile);
		}

		private void Construct(byte[] pbFileData, IOConnectionInfo iocKeyFile, bool bThrowIfDbFile)
		{
			if (pbFileData == null) throw new Java.IO.FileNotFoundException();
			m_pbFileData = new ProtectedBinary(true, pbFileData);

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

            byte[] pbKey = LoadKeyFile(pbFileData);

			if (pbKey == null) throw new InvalidOperationException();

			m_ioc = iocKeyFile;
			m_pbKeyData = new ProtectedBinary(true, pbKey);

			MemUtil.ZeroByteArray(pbKey);
		}

		private void Construct(IOConnectionInfo iocFile, bool bThrowIfDbFile)
		{
			byte[] pbFileData = IOConnection.ReadFile(iocFile);
			Construct(pbFileData, iocFile, bThrowIfDbFile);
		}
		// public void Clear()
		// {
		//	m_strPath = string.Empty;
		//	m_pbKeyData = null;
		// }


        private static byte[] LoadKeyFile(byte[] pbFileData)
        {
            if (pbFileData == null) throw new ArgumentNullException("pbFileData");

            byte[] pbKey = LoadKeyFileXml(pbFileData);
            if (pbKey != null) return pbKey;

            int cb = pbFileData.Length;
            if (cb == 32) return pbFileData;

            if (cb == 64)
            {
                pbKey = LoadKeyFileHex(pbFileData);
                if (pbKey != null) return pbKey;
            }

            return CryptoUtil.HashSha256(pbFileData);
        }

        private static byte[] LoadKeyFileXml(byte[] pbFileData)
        {
            KfxFile kf;
            try
            {
                using (MemoryStream ms = new MemoryStream(pbFileData, false))
                {
                    kf = KfxFile.Load(ms);
                }
            }
            catch (Exception) { return null; }

            // We have a syntactically valid XML key file;
            // failing to verify the key should throw an exception
            return ((kf != null) ? kf.GetKey() : null);
        }

        private static byte[] LoadKeyFileHex(byte[] pbFileData)
        {
            if (pbFileData == null) { Debug.Assert(false); return null; }

            try
            {
                int cc = pbFileData.Length;
                if ((cc & 1) != 0) { Debug.Assert(false); return null; }

                if (!StrUtil.IsHexString(pbFileData, true)) return null;

                string strHex = StrUtil.Utf8.GetString(pbFileData);
                return MemUtil.HexStringToByteArray(strHex);
            }
            catch (Exception) { Debug.Assert(false); }

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
				using(MemoryStream ms = new MemoryStream())
				{
					MemUtil.Write(ms, pbAdditionalEntropy);
					MemUtil.Write(ms, pbKey32);

					pbFinalKey32 = CryptoUtil.HashSha256(ms.ToArray());
				}
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
				XmlDocument doc = new XmlDocument() { XmlResolver = null };
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

		/// <summary>
		/// Allows to change the ioc value (without reloading the data, assuming it's the same content)
		/// </summary>
		/// <param name="newIoc"></param>
		public void ResetIoc(IOConnectionInfo newIoc)
		{
			m_ioc = newIoc;
	}
}
}
