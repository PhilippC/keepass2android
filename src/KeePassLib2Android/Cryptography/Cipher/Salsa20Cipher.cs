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

// Implementation of the Salsa20 cipher, based on the eSTREAM
// submission by D. J. Bernstein.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using KeePassLib.Utility;

namespace KeePassLib.Cryptography.Cipher
{
	public sealed class Salsa20Cipher : CtrBlockCipher
	{
		private uint[] m_s = new uint[16]; // State
		private uint[] m_x = new uint[16]; // Working buffer

		private static readonly uint[] g_sigma = new uint[4] {
			0x61707865, 0x3320646E, 0x79622D32, 0x6B206574
		};

		public override int BlockSize
		{
			get { return 64; }
		}

		public Salsa20Cipher(byte[] pbKey32, byte[] pbIV8) : base()
		{
			if(pbKey32 == null) throw new ArgumentNullException("pbKey32");
			if(pbKey32.Length != 32) throw new ArgumentOutOfRangeException("pbKey32");
			if(pbIV8 == null) throw new ArgumentNullException("pbIV8");
			if(pbIV8.Length != 8) throw new ArgumentOutOfRangeException("pbIV8");

			// Key setup
			m_s[1] = MemUtil.BytesToUInt32(pbKey32, 0);
			m_s[2] = MemUtil.BytesToUInt32(pbKey32, 4);
			m_s[3] = MemUtil.BytesToUInt32(pbKey32, 8);
			m_s[4] = MemUtil.BytesToUInt32(pbKey32, 12);
			m_s[11] = MemUtil.BytesToUInt32(pbKey32, 16);
			m_s[12] = MemUtil.BytesToUInt32(pbKey32, 20);
			m_s[13] = MemUtil.BytesToUInt32(pbKey32, 24);
			m_s[14] = MemUtil.BytesToUInt32(pbKey32, 28);
			m_s[0] = g_sigma[0];
			m_s[5] = g_sigma[1];
			m_s[10] = g_sigma[2];
			m_s[15] = g_sigma[3];

			// IV setup
			m_s[6] = MemUtil.BytesToUInt32(pbIV8, 0);
			m_s[7] = MemUtil.BytesToUInt32(pbIV8, 4);
			m_s[8] = 0; // Counter, low
			m_s[9] = 0; // Counter, high
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
					x[ 4] ^= MemUtil.RotateLeft32(x[ 0] + x[12],  7);
					x[ 8] ^= MemUtil.RotateLeft32(x[ 4] + x[ 0],  9);
					x[12] ^= MemUtil.RotateLeft32(x[ 8] + x[ 4], 13);
					x[ 0] ^= MemUtil.RotateLeft32(x[12] + x[ 8], 18);

					x[ 9] ^= MemUtil.RotateLeft32(x[ 5] + x[ 1],  7);
					x[13] ^= MemUtil.RotateLeft32(x[ 9] + x[ 5],  9);
					x[ 1] ^= MemUtil.RotateLeft32(x[13] + x[ 9], 13);
					x[ 5] ^= MemUtil.RotateLeft32(x[ 1] + x[13], 18);

					x[14] ^= MemUtil.RotateLeft32(x[10] + x[ 6],  7);
					x[ 2] ^= MemUtil.RotateLeft32(x[14] + x[10],  9);
					x[ 6] ^= MemUtil.RotateLeft32(x[ 2] + x[14], 13);
					x[10] ^= MemUtil.RotateLeft32(x[ 6] + x[ 2], 18);

					x[ 3] ^= MemUtil.RotateLeft32(x[15] + x[11],  7);
					x[ 7] ^= MemUtil.RotateLeft32(x[ 3] + x[15],  9);
					x[11] ^= MemUtil.RotateLeft32(x[ 7] + x[ 3], 13);
					x[15] ^= MemUtil.RotateLeft32(x[11] + x[ 7], 18);

					x[ 1] ^= MemUtil.RotateLeft32(x[ 0] + x[ 3],  7);
					x[ 2] ^= MemUtil.RotateLeft32(x[ 1] + x[ 0],  9);
					x[ 3] ^= MemUtil.RotateLeft32(x[ 2] + x[ 1], 13);
					x[ 0] ^= MemUtil.RotateLeft32(x[ 3] + x[ 2], 18);

					x[ 6] ^= MemUtil.RotateLeft32(x[ 5] + x[ 4],  7);
					x[ 7] ^= MemUtil.RotateLeft32(x[ 6] + x[ 5],  9);
					x[ 4] ^= MemUtil.RotateLeft32(x[ 7] + x[ 6], 13);
					x[ 5] ^= MemUtil.RotateLeft32(x[ 4] + x[ 7], 18);

					x[11] ^= MemUtil.RotateLeft32(x[10] + x[ 9],  7);
					x[ 8] ^= MemUtil.RotateLeft32(x[11] + x[10],  9);
					x[ 9] ^= MemUtil.RotateLeft32(x[ 8] + x[11], 13);
					x[10] ^= MemUtil.RotateLeft32(x[ 9] + x[ 8], 18);

					x[12] ^= MemUtil.RotateLeft32(x[15] + x[14],  7);
					x[13] ^= MemUtil.RotateLeft32(x[12] + x[15],  9);
					x[14] ^= MemUtil.RotateLeft32(x[13] + x[12], 13);
					x[15] ^= MemUtil.RotateLeft32(x[14] + x[13], 18);
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

				++s[8];
				if(s[8] == 0) ++s[9];
			}
		}
	}
}
