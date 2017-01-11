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

#if KeePassUAP
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
#else
using System.Security.Cryptography;
#endif

using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.Hash;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Keys;
using KeePassLib.Native;
using KeePassLib.Resources;
using KeePassLib.Security;
using KeePassLib.Utility;

#if (KeePassUAP && KeePassLibSD)
#error KeePassUAP and KeePassLibSD are mutually exclusive.
#endif

namespace KeePassLib.Cryptography
{
	/// <summary>
	/// Class containing self-test methods.
	/// </summary>
	public static class SelfTest
	{
		/// <summary>
		/// Perform a self-test.
		/// </summary>
		public static void Perform()
		{
			TestFipsComplianceProblems(); // Must be the first test

			TestRijndael();
			TestSalsa20();
			TestChaCha20();
			TestBlake2b();
			TestArgon2();
			TestHmac();

			TestNativeKeyTransform();
			
			TestHmacOtp();

			TestProtectedObjects();
			TestMemUtil();
			TestStrUtil();
			TestUrlUtil();

			Debug.Assert((int)PwIcon.World == 1);
			Debug.Assert((int)PwIcon.Warning == 2);
			Debug.Assert((int)PwIcon.BlackBerry == 68);

#if KeePassUAP
			SelfTestEx.Perform();
#endif
		}

		internal static void TestFipsComplianceProblems()
		{
#if !KeePassUAP
			try { using(RijndaelManaged r = new RijndaelManaged()) { } }
			catch(Exception exAes)
			{
				throw new SecurityException("AES/Rijndael: " + exAes.Message);
			}
#endif

			try { using(SHA256Managed h = new SHA256Managed()) { } }
			catch(Exception exSha256)
			{
				throw new SecurityException("SHA-256: " + exSha256.Message);
			}
		}

		private static void TestRijndael()
		{
			// Test vector (official ECB test vector #356)
			byte[] pbIV = new byte[16];
			byte[] pbTestKey = new byte[32];
			byte[] pbTestData = new byte[16];
			byte[] pbReferenceCT = new byte[16] {
				0x75, 0xD1, 0x1B, 0x0E, 0x3A, 0x68, 0xC4, 0x22,
				0x3D, 0x88, 0xDB, 0xF0, 0x17, 0x97, 0x7D, 0xD7 };
			int i;

			for(i = 0; i < 16; ++i) pbIV[i] = 0;
			for(i = 0; i < 32; ++i) pbTestKey[i] = 0;
			for(i = 0; i < 16; ++i) pbTestData[i] = 0;
			pbTestData[0] = 0x04;

#if KeePassUAP
			AesEngine r = new AesEngine();
			r.Init(true, new KeyParameter(pbTestKey));
			if(r.GetBlockSize() != pbTestData.Length)
				throw new SecurityException("AES (BC)");
			r.ProcessBlock(pbTestData, 0, pbTestData, 0);
#else
			RijndaelManaged r = new RijndaelManaged();

			if(r.BlockSize != 128) // AES block size
			{
				Debug.Assert(false);
				r.BlockSize = 128;
			}

			r.IV = pbIV;
			r.KeySize = 256;
			r.Key = pbTestKey;
			r.Mode = CipherMode.ECB;
			ICryptoTransform iCrypt = r.CreateEncryptor();

			iCrypt.TransformBlock(pbTestData, 0, 16, pbTestData, 0);
#endif

			if(!MemUtil.ArraysEqual(pbTestData, pbReferenceCT))
				throw new SecurityException("AES");
		}

		private static void TestSalsa20()
		{
#if DEBUG
			// Test values from official set 6, vector 3
			byte[] pbKey = new byte[32] {
				0x0F, 0x62, 0xB5, 0x08, 0x5B, 0xAE, 0x01, 0x54,
				0xA7, 0xFA, 0x4D, 0xA0, 0xF3, 0x46, 0x99, 0xEC,
				0x3F, 0x92, 0xE5, 0x38, 0x8B, 0xDE, 0x31, 0x84,
				0xD7, 0x2A, 0x7D, 0xD0, 0x23, 0x76, 0xC9, 0x1C
			};
			byte[] pbIV = new byte[8] { 0x28, 0x8F, 0xF6, 0x5D,
				0xC4, 0x2B, 0x92, 0xF9 };
			byte[] pbExpected = new byte[16] {
				0x5E, 0x5E, 0x71, 0xF9, 0x01, 0x99, 0x34, 0x03,
				0x04, 0xAB, 0xB2, 0x2A, 0x37, 0xB6, 0x62, 0x5B
			};

			byte[] pb = new byte[16];
			Salsa20Cipher c = new Salsa20Cipher(pbKey, pbIV);
			c.Encrypt(pb, 0, pb.Length);
			if(!MemUtil.ArraysEqual(pb, pbExpected))
				throw new SecurityException("Salsa20-1");

			// Extended test
			byte[] pbExpected2 = new byte[16] {
				0xAB, 0xF3, 0x9A, 0x21, 0x0E, 0xEE, 0x89, 0x59,
				0x8B, 0x71, 0x33, 0x37, 0x70, 0x56, 0xC2, 0xFE
			};
			byte[] pbExpected3 = new byte[16] {
				0x1B, 0xA8, 0x9D, 0xBD, 0x3F, 0x98, 0x83, 0x97,
				0x28, 0xF5, 0x67, 0x91, 0xD5, 0xB7, 0xCE, 0x23
			};

			Random r = new Random();
			int nPos = Salsa20ToPos(c, r, pb.Length, 65536);
			Array.Clear(pb, 0, pb.Length);
			c.Encrypt(pb, 0, pb.Length);
			if(!MemUtil.ArraysEqual(pb, pbExpected2))
				throw new SecurityException("Salsa20-2");

			nPos = Salsa20ToPos(c, r, nPos + pb.Length, 131008);
			Array.Clear(pb, 0, pb.Length);
			c.Encrypt(pb, 0, pb.Length);
			if(!MemUtil.ArraysEqual(pb, pbExpected3))
				throw new SecurityException("Salsa20-3");

			Dictionary<string, bool> d = new Dictionary<string, bool>();
			const int nRounds = 100;
			for(int i = 0; i < nRounds; ++i)
			{
				byte[] z = new byte[32];
				c = new Salsa20Cipher(z, MemUtil.Int64ToBytes(i));
				c.Encrypt(z, 0, z.Length);
				d[MemUtil.ByteArrayToHexString(z)] = true;
			}
			if(d.Count != nRounds) throw new SecurityException("Salsa20-4");
#endif
		}

#if DEBUG
		private static int Salsa20ToPos(Salsa20Cipher c, Random r, int nPos,
			int nTargetPos)
		{
			byte[] pb = new byte[512];

			while(nPos < nTargetPos)
			{
				int x = r.Next(1, 513);
				int nGen = Math.Min(nTargetPos - nPos, x);
				c.Encrypt(pb, 0, nGen);
				nPos += nGen;
			}

			return nTargetPos;
		}
#endif

