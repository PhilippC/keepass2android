/*
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
using System.Windows.Forms;
using System.Diagnostics;

using KeePassLib.Utility;

namespace OtpKeyProv
{
	public static class EncodingUtil
	{
		private const string FmtHex = "Hex";
		private const string FmtBase64 = "Base64";
		private const string FmtBase32 = "Base32";
		private const string FmtUtf8 = "UTF-8";
		private const string FmtDec = "Dec";

		public static readonly string[] Formats = new string[]{
			FmtHex, FmtBase64, FmtBase32, FmtUtf8, FmtDec
		};

		public static OtpDataFmt? GetOtpDataFormat(ComboBox cmb)
		{
			string strFmt = (cmb.SelectedItem as string);
			if(strFmt == null) return null; // No assert

			if(strFmt == FmtHex) return OtpDataFmt.Hex;
			if(strFmt == FmtBase64) return OtpDataFmt.Base64;
			if(strFmt == FmtBase32) return OtpDataFmt.Base32;
			if(strFmt == FmtUtf8) return OtpDataFmt.Utf8;
			if(strFmt == FmtDec) return OtpDataFmt.Dec;
			return null;
		}

		public static byte[] ParseKey(string strKey, OtpDataFmt fmt)
		{
			if(strKey == null) { Debug.Assert(false); return null; }

			strKey = strKey.Trim();
			if(strKey.Length == 0) return null; // No assert

			if(fmt == OtpDataFmt.Hex)
			{
				strKey = strKey.Replace(" ", string.Empty);
				strKey = strKey.Replace("\t", string.Empty);
				strKey = strKey.Replace("\r", string.Empty);
				strKey = strKey.Replace("\n", string.Empty);

				if((strKey.Length % 2) == 1) strKey = "0" + strKey;
				return MemUtil.HexStringToByteArray(strKey);
			}
			else if(fmt == OtpDataFmt.Base64)
			{
				try { return Convert.FromBase64String(strKey); }
				catch(Exception) { }
			}
			else if(fmt == OtpDataFmt.Base32)
				return ParseBase32(strKey);
			else if(fmt == OtpDataFmt.Utf8)
			{
				try { return StrUtil.Utf8.GetBytes(strKey); }
				catch(Exception) { }
			}
			else if(fmt == OtpDataFmt.Dec)
			{
				ulong u;
				if(ulong.TryParse(strKey, out u))
				{
					byte[] pb = MemUtil.UInt64ToBytes(u);
					Array.Reverse(pb); // Little endian -> big endian
					return pb;
				}
			}

			return null;
		}

		public static ulong? ParseCounter(string strCounter, OtpDataFmt fmt)
		{
			byte[] pb = ParseKey(strCounter, fmt);
			if(pb == null) return null;
			if(pb.Length > 8) return null;

			Array.Reverse(pb); // Big endian -> little endian

			byte[] pb8 = new byte[8];
			Array.Copy(pb, 0, pb8, 0, pb.Length);
			return MemUtil.BytesToUInt64(pb8); // Little endian
		}

		private const string Base32Alph = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
		/// <summary>
		/// Decode base32 strings according to RFC 4648.
		/// </summary>
		private static byte[] ParseBase32(string str)
		{
			if((str == null) || ((str.Length % 8) != 0)) return null;

			List<byte> l = new List<byte>();
			for(int i = 0; i < str.Length; i += 8)
			{
				ulong u = 0;
				int nBits = 0;

				for(int j = 0; j < 8; ++j)
				{
					char ch = char.ToUpper(str[i + j]);
					if(ch == '=') break;

					int iValue = Base32Alph.IndexOf(ch);
					if(iValue < 0) return null;

					u <<= 5;
					u += (ulong)iValue;
					nBits += 5;
				}

				int nBitsTooMany = (nBits % 8);
				u >>= nBitsTooMany;
				nBits -= nBitsTooMany;
				Debug.Assert((nBits % 8) == 0);

				int idxNewBytes = l.Count;
				while(nBits > 0)
				{
					l.Add((byte)(u & 0xFF));
					u >>= 8;
					nBits -= 8;
				}
				l.Reverse(idxNewBytes, l.Count - idxNewBytes);
			}

			return l.ToArray();
		}

		internal static void SelfTest()
		{
#if DEBUG
			byte[] pbRes = ParseBase32("MY======");
			byte[] pbExp = Encoding.ASCII.GetBytes("f");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-1");

			pbRes = ParseBase32("MZXQ====");
			pbExp = Encoding.ASCII.GetBytes("fo");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-2");

			pbRes = ParseBase32("MZXW6===");
			pbExp = Encoding.ASCII.GetBytes("foo");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-3");

			pbRes = ParseBase32("MZXW6YQ=");
			pbExp = Encoding.ASCII.GetBytes("foob");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-4");

			pbRes = ParseBase32("MZXW6YTB");
			pbExp = Encoding.ASCII.GetBytes("fooba");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-5");

			pbRes = ParseBase32("MZXW6YTBOI======");
			pbExp = Encoding.ASCII.GetBytes("foobar");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-6");

			pbRes = ParseBase32("JNSXSIDQOJXXM2LEMVZCAYTBONSWIIDPNYQG63TFFV2GS3LFEBYGC43TO5XXEZDTFY======");
			pbExp = Encoding.ASCII.GetBytes("Key provider based on one-time passwords.");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-7");
#endif
		}
	}
}
