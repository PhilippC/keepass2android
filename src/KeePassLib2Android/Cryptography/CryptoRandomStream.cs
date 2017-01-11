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
using System.Diagnostics;

#if !KeePassUAP
using System.Security.Cryptography;
#endif

using KeePassLib.Cryptography.Cipher;
using KeePassLib.Utility;

namespace KeePassLib.Cryptography
{
	/// <summary>
	/// Algorithms supported by <c>CryptoRandomStream</c>.
	/// </summary>
	public enum CrsAlgorithm
	{
		/// <summary>
		/// Not supported.
		/// </summary>
		Null = 0,

		/// <summary>
		/// A variant of the ARCFour algorithm (RC4 incompatible).
		/// Insecure; for backward compatibility only.
		/// </summary>
		ArcFourVariant = 1,

		/// <summary>
		/// Salsa20 stream cipher algorithm.
		/// </summary>
		Salsa20 = 2,

		/// <summary>
		/// ChaCha20 stream cipher algorithm.
		/// </summary>
		ChaCha20 = 3,

		Count = 4
	}

	/// <summary>
	/// A random stream class. The class is initialized using random
	/// bytes provided by the caller. The produced stream has random
	/// properties, but for the same seed always the same stream
	/// is produced, i.e. this class can be used as stream cipher.
	/// </summary>
	public sealed class CryptoRandomStream : IDisposable
	{
		private readonly CrsAlgorithm m_crsAlgorithm;

		private byte[] m_pbState = null;
		private byte m_i = 0;
		private byte m_j = 0;

		private Salsa20Cipher m_salsa20 = null;
		private ChaCha20Cipher m_chacha20 = null;