		private static void TestChaCha20()
		{
			// ======================================================
			// Test vector from RFC 7539, section 2.3.2

			byte[] pbKey = new byte[32];
			for(int i = 0; i < 32; ++i) pbKey[i] = (byte)i;

			byte[] pbIV = new byte[12];
			pbIV[3] = 0x09;
			pbIV[7] = 0x4A;

			byte[] pbExpc = new byte[64] {
				0x10, 0xF1, 0xE7, 0xE4, 0xD1, 0x3B, 0x59, 0x15,
				0x50, 0x0F, 0xDD, 0x1F, 0xA3, 0x20, 0x71, 0xC4,
				0xC7, 0xD1, 0xF4, 0xC7, 0x33, 0xC0, 0x68, 0x03,
				0x04, 0x22, 0xAA, 0x9A, 0xC3, 0xD4, 0x6C, 0x4E,
				0xD2, 0x82, 0x64, 0x46, 0x07, 0x9F, 0xAA, 0x09,
				0x14, 0xC2, 0xD7, 0x05, 0xD9, 0x8B, 0x02, 0xA2,
				0xB5, 0x12, 0x9C, 0xD1, 0xDE, 0x16, 0x4E, 0xB9,
				0xCB, 0xD0, 0x83, 0xE8, 0xA2, 0x50, 0x3C, 0x4E
			};

			byte[] pb = new byte[64];

			using(ChaCha20Cipher c = new ChaCha20Cipher(pbKey, pbIV))
			{
				c.Seek(64, SeekOrigin.Begin); // Skip first block
				c.Encrypt(pb, 0, pb.Length);

				if(!MemUtil.ArraysEqual(pb, pbExpc))
					throw new SecurityException("ChaCha20-1");
			}

#if DEBUG
			// ======================================================
			// Test vector from RFC 7539, section 2.4.2

			pbIV[3] = 0;

			pb = StrUtil.Utf8.GetBytes("Ladies and Gentlemen of the clas" +
				@"s of '99: If I could offer you only one tip for " +
				@"the future, sunscreen would be it.");

			pbExpc = new byte[] {
				0x6E, 0x2E, 0x35, 0x9A, 0x25, 0x68, 0xF9, 0x80,
				0x41, 0xBA, 0x07, 0x28, 0xDD, 0x0D, 0x69, 0x81,
				0xE9, 0x7E, 0x7A, 0xEC, 0x1D, 0x43, 0x60, 0xC2,
				0x0A, 0x27, 0xAF, 0xCC, 0xFD, 0x9F, 0xAE, 0x0B,
				0xF9, 0x1B, 0x65, 0xC5, 0x52, 0x47, 0x33, 0xAB,
				0x8F, 0x59, 0x3D, 0xAB, 0xCD, 0x62, 0xB3, 0x57,
				0x16, 0x39, 0xD6, 0x24, 0xE6, 0x51, 0x52, 0xAB,
				0x8F, 0x53, 0x0C, 0x35, 0x9F, 0x08, 0x61, 0xD8,
				0x07, 0xCA, 0x0D, 0xBF, 0x50, 0x0D, 0x6A, 0x61,
				0x56, 0xA3, 0x8E, 0x08, 0x8A, 0x22, 0xB6, 0x5E,
				0x52, 0xBC, 0x51, 0x4D, 0x16, 0xCC, 0xF8, 0x06,
				0x81, 0x8C, 0xE9, 0x1A, 0xB7, 0x79, 0x37, 0x36,
				0x5A, 0xF9, 0x0B, 0xBF, 0x74, 0xA3, 0x5B, 0xE6,
				0xB4, 0x0B, 0x8E, 0xED, 0xF2, 0x78, 0x5E, 0x42,
				0x87, 0x4D
			};

			byte[] pb64 = new byte[64];

			using(ChaCha20Cipher c = new ChaCha20Cipher(pbKey, pbIV))
			{
				c.Encrypt(pb64, 0, pb64.Length); // Skip first block
				c.Encrypt(pb, 0, pb.Length);

				if(!MemUtil.ArraysEqual(pb, pbExpc))
					throw new SecurityException("ChaCha20-2");
			}

			// ======================================================
			// Test vector from RFC 7539, appendix A.2 #2

			Array.Clear(pbKey, 0, pbKey.Length);
			pbKey[31] = 1;

			Array.Clear(pbIV, 0, pbIV.Length);
			pbIV[11] = 2;

			pb = StrUtil.Utf8.GetBytes("Any submission to the IETF inten" +
				"ded by the Contributor for publication as all or" +
				" part of an IETF Internet-Draft or RFC and any s" +
				"tatement made within the context of an IETF acti" +
				"vity is considered an \"IETF Contribution\". Such " +
				"statements include oral statements in IETF sessi" +
				"ons, as well as written and electronic communica" +
				"tions made at any time or place, which are addressed to");

			pbExpc = MemUtil.HexStringToByteArray(
				"A3FBF07DF3FA2FDE4F376CA23E82737041605D9F4F4F57BD8CFF2C1D4B7955EC" +
				"2A97948BD3722915C8F3D337F7D370050E9E96D647B7C39F56E031CA5EB6250D" +
				"4042E02785ECECFA4B4BB5E8EAD0440E20B6E8DB09D881A7C6132F420E527950" +
				"42BDFA7773D8A9051447B3291CE1411C680465552AA6C405B7764D5E87BEA85A" +
				"D00F8449ED8F72D0D662AB052691CA66424BC86D2DF80EA41F43ABF937D3259D" +
				"C4B2D0DFB48A6C9139DDD7F76966E928E635553BA76C5C879D7B35D49EB2E62B" +
				"0871CDAC638939E25E8A1E0EF9D5280FA8CA328B351C3C765989CBCF3DAA8B6C" +
				"CC3AAF9F3979C92B3720FC88DC95ED84A1BE059C6499B9FDA236E7E818B04B0B" +
				"C39C1E876B193BFE5569753F88128CC08AAA9B63D1A16F80EF2554D7189C411F" +
				"5869CA52C5B83FA36FF216B9C1D30062BEBCFD2DC5BCE0911934FDA79A86F6E6" +
				"98CED759C3FF9B6477338F3DA4F9CD8514EA9982CCAFB341B2384DD902F3D1AB" +
				"7AC61DD29C6F21BA5B862F3730E37CFDC4FD806C22F221");

			Random r = new Random();
			using(MemoryStream msEnc = new MemoryStream())
			{
				using(ChaCha20Stream c = new ChaCha20Stream(msEnc, true, pbKey, pbIV))
				{
					r.NextBytes(pb64);
					c.Write(pb64, 0, pb64.Length); // Skip first block

					int p = 0;
					while(p < pb.Length)
					{
						int cb = r.Next(1, pb.Length - p + 1);
						c.Write(pb, p, cb);
						p += cb;
					}
					Debug.Assert(p == pb.Length);
				}

				byte[] pbEnc0 = msEnc.ToArray();
				byte[] pbEnc = MemUtil.Mid(pbEnc0, 64, pbEnc0.Length - 64);
				if(!MemUtil.ArraysEqual(pbEnc, pbExpc))
					throw new SecurityException("ChaCha20-3");

				using(MemoryStream msCT = new MemoryStream(pbEnc0, false))
				{
					using(ChaCha20Stream cDec = new ChaCha20Stream(msCT, false,
						pbKey, pbIV))
					{
						byte[] pbPT = MemUtil.Read(cDec, pbEnc0.Length);
						if(cDec.ReadByte() >= 0)
							throw new SecurityException("ChaCha20-4");
						if(!MemUtil.ArraysEqual(MemUtil.Mid(pbPT, 0, 64), pb64))
							throw new SecurityException("ChaCha20-5");
						if(!MemUtil.ArraysEqual(MemUtil.Mid(pbPT, 64, pbEnc.Length), pb))
							throw new SecurityException("ChaCha20-6");
					}
				}
			}

			// ======================================================
			// Test vector TC8 from RFC draft by J. Strombergson:
			// https://tools.ietf.org/html/draft-strombergson-chacha-test-vectors-01

			pbKey = new byte[32] {
				0xC4, 0x6E, 0xC1, 0xB1, 0x8C, 0xE8, 0xA8, 0x78,
				0x72, 0x5A, 0x37, 0xE7, 0x80, 0xDF, 0xB7, 0x35,
				0x1F, 0x68, 0xED, 0x2E, 0x19, 0x4C, 0x79, 0xFB,
				0xC6, 0xAE, 0xBE, 0xE1, 0xA6, 0x67, 0x97, 0x5D
			};

			// The first 4 bytes are set to zero and a large counter
			// is used; this makes the RFC 7539 version of ChaCha20
			// compatible with the original specification by
			// D. J. Bernstein.
			pbIV = new byte[12] { 0x00, 0x00, 0x00, 0x00,
				0x1A, 0xDA, 0x31, 0xD5, 0xCF, 0x68, 0x82, 0x21
			};

			pb = new byte[128];

			pbExpc = new byte[128] {
				0xF6, 0x3A, 0x89, 0xB7, 0x5C, 0x22, 0x71, 0xF9,
				0x36, 0x88, 0x16, 0x54, 0x2B, 0xA5, 0x2F, 0x06,
				0xED, 0x49, 0x24, 0x17, 0x92, 0x30, 0x2B, 0x00,
				0xB5, 0xE8, 0xF8, 0x0A, 0xE9, 0xA4, 0x73, 0xAF,
				0xC2, 0x5B, 0x21, 0x8F, 0x51, 0x9A, 0xF0, 0xFD,
				0xD4, 0x06, 0x36, 0x2E, 0x8D, 0x69, 0xDE, 0x7F,
				0x54, 0xC6, 0x04, 0xA6, 0xE0, 0x0F, 0x35, 0x3F,
				0x11, 0x0F, 0x77, 0x1B, 0xDC, 0xA8, 0xAB, 0x92,

				0xE5, 0xFB, 0xC3, 0x4E, 0x60, 0xA1, 0xD9, 0xA9,
				0xDB, 0x17, 0x34, 0x5B, 0x0A, 0x40, 0x27, 0x36,
				0x85, 0x3B, 0xF9, 0x10, 0xB0, 0x60, 0xBD, 0xF1,
				0xF8, 0x97, 0xB6, 0x29, 0x0F, 0x01, 0xD1, 0x38,
				0xAE, 0x2C, 0x4C, 0x90, 0x22, 0x5B, 0xA9, 0xEA,
				0x14, 0xD5, 0x18, 0xF5, 0x59, 0x29, 0xDE, 0xA0,
				0x98, 0xCA, 0x7A, 0x6C, 0xCF, 0xE6, 0x12, 0x27,
				0x05, 0x3C, 0x84, 0xE4, 0x9A, 0x4A, 0x33, 0x32
			};

			using(ChaCha20Cipher c = new ChaCha20Cipher(pbKey, pbIV, true))
			{
				c.Decrypt(pb, 0, pb.Length);

				if(!MemUtil.ArraysEqual(pb, pbExpc))
					throw new SecurityException("ChaCha20-7");
			}
#endif
		}

