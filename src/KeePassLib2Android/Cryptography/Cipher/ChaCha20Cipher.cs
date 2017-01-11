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
using System.IO;

using KeePassLib.Resources;
using KeePassLib.Utility;

namespace KeePassLib.Cryptography.Cipher
{
	/// <summary>
	/// Implementation of the ChaCha20 cipher with a 96-bit nonce,
	/// as specified in RFC 7539.
	/// https://tools.ietf.org/html/rfc7539
	/// </summary>
	public sealed class ChaCha20Cipher : CtrBlockCipher
	{
		private uint[] m_s = new uint[16]; // State
		private uint[] m_x = new uint[16]; // Working buffer

		private bool m_bLargeCounter; // See constructor documentation

		private static readonly uint[] g_sigma = new uint[4] {
			0x61707865, 0x3320646E, 0x79622D32, 0x6B206574
		};

		private const string StrNameRfc = "ChaCha20 (RFC 7539)";

		public override int BlockSize
		{
			get { return 64; }
		}

		public ChaCha20Cipher(byte[] pbKey32, byte[] pbIV12) :
			this(pbKey32, pbIV12, false)
		{
		}

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="pbKey32">Key (32 bytes).</param>
		/// <param name="pbIV12">Nonce (12 bytes).</param>
		/// <param name="bLargeCounter">If <c>false</c>, the RFC 7539 version
		/// of ChaCha20 is used. In this case, only 256 GB of data can be
		/// encrypted securely (because the block counter is a 32-bit variable);
		/// an attempt to encrypt more data throws an exception.
		/// If <paramref name="bLargeCounter" /> is <c>true</c>, the 32-bit
		/// counter overflows to another 32-bit variable (i.e. the counter
		/// effectively is a 64-bit variable), like in the original ChaCha20
		/// specification by D. J. Bernstein (which has a 64-bit counter and a
		/// 64-bit nonce). To be compatible with this version, the 64-bit nonce
		/// must be stored in the last 8 bytes of <paramref name="pbIV12" />
		/// and the first 4 bytes must be 0.
		/// If the IV was generated randomly, a 12-byte IV and a large counter
		/// can be used to securely encrypt more than 256 GB of data (but note
		/// this is incompatible with RFC 7539 and the original specification).</param>
		public ChaCha20Cipher(byte[] pbKey32, byte[] pbIV12, bool bLargeCounter) :
			base()
		{
			if(pbKey32 == null) throw new ArgumentNullException("pbKey32");
			if(pbKey32.Length != 32) throw new ArgumentOutOfRangeException("pbKey32");
			if(pbIV12 == null) throw new ArgumentNullException("pbIV12");
			if(pbIV12.Length != 12) throw new ArgumentOutOfRangeException("pbIV12");

			m_bLargeCounter = bLargeCounter;

			// Key setup
			m_s[4] = MemUtil.BytesToUInt32(pbKey32, 0);
			m_s[5] = MemUtil.BytesToUInt32(pbKey32, 4);
			m_s[6] = MemUtil.BytesToUInt32(pbKey32, 8);
			m_s[7] = MemUtil.BytesToUInt32(pbKey32, 12);
			m_s[8] = MemUtil.BytesToUInt32(pbKey32, 16);
			m_s[9] = MemUtil.BytesToUInt32(pbKey32, 20);
			m_s[10] = MemUtil.BytesToUInt32(pbKey32, 24);
			m_s[11] = MemUtil.BytesToUInt32(pbKey32, 28);
			m_s[0] = g_sigma[0];
			m_s[1] = g_sigma[1];
			m_s[2] = g_sigma[2];
			m_s[3] = g_sigma[3];

			// IV setup
			m_s[12] = 0; // Counter
			m_s[13] = MemUtil.BytesToUInt32(pbIV12, 0);
			m_s[14] = MemUtil.BytesToUInt32(pbIV12, 4);
			m_s[15] = MemUtil.BytesToUInt32(pbIV12, 8);
		}

		protected override void Dispose(bool bDisposing)
		{
			if(bDisposing)
			{
				MemUtil.ZeroArray<uint>(m_s);
				MemUtil.ZeroArray<uint>(m_x);
			}

			base.Dispose(bDisposing);
		}

