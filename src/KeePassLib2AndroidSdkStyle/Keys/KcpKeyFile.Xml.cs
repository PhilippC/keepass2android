/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2021 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Serialization;

using KeePassLib.Cryptography;
using KeePassLib.Resources;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace KeePassLib.Keys
{
	[XmlType("KeyFile")]
	public sealed class KfxFile
	{
		private const ulong KfxVersionCriticalMask = 0xFFFF000000000000UL;
		private const int KfxDataHashLength = 4;

		private KfxMeta m_meta = new KfxMeta();
		public KfxMeta Meta
		{
			get { return m_meta; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_meta = value;
			}
		}

		private KfxKey m_key = new KfxKey();
		public KfxKey Key
		{
			get { return m_key; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_key = value;
			}
		}

		public static KfxFile Create(ulong uVersion, byte[] pbKey, byte[] pbHash)
		{
			if(pbKey == null) throw new ArgumentNullException("pbKey");
			if(pbKey.Length == 0) throw new ArgumentOutOfRangeException("pbKey");

			if(uVersion == 0) uVersion = 0x0002000000000000;

			// Null hash: generate one, empty hash: store no hash
			if(pbHash == null) pbHash = HashData(pbKey);
			VerifyHash(pbKey, pbHash);

			KfxFile kf = new KfxFile();

			if(uVersion == 0x0001000000000000)
				kf.Meta.Version = "1.00"; // KeePass <= 2.46 used two zeros
			else kf.Meta.Version = StrUtil.VersionToString(uVersion, 2);

			if(uVersion == 0x0001000000000000)
				kf.Key.Data.Value = Convert.ToBase64String(pbKey);
			else if(uVersion == 0x0002000000000000)
			{
				kf.Key.Data.Value = FormatKeyHex(pbKey, 3);

				if(pbHash.Length != 0)
					kf.Key.Data.Hash = MemUtil.ByteArrayToHexString(pbHash);
			}
			else throw new NotSupportedException(KLRes.FileVersionUnsupported);

			return kf;
		}

		internal static KfxFile Create(ulong uVersion, string strKey, string strHash)
		{
			byte[] pbKey = ParseKey(uVersion, strKey);
			byte[] pbHash = ((strHash != null) ? ParseHash(strHash) : null);

			return Create(uVersion, pbKey, pbHash);
		}

		internal static bool CanLoad(string strFilePath)
		{
			if(string.IsNullOrEmpty(strFilePath)) { Debug.Assert(false); return false; }

			try
			{
				IOConnectionInfo ioc = IOConnectionInfo.FromPath(strFilePath);
				using(Stream s = IOConnection.OpenRead(ioc))
				{
					return (Load(s) != null);
				}
			}
			catch(Exception) { }

			return false;
		}

		public static KfxFile Load(Stream s)
		{
			return XmlUtilEx.Deserialize<KfxFile>(s);
		}

		public void Save(Stream s)
		{
			XmlUtilEx.Serialize<KfxFile>(s, this, true);
		}

		private static string FormatKeyHex(byte[] pb, int cTabs)
		{
			StringBuilder sb = new StringBuilder();
			string str = MemUtil.ByteArrayToHexString(pb);

			for(int i = 0; i < str.Length; ++i)
			{
				if((i & 0x1F) == 0)
				{
					sb.AppendLine();
					sb.Append('\t', cTabs);
				}
				else if((i & 0x07) == 0) sb.Append(' ');

				sb.Append(str[i]);
			}

			sb.AppendLine();
			if(cTabs > 0) sb.Append('\t', cTabs - 1);
			return sb.ToString();
		}

		private ulong GetVersion()
		{
			string str = m_meta.Version;
			if(string.IsNullOrEmpty(str)) return 0;

			return StrUtil.ParseVersion(str);
		}

		public byte[] GetKey()
		{
			ulong uVersion = GetVersion();

			byte[] pbKey = ParseKey(uVersion, m_key.Data.Value);
			if((pbKey == null) || (pbKey.Length == 0))
				throw new FormatException(KLRes.FileCorrupted);

			byte[] pbHash = ParseHash(m_key.Data.Hash);
			VerifyHash(pbKey, pbHash);

			return pbKey;
		}

		private static byte[] HashData(byte[] pb)
		{
			return MemUtil.Mid(CryptoUtil.HashSha256(pb), 0, KfxDataHashLength);
		}

		private static void VerifyHash(byte[] pbKey, byte[] pbHash)
		{
			// The hash is optional; empty hash means success
			if((pbHash == null) || (pbHash.Length == 0)) return;

			byte[] pbHashCmp = HashData(pbKey);
			if(!MemUtil.ArraysEqual(pbHash, pbHashCmp))
				throw new Exception("Keyfile hash mismatch!");
		}

		private static byte[] ParseKey(ulong uVersion, string strKey)
		{
			if(strKey == null) throw new ArgumentNullException("strKey");

			strKey = StrUtil.RemoveWhiteSpace(strKey);
			if(string.IsNullOrEmpty(strKey)) return MemUtil.EmptyByteArray;

			uVersion &= KfxVersionCriticalMask;

			byte[] pbKey;
			if(uVersion == 0x0001000000000000)
				pbKey = Convert.FromBase64String(strKey);
			else if(uVersion == 0x0002000000000000)
				pbKey = ParseHex(strKey);
			else throw new NotSupportedException(KLRes.FileVersionUnsupported);

			return pbKey;
		}

		private static byte[] ParseHash(string strHash)
		{
			return ParseHex(strHash);
		}

		private static byte[] ParseHex(string str)
		{
			if(str == null) throw new ArgumentNullException("str");
			if(str.Length == 0) return MemUtil.EmptyByteArray;

			if(((str.Length & 1) != 0) || !StrUtil.IsHexString(str, true))
				throw new FormatException();

			return MemUtil.HexStringToByteArray(str);
		}
	}

	public sealed class KfxMeta
	{
		private string m_strVersion = string.Empty;
		[DefaultValue("")]
		public string Version
		{
			get { return m_strVersion; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strVersion = value;
			}
		}
	}

	public sealed class KfxKey
	{
		private KfxData m_data = new KfxData();
		public KfxData Data
		{
			get { return m_data; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_data = value;
			}
		}
	}

	public sealed class KfxData
	{
		private string m_strHash = string.Empty;
		[DefaultValue("")]
		[XmlAttribute("Hash")]
		public string Hash
		{
			get { return m_strHash; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strHash = value;
			}
		}

		private string m_strValue = string.Empty;
		[DefaultValue("")]
		[XmlText]
		public string Value
		{
			get { return m_strValue; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strValue = value;
			}
		}
	}
}