		private static void TestBlake2b()
		{
#if DEBUG
			Blake2b h = new Blake2b();

			// ======================================================
			// From https://tools.ietf.org/html/rfc7693

			byte[] pbData = StrUtil.Utf8.GetBytes("abc");
			byte[] pbExpc = new byte[64] {
				0xBA, 0x80, 0xA5, 0x3F, 0x98, 0x1C, 0x4D, 0x0D,
				0x6A, 0x27, 0x97, 0xB6, 0x9F, 0x12, 0xF6, 0xE9,
				0x4C, 0x21, 0x2F, 0x14, 0x68, 0x5A, 0xC4, 0xB7,
				0x4B, 0x12, 0xBB, 0x6F, 0xDB, 0xFF, 0xA2, 0xD1,
				0x7D, 0x87, 0xC5, 0x39, 0x2A, 0xAB, 0x79, 0x2D,
				0xC2, 0x52, 0xD5, 0xDE, 0x45, 0x33, 0xCC, 0x95,
				0x18, 0xD3, 0x8A, 0xA8, 0xDB, 0xF1, 0x92, 0x5A,
				0xB9, 0x23, 0x86, 0xED, 0xD4, 0x00, 0x99, 0x23
			};

			byte[] pbC = h.ComputeHash(pbData);
			if(!MemUtil.ArraysEqual(pbC, pbExpc))
				throw new SecurityException("Blake2b-1");

			// ======================================================
			// Computed using the official b2sum tool

			pbExpc = new byte[64] {
				0x78, 0x6A, 0x02, 0xF7, 0x42, 0x01, 0x59, 0x03,
				0xC6, 0xC6, 0xFD, 0x85, 0x25, 0x52, 0xD2, 0x72,
				0x91, 0x2F, 0x47, 0x40, 0xE1, 0x58, 0x47, 0x61,
				0x8A, 0x86, 0xE2, 0x17, 0xF7, 0x1F, 0x54, 0x19,
				0xD2, 0x5E, 0x10, 0x31, 0xAF, 0xEE, 0x58, 0x53,
				0x13, 0x89, 0x64, 0x44, 0x93, 0x4E, 0xB0, 0x4B,
				0x90, 0x3A, 0x68, 0x5B, 0x14, 0x48, 0xB7, 0x55,
				0xD5, 0x6F, 0x70, 0x1A, 0xFE, 0x9B, 0xE2, 0xCE
			};

			pbC = h.ComputeHash(MemUtil.EmptyByteArray);
			if(!MemUtil.ArraysEqual(pbC, pbExpc))
				throw new SecurityException("Blake2b-2");

			// ======================================================
			// Computed using the official b2sum tool

			string strS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.:,;_-\r\n";
			StringBuilder sb = new StringBuilder();
			for(int i = 0; i < 1000; ++i) sb.Append(strS);
			pbData = StrUtil.Utf8.GetBytes(sb.ToString());

			pbExpc = new byte[64] {
				0x59, 0x69, 0x8D, 0x3B, 0x83, 0xF4, 0x02, 0x4E,
				0xD8, 0x99, 0x26, 0x0E, 0xF4, 0xE5, 0x9F, 0x20,
				0xDC, 0x31, 0xEE, 0x5B, 0x45, 0xEA, 0xBB, 0xFC,
				0x1C, 0x0A, 0x8E, 0xED, 0xAA, 0x7A, 0xFF, 0x50,
				0x82, 0xA5, 0x8F, 0xBC, 0x4A, 0x46, 0xFC, 0xC5,
				0xEF, 0x44, 0x4E, 0x89, 0x80, 0x7D, 0x3F, 0x1C,
				0xC1, 0x94, 0x45, 0xBB, 0xC0, 0x2C, 0x95, 0xAA,
				0x3F, 0x08, 0x8A, 0x93, 0xF8, 0x75, 0x91, 0xB0
			};

			Random r = new Random();
			int p = 0;
			while(p < pbData.Length)
			{
				int cb = r.Next(1, pbData.Length - p + 1);
				h.TransformBlock(pbData, p, cb, pbData, p);
				p += cb;
			}
			Debug.Assert(p == pbData.Length);

			h.TransformFinalBlock(MemUtil.EmptyByteArray, 0, 0);

			if(!MemUtil.ArraysEqual(h.Hash, pbExpc))
				throw new SecurityException("Blake2b-3");

			h.Clear();
#endif
		}

