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
using System.Runtime.InteropServices;
using System.Text;

namespace KeePassLib.Cryptography.KeyDerivation
{
	public sealed partial class Argon2Kdf : KdfEngine
	{
		private static readonly PwUuid g_uuid = new PwUuid(new byte[] {
			0xEF, 0x63, 0x6D, 0xDF, 0x8C, 0x29, 0x44, 0x4B,
			0x91, 0xF7, 0xA9, 0xA4, 0x03, 0xE3, 0x0A, 0x0C });

		public const string ParamSalt = "S"; // Byte[]
		public const string ParamParallelism = "P"; // UInt32
		public const string ParamMemory = "M"; // UInt64
		public const string ParamIterations = "I"; // UInt64
		public const string ParamVersion = "V"; // UInt32
		public const string ParamSecretKey = "K"; // Byte[]
		public const string ParamAssocData = "A"; // Byte[]

		private const uint MinVersion = 0x10;
		private const uint MaxVersion = 0x13;

		private const int MinSalt = 8;
		private const int MaxSalt = int.MaxValue; // .NET limit; 2^32 - 1 in spec

		internal const ulong MinIterations = 1;
		internal const ulong MaxIterations = uint.MaxValue;

		internal const ulong MinMemory = 1024 * 8; // For parallelism = 1
		// internal const ulong MaxMemory = (ulong)uint.MaxValue * 1024UL; // Spec
		internal const ulong MaxMemory = int.MaxValue; // .NET limit

		internal const uint MinParallelism = 1;
		internal const uint MaxParallelism = (1 << 24) - 1;

		internal const ulong DefaultIterations = 2;
		internal const ulong DefaultMemory = 1024 * 1024; // 1 MB
		internal const uint DefaultParallelism = 2;

		public override PwUuid Uuid
		{
			get { return g_uuid; }
		}

		public override string Name
		{
			get { return "Argon2"; }
		}

        public override byte[] GetSeed(KdfParameters p)
        { return p.GetByteArray(ParamSalt); }

        public Argon2Kdf()
		{
		}

		public override KdfParameters GetDefaultParameters()
		{
			KdfParameters p = base.GetDefaultParameters();

			p.SetUInt32(ParamVersion, MaxVersion);

			p.SetUInt64(ParamIterations, DefaultIterations);
			p.SetUInt64(ParamMemory, DefaultMemory);
			p.SetUInt32(ParamParallelism, DefaultParallelism);

			return p;
		}

		public override void Randomize(KdfParameters p)
		{
			if(p == null) { Debug.Assert(false); return; }
			Debug.Assert(g_uuid.Equals(p.KdfUuid));

			byte[] pb = CryptoRandom.Instance.GetRandomBytes(32);
			p.SetByteArray(ParamSalt, pb);
		}

		public override byte[] Transform(byte[] pbMsg, KdfParameters p)
		{
			if(pbMsg == null) throw new ArgumentNullException("pbMsg");
			if(p == null) throw new ArgumentNullException("p");

			byte[] pbSalt = p.GetByteArray(ParamSalt);
			if(pbSalt == null)
				throw new ArgumentNullException("p.Salt");
			if((pbSalt.Length < MinSalt) || (pbSalt.Length > MaxSalt))
				throw new ArgumentOutOfRangeException("p.Salt");

			uint uPar = p.GetUInt32(ParamParallelism, 0);
			if((uPar < MinParallelism) || (uPar > MaxParallelism))
				throw new ArgumentOutOfRangeException("p.Parallelism");

			ulong uMem = p.GetUInt64(ParamMemory, 0);
			if((uMem < MinMemory) || (uMem > MaxMemory))
				throw new ArgumentOutOfRangeException("p.Memory");

			ulong uIt = p.GetUInt64(ParamIterations, 0);
			if((uIt < MinIterations) || (uIt > MaxIterations))
				throw new ArgumentOutOfRangeException("p.Iterations");

			uint v = p.GetUInt32(ParamVersion, 0);
			if((v < MinVersion) || (v > MaxVersion))
				throw new ArgumentOutOfRangeException("p.Version");

			byte[] pbSecretKey = p.GetByteArray(ParamSecretKey);
			byte[] pbAssocData = p.GetByteArray(ParamAssocData);

			if (pbSecretKey != null) {
				throw new ArgumentOutOfRangeException("Unsupported configuration: non-null pbSecretKey");
			}

			if (pbAssocData != null) {
				throw new ArgumentOutOfRangeException("Unsupported configuration: non-null pbAssocData");
			}

			/*
			byte[] pbRet = Argon2d(pbMsg, pbSalt, uPar, uMem, uIt,
				32, v, pbSecretKey, pbAssocData);
			*/

			IntPtr msgPtr = Marshal.AllocHGlobal(pbMsg.Length);
			IntPtr saltPtr = Marshal.AllocHGlobal(pbSalt.Length);
			IntPtr retPtr = Marshal.AllocHGlobal(32);
			Marshal.Copy(pbMsg, 0, msgPtr, pbMsg.Length);
			Marshal.Copy(pbSalt, 0, saltPtr, pbSalt.Length);

			const UInt32 Argon2_d = 0;

			int ret = argon2_hash(
					(UInt32)uIt, (UInt32)(uMem / 1024), uPar,
					msgPtr, (IntPtr)pbMsg.Length,
					saltPtr, (IntPtr)pbSalt.Length,
					retPtr, (IntPtr)32,
					(IntPtr)0, (IntPtr)0, Argon2_d, v);

			if (ret != 0) {
				throw new Exception("argon2_hash failed with " + ret);
			}

			byte[] pbRet = new byte[32];
			Marshal.Copy(retPtr, pbRet, 0, 32);

			Marshal.FreeHGlobal(msgPtr);
			Marshal.FreeHGlobal(saltPtr);
			Marshal.FreeHGlobal(retPtr);

			if(uMem > (100UL * 1024UL * 1024UL)) GC.Collect();
			return pbRet;
		}

		public override KdfParameters GetBestParameters(uint uMilliseconds)
		{
			KdfParameters p = GetDefaultParameters();
			Randomize(p);

			MaximizeParamUInt64(p, ParamIterations, MinIterations,
				MaxIterations, uMilliseconds, true);
			return p;
		}

		[DllImport("argon2")]
		static extern int argon2_hash(
				UInt32 t_cost, UInt32 m_cost, UInt32 parallelism,
				IntPtr pwd, IntPtr pwdlen,
				IntPtr salt, IntPtr saltlen,
				IntPtr hash, IntPtr hashlen,
				IntPtr encoded, IntPtr encodedlen,
				UInt32 type, UInt32 version);
	}
}