		/// <summary>
		/// Construct a new cryptographically secure random stream object.
		/// </summary>
		/// <param name="a">Algorithm to use.</param>
		/// <param name="pbKey">Initialization key. Must not be <c>null</c> and
		/// must contain at least 1 byte.</param>
		public CryptoRandomStream(CrsAlgorithm a, byte[] pbKey)
		{
			if(pbKey == null) { Debug.Assert(false); throw new ArgumentNullException("pbKey"); }

			int cbKey = pbKey.Length;
			if(cbKey <= 0)
			{
				Debug.Assert(false); // Need at least one byte
				throw new ArgumentOutOfRangeException("pbKey");
			}

			m_crsAlgorithm = a;

			if(a == CrsAlgorithm.ChaCha20)
			{
				byte[] pbKey32 = new byte[32];
				byte[] pbIV12 = new byte[12];

				using(SHA512Managed h = new SHA512Managed())
				{
					byte[] pbHash = h.ComputeHash(pbKey);
					Array.Copy(pbHash, pbKey32, 32);
					Array.Copy(pbHash, 32, pbIV12, 0, 12);
					MemUtil.ZeroByteArray(pbHash);
				}

				m_chacha20 = new ChaCha20Cipher(pbKey32, pbIV12, true);
			}
			else if(a == CrsAlgorithm.Salsa20)
			{
				byte[] pbKey32 = CryptoUtil.HashSha256(pbKey);
				byte[] pbIV8 = new byte[8] { 0xE8, 0x30, 0x09, 0x4B,
					0x97, 0x20, 0x5D, 0x2A }; // Unique constant

				m_salsa20 = new Salsa20Cipher(pbKey32, pbIV8);
			}
			else if(a == CrsAlgorithm.ArcFourVariant)
			{
				// Fill the state linearly
				m_pbState = new byte[256];
				for(int w = 0; w < 256; ++w) m_pbState[w] = (byte)w;

				unchecked
				{
					byte j = 0, t;
					int inxKey = 0;
					for(int w = 0; w < 256; ++w) // Key setup
					{
						j += (byte)(m_pbState[w] + pbKey[inxKey]);

						t = m_pbState[0]; // Swap entries
						m_pbState[0] = m_pbState[j];
						m_pbState[j] = t;

						++inxKey;
						if(inxKey >= cbKey) inxKey = 0;
					}
				}

				GetRandomBytes(512); // Increases security, see cryptanalysis
			}
			else // Unknown algorithm
			{
				Debug.Assert(false);
				throw new ArgumentOutOfRangeException("a");
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if(disposing)
			{
				if(m_crsAlgorithm == CrsAlgorithm.ChaCha20)
					m_chacha20.Dispose();
				else if(m_crsAlgorithm == CrsAlgorithm.Salsa20)
					m_salsa20.Dispose();
				else if(m_crsAlgorithm == CrsAlgorithm.ArcFourVariant)
				{
					MemUtil.ZeroByteArray(m_pbState);
					m_i = 0;
					m_j = 0;
				}
				else { Debug.Assert(false); }
			}
		}

		/// <summary>
		/// Get <paramref name="uRequestedCount" /> random bytes.
		/// </summary>
		/// <param name="uRequestedCount">Number of random bytes to retrieve.</param>
		/// <returns>Returns <paramref name="uRequestedCount" /> random bytes.</returns>
		public byte[] GetRandomBytes(uint uRequestedCount)
		{
			if(uRequestedCount == 0) return MemUtil.EmptyByteArray;

			if(uRequestedCount > (uint)int.MaxValue)
				throw new ArgumentOutOfRangeException("uRequestedCount");
			int cb = (int)uRequestedCount;

			byte[] pbRet = new byte[cb];

			if(m_crsAlgorithm == CrsAlgorithm.ChaCha20)
				m_chacha20.Encrypt(pbRet, 0, cb);
			else if(m_crsAlgorithm == CrsAlgorithm.Salsa20)
				m_salsa20.Encrypt(pbRet, 0, cb);
			else if(m_crsAlgorithm == CrsAlgorithm.ArcFourVariant)
			{
				unchecked
				{
					for(int w = 0; w < cb; ++w)
					{
						++m_i;
						m_j += m_pbState[m_i];

						byte t = m_pbState[m_i]; // Swap entries
						m_pbState[m_i] = m_pbState[m_j];
						m_pbState[m_j] = t;

						t = (byte)(m_pbState[m_i] + m_pbState[m_j]);
						pbRet[w] = m_pbState[t];
					}
				}
			}
			else { Debug.Assert(false); }

			return pbRet;
		}

		public ulong GetRandomUInt64()
		{
			byte[] pb = GetRandomBytes(8);
			return MemUtil.BytesToUInt64(pb);
		}

#if CRSBENCHMARK
		public static string Benchmark()
		{
			int nRounds = 2000000;
			
			string str = "ArcFour small: " + BenchTime(CrsAlgorithm.ArcFourVariant,
				nRounds, 16).ToString() + "\r\n";
			str += "ArcFour big: " + BenchTime(CrsAlgorithm.ArcFourVariant,
				32, 2 * 1024 * 1024).ToString() + "\r\n";
			str += "Salsa20 small: " + BenchTime(CrsAlgorithm.Salsa20,
				nRounds, 16).ToString() + "\r\n";
			str += "Salsa20 big: " + BenchTime(CrsAlgorithm.Salsa20,
				32, 2 * 1024 * 1024).ToString();
			return str;
		}

		private static int BenchTime(CrsAlgorithm cra, int nRounds, int nDataSize)
		{
			byte[] pbKey = new byte[4] { 0x00, 0x01, 0x02, 0x03 };

			int nStart = Environment.TickCount;
			for(int i = 0; i < nRounds; ++i)
			{
				using(CryptoRandomStream c = new CryptoRandomStream(cra, pbKey))
				{
					c.GetRandomBytes((uint)nDataSize);
				}
			}
			int nEnd = Environment.TickCount;

			return (nEnd - nStart);
		}
#endif
	}
}
