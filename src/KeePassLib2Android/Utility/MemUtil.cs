/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2016 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Runtime.CompilerServices;
using System.Text;

#if KeePassLibSD
using KeePassLibSD;
#else
using System.IO.Compression;
#endif

namespace KeePassLib.Utility
{
	/// <summary>
	/// Contains static buffer manipulation and string conversion routines.
	/// </summary>
	public static class MemUtil
	{
		private static readonly uint[] m_vSBox = new uint[256] {
			0xCD2FACB3, 0xE78A7F5C, 0x6F0803FC, 0xBCF6E230,
			0x3A321712, 0x06403DB1, 0xD2F84B95, 0xDF22A6E4,
			0x07CE9E5B, 0x31788A0C, 0xF683F6F4, 0xEA061F49,
			0xFA5C2ACA, 0x4B9E494E, 0xB0AB25BA, 0x767731FC,
			0x261893A7, 0x2B09F2CE, 0x046261E4, 0x41367B4B,
			0x18A7F225, 0x8F923C0E, 0x5EF3A325, 0x28D0435E,
			0x84C22919, 0xED66873C, 0x8CEDE444, 0x7FC47C24,
			0xFCFC6BA3, 0x676F928D, 0xB4147187, 0xD8FB126E,
			0x7D798D17, 0xFF82E424, 0x1712FA5B, 0xABB09DD5,
			0x8156BA63, 0x84E4D969, 0xC937FB9A, 0x2F1E5BFC,
			0x178ECA11, 0x0E71CD5F, 0x52AAC6F4, 0x71EEFC8F,
			0x7090D749, 0x21CACA31, 0x92996378, 0x0939A8A8,
			0xE9EE1934, 0xD2718616, 0xF2500543, 0xB911873C,
			0xD3CB3EEC, 0x2BA0DBEB, 0xB42D0A27, 0xECE67C0F,
			0x302925F0, 0x6114F839, 0xD39E6307, 0xE28970D6,
			0xEB982F99, 0x941B4CDF, 0xC540E550, 0x8124FC45,
			0x98B025C7, 0xE2BF90EA, 0x4F57C976, 0xCF546FE4,
			0x59566DC8, 0xE3F4360D, 0xF5F9D231, 0xD6180B22,
			0xB54E088A, 0xB5DFE6A6, 0x3637A36F, 0x056E9284,
			0xAFF8FBC5, 0x19E01648, 0x8611F043, 0xDAE44337,
			0xF61B6A1C, 0x257ACD9E, 0xDD35F507, 0xEF05CAFA,
			0x05EB4A83, 0xFC25CA92, 0x0A4728E6, 0x9CF150EF,
			0xAEEF67DE, 0xA9472337, 0x57C81EFE, 0x3E5E009F,
			0x02CB03BB, 0x2BA85674, 0xF21DC251, 0x78C34A34,
			0xABB1F5BF, 0xB95A2FBD, 0x1FB47777, 0x9A96E8AC,
			0x5D2D2838, 0x55AAC92A, 0x99EE324E, 0x10F6214B,
			0x58ABDFB1, 0x2008794D, 0xBEC880F0, 0xE75E5341,
			0x88015C34, 0x352D8FBF, 0x622B7F6C, 0xF5C59EA2,
			0x1F759D8E, 0xADE56159, 0xCC7B4C25, 0x5B8BC48C,
			0xB6BD15AF, 0x3C5B5110, 0xE74A7C3D, 0xEE613161,
			0x156A1C67, 0x72C06817, 0xEA0A6F69, 0x4CECF993,
			0xCA9D554C, 0x8E20361F, 0x42D396B9, 0x595DE578,
			0x749D7955, 0xFD1BA5FD, 0x81FC160E, 0xDB97E28C,
			0x7CF148F7, 0x0B0B3CF5, 0x534DE605, 0x46421066,
			0xD4B68DD1, 0x9E479CE6, 0xAE667A9D, 0xBC082082,
			0xB06DD6EF, 0x20F0F23F, 0xB99E1551, 0xF47A2E3A,
			0x71DA50C6, 0x67B65779, 0x2A8CB376, 0x1EA71EEE,
			0x29ABCD50, 0xB6EB0C6B, 0x23C10511, 0x6F3F2144,
			0x6AF23012, 0xF696BD9E, 0xB94099D8, 0xAD5A9C81,
			0x7A0794FA, 0x7EDF59D6, 0x1E72E574, 0x8561913C,
			0x4E4D568F, 0xEECB9928, 0x9C124D2E, 0x0848B82C,
			0xF1CA395F, 0x9DAF43DC, 0xF77EC323, 0x394E9B59,
			0x7E200946, 0x8B811D68, 0x16DA3305, 0xAB8DE2C3,
			0xE6C53B64, 0x98C2D321, 0x88A97D81, 0xA7106419,
			0x8E52F7BF, 0x8ED262AF, 0x7CCA974E, 0xF0933241,
			0x040DD437, 0xE143B3D4, 0x3019F56F, 0xB741521D,
			0xF1745362, 0x4C435F9F, 0xB4214D0D, 0x0B0C348B,
			0x5051D189, 0x4C30447E, 0x7393D722, 0x95CEDD0B,
			0xDD994E80, 0xC3D22ED9, 0x739CD900, 0x131EB9C4,
			0xEF1062B2, 0x4F0DE436, 0x52920073, 0x9A7F3D80,
			0x896E7B1B, 0x2C8BBE5A, 0xBD304F8A, 0xA993E22C,
			0x134C41A0, 0xFA989E00, 0x39CE9726, 0xFB89FCCF,
			0xE8FBAC97, 0xD4063FFC, 0x935A2B5A, 0x44C8EE83,
			0xCB2BC7B6, 0x02989E92, 0x75478BEA, 0x144378D0,
			0xD853C087, 0x8897A34E, 0xDD23629D, 0xBDE2A2A2,
			0x581D8ECC, 0x5DA8AEE8, 0xFF8AAFD0, 0xBA2BCF6E,
			0x4BD98DAC, 0xF2EDB9E4, 0xFA2DC868, 0x47E84661,
			0xECEB1C7D, 0x41705CA4, 0x5982E4D4, 0xEB5204A1,
			0xD196CAFB, 0x6414804D, 0x3ABD4B46, 0x8B494C26,
			0xB432D52B, 0x39C5356B, 0x6EC80BF7, 0x71BE5483,
			0xCEC4A509, 0xE9411D61, 0x52F341E5, 0xD2E6197B,
			0x4F02826C, 0xA9E48838, 0xD1F8F247, 0xE4957FB3,
			0x586CCA99, 0x9A8B6A5B, 0x4998FBEA, 0xF762BE4C,
			0x90DFE33C, 0x9731511E, 0x88C6A82F, 0xDD65A4D4
		};

