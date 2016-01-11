/*
  This file was modified my Philipp Crocoll, 2013. Based on: 

  OtpKeyProv Plugin
  Copyright (C) 2011-2012 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Xml;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Diagnostics;

using KeePassLib.Cryptography;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using keepass2android;

namespace OtpKeyProv
{
	public sealed class OtpInfo
	{
		private string m_strType = string.Empty;
		public string Type
		{
			get { return m_strType; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strType = value;
			}
		}

		private string m_strVersion = string.Empty;
		public string Version
		{
			get { return m_strVersion; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strVersion = value;
			}
		}

		private string m_strGen = string.Empty;
		public string Generator
		{
			get { return m_strGen; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strGen = value;
			}
		}

		private byte[] m_pbSecret = null;
		[XmlIgnore]
		public byte[] Secret
		{
			get { return m_pbSecret; }
			set { m_pbSecret = value; }
		}

		private string m_strEncSecret = string.Empty;
		[DefaultValue("")]
		public string EncryptedSecret // Deprecated, < v2.0
		{
			get { return m_strEncSecret; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strEncSecret = value;
			}
		}

		private List<OtpEncryptedData> m_lSecrets = new List<OtpEncryptedData>();
		[XmlArrayItem("EncryptedData")]
		public List<OtpEncryptedData> EncryptedSecrets
		{
			get { return m_lSecrets; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_lSecrets = value;
			}
		}

		private string m_strEncIV = string.Empty;
		[DefaultValue("")]
		public string EncryptionIV // Deprecated, < v2.0
		{
			get { return m_strEncIV; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strEncIV = value;
			}
		}

		private string m_strTrfKey = string.Empty;
		[DefaultValue("")]
		public string TransformationKey // Deprecated, < v2.0
		{
			get { return m_strTrfKey; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strTrfKey = value;
			}
		}

		private const ulong DefaultTrfRounds = 12000;
		private ulong m_uTrfRounds = DefaultTrfRounds;
		[DefaultValue(typeof(ulong), "12000")]
		public ulong TransformationRounds // Deprecated, < v2.0
		{
			get { return m_uTrfRounds; }
			set { m_uTrfRounds = value; }
		}

		private ulong m_uCounter = 0;
		public ulong Counter
		{
			get { return m_uCounter; }
			set { m_uCounter = value; }
		}

		private uint m_uOtpLength = 8;
		public uint OtpLength
		{
			get { return m_uOtpLength; }
			set { m_uOtpLength = value; }
		}

		private uint m_uOtpsReq = 4;
		public uint OtpsRequired
		{
			get { return m_uOtpsReq; }
			set { m_uOtpsReq = value; }
		}

		private uint m_uLookAhead = 0;
		public uint LookAheadCount
		{
			get { return m_uLookAhead; }
			set { m_uLookAhead = value; }
		}

		public static OtpInfo Load(IOConnectionInfo ioc)
		{
			Stream sIn = null;

			try
			{
				sIn = App.Kp2a.GetOtpAuxFileStorage(ioc).OpenFileForRead(ioc);

				XmlSerializer xs = new XmlSerializer(typeof (OtpInfo));
				return (OtpInfo) xs.Deserialize(sIn);
			}
			catch (Exception e)
			{
				Kp2aLog.LogUnexpectedError(e);
			}
			finally
			{
				if(sIn != null) sIn.Close();
			}

			return null;
		}

		public static bool Save(IOConnectionInfo ioc, OtpInfo otpInfo)
		{
			Stream sOut = null;

			try
			{
				using (var trans = App.Kp2a.GetOtpAuxFileStorage(ioc)
					               .OpenWriteTransaction(ioc, App.Kp2a.GetBooleanPreference(PreferenceKey.UseFileTransactions)))
				{
					var stream = trans.OpenFile();
					WriteToStream(otpInfo, stream);
					trans.CommitWrite();
				}
				return true;
			}
			catch(Exception) { Debug.Assert(false); }
			finally
			{
				if(sOut != null) sOut.Close();
			}

			return false;
		}


		public static void WriteToStream(OtpInfo otpInfo, Stream stream)
		{
			var xws = XmlWriterSettings();

			XmlWriter xw = XmlWriter.Create(stream, xws);

			XmlSerializer xs = new XmlSerializer(typeof (OtpInfo));
			xs.Serialize(xw, otpInfo);

			xw.Close();
		}

		public static XmlWriterSettings XmlWriterSettings()
		{
			XmlWriterSettings xws = new XmlWriterSettings
				{
					CloseOutput = true,
					Encoding = StrUtil.Utf8,
					Indent = true,
					IndentChars = "\t"
				};
			return xws;
		}

		public void EncryptSecret()
		{
			if(m_pbSecret == null) throw new InvalidOperationException();

			string[] vOtps = new string[m_uOtpsReq + m_uLookAhead];
			ulong uCounter = m_uCounter;
			for(int i = 0; i < vOtps.Length; ++i)
			{
				vOtps[i] = HmacOtp.Generate(m_pbSecret, uCounter,
					m_uOtpLength, false, -1);
				++uCounter;
			}

			m_strEncSecret = string.Empty;
			m_strEncIV = string.Empty;
			m_strTrfKey = string.Empty;
			m_uTrfRounds = DefaultTrfRounds;

			m_lSecrets.Clear();
			for(int i = 0; i <= (int)m_uLookAhead; ++i)
				m_lSecrets.Add(OtpUtil.EncryptSecret(m_pbSecret, vOtps, i,
					(int)m_uOtpsReq));
		}
	}

	public sealed class OtpEncryptedData
	{
		private string m_strCipherText = string.Empty;
		[DefaultValue("")]
		public string CipherText
		{
			get { return m_strCipherText; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strCipherText = value;
			}
		}

		private string m_strIV = string.Empty;
		[DefaultValue("")]
		public string IV
		{
			get { return m_strIV; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strIV = value;
			}
		}

		private string m_strTrfKey = string.Empty;
		[DefaultValue("")]
		public string TransformationKey
		{
			get { return m_strTrfKey; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strTrfKey = value;
			}
		}

		private ulong m_uTrfRounds = 10000;
		public ulong TransformationRounds
		{
			get { return m_uTrfRounds; }
			set { m_uTrfRounds = value; }
		}

		private string m_strPlainHash = string.Empty;
		[DefaultValue("")]
		public string PlainTextHash
		{
			get { return m_strPlainHash; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strPlainHash = value;
			}
		}

		private string m_strPlainHashTrfKey = string.Empty;
		[DefaultValue("")]
		public string PlainTextHashTransformationKey
		{
			get { return m_strPlainHashTrfKey; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strPlainHashTrfKey = value;
			}
		}

		private ulong m_uHashTrfRounds = 10000;
		public ulong PlainTextHashTransformationRounds
		{
			get { return m_uHashTrfRounds; }
			set { m_uHashTrfRounds = value; }
		}
	}
}
