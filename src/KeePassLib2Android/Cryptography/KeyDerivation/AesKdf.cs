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
using System.Text;

#if KeePassUAP
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
#else
using System.Security.Cryptography;
#endif

using KeePassLib.Cryptography;
using KeePassLib.Native;
using KeePassLib.Utility;

namespace KeePassLib.Cryptography.KeyDerivation
{
	public sealed class AesKdf : KdfEngine
	{
		private static readonly PwUuid g_uuid = new PwUuid(new byte[] {
			0xC9, 0xD9, 0xF3, 0x9A, 0x62, 0x8A, 0x44, 0x60,
			0xBF, 0x74, 0x0D, 0x08, 0xC1, 0x8A, 0x4F, 0xEA });

		public const string ParamRounds = "R"; // UInt64
		public const string ParamSeed = "S"; // Byte[32]

		public override PwUuid Uuid
		{
			get { return g_uuid; }
		}

		public override string Name
		{
			get { return "AES-KDF"; }
		}

		public AesKdf()
		{
		}

		public override KdfParameters GetDefaultParameters()
		{
			KdfParameters p = base.GetDefaultParameters();
			p.SetUInt64(ParamRounds, PwDefs.DefaultKeyEncryptionRounds);
			return p;
		}

		public override void Randomize(KdfParameters p)
		{
			if(p == null) { Debug.Assert(false); return; }
			Debug.Assert(g_uuid.Equals(p.KdfUuid));

			byte[] pbSeed = CryptoRandom.Instance.GetRandomBytes(32);
			p.SetByteArray(ParamSeed, pbSeed);
		}

		public override byte[] Transform(byte[] pbMsg, KdfParameters p)
		{
			if(pbMsg == null) throw new ArgumentNullException("pbMsg");
			if(p == null) throw new ArgumentNullException("p");

			Type tRounds = p.GetTypeOf(ParamRounds);
			if(tRounds == null) throw new ArgumentNullException("p.Rounds");
			if(tRounds != typeof(ulong)) throw new ArgumentOutOfRangeException("p.Rounds");
			ulong uRounds = p.GetUInt64(ParamRounds, 0);

			byte[] pbSeed = p.GetByteArray(ParamSeed);
			if(pbSeed == null) throw new ArgumentNullException("p.Seed");

			if(pbMsg.Length != 32)
			{
				Debug.Assert(false);
				pbMsg = CryptoUtil.HashSha256(pbMsg);
			}

			if(pbSeed.Length != 32)
			{
				Debug.Assert(false);
				pbSeed = CryptoUtil.HashSha256(pbSeed);
			}

			return TransformKey(pbMsg, pbSeed, uRounds);
		}

		private static byte[] TransformKey(byte[] pbOriginalKey32, byte[] pbKeySeed32,
			ulong uNumRounds)
		{
			Debug.Assert((pbOriginalKey32 != null) && (pbOriginalKey32.Length == 32));
			if(pbOriginalKey32 == null) throw new ArgumentNullException("pbOriginalKey32");
			if(pbOriginalKey32.Length != 32) throw new ArgumentException();

			Debug.Assert((pbKeySeed32 != null) && (pbKeySeed32.Length == 32));
			if(pbKeySeed32 == null) throw new ArgumentNullException("pbKeySeed32");
			if(pbKeySeed32.Length != 32) throw new ArgumentException();

			byte[] pbNewKey = new byte[32];
			Array.Copy(pbOriginalKey32, pbNewKey, pbNewKey.Length);

			try
			{
				// Try to use the native library first
				if(NativeLib.TransformKey256(pbNewKey, pbKeySeed32, uNumRounds))
					return CryptoUtil.HashSha256(pbNewKey);

				if(TransformKeyManaged(pbNewKey, pbKeySeed32, uNumRounds))
					return CryptoUtil.HashSha256(pbNewKey);
			}
			finally { MemUtil.ZeroByteArray(pbNewKey); }

			return null;
		}