		/// <summary>
		/// Convert a hexadecimal string to a byte array. The input string must be
		/// even (i.e. its length is a multiple of 2).
		/// </summary>
		/// <param name="strHex">String containing hexadecimal characters.</param>
		/// <returns>Returns a byte array. Returns <c>null</c> if the string parameter
		/// was <c>null</c> or is an uneven string (i.e. if its length isn't a
		/// multiple of 2).</returns>
		/// <exception cref="System.ArgumentNullException">Thrown if <paramref name="strHex" />
		/// is <c>null</c>.</exception>
		public static byte[] HexStringToByteArray(string strHex)
		{
			if(strHex == null) { Debug.Assert(false); throw new ArgumentNullException("strHex"); }

			int nStrLen = strHex.Length;
			if((nStrLen & 1) != 0) { Debug.Assert(false); return null; }

			byte[] pb = new byte[nStrLen / 2];
			byte bt;
			char ch;

			for(int i = 0; i < nStrLen; i += 2)
			{
				ch = strHex[i];

				if((ch >= '0') && (ch <= '9'))
					bt = (byte)(ch - '0');
				else if((ch >= 'a') && (ch <= 'f'))
					bt = (byte)(ch - 'a' + 10);
				else if((ch >= 'A') && (ch <= 'F'))
					bt = (byte)(ch - 'A' + 10);
				else { Debug.Assert(false); bt = 0; }

				bt <<= 4;

				ch = strHex[i + 1];
				if((ch >= '0') && (ch <= '9'))
					bt += (byte)(ch - '0');
				else if((ch >= 'a') && (ch <= 'f'))
					bt += (byte)(ch - 'a' + 10);
				else if((ch >= 'A') && (ch <= 'F'))
					bt += (byte)(ch - 'A' + 10);
				else { Debug.Assert(false); }

				pb[i >> 1] = bt;
			}

			return pb;
		}