		private static void TestArgon2()
		{
#if DEBUG
			Argon2Kdf kdf = new Argon2Kdf();

			// ======================================================
			// From the official Argon2 1.3 reference code package
			// (test vector for Argon2d 1.3); also on
			// https://tools.ietf.org/html/draft-irtf-cfrg-argon2-00

			KdfParameters p = kdf.GetDefaultParameters();
			kdf.Randomize(p);

			Debug.Assert(p.GetUInt32(Argon2Kdf.ParamVersion, 0) == 0x13U);

			byte[] pbMsg = new byte[32];
			for(int i = 0; i < pbMsg.Length; ++i) pbMsg[i] = 1;

			p.SetUInt64(Argon2Kdf.ParamMemory, 32 * 1024);
			p.SetUInt64(Argon2Kdf.ParamIterations, 3);
			p.SetUInt32(Argon2Kdf.ParamParallelism, 4);

			byte[] pbSalt = new byte[16];
			for(int i = 0; i < pbSalt.Length; ++i) pbSalt[i] = 2;
			p.SetByteArray(Argon2Kdf.ParamSalt, pbSalt);

			byte[] pbKey = new byte[8];
			for(int i = 0; i < pbKey.Length; ++i) pbKey[i] = 3;
			p.SetByteArray(Argon2Kdf.ParamSecretKey, pbKey);

			byte[] pbAssoc = new byte[12];
			for(int i = 0; i < pbAssoc.Length; ++i) pbAssoc[i] = 4;
			p.SetByteArray(Argon2Kdf.ParamAssocData, pbAssoc);

			byte[] pbExpc = new byte[32] {
				0x51, 0x2B, 0x39, 0x1B, 0x6F, 0x11, 0x62, 0x97,
				0x53, 0x71, 0xD3, 0x09, 0x19, 0x73, 0x42, 0x94,
				0xF8, 0x68, 0xE3, 0xBE, 0x39, 0x84, 0xF3, 0xC1,
				0xA1, 0x3A, 0x4D, 0xB9, 0xFA, 0xBE, 0x4A, 0xCB
			};

			byte[] pb = kdf.Transform(pbMsg, p);

			if(!MemUtil.ArraysEqual(pb, pbExpc))
				throw new SecurityException("Argon2-1");

			// ======================================================
			// From the official Argon2 1.3 reference code package
			// (test vector for Argon2d 1.0)

			p.SetUInt32(Argon2Kdf.ParamVersion, 0x10);

			pbExpc = new byte[32] {
				0x96, 0xA9, 0xD4, 0xE5, 0xA1, 0x73, 0x40, 0x92,
				0xC8, 0x5E, 0x29, 0xF4, 0x10, 0xA4, 0x59, 0x14,
				0xA5, 0xDD, 0x1F, 0x5C, 0xBF, 0x08, 0xB2, 0x67,
				0x0D, 0xA6, 0x8A, 0x02, 0x85, 0xAB, 0xF3, 0x2B
			};

			pb = kdf.Transform(pbMsg, p);

			if(!MemUtil.ArraysEqual(pb, pbExpc))
				throw new SecurityException("Argon2-2");

			// ======================================================
			// From the official 'phc-winner-argon2-20151206.zip'
			// (test vector for Argon2d 1.0)

			p.SetUInt64(Argon2Kdf.ParamMemory, 16 * 1024);

			pbExpc = new byte[32] {
				0x57, 0xB0, 0x61, 0x3B, 0xFD, 0xD4, 0x13, 0x1A,
				0x0C, 0x34, 0x88, 0x34, 0xC6, 0x72, 0x9C, 0x2C,
				0x72, 0x29, 0x92, 0x1E, 0x6B, 0xBA, 0x37, 0x66,
				0x5D, 0x97, 0x8C, 0x4F, 0xE7, 0x17, 0x5E, 0xD2
			};

			pb = kdf.Transform(pbMsg, p);

			if(!MemUtil.ArraysEqual(pb, pbExpc))
				throw new SecurityException("Argon2-3");

#if SELFTEST_ARGON2_LONG
			// ======================================================
			// Computed using the official 'argon2' application
			// (test vectors for Argon2d 1.3)

			p = kdf.GetDefaultParameters();

			pbMsg = StrUtil.Utf8.GetBytes("ABC1234");

			p.SetUInt64(Argon2Kdf.ParamMemory, (1 << 11) * 1024); // 2 MB
			p.SetUInt64(Argon2Kdf.ParamIterations, 2);
			p.SetUInt32(Argon2Kdf.ParamParallelism, 2);

			pbSalt = StrUtil.Utf8.GetBytes("somesalt");
			p.SetByteArray(Argon2Kdf.ParamSalt, pbSalt);

			pbExpc = new byte[32] {
				0x29, 0xCB, 0xD3, 0xA1, 0x93, 0x76, 0xF7, 0xA2,
				0xFC, 0xDF, 0xB0, 0x68, 0xAC, 0x0B, 0x99, 0xBA,
				0x40, 0xAC, 0x09, 0x01, 0x73, 0x42, 0xCE, 0xF1,
				0x29, 0xCC, 0xA1, 0x4F, 0xE1, 0xC1, 0xB7, 0xA3
			};

			pb = kdf.Transform(pbMsg, p);

			if(!MemUtil.ArraysEqual(pb, pbExpc))
				throw new SecurityException("Argon2-4");

			p.SetUInt64(Argon2Kdf.ParamMemory, (1 << 10) * 1024); // 1 MB
			p.SetUInt64(Argon2Kdf.ParamIterations, 3);

			pbExpc = new byte[32] {
				0x7A, 0xBE, 0x1C, 0x1C, 0x8D, 0x7F, 0xD6, 0xDC,
				0x7C, 0x94, 0x06, 0x3E, 0xD8, 0xBC, 0xD8, 0x1C,
				0x2F, 0x87, 0x84, 0x99, 0x12, 0x83, 0xFE, 0x76,
				0x00, 0x64, 0xC4, 0x58, 0xA4, 0xDA, 0x35, 0x70
			};

			pb = kdf.Transform(pbMsg, p);

			if(!MemUtil.ArraysEqual(pb, pbExpc))
				throw new SecurityException("Argon2-5");

#if SELFTEST_ARGON2_LONGER
			p.SetUInt64(Argon2Kdf.ParamMemory, (1 << 20) * 1024); // 1 GB
			p.SetUInt64(Argon2Kdf.ParamIterations, 2);
			p.SetUInt32(Argon2Kdf.ParamParallelism, 3);

			pbExpc = new byte[32] {
				0xE6, 0xE7, 0xCB, 0xF5, 0x5A, 0x06, 0x93, 0x05,
				0x32, 0xBA, 0x86, 0xC6, 0x1F, 0x45, 0x17, 0x99,
				0x65, 0x41, 0x77, 0xF9, 0x30, 0x55, 0x9A, 0xE8,
				0x3D, 0x21, 0x48, 0xC6, 0x2D, 0x0C, 0x49, 0x11
			};

			pb = kdf.Transform(pbMsg, p);

			if(!MemUtil.ArraysEqual(pb, pbExpc))
				throw new SecurityException("Argon2-6");
#endif // SELFTEST_ARGON2_LONGER
#endif // SELFTEST_ARGON2_LONG
#endif // DEBUG
		}

