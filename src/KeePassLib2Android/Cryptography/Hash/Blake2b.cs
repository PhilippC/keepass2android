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

// This implementation is based on the official reference C
// implementation by Samuel Neves (CC0 1.0 Universal).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

#if !KeePassUAP
using System.Security.Cryptography;
#endif

using KeePassLib.Utility;

namespace KeePassLib.Cryptography.Hash
{
	public sealed class Blake2b : HashAlgorithm
	{
		private const int NbRounds = 12;
		private const int NbBlockBytes = 128;
		private const int NbMaxOutBytes = 64;

		private static readonly ulong[] g_vIV = new ulong[8] {
			0x6A09E667F3BCC908UL, 0xBB67AE8584CAA73BUL,
			0x3C6EF372FE94F82BUL, 0xA54FF53A5F1D36F1UL,
			0x510E527FADE682D1UL, 0x9B05688C2B3E6C1FUL,
			0x1F83D9ABFB41BD6BUL, 0x5BE0CD19137E2179UL
		};

		private static readonly int[] g_vSigma = new int[NbRounds * 16] {
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
			14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3,
			11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4,
			7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8,
			9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13,
			2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9,
			12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11,
			13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10,
			6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5,
			10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0,
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
			14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3
		};

		private readonly int m_cbHashLength;

		private ulong[] m_h = new ulong[8];
		private ulong[] m_t = new ulong[2];
		private ulong[] m_f = new ulong[2];
		private byte[] m_buf = new byte[NbBlockBytes];
		private int m_cbBuf = 0;

		private ulong[] m_m = new ulong[16];
		private ulong[] m_v = new ulong[16];

		public Blake2b()
		{
			m_cbHashLength = NbMaxOutBytes;
			this.HashSizeValue = NbMaxOutBytes * 8; // Bits

			Initialize();
		}

		public Blake2b(int cbHashLength)
		{
			if((cbHashLength < 0) || (cbHashLength > NbMaxOutBytes))
				throw new ArgumentOutOfRangeException("cbHashLength");

			m_cbHashLength = cbHashLength;
			this.HashSizeValue = cbHashLength * 8; // Bits

			Initialize();
		}

		public override void Initialize()
		{
			Debug.Assert(m_h.Length == g_vIV.Length);
			Array.Copy(g_vIV, m_h, m_h.Length);

			// Fan-out = 1, depth = 1
			m_h[0] ^= 0x0000000001010000UL ^ (ulong)m_cbHashLength;

			Array.Clear(m_t, 0, m_t.Length);
			Array.Clear(m_f, 0, m_f.Length);
			Array.Clear(m_buf, 0, m_buf.Length);
			m_cbBuf = 0;

			Array.Clear(m_m, 0, m_m.Length);
			Array.Clear(m_v, 0, m_v.Length);
		}

		private static void G(ulong[] v, ulong[] m, int r16, int i,
			int a, int b, int c, int d)
		{
			int p = r16 + i;

			v[a] += v[b] + m[g_vSigma[p]];
			v[d] = MemUtil.RotateRight64(v[d] ^ v[a], 32);
			v[c] += v[d];
			v[b] = MemUtil.RotateRight64(v[b] ^ v[c], 24);
			v[a] += v[b] + m[g_vSigma[p + 1]];
			v[d] = MemUtil.RotateRight64(v[d] ^ v[a], 16);
			v[c] += v[d];
			v[b] = MemUtil.RotateRight64(v[b] ^ v[c], 63);
		}

		private void Compress(byte[] pb, int iOffset)
		{
			ulong[] v = m_v;
			ulong[] m = m_m;
			ulong[] h = m_h;

			for(int i = 0; i < 16; ++i)
				m[i] = MemUtil.BytesToUInt64(pb, iOffset + (i << 3));

			Array.Copy(h, v, 8);
			v[8] = g_vIV[0];
			v[9] = g_vIV[1];
			v[10] = g_vIV[2];
			v[11] = g_vIV[3];
			v[12] = g_vIV[4] ^ m_t[0];
			v[13] = g_vIV[5] ^ m_t[1];
			v[14] = g_vIV[6] ^ m_f[0];
			v[15] = g_vIV[7] ^ m_f[1];

			for(int r = 0; r < NbRounds; ++r)
			{
				int r16 = r << 4;

				G(v, m, r16, 0, 0, 4, 8, 12);
				G(v, m, r16, 2, 1, 5, 9, 13);
				G(v, m, r16, 4, 2, 6, 10, 14);
				G(v, m, r16, 6, 3, 7, 11, 15);
				G(v, m, r16, 8, 0, 5, 10, 15);
				G(v, m, r16, 10, 1, 6, 11, 12);
				G(v, m, r16, 12, 2, 7, 8, 13);
				G(v, m, r16, 14, 3, 4, 9, 14);
			}

			for(int i = 0; i < 8; ++i)
				h[i] ^= v[i] ^ v[i + 8];
		}

		private void IncrementCounter(ulong cb)
		{
			m_t[0] += cb;
			if(m_t[0] < cb) ++m_t[1];
		}

		protected override void HashCore(byte[] array, int ibStart, int cbSize)
		{
			Debug.Assert(m_f[0] == 0);

			if((m_cbBuf + cbSize) > NbBlockBytes) // Not '>=' (buffer must not be empty)
			{
				int cbFill = NbBlockBytes - m_cbBuf;
				if(cbFill > 0) Array.Copy(array, ibStart, m_buf, m_cbBuf, cbFill);

				IncrementCounter((ulong)NbBlockBytes);
				Compress(m_buf, 0);

				m_cbBuf = 0;
				cbSize -= cbFill;
				ibStart += cbFill;

				while(cbSize > NbBlockBytes) // Not '>=' (buffer must not be empty)
				{
					IncrementCounter((ulong)NbBlockBytes);
					Compress(array, ibStart);

					cbSize -= NbBlockBytes;
					ibStart += NbBlockBytes;
				}
			}

			if(cbSize > 0)
			{
				Debug.Assert((m_cbBuf + cbSize) <= NbBlockBytes);

				Array.Copy(array, ibStart, m_buf, m_cbBuf, cbSize);
				m_cbBuf += cbSize;
			}
		}

		protected override byte[] HashFinal()
		{
			if(m_f[0] != 0) { Debug.Assert(false); throw new InvalidOperationException(); }
			Debug.Assert(((m_t[1] == 0) && (m_t[0] == 0)) ||
				(m_cbBuf > 0)); // Buffer must not be empty for last block processing

			m_f[0] = ulong.MaxValue; // Indicate last block

			int cbFill = NbBlockBytes - m_cbBuf;
			if(cbFill > 0) Array.Clear(m_buf, m_cbBuf, cbFill);

			IncrementCounter((ulong)m_cbBuf);
			Compress(m_buf, 0);

			byte[] pbHash = new byte[NbMaxOutBytes];
			for(int i = 0; i < m_h.Length; ++i)
				MemUtil.UInt64ToBytesEx(m_h[i], pbHash, i << 3);

			if(m_cbHashLength == NbMaxOutBytes) return pbHash;
			Debug.Assert(m_cbHashLength < NbMaxOutBytes);

			byte[] pbShort = new byte[m_cbHashLength];
			if(m_cbHashLength > 0)
				Array.Copy(pbHash, pbShort, m_cbHashLength);
			MemUtil.ZeroByteArray(pbHash);
			return pbShort;
		}
	}
}