		/// <summary>
		/// Convert a byte array to a hexadecimal string.
		/// </summary>
		/// <param name="pbArray">Input byte array.</param>
		/// <returns>Returns the hexadecimal string representing the byte
		/// array. Returns <c>null</c>, if the input byte array was <c>null</c>. Returns
		/// an empty string, if the input byte array has length 0.</returns>
		public static string ByteArrayToHexString(byte[] pbArray)
		{
			if(pbArray == null) return null;

			int nLen = pbArray.Length;
			if(nLen == 0) return string.Empty;

			StringBuilder sb = new StringBuilder();

			byte bt, btHigh, btLow;
			for(int i = 0; i < nLen; ++i)
			{
				bt = pbArray[i];
				btHigh = bt; btHigh >>= 4;
				btLow = (byte)(bt & 0x0F);

				if(btHigh >= 10) sb.Append((char)('A' + btHigh - 10));
				else sb.Append((char)('0' + btHigh));

				if(btLow >= 10) sb.Append((char)('A' + btLow - 10));
				else sb.Append((char)('0' + btLow));
			}

			return sb.ToString();
		}

		/// <summary>
		/// Decode Base32 strings according to RFC 4648.
		/// </summary>
		public static byte[] ParseBase32(string str)
		{
			if((str == null) || ((str.Length % 8) != 0))
			{
				Debug.Assert(false);
				return null;
			}

			ulong uMaxBits = (ulong)str.Length * 5UL;
			List<byte> l = new List<byte>((int)(uMaxBits / 8UL) + 1);
			Debug.Assert(l.Count == 0);

			for(int i = 0; i < str.Length; i += 8)
			{
				ulong u = 0;
				int nBits = 0;

				for(int j = 0; j < 8; ++j)
				{
					char ch = str[i + j];
					if(ch == '=') break;

					ulong uValue;
					if((ch >= 'A') && (ch <= 'Z'))
						uValue = (ulong)(ch - 'A');
					else if((ch >= 'a') && (ch <= 'z'))
						uValue = (ulong)(ch - 'a');
					else if((ch >= '2') && (ch <= '7'))
						uValue = (ulong)(ch - '2') + 26UL;
					else { Debug.Assert(false); return null; }

					u <<= 5;
					u += uValue;
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

		/// <summary>
		/// Set all bytes in a byte array to zero.
		/// </summary>
		/// <param name="pbArray">Input array. All bytes of this array
		/// will be set to zero.</param>
#if KeePassLibSD
		[MethodImpl(MethodImplOptions.NoInlining)]
#else
		[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
#endif
		public static void ZeroByteArray(byte[] pbArray)
		{
			Debug.Assert(pbArray != null);
			if(pbArray == null) throw new ArgumentNullException("pbArray");

			Array.Clear(pbArray, 0, pbArray.Length);
		}

		/// <summary>
		/// Convert 2 bytes to a 16-bit unsigned integer (little-endian).
		/// </summary>
		public static ushort BytesToUInt16(byte[] pb)
		{
			Debug.Assert((pb != null) && (pb.Length == 2));
			if(pb == null) throw new ArgumentNullException("pb");
			if(pb.Length != 2) throw new ArgumentException();

			return (ushort)((ushort)pb[0] | ((ushort)pb[1] << 8));
		}

		/// <summary>
		/// Convert 4 bytes to a 32-bit unsigned integer (little-endian).
		/// </summary>
		public static uint BytesToUInt32(byte[] pb)
		{
			Debug.Assert((pb != null) && (pb.Length == 4));
			if(pb == null) throw new ArgumentNullException("pb");
			if(pb.Length != 4) throw new ArgumentException();

			return ((uint)pb[0] | ((uint)pb[1] << 8) | ((uint)pb[2] << 16) |
				((uint)pb[3] << 24));
		}

		/// <summary>
		/// Convert 8 bytes to a 64-bit unsigned integer (little-endian).
		/// </summary>
		public static ulong BytesToUInt64(byte[] pb)
		{
			Debug.Assert((pb != null) && (pb.Length == 8));
			if(pb == null) throw new ArgumentNullException("pb");
			if(pb.Length != 8) throw new ArgumentException();

			return ((ulong)pb[0] | ((ulong)pb[1] << 8) | ((ulong)pb[2] << 16) |
				((ulong)pb[3] << 24) | ((ulong)pb[4] << 32) | ((ulong)pb[5] << 40) |
				((ulong)pb[6] << 48) | ((ulong)pb[7] << 56));
		}

		/// <summary>
		/// Convert a 16-bit unsigned integer to 2 bytes (little-endian).
		/// </summary>
		public static byte[] UInt16ToBytes(ushort uValue)
		{
			byte[] pb = new byte[2];

			unchecked
			{
				pb[0] = (byte)uValue;
				pb[1] = (byte)(uValue >> 8);
			}

			return pb;
		}

		/// <summary>
		/// Convert a 32-bit unsigned integer to 4 bytes (little-endian).
		/// </summary>
		public static byte[] UInt32ToBytes(uint uValue)
		{
			byte[] pb = new byte[4];

			unchecked
			{
				pb[0] = (byte)uValue;
				pb[1] = (byte)(uValue >> 8);
				pb[2] = (byte)(uValue >> 16);
				pb[3] = (byte)(uValue >> 24);
			}

			return pb;
		}

		/// <summary>
		/// Convert a 64-bit unsigned integer to 8 bytes (little-endian).
		/// </summary>
		public static byte[] UInt64ToBytes(ulong uValue)
		{
			byte[] pb = new byte[8];

			unchecked
			{
				pb[0] = (byte)uValue;
				pb[1] = (byte)(uValue >> 8);
				pb[2] = (byte)(uValue >> 16);
				pb[3] = (byte)(uValue >> 24);
				pb[4] = (byte)(uValue >> 32);
				pb[5] = (byte)(uValue >> 40);
				pb[6] = (byte)(uValue >> 48);
				pb[7] = (byte)(uValue >> 56);
			}

			return pb;
		}

		public static bool ArraysEqual(byte[] x, byte[] y)
		{
			// Return false if one of them is null (not comparable)!
			if((x == null) || (y == null)) { Debug.Assert(false); return false; }

			if(x.Length != y.Length) return false;

			for(int i = 0; i < x.Length; ++i)
			{
				if(x[i] != y[i]) return false;
			}

			return true;
		}

		public static void XorArray(byte[] pbSource, int nSourceOffset,
			byte[] pbBuffer, int nBufferOffset, int nLength)
		{
			if(pbSource == null) throw new ArgumentNullException("pbSource");
			if(nSourceOffset < 0) throw new ArgumentException();
			if(pbBuffer == null) throw new ArgumentNullException("pbBuffer");
			if(nBufferOffset < 0) throw new ArgumentException();
			if(nLength < 0) throw new ArgumentException();
			if((nSourceOffset + nLength) > pbSource.Length) throw new ArgumentException();
			if((nBufferOffset + nLength) > pbBuffer.Length) throw new ArgumentException();

			for(int i = 0; i < nLength; ++i)
				pbBuffer[nBufferOffset + i] ^= pbSource[nSourceOffset + i];
		}

		/// <summary>
		/// Fast hash that can be used e.g. for hash tables.
		/// The algorithm might change in the future; do not store
		/// the hashes for later use.
		/// </summary>
		public static uint Hash32(byte[] v, int iStart, int iLength)
		{
			uint u = 0x326F637B;

			if(v == null) { Debug.Assert(false); return u; }
			if(iStart < 0) { Debug.Assert(false); return u; }
			if(iLength < 0) { Debug.Assert(false); return u; }

			int m = iStart + iLength;
			if(m > v.Length) { Debug.Assert(false); return u; }

			for(int i = iStart; i < m; ++i)
			{
				u ^= m_vSBox[v[i]];
				u *= 3;
			}

			return u;
		}

		public static void CopyStream(Stream sSource, Stream sTarget)
		{
			Debug.Assert((sSource != null) && (sTarget != null));
			if(sSource == null) throw new ArgumentNullException("sSource");
			if(sTarget == null) throw new ArgumentNullException("sTarget");

			const int nBufSize = 4096;
			byte[] pbBuf = new byte[nBufSize];

			while(true)
			{
				int nRead = sSource.Read(pbBuf, 0, nBufSize);
				if(nRead == 0) break;

				sTarget.Write(pbBuf, 0, nRead);
			}

			// Do not close any of the streams
		}

		public static byte[] Read(Stream s, int nCount)
		{
			if(s == null) throw new ArgumentNullException("s");
			if(nCount < 0) throw new ArgumentOutOfRangeException("nCount");

			byte[] pb = new byte[nCount];
			int iOffset = 0;
			while(nCount > 0)
			{
				int iRead = s.Read(pb, iOffset, nCount);
				if(iRead == 0) break;

				iOffset += iRead;
				nCount -= iRead;
			}

			if(iOffset != pb.Length)
			{
				byte[] pbPart = new byte[iOffset];
				Array.Copy(pb, pbPart, iOffset);
				return pbPart;
			}

			return pb;
		}

		public static void Write(Stream s, byte[] pbData)
		{
			if(s == null) { Debug.Assert(false); return; }
			if(pbData == null) { Debug.Assert(false); return; }

			s.Write(pbData, 0, pbData.Length);
		}

		public static byte[] Compress(byte[] pbData)
		{
			if(pbData == null) throw new ArgumentNullException("pbData");
			if(pbData.Length == 0) return pbData;

			byte[] pbCompressed;
			using(MemoryStream msSource = new MemoryStream(pbData, false))
			{
				using(MemoryStream msCompressed = new MemoryStream())
				{
					using(GZipStream gz = new GZipStream(msCompressed,
						CompressionMode.Compress))
					{
						MemUtil.CopyStream(msSource, gz);
					}

					pbCompressed = msCompressed.ToArray();
				}
			}

			return pbCompressed;
		}

		public static byte[] Decompress(byte[] pbCompressed)
		{
			if(pbCompressed == null) throw new ArgumentNullException("pbCompressed");
			if(pbCompressed.Length == 0) return pbCompressed;

			byte[] pbData;
			using(MemoryStream msData = new MemoryStream())
			{
				using(MemoryStream msCompressed = new MemoryStream(pbCompressed, false))
				{
					using(GZipStream gz = new GZipStream(msCompressed,
						CompressionMode.Decompress))
					{
						MemUtil.CopyStream(gz, msData);
					}
				}

				pbData = msData.ToArray();
			}

			return pbData;
		}

		public static int IndexOf<T>(T[] vHaystack, T[] vNeedle)
			where T : IEquatable<T>
		{
			if(vHaystack == null) throw new ArgumentNullException("vHaystack");
			if(vNeedle == null) throw new ArgumentNullException("vNeedle");
			if(vNeedle.Length == 0) return 0;

			for(int i = 0; i <= (vHaystack.Length - vNeedle.Length); ++i)
			{
				bool bFound = true;
				for(int m = 0; m < vNeedle.Length; ++m)
				{
					if(!vHaystack[i + m].Equals(vNeedle[m]))
					{
						bFound = false;
						break;
					}
				}
				if(bFound) return i;
			}

			return -1;
		}

		public static T[] Mid<T>(T[] v, int iOffset, int iLength)
		{
			if(v == null) throw new ArgumentNullException("v");
			if(iOffset < 0) throw new ArgumentOutOfRangeException("iOffset");
			if(iLength < 0) throw new ArgumentOutOfRangeException("iLength");
			if((iOffset + iLength) > v.Length) throw new ArgumentException();

			T[] r = new T[iLength];
			Array.Copy(v, iOffset, r, 0, iLength);
			return r;
		}

		public static IEnumerable<T> Union<T>(IEnumerable<T> a, IEnumerable<T> b,
			IEqualityComparer<T> cmp)
		{
			if(a == null) throw new ArgumentNullException("a");
			if(b == null) throw new ArgumentNullException("b");

			Dictionary<T, bool> d = ((cmp != null) ?
				(new Dictionary<T, bool>(cmp)) : (new Dictionary<T, bool>()));

			foreach(T ta in a)
			{
				if(d.ContainsKey(ta)) continue; // Prevent duplicates

				d[ta] = true;
				yield return ta;
			}

			foreach(T tb in b)
			{
				if(d.ContainsKey(tb)) continue; // Prevent duplicates

				d[tb] = true;
				yield return tb;
			}

			yield break;
		}

		public static IEnumerable<T> Intersect<T>(IEnumerable<T> a, IEnumerable<T> b,
			IEqualityComparer<T> cmp)
		{
			if(a == null) throw new ArgumentNullException("a");
			if(b == null) throw new ArgumentNullException("b");

			Dictionary<T, bool> d = ((cmp != null) ?
				(new Dictionary<T, bool>(cmp)) : (new Dictionary<T, bool>()));

			foreach(T tb in b) { d[tb] = true; }

			foreach(T ta in a)
			{
				if(d.Remove(ta)) // Prevent duplicates
					yield return ta;
			}

			yield break;
		}

		public static IEnumerable<T> Except<T>(IEnumerable<T> a, IEnumerable<T> b,
			IEqualityComparer<T> cmp)
		{
			if(a == null) throw new ArgumentNullException("a");
			if(b == null) throw new ArgumentNullException("b");

			Dictionary<T, bool> d = ((cmp != null) ?
				(new Dictionary<T, bool>(cmp)) : (new Dictionary<T, bool>()));

			foreach(T tb in b) { d[tb] = true; }

			foreach(T ta in a)
			{
				if(d.ContainsKey(ta)) continue;

				d[ta] = true; // Prevent duplicates
				yield return ta;
			}

			yield break;
		}
	}
}