		private static void TestHmac()
		{
#if DEBUG
			// Test vectors from RFC 4231

			byte[] pbKey = new byte[20];
			for(int i = 0; i < pbKey.Length; ++i) pbKey[i] = 0x0B;
			byte[] pbMsg = StrUtil.Utf8.GetBytes("Hi There");
			byte[] pbExpc = new byte[32] {
				0xB0, 0x34, 0x4C, 0x61, 0xD8, 0xDB, 0x38, 0x53,
				0x5C, 0xA8, 0xAF, 0xCE, 0xAF, 0x0B, 0xF1, 0x2B,
				0x88, 0x1D, 0xC2, 0x00, 0xC9, 0x83, 0x3D, 0xA7,
				0x26, 0xE9, 0x37, 0x6C, 0x2E, 0x32, 0xCF, 0xF7
			};
			HmacEval(pbKey, pbMsg, pbExpc, "1");

			pbKey = new byte[131];
			for(int i = 0; i < pbKey.Length; ++i) pbKey[i] = 0xAA;
			pbMsg = StrUtil.Utf8.GetBytes(
				"This is a test using a larger than block-size key and " +
				"a larger than block-size data. The key needs to be " +
				"hashed before being used by the HMAC algorithm.");
			pbExpc = new byte[32] {
				0x9B, 0x09, 0xFF, 0xA7, 0x1B, 0x94, 0x2F, 0xCB,
				0x27, 0x63, 0x5F, 0xBC, 0xD5, 0xB0, 0xE9, 0x44,
				0xBF, 0xDC, 0x63, 0x64, 0x4F, 0x07, 0x13, 0x93,
				0x8A, 0x7F, 0x51, 0x53, 0x5C, 0x3A, 0x35, 0xE2
			};
			HmacEval(pbKey, pbMsg, pbExpc, "2");
#endif
		}

#if DEBUG
		private static void HmacEval(byte[] pbKey, byte[] pbMsg,
			byte[] pbExpc, string strID)
		{
			using(HMACSHA256 h = new HMACSHA256(pbKey))
			{
				h.TransformBlock(pbMsg, 0, pbMsg.Length, pbMsg, 0);
				h.TransformFinalBlock(MemUtil.EmptyByteArray, 0, 0);

				byte[] pbHash = h.Hash;
				if(!MemUtil.ArraysEqual(pbHash, pbExpc))
					throw new SecurityException("HMAC-SHA-256-" + strID);

				// Reuse the object
				h.Initialize();
				h.TransformBlock(pbMsg, 0, pbMsg.Length, pbMsg, 0);
				h.TransformFinalBlock(MemUtil.EmptyByteArray, 0, 0);

				pbHash = h.Hash;
				if(!MemUtil.ArraysEqual(pbHash, pbExpc))
					throw new SecurityException("HMAC-SHA-256-" + strID + "-R");
			}
		}
#endif