		protected override void NextBlock(byte[] pBlock)
		{
			if(pBlock == null) throw new ArgumentNullException("pBlock");
			if(pBlock.Length != 64) throw new ArgumentOutOfRangeException("pBlock");

			// x is a local alias for the working buffer; with this,
			// the compiler/runtime might remove some checks
			uint[] x = m_x;
			if(x == null) throw new InvalidOperationException();
			if(x.Length < 16) throw new InvalidOperationException();

			uint[] s = m_s;
			if(s == null) throw new InvalidOperationException();
			if(s.Length < 16) throw new InvalidOperationException();

			Array.Copy(s, x, 16);

			unchecked
			{
				// 10 * 8 quarter rounds = 20 rounds
				for(int i = 0; i < 10; ++i)
				{
					// Column quarter rounds
					x[ 0] += x[ 4];
					x[12] = MemUtil.RotateLeft32(x[12] ^ x[ 0], 16);
					x[ 8] += x[12];
					x[ 4] = MemUtil.RotateLeft32(x[ 4] ^ x[ 8], 12);
					x[ 0] += x[ 4];
					x[12] = MemUtil.RotateLeft32(x[12] ^ x[ 0], 8);
					x[ 8] += x[12];
					x[ 4] = MemUtil.RotateLeft32(x[ 4] ^ x[ 8], 7);

					x[ 1] += x[ 5];
					x[13] = MemUtil.RotateLeft32(x[13] ^ x[ 1], 16);
					x[ 9] += x[13];
					x[ 5] = MemUtil.RotateLeft32(x[ 5] ^ x[ 9], 12);
					x[ 1] += x[ 5];
					x[13] = MemUtil.RotateLeft32(x[13] ^ x[ 1], 8);
					x[ 9] += x[13];
					x[ 5] = MemUtil.RotateLeft32(x[ 5] ^ x[ 9], 7);

					x[ 2] += x[ 6];
					x[14] = MemUtil.RotateLeft32(x[14] ^ x[ 2], 16);
					x[10] += x[14];
					x[ 6] = MemUtil.RotateLeft32(x[ 6] ^ x[10], 12);
					x[ 2] += x[ 6];
					x[14] = MemUtil.RotateLeft32(x[14] ^ x[ 2], 8);
					x[10] += x[14];
					x[ 6] = MemUtil.RotateLeft32(x[ 6] ^ x[10], 7);

					x[ 3] += x[ 7];
					x[15] = MemUtil.RotateLeft32(x[15] ^ x[ 3], 16);
					x[11] += x[15];
					x[ 7] = MemUtil.RotateLeft32(x[ 7] ^ x[11], 12);
					x[ 3] += x[ 7];
					x[15] = MemUtil.RotateLeft32(x[15] ^ x[ 3], 8);
					x[11] += x[15];
					x[ 7] = MemUtil.RotateLeft32(x[ 7] ^ x[11], 7);

					// Diagonal quarter rounds
					x[ 0] += x[ 5];
					x[15] = MemUtil.RotateLeft32(x[15] ^ x[ 0], 16);
					x[10] += x[15];
					x[ 5] = MemUtil.RotateLeft32(x[ 5] ^ x[10], 12);
					x[ 0] += x[ 5];
					x[15] = MemUtil.RotateLeft32(x[15] ^ x[ 0],  8);
					x[10] += x[15];
					x[ 5] = MemUtil.RotateLeft32(x[ 5] ^ x[10],  7);

					x[ 1] += x[ 6];
					x[12] = MemUtil.RotateLeft32(x[12] ^ x[ 1], 16);
					x[11] += x[12];
					x[ 6] = MemUtil.RotateLeft32(x[ 6] ^ x[11], 12);
					x[ 1] += x[ 6];
					x[12] = MemUtil.RotateLeft32(x[12] ^ x[ 1],  8);
					x[11] += x[12];
					x[ 6] = MemUtil.RotateLeft32(x[ 6] ^ x[11],  7);

					x[ 2] += x[ 7];
					x[13] = MemUtil.RotateLeft32(x[13] ^ x[ 2], 16);
					x[ 8] += x[13];
					x[ 7] = MemUtil.RotateLeft32(x[ 7] ^ x[ 8], 12);
					x[ 2] += x[ 7];
					x[13] = MemUtil.RotateLeft32(x[13] ^ x[ 2],  8);
					x[ 8] += x[13];
					x[ 7] = MemUtil.RotateLeft32(x[ 7] ^ x[ 8],  7);

					x[ 3] += x[ 4];
					x[14] = MemUtil.RotateLeft32(x[14] ^ x[ 3], 16);
					x[ 9] += x[14];
					x[ 4] = MemUtil.RotateLeft32(x[ 4] ^ x[ 9], 12);
					x[ 3] += x[ 4];
					x[14] = MemUtil.RotateLeft32(x[14] ^ x[ 3],  8);
					x[ 9] += x[14];
					x[ 4] = MemUtil.RotateLeft32(x[ 4] ^ x[ 9],  7);
				}

				for(int i = 0; i < 16; ++i) x[i] += s[i];

				for(int i = 0; i < 16; ++i)
				{
					int i4 = i << 2;
					uint xi = x[i];

					pBlock[i4] = (byte)xi;
					pBlock[i4 + 1] = (byte)(xi >> 8);
					pBlock[i4 + 2] = (byte)(xi >> 16);
					pBlock[i4 + 3] = (byte)(xi >> 24);
				}

				++s[12];
				if(s[12] == 0)
				{
					if(!m_bLargeCounter)
						throw new InvalidOperationException(
							KLRes.EncDataTooLarge.Replace(@"{PARAM}", StrNameRfc));
					++s[13]; // Increment high half of large counter
				}
			}
		}

		public long Seek(long lOffset, SeekOrigin so)
		{
			if(so != SeekOrigin.Begin) throw new NotSupportedException();

			if((lOffset < 0) || ((lOffset & 63) != 0) ||
				((lOffset >> 6) > (long)uint.MaxValue))
				throw new ArgumentOutOfRangeException("lOffset");

			m_s[12] = (uint)(lOffset >> 6);
			InvalidateBlock();

			return lOffset;
		}
	}
}
