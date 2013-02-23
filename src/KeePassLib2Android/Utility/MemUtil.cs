/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2012 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Text;
using System.Security.Cryptography;
using System.Diagnostics;
using System.IO;

#if !KeePassLibSD
using System.IO.Compression;
#else
using KeePassLibSD;
#endif

namespace KeePassLib.Utility
{
	/// <summary>
	/// Contains static buffer manipulation and string conversion routines.
	/// </summary>
	public static class MemUtil
	{
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
		/// Set all bytes in a byte array to zero.
		/// </summary>
		/// <param name="pbArray">Input array. All bytes of this array will be set
		/// to zero.</param>
		public static void ZeroByteArray(byte[] pbArray)
		{
			Debug.Assert(pbArray != null); if(pbArray == null) throw new ArgumentNullException("pbArray");

			// for(int i = 0; i < pbArray.Length; ++i)
			//	pbArray[i] = 0;

			Array.Clear(pbArray, 0, pbArray.Length);
		}

		/// <summary>
		/// Convert 2 bytes to a 16-bit unsigned integer using Little-Endian
		/// encoding.
		/// </summary>
		/// <param name="pb">Input bytes. Array must contain at least 2 bytes.</param>
		/// <returns>16-bit unsigned integer.</returns>
		public static ushort BytesToUInt16(byte[] pb)
		{
			Debug.Assert((pb != null) && (pb.Length == 2));
			if(pb == null) throw new ArgumentNullException("pb");
			if(pb.Length != 2) throw new ArgumentException();

			return (ushort)((ushort)pb[0] | ((ushort)pb[1] << 8));
		}

		/// <summary>
		/// Convert 4 bytes to a 32-bit unsigned integer using Little-Endian
		/// encoding.
		/// </summary>
		/// <param name="pb">Input bytes.</param>
		/// <returns>32-bit unsigned integer.</returns>
		public static uint BytesToUInt32(byte[] pb)
		{
			Debug.Assert((pb != null) && (pb.Length == 4));
			if(pb == null) throw new ArgumentNullException("pb");
			if(pb.Length != 4) throw new ArgumentException("Input array must contain 4 bytes!");

			return (uint)pb[0] | ((uint)pb[1] << 8) | ((uint)pb[2] << 16) |
				((uint)pb[3] << 24);
		}

		/// <summary>
		/// Convert 8 bytes to a 64-bit unsigned integer using Little-Endian
		/// encoding.
		/// </summary>
		/// <param name="pb">Input bytes.</param>
		/// <returns>64-bit unsigned integer.</returns>
		public static ulong BytesToUInt64(byte[] pb)
		{
			Debug.Assert((pb != null) && (pb.Length == 8));
			if(pb == null) throw new ArgumentNullException("pb");
			if(pb.Length != 8) throw new ArgumentException();

			return (ulong)pb[0] | ((ulong)pb[1] << 8) | ((ulong)pb[2] << 16) |
				((ulong)pb[3] << 24) | ((ulong)pb[4] << 32) | ((ulong)pb[5] << 40) |
				((ulong)pb[6] << 48) | ((ulong)pb[7] << 56);
		}

		/// <summary>
		/// Convert a 16-bit unsigned integer to 2 bytes using Little-Endian
		/// encoding.
		/// </summary>
		/// <param name="uValue">16-bit input word.</param>
		/// <returns>Two bytes representing the 16-bit value.</returns>
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
		/// Convert a 32-bit unsigned integer to 4 bytes using Little-Endian
		/// encoding.
		/// </summary>
		/// <param name="uValue">32-bit input word.</param>
		/// <returns>Four bytes representing the 32-bit value.</returns>
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
		/// Convert a 64-bit unsigned integer to 8 bytes using Little-Endian
		/// encoding.
		/// </summary>
		/// <param name="uValue">64-bit input word.</param>
		/// <returns>Eight bytes representing the 64-bit value.</returns>
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

			MemoryStream msCompressed = new MemoryStream();
			GZipStream gz = new GZipStream(msCompressed, CompressionMode.Compress);
			MemoryStream msSource = new MemoryStream(pbData, false);
			MemUtil.CopyStream(msSource, gz);
			gz.Close();
			msSource.Close();

			byte[] pbCompressed = msCompressed.ToArray();
			msCompressed.Close();
			return pbCompressed;
		}

		public static byte[] Decompress(byte[] pbCompressed)
		{
			if(pbCompressed == null) throw new ArgumentNullException("pbCompressed");
			if(pbCompressed.Length == 0) return pbCompressed;

			MemoryStream msCompressed = new MemoryStream(pbCompressed, false);
			GZipStream gz = new GZipStream(msCompressed, CompressionMode.Decompress);
			MemoryStream msData = new MemoryStream();
			MemUtil.CopyStream(gz, msData);
			gz.Close();
			msCompressed.Close();

			byte[] pbData = msData.ToArray();
			msData.Close();
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
			if(iOffset + iLength > v.Length) throw new ArgumentException();

			T[] r = new T[iLength];
			Array.Copy(v, iOffset, r, 0, iLength);
			return r;
		}
	}
}