		private static void TestNativeKeyTransform()
		{
#if DEBUG
			byte[] pbOrgKey = CryptoRandom.Instance.GetRandomBytes(32);
			byte[] pbSeed = CryptoRandom.Instance.GetRandomBytes(32);
			ulong uRounds = (ulong)((new Random()).Next(1, 0x3FFF));

			byte[] pbManaged = new byte[32];
			Array.Copy(pbOrgKey, pbManaged, 32);
			if(!AesKdf.TransformKeyManaged(pbManaged, pbSeed, uRounds))
				throw new SecurityException("AES-KDF-1");

			byte[] pbNative = new byte[32];
			Array.Copy(pbOrgKey, pbNative, 32);
			if(!NativeLib.TransformKey256(pbNative, pbSeed, uRounds))
				return; // Native library not available ("success")

			if(!MemUtil.ArraysEqual(pbManaged, pbNative))
				throw new SecurityException("AES-KDF-2");
#endif
		}

		private static void TestMemUtil()
		{
#if DEBUG
			Random r = new Random();
			byte[] pb = CryptoRandom.Instance.GetRandomBytes((uint)r.Next(
				0, 0x2FFFF));

			byte[] pbCompressed = MemUtil.Compress(pb);
			if(!MemUtil.ArraysEqual(MemUtil.Decompress(pbCompressed), pb))
				throw new InvalidOperationException("GZip");

			Encoding enc = StrUtil.Utf8;
			pb = enc.GetBytes("012345678901234567890a");
			byte[] pbN = enc.GetBytes("9012");
			if(MemUtil.IndexOf<byte>(pb, pbN) != 9)
				throw new InvalidOperationException("MemUtil-1");
			pbN = enc.GetBytes("01234567890123");
			if(MemUtil.IndexOf<byte>(pb, pbN) != 0)
				throw new InvalidOperationException("MemUtil-2");
			pbN = enc.GetBytes("a");
			if(MemUtil.IndexOf<byte>(pb, pbN) != 21)
				throw new InvalidOperationException("MemUtil-3");
			pbN = enc.GetBytes("0a");
			if(MemUtil.IndexOf<byte>(pb, pbN) != 20)
				throw new InvalidOperationException("MemUtil-4");
			pbN = enc.GetBytes("1");
			if(MemUtil.IndexOf<byte>(pb, pbN) != 1)
				throw new InvalidOperationException("MemUtil-5");
			pbN = enc.GetBytes("b");
			if(MemUtil.IndexOf<byte>(pb, pbN) >= 0)
				throw new InvalidOperationException("MemUtil-6");
			pbN = enc.GetBytes("012b");
			if(MemUtil.IndexOf<byte>(pb, pbN) >= 0)
				throw new InvalidOperationException("MemUtil-7");

			byte[] pbRes = MemUtil.ParseBase32("MY======");
			byte[] pbExp = Encoding.ASCII.GetBytes("f");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-1");

			pbRes = MemUtil.ParseBase32("MZXQ====");
			pbExp = Encoding.ASCII.GetBytes("fo");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-2");

			pbRes = MemUtil.ParseBase32("MZXW6===");
			pbExp = Encoding.ASCII.GetBytes("foo");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-3");

			pbRes = MemUtil.ParseBase32("MZXW6YQ=");
			pbExp = Encoding.ASCII.GetBytes("foob");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-4");

			pbRes = MemUtil.ParseBase32("MZXW6YTB");
			pbExp = Encoding.ASCII.GetBytes("fooba");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-5");

			pbRes = MemUtil.ParseBase32("MZXW6YTBOI======");
			pbExp = Encoding.ASCII.GetBytes("foobar");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-6");

			pbRes = MemUtil.ParseBase32("JNSXSIDQOJXXM2LEMVZCAYTBONSWIIDPNYQG63TFFV2GS3LFEBYGC43TO5XXEZDTFY======");
			pbExp = Encoding.ASCII.GetBytes("Key provider based on one-time passwords.");
			if(!MemUtil.ArraysEqual(pbRes, pbExp)) throw new Exception("Base32-7");

			int i = 0 - 0x10203040;
			pbRes = MemUtil.Int32ToBytes(i);
			if(MemUtil.ByteArrayToHexString(pbRes) != "C0CFDFEF")
				throw new Exception("MemUtil-8"); // Must be little-endian
			if(MemUtil.BytesToUInt32(pbRes) != (uint)i)
				throw new Exception("MemUtil-9");
			if(MemUtil.BytesToInt32(pbRes) != i)
				throw new Exception("MemUtil-10");
#endif
		}

		private static void TestHmacOtp()
		{
#if (DEBUG && !KeePassLibSD)
			byte[] pbSecret = StrUtil.Utf8.GetBytes("12345678901234567890");
			string[] vExp = new string[]{ "755224", "287082", "359152",
				"969429", "338314", "254676", "287922", "162583", "399871",
				"520489" };

			for(int i = 0; i < vExp.Length; ++i)
			{
				if(HmacOtp.Generate(pbSecret, (ulong)i, 6, false, -1) != vExp[i])
					throw new InvalidOperationException("HmacOtp");
			}
#endif
		}

