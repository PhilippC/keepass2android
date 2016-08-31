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
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;

using KeePassLib.Cryptography;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Keys;
using KeePassLib.Utility;

namespace OtpKeyProv
{
	public enum OtpDataFmt
	{
		Hex = 0,
		Base64 = 1,
		Base32 = 4,
		Utf8 = 2,
		Dec = 3
	}

	public static class OtpUtil
	{
		public static byte[] KeyFromOtps(string[] vOtps, int iOtpsOffset,
			int iOtpsCount, byte[] pbTrfKey32, ulong uTrfRounds)
		{
			StringBuilder sb = new StringBuilder();
			for(int i = iOtpsOffset; i < (iOtpsOffset + iOtpsCount); ++i)
			{
				if(sb.Length > 0) sb.Append(':');
				sb.Append(vOtps[i].Trim());
			}

			string strKey = sb.ToString();
			byte[] pb = StrUtil.Utf8.GetBytes(strKey);
			if(pb.Length == 0) return null;

			byte[] pbKey = HashAndTransform(pb, pbTrfKey32, uTrfRounds);
			if(pbKey == null) throw new InvalidOperationException();

			return pbKey;
		}

		public static string EncryptData(byte[] pbData, byte[] pbKey32,
			byte[] pbIV16)
		{
			byte[] pbIV8 = new byte[8];
			Array.Copy(pbIV16, 0, pbIV8, 0, 8);

			byte[] pbEnc = new byte[pbData.Length];
			Array.Copy(pbData, pbEnc, pbData.Length);

			Salsa20Cipher enc = new Salsa20Cipher(pbKey32, pbIV8);
			enc.Encrypt(pbEnc, 0, pbEnc.Length);

			return ("s20://" + Convert.ToBase64String(pbEnc,
				Base64FormattingOptions.None));
		}

		public static byte[] DecryptData(string strData, byte[] pbKey32,
			byte[] pbIV16)
		{
			if(!strData.StartsWith("s20://")) return null;

			string strEnc = strData.Substring(6);
			byte[] pb = Convert.FromBase64String(strEnc);

			byte[] pbIV8 = new byte[8];
			Array.Copy(pbIV16, 0, pbIV8, 0, 8);

			Salsa20Cipher dec = new Salsa20Cipher(pbKey32, pbIV8);
			dec.Encrypt(pb, 0, pb.Length);

			return pb;
		}

		private static byte[] HashAndTransform(byte[] pbData, byte[] pbTrfKey32,
			ulong uTrfRounds)
		{
			SHA256Managed sha256 = new SHA256Managed();
			byte[] pbHash = sha256.ComputeHash(pbData);
			sha256.Clear();

			if(!AesKdf.TransformKeyManaged(pbHash, pbTrfKey32, uTrfRounds))
				return null;

			sha256 = new SHA256Managed();
			pbHash = sha256.ComputeHash(pbHash);
			sha256.Clear();

			return pbHash;
		}

		public static OtpEncryptedData EncryptSecret(byte[] pbSecret, string[] vOtps,
			int iOtpsOffset, int iOtpsCount)
		{
			OtpEncryptedData d = new OtpEncryptedData();
			CryptoRandom r = CryptoRandom.Instance;

			byte[] pbIV16 = r.GetRandomBytes(16);
			d.IV = Convert.ToBase64String(pbIV16, Base64FormattingOptions.None);

			byte[] pbTrfKey32 = r.GetRandomBytes(32);
			d.TransformationKey = Convert.ToBase64String(pbTrfKey32, Base64FormattingOptions.None);

			byte[] pbKey32 = OtpUtil.KeyFromOtps(vOtps, iOtpsOffset, iOtpsCount,
				pbTrfKey32, d.TransformationRounds);

			d.CipherText = OtpUtil.EncryptData(pbSecret, pbKey32, pbIV16);

			byte[] pbHashTrfKey32 = r.GetRandomBytes(32);
			d.PlainTextHashTransformationKey = Convert.ToBase64String(pbHashTrfKey32,
				Base64FormattingOptions.None);

			byte[] pbHash = HashAndTransform(pbSecret, pbHashTrfKey32,
				d.PlainTextHashTransformationRounds);
			d.PlainTextHash = Convert.ToBase64String(pbHash, Base64FormattingOptions.None);

			return d;
		}

		public static byte[] DecryptSecret(OtpEncryptedData d, string[] vOtps,
			int iOtpsOffset, int iOtpsCount)
		{
			try { return DecryptSecretPriv(d, vOtps, iOtpsOffset, iOtpsCount); }
			catch(Exception) { Debug.Assert(false); }
			return null;
		}

		private static byte[] DecryptSecretPriv(OtpEncryptedData d, string[] vOtps,
			int iOtpsOffset, int iOtpsCount)
		{
			if(d == null) throw new ArgumentNullException("d");

			byte[] pbTrfKey32 = Convert.FromBase64String(d.TransformationKey);
			byte[] pbKey32 = OtpUtil.KeyFromOtps(vOtps, iOtpsOffset, iOtpsCount,
				pbTrfKey32, d.TransformationRounds);
			byte[] pbIV = Convert.FromBase64String(d.IV);

			byte[] pbSecret = OtpUtil.DecryptData(d.CipherText, pbKey32, pbIV);

			byte[] pbHashTrfKey32 = Convert.FromBase64String(d.PlainTextHashTransformationKey);
			byte[] pbHash = HashAndTransform(pbSecret, pbHashTrfKey32,
				d.PlainTextHashTransformationRounds);

			if(!MemUtil.ArraysEqual(pbHash, Convert.FromBase64String(d.PlainTextHash)))
				return null;

			return pbSecret;
		}
	}
}