		internal static bool TransformKeyManaged(byte[] pbNewKey32, byte[] pbKeySeed32,
			ulong uNumRounds)
		{
#if KeePassUAP
			KeyParameter kp = new KeyParameter(pbKeySeed32);
			AesEngine aes = new AesEngine();
			aes.Init(true, kp);

			for(ulong i = 0; i < uNumRounds; ++i)
			{
				aes.ProcessBlock(pbNewKey32, 0, pbNewKey32, 0);
				aes.ProcessBlock(pbNewKey32, 16, pbNewKey32, 16);
			}
#else
			byte[] pbIV = new byte[16];
			Array.Clear(pbIV, 0, pbIV.Length);

			RijndaelManaged r = new RijndaelManaged();
			if(r.BlockSize != 128) // AES block size
			{
				Debug.Assert(false);
				r.BlockSize = 128;
			}

			r.IV = pbIV;
			r.Mode = CipherMode.ECB;
			r.KeySize = 256;
			r.Key = pbKeySeed32;
			ICryptoTransform iCrypt = r.CreateEncryptor();

			// !iCrypt.CanReuseTransform -- doesn't work with Mono
			if((iCrypt == null) || (iCrypt.InputBlockSize != 16) ||
				(iCrypt.OutputBlockSize != 16))
			{
				Debug.Assert(false, "Invalid ICryptoTransform.");
				Debug.Assert((iCrypt.InputBlockSize == 16), "Invalid input block size!");
				Debug.Assert((iCrypt.OutputBlockSize == 16), "Invalid output block size!");
				return false;
			}

			for(ulong i = 0; i < uNumRounds; ++i)
			{
				iCrypt.TransformBlock(pbNewKey32, 0, 16, pbNewKey32, 0);
				iCrypt.TransformBlock(pbNewKey32, 16, 16, pbNewKey32, 16);
			}
#endif

			return true;
		}

		public override KdfParameters GetBestParameters(uint uMilliseconds)
		{
			const ulong uStep = 3001;
			ulong uRounds;

			KdfParameters p = GetDefaultParameters();

			// Try native method
			if(NativeLib.TransformKeyBenchmark256(uMilliseconds, out uRounds))
			{
				p.SetUInt64(ParamRounds, uRounds);
				return p;
			}

			byte[] pbKey = new byte[32];
			byte[] pbNewKey = new byte[32];
			for(int i = 0; i < pbKey.Length; ++i)
			{
				pbKey[i] = (byte)i;
				pbNewKey[i] = (byte)i;
			}

#if KeePassUAP
			KeyParameter kp = new KeyParameter(pbKey);
			AesEngine aes = new AesEngine();
			aes.Init(true, kp);
#else
			byte[] pbIV = new byte[16];
			Array.Clear(pbIV, 0, pbIV.Length);

			RijndaelManaged r = new RijndaelManaged();
			if(r.BlockSize != 128) // AES block size
			{
				Debug.Assert(false);
				r.BlockSize = 128;
			}

			r.IV = pbIV;
			r.Mode = CipherMode.ECB;
			r.KeySize = 256;
			r.Key = pbKey;
			ICryptoTransform iCrypt = r.CreateEncryptor();

			// !iCrypt.CanReuseTransform -- doesn't work with Mono
			if((iCrypt == null) || (iCrypt.InputBlockSize != 16) ||
				(iCrypt.OutputBlockSize != 16))
			{
				Debug.Assert(false, "Invalid ICryptoTransform.");
				Debug.Assert(iCrypt.InputBlockSize == 16, "Invalid input block size!");
				Debug.Assert(iCrypt.OutputBlockSize == 16, "Invalid output block size!");

				p.SetUInt64(ParamRounds, PwDefs.DefaultKeyEncryptionRounds);
				return p;
			}
#endif

			uRounds = 0;
			int tStart = Environment.TickCount;
			while(true)
			{
				for(ulong j = 0; j < uStep; ++j)
				{
#if KeePassUAP
					aes.ProcessBlock(pbNewKey, 0, pbNewKey, 0);
					aes.ProcessBlock(pbNewKey, 16, pbNewKey, 16);
#else
					iCrypt.TransformBlock(pbNewKey, 0, 16, pbNewKey, 0);
					iCrypt.TransformBlock(pbNewKey, 16, 16, pbNewKey, 16);
#endif
				}

				uRounds += uStep;
				if(uRounds < uStep) // Overflow check
				{
					uRounds = ulong.MaxValue;
					break;
				}

				uint tElapsed = (uint)(Environment.TickCount - tStart);
				if(tElapsed > uMilliseconds) break;
			}

			p.SetUInt64(ParamRounds, uRounds);
			return p;
		}
	}
}