		private static void TestProtectedObjects()
		{
#if DEBUG
			Encoding enc = StrUtil.Utf8;

			byte[] pbData = enc.GetBytes("Test Test Test Test");
			ProtectedBinary pb = new ProtectedBinary(true, pbData);
			if(!pb.IsProtected) throw new SecurityException("ProtectedBinary-1");

			byte[] pbDec = pb.ReadData();
			if(!MemUtil.ArraysEqual(pbData, pbDec))
				throw new SecurityException("ProtectedBinary-2");
			if(!pb.IsProtected) throw new SecurityException("ProtectedBinary-3");

			byte[] pbData2 = enc.GetBytes("Test Test Test Test");
			byte[] pbData3 = enc.GetBytes("Test Test Test Test Test");
			ProtectedBinary pb2 = new ProtectedBinary(true, pbData2);
			ProtectedBinary pb3 = new ProtectedBinary(true, pbData3);
			if(!pb.Equals(pb2)) throw new SecurityException("ProtectedBinary-4");
			if(pb.Equals(pb3)) throw new SecurityException("ProtectedBinary-5");
			if(pb2.Equals(pb3)) throw new SecurityException("ProtectedBinary-6");

			if(pb.GetHashCode() != pb2.GetHashCode())
				throw new SecurityException("ProtectedBinary-7");
			if(!((object)pb).Equals((object)pb2))
				throw new SecurityException("ProtectedBinary-8");
			if(((object)pb).Equals((object)pb3))
				throw new SecurityException("ProtectedBinary-9");
			if(((object)pb2).Equals((object)pb3))
				throw new SecurityException("ProtectedBinary-10");

			ProtectedString ps = new ProtectedString();
			if(ps.Length != 0) throw new SecurityException("ProtectedString-1");
			if(!ps.IsEmpty) throw new SecurityException("ProtectedString-2");
			if(ps.ReadString().Length != 0)
				throw new SecurityException("ProtectedString-3");

			ps = new ProtectedString(true, "Test");
			ProtectedString ps2 = new ProtectedString(true, enc.GetBytes("Test"));
			if(ps.IsEmpty) throw new SecurityException("ProtectedString-4");
			pbData = ps.ReadUtf8();
			pbData2 = ps2.ReadUtf8();
			if(!MemUtil.ArraysEqual(pbData, pbData2))
				throw new SecurityException("ProtectedString-5");
			if(pbData.Length != 4)
				throw new SecurityException("ProtectedString-6");
			if(ps.ReadString() != ps2.ReadString())
				throw new SecurityException("ProtectedString-7");
			pbData = ps.ReadUtf8();
			pbData2 = ps2.ReadUtf8();
			if(!MemUtil.ArraysEqual(pbData, pbData2))
				throw new SecurityException("ProtectedString-8");
			if(!ps.IsProtected) throw new SecurityException("ProtectedString-9");
			if(!ps2.IsProtected) throw new SecurityException("ProtectedString-10");

			Random r = new Random();
			string str = string.Empty;
			ps = new ProtectedString();
			for(int i = 0; i < 100; ++i)
			{
				bool bProt = ((r.Next() % 4) != 0);
				ps = ps.WithProtection(bProt);

				int x = r.Next(str.Length + 1);
				int c = r.Next(20);
				char ch = (char)r.Next(1, 256);

				string strIns = new string(ch, c);
				str = str.Insert(x, strIns);
				ps = ps.Insert(x, strIns);

				if(ps.IsProtected != bProt)
					throw new SecurityException("ProtectedString-11");
				if(ps.ReadString() != str)
					throw new SecurityException("ProtectedString-12");

				ps = ps.WithProtection(bProt);

				x = r.Next(str.Length);
				c = r.Next(str.Length - x + 1);

				str = str.Remove(x, c);
				ps = ps.Remove(x, c);

				if(ps.IsProtected != bProt)
					throw new SecurityException("ProtectedString-13");
				if(ps.ReadString() != str)
					throw new SecurityException("ProtectedString-14");
			}
#endif
		}

		private static void TestStrUtil()
		{
#if DEBUG
			string[] vSeps = new string[]{ "ax", "b", "c" };
			const string str1 = "axbqrstcdeax";
			List<string> v1 = StrUtil.SplitWithSep(str1, vSeps, true);

			if(v1.Count != 9) throw new InvalidOperationException("StrUtil-1");
			if(v1[0].Length > 0) throw new InvalidOperationException("StrUtil-2");
			if(!v1[1].Equals("ax")) throw new InvalidOperationException("StrUtil-3");
			if(v1[2].Length > 0) throw new InvalidOperationException("StrUtil-4");
			if(!v1[3].Equals("b")) throw new InvalidOperationException("StrUtil-5");
			if(!v1[4].Equals("qrst")) throw new InvalidOperationException("StrUtil-6");
			if(!v1[5].Equals("c")) throw new InvalidOperationException("StrUtil-7");
			if(!v1[6].Equals("de")) throw new InvalidOperationException("StrUtil-8");
			if(!v1[7].Equals("ax")) throw new InvalidOperationException("StrUtil-9");
			if(v1[8].Length > 0) throw new InvalidOperationException("StrUtil-10");

			const string str2 = "12ab56";
			List<string> v2 = StrUtil.SplitWithSep(str2, new string[]{ "AB" }, false);
			if(v2.Count != 3) throw new InvalidOperationException("StrUtil-11");
			if(!v2[0].Equals("12")) throw new InvalidOperationException("StrUtil-12");
			if(!v2[1].Equals("AB")) throw new InvalidOperationException("StrUtil-13");
			if(!v2[2].Equals("56")) throw new InvalidOperationException("StrUtil-14");

			List<string> v3 = StrUtil.SplitWithSep("pqrs", vSeps, false);
			if(v3.Count != 1) throw new InvalidOperationException("StrUtil-15");
			if(!v3[0].Equals("pqrs")) throw new InvalidOperationException("StrUtil-16");

			if(StrUtil.VersionToString(0x000F000E000D000CUL) != "15.14.13.12")
				throw new InvalidOperationException("StrUtil-V1");
			if(StrUtil.VersionToString(0x00FF000E00010000UL) != "255.14.1")
				throw new InvalidOperationException("StrUtil-V2");
			if(StrUtil.VersionToString(0x000F00FF00000000UL) != "15.255")
				throw new InvalidOperationException("StrUtil-V3");
			if(StrUtil.VersionToString(0x00FF000000000000UL) != "255")
				throw new InvalidOperationException("StrUtil-V4");
			if(StrUtil.VersionToString(0x00FF000000000000UL, 2) != "255.0")
				throw new InvalidOperationException("StrUtil-V5");
			if(StrUtil.VersionToString(0x0000000000070000UL) != "0.0.7")
				throw new InvalidOperationException("StrUtil-V6");
			if(StrUtil.VersionToString(0x0000000000000000UL) != "0")
				throw new InvalidOperationException("StrUtil-V7");
			if(StrUtil.VersionToString(0x00000000FFFF0000UL, 4) != "0.0.65535.0")
				throw new InvalidOperationException("StrUtil-V8");
			if(StrUtil.VersionToString(0x0000000000000000UL, 4) != "0.0.0.0")
				throw new InvalidOperationException("StrUtil-V9");

			if(StrUtil.RtfEncodeChar('\u0000') != "\\u0?")
				throw new InvalidOperationException("StrUtil-Rtf1");
			if(StrUtil.RtfEncodeChar('\u7FFF') != "\\u32767?")
				throw new InvalidOperationException("StrUtil-Rtf2");
			if(StrUtil.RtfEncodeChar('\u8000') != "\\u-32768?")
				throw new InvalidOperationException("StrUtil-Rtf3");
			if(StrUtil.RtfEncodeChar('\uFFFF') != "\\u-1?")
				throw new InvalidOperationException("StrUtil-Rtf4");

			if(!StrUtil.StringToBool(Boolean.TrueString))
				throw new InvalidOperationException("StrUtil-Bool1");
			if(StrUtil.StringToBool(Boolean.FalseString))
				throw new InvalidOperationException("StrUtil-Bool2");

			if(StrUtil.Count("Abracadabra", "a") != 4)
				throw new InvalidOperationException("StrUtil-Count1");
			if(StrUtil.Count("Bla", "U") != 0)
				throw new InvalidOperationException("StrUtil-Count2");
			if(StrUtil.Count("AAAAA", "AA") != 4)
				throw new InvalidOperationException("StrUtil-Count3");

			const string sU = "data:mytype;base64,";
			if(!StrUtil.IsDataUri(sU))
				throw new InvalidOperationException("StrUtil-DataUri1");
			if(!StrUtil.IsDataUri(sU, "mytype"))
				throw new InvalidOperationException("StrUtil-DataUri2");
			if(StrUtil.IsDataUri(sU, "notmytype"))
				throw new InvalidOperationException("StrUtil-DataUri3");

			uint u = 0x7FFFFFFFU;
			if(u.ToString(NumberFormatInfo.InvariantInfo) != "2147483647")
				throw new InvalidOperationException("StrUtil-Inv1");
			if(uint.MaxValue.ToString(NumberFormatInfo.InvariantInfo) !=
				"4294967295")
				throw new InvalidOperationException("StrUtil-Inv2");
			if(long.MinValue.ToString(NumberFormatInfo.InvariantInfo) !=
				"-9223372036854775808")
				throw new InvalidOperationException("StrUtil-Inv3");
			if(short.MinValue.ToString(NumberFormatInfo.InvariantInfo) !=
				"-32768")
				throw new InvalidOperationException("StrUtil-Inv4");

			if(!string.Equals("abcd", "aBcd", StrUtil.CaseIgnoreCmp))
				throw new InvalidOperationException("StrUtil-Case1");
			if(string.Equals(@"a<b", @"a>b", StrUtil.CaseIgnoreCmp))
				throw new InvalidOperationException("StrUtil-Case2");
#endif
		}

		private static void TestUrlUtil()
		{
#if DEBUG
#if !KeePassUAP
			Debug.Assert(Uri.UriSchemeHttp.Equals("http", StrUtil.CaseIgnoreCmp));
			Debug.Assert(Uri.UriSchemeHttps.Equals("https", StrUtil.CaseIgnoreCmp));
#endif

			if(UrlUtil.GetHost(@"scheme://domain:port/path?query_string#fragment_id") !=
				"domain")
				throw new InvalidOperationException("UrlUtil-H1");
			if(UrlUtil.GetHost(@"http://example.org:80") != "example.org")
				throw new InvalidOperationException("UrlUtil-H2");
			if(UrlUtil.GetHost(@"mailto:bob@example.com") != "example.com")
				throw new InvalidOperationException("UrlUtil-H3");
			if(UrlUtil.GetHost(@"ftp://asmith@ftp.example.org") != "ftp.example.org")
				throw new InvalidOperationException("UrlUtil-H4");
			if(UrlUtil.GetHost(@"scheme://username:password@domain:port/path?query_string#fragment_id") !=
				"domain")
				throw new InvalidOperationException("UrlUtil-H5");
			if(UrlUtil.GetHost(@"bob@example.com") != "example.com")
				throw new InvalidOperationException("UrlUtil-H6");
			if(UrlUtil.GetHost(@"s://u:p@d.tld:p/p?q#f") != "d.tld")
				throw new InvalidOperationException("UrlUtil-H7");

			if(NativeLib.IsUnix()) return;

			string strBase = "\\\\HOMESERVER\\Apps\\KeePass\\KeePass.exe";
			string strDoc = "\\\\HOMESERVER\\Documents\\KeePass\\NewDatabase.kdbx";
			string strRel = "..\\..\\Documents\\KeePass\\NewDatabase.kdbx";

			string str = UrlUtil.MakeRelativePath(strBase, strDoc);
			if(!str.Equals(strRel)) throw new InvalidOperationException("UrlUtil-R1");

			str = UrlUtil.MakeAbsolutePath(strBase, strRel);
			if(!str.Equals(strDoc)) throw new InvalidOperationException("UrlUtil-R2");

			str = UrlUtil.GetQuotedAppPath(" \"Test\" \"%1\" ");
			if(str != "Test") throw new InvalidOperationException("UrlUtil-Q1");
			str = UrlUtil.GetQuotedAppPath("C:\\Program Files\\Test.exe");
			if(str != "C:\\Program Files\\Test.exe") throw new InvalidOperationException("UrlUtil-Q2");
			str = UrlUtil.GetQuotedAppPath("Reg.exe \"Test\" \"Test 2\"");
			if(str != "Reg.exe \"Test\" \"Test 2\"") throw new InvalidOperationException("UrlUtil-Q3");
#endif
		}
	}
}
