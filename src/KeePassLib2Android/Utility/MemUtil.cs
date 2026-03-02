/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2025 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Runtime.InteropServices;
using System.Text;

#if KeePassLibSD
using KeePassLibSD;
#else
using System.IO.Compression;
#endif

using KeePassLib.Delegates;

namespace KeePassLib.Utility
{
  /// <summary>
  /// Buffer manipulation and conversion routines.
  /// </summary>
  public static class MemUtil
  {
    public static readonly byte[] EmptyByteArray = new byte[0];

    internal static readonly ArrayHelperEx<char> ArrayHelperExOfChar =
        new ArrayHelperEx<char>();

    private const MethodImplOptions MioNoOptimize =
#if KeePassLibSD
			MethodImplOptions.NoInlining;
#else
        (MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining);
#endif

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
      if (strHex == null) { Debug.Assert(false); throw new ArgumentNullException("strHex"); }

      int nStrLen = strHex.Length;
      if ((nStrLen & 1) != 0) { Debug.Assert(false); return null; }

      byte[] pb = new byte[nStrLen / 2];
      byte bt;
      char ch;

      for (int i = 0; i < nStrLen; i += 2)
      {
        ch = strHex[i];

        if ((ch >= '0') && (ch <= '9'))
          bt = (byte)(ch - '0');
        else if ((ch >= 'a') && (ch <= 'f'))
          bt = (byte)(ch - 'a' + 10);
        else if ((ch >= 'A') && (ch <= 'F'))
          bt = (byte)(ch - 'A' + 10);
        else { Debug.Assert(false); bt = 0; }

        bt <<= 4;

        ch = strHex[i + 1];
        if ((ch >= '0') && (ch <= '9'))
          bt |= (byte)(ch - '0');
        else if ((ch >= 'a') && (ch <= 'f'))
          bt |= (byte)(ch - 'a' + 10);
        else if ((ch >= 'A') && (ch <= 'F'))
          bt |= (byte)(ch - 'A' + 10);
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
      if (pbArray == null) return null;

      int nLen = pbArray.Length;
      if (nLen == 0) return string.Empty;

      StringBuilder sb = new StringBuilder();

      byte bt, btHigh, btLow;
      for (int i = 0; i < nLen; ++i)
      {
        bt = pbArray[i];
        btHigh = bt; btHigh >>= 4;
        btLow = (byte)(bt & 0x0F);

        if (btHigh >= 10) sb.Append((char)('A' + btHigh - 10));
        else sb.Append((char)('0' + btHigh));

        if (btLow >= 10) sb.Append((char)('A' + btLow - 10));
        else sb.Append((char)('0' + btLow));
      }

      return sb.ToString();
    }

    /// <summary>
    /// Decode Base32 strings according to RFC 4648.
    /// </summary>
    public static byte[] ParseBase32(string str)
    {
      if ((str == null) || ((str.Length % 8) != 0))
      {
        Debug.Assert(false);
        return null;
      }

      ulong uMaxBits = (ulong)str.Length * 5UL;
      List<byte> l = new List<byte>((int)(uMaxBits / 8UL) + 1);
      Debug.Assert(l.Count == 0);

      for (int i = 0; i < str.Length; i += 8)
      {
        ulong u = 0;
        int nBits = 0;

        for (int j = 0; j < 8; ++j)
        {
          char ch = str[i + j];
          if (ch == '=') break;

          ulong uValue;
          if ((ch >= 'A') && (ch <= 'Z'))
            uValue = (ulong)(ch - 'A');
          else if ((ch >= 'a') && (ch <= 'z'))
            uValue = (ulong)(ch - 'a');
          else if ((ch >= '2') && (ch <= '7'))
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
        while (nBits > 0)
        {
          l.Add((byte)(u & 0xFF));
          u >>= 8;
          nBits -= 8;
        }
        l.Reverse(idxNewBytes, l.Count - idxNewBytes);
      }

      return l.ToArray();
    }

    internal static byte[] ParseBase32(string str, bool bAutoPad)
    {
      if (str == null) { Debug.Assert(false); return null; }

      // https://sourceforge.net/p/keepass/discussion/329220/thread/59b61fddea/
      if (bAutoPad && ((str.Length % 8) != 0))
        str = str.PadRight((str.Length & ~7) + 8, '=');

      return ParseBase32(str);
    }

    /// <summary>
    /// Set all bytes in a byte array to zero.
    /// </summary>
    /// <param name="pbArray">Input array. All bytes of this array
    /// will be set to zero.</param>
    [MethodImpl(MioNoOptimize)]
    public static void ZeroByteArray(byte[] pbArray)
    {
      if (pbArray == null) { Debug.Assert(false); return; }

      Array.Clear(pbArray, 0, pbArray.Length);
    }

    /// <summary>
    /// Set all elements of an array to the default value.
    /// </summary>
    /// <param name="v">Input array.</param>
    [MethodImpl(MioNoOptimize)]
    public static void ZeroArray<T>(T[] v)
    {
      if (v == null) { Debug.Assert(false); return; }

      Array.Clear(v, 0, v.Length);
    }

    private static byte[] g_pbZero = null;
    [MethodImpl(MioNoOptimize)]
    public static void ZeroMemory(IntPtr pb, long cb)
    {
      if (pb == IntPtr.Zero) { Debug.Assert(false); return; }
      if (cb < 0) { Debug.Assert(false); return; }

      byte[] pbZero = g_pbZero;
      if (pbZero == null)
      {
        pbZero = new byte[4096];
        g_pbZero = pbZero;
      }

      long cbZero = pbZero.Length;

      while (cb != 0)
      {
        long cbBlock = Math.Min(cb, cbZero);

        Marshal.Copy(pbZero, 0, pb, (int)cbBlock);

        pb = AddPtr(pb, cbBlock);
        cb -= cbBlock;
      }
    }

    /// <summary>
    /// Convert 2 bytes to a 16-bit unsigned integer (little-endian).
    /// </summary>
    public static ushort BytesToUInt16(byte[] pb)
    {
      Debug.Assert((pb != null) && (pb.Length == 2));
      if (pb == null) throw new ArgumentNullException("pb");
      if (pb.Length != 2) throw new ArgumentOutOfRangeException("pb");

      return (ushort)((ushort)pb[0] | ((ushort)pb[1] << 8));
    }

    /// <summary>
    /// Convert 2 bytes to a 16-bit unsigned integer (little-endian).
    /// </summary>
    public static ushort BytesToUInt16(byte[] pb, int iOffset)
    {
      if (pb == null) { Debug.Assert(false); throw new ArgumentNullException("pb"); }
      if ((iOffset < 0) || ((iOffset + 1) >= pb.Length))
      {
        Debug.Assert(false);
        throw new ArgumentOutOfRangeException("iOffset");
      }

      return (ushort)((ushort)pb[iOffset] | ((ushort)pb[iOffset + 1] << 8));
    }

    /// <summary>
    /// Convert 4 bytes to a 32-bit unsigned integer (little-endian).
    /// </summary>
    public static uint BytesToUInt32(byte[] pb)
    {
      Debug.Assert((pb != null) && (pb.Length == 4));
      if (pb == null) throw new ArgumentNullException("pb");
      if (pb.Length != 4) throw new ArgumentOutOfRangeException("pb");

      return ((uint)pb[0] | ((uint)pb[1] << 8) | ((uint)pb[2] << 16) |
          ((uint)pb[3] << 24));
    }

    /// <summary>
    /// Convert 4 bytes to a 32-bit unsigned integer (little-endian).
    /// </summary>
    public static uint BytesToUInt32(byte[] pb, int iOffset)
    {
      if (pb == null) { Debug.Assert(false); throw new ArgumentNullException("pb"); }
      if ((iOffset < 0) || ((iOffset + 3) >= pb.Length))
      {
        Debug.Assert(false);
        throw new ArgumentOutOfRangeException("iOffset");
      }

      return ((uint)pb[iOffset] | ((uint)pb[iOffset + 1] << 8) |
          ((uint)pb[iOffset + 2] << 16) | ((uint)pb[iOffset + 3] << 24));
    }

    /// <summary>
    /// Convert 8 bytes to a 64-bit unsigned integer (little-endian).
    /// </summary>
    public static ulong BytesToUInt64(byte[] pb)
    {
      Debug.Assert((pb != null) && (pb.Length == 8));
      if (pb == null) throw new ArgumentNullException("pb");
      if (pb.Length != 8) throw new ArgumentOutOfRangeException("pb");

      return ((ulong)pb[0] | ((ulong)pb[1] << 8) | ((ulong)pb[2] << 16) |
          ((ulong)pb[3] << 24) | ((ulong)pb[4] << 32) | ((ulong)pb[5] << 40) |
          ((ulong)pb[6] << 48) | ((ulong)pb[7] << 56));
    }

    /// <summary>
    /// Convert 8 bytes to a 64-bit unsigned integer (little-endian).
    /// </summary>
    public static ulong BytesToUInt64(byte[] pb, int iOffset)
    {
      if (pb == null) { Debug.Assert(false); throw new ArgumentNullException("pb"); }
      if ((iOffset < 0) || ((iOffset + 7) >= pb.Length))
      {
        Debug.Assert(false);
        throw new ArgumentOutOfRangeException("iOffset");
      }

      // if(BitConverter.IsLittleEndian)
      //	return BitConverter.ToUInt64(pb, iOffset);

      return ((ulong)pb[iOffset] | ((ulong)pb[iOffset + 1] << 8) |
          ((ulong)pb[iOffset + 2] << 16) | ((ulong)pb[iOffset + 3] << 24) |
          ((ulong)pb[iOffset + 4] << 32) | ((ulong)pb[iOffset + 5] << 40) |
          ((ulong)pb[iOffset + 6] << 48) | ((ulong)pb[iOffset + 7] << 56));
    }

    public static int BytesToInt32(byte[] pb)
    {
      return (int)BytesToUInt32(pb);
    }

    public static int BytesToInt32(byte[] pb, int iOffset)
    {
      return (int)BytesToUInt32(pb, iOffset);
    }

    public static long BytesToInt64(byte[] pb)
    {
      return (long)BytesToUInt64(pb);
    }

    public static long BytesToInt64(byte[] pb, int iOffset)
    {
      return (long)BytesToUInt64(pb, iOffset);
    }

    /// <summary>
    /// Convert a 16-bit unsigned integer to 2 bytes (little-endian).
    /// </summary>
    public static byte[] UInt16ToBytes(ushort uValue)
    {
      return new byte[2] { (byte)uValue, (byte)(uValue >> 8) };
    }

    /// <summary>
    /// Convert a 32-bit unsigned integer to 4 bytes (little-endian).
    /// </summary>
    public static byte[] UInt32ToBytes(uint uValue)
    {
      return new byte[4] { (byte)uValue, (byte)(uValue >> 8),
                (byte)(uValue >> 16), (byte)(uValue >> 24) };
    }

    /// <summary>
    /// Convert a 32-bit unsigned integer to 4 bytes (little-endian).
    /// </summary>
    public static void UInt32ToBytesEx(uint uValue, byte[] pb, int iOffset)
    {
      if (pb == null) { Debug.Assert(false); throw new ArgumentNullException("pb"); }
      if ((iOffset < 0) || (iOffset >= (pb.Length - 3)))
      {
        Debug.Assert(false);
        throw new ArgumentOutOfRangeException("iOffset");
      }

      pb[iOffset] = (byte)uValue;
      pb[iOffset + 1] = (byte)(uValue >> 8);
      pb[iOffset + 2] = (byte)(uValue >> 16);
      pb[iOffset + 3] = (byte)(uValue >> 24);
    }

    /// <summary>
    /// Convert a 64-bit unsigned integer to 8 bytes (little-endian).
    /// </summary>
    public static byte[] UInt64ToBytes(ulong uValue)
    {
      return new byte[8] { (byte)uValue, (byte)(uValue >> 8),
                (byte)(uValue >> 16), (byte)(uValue >> 24),
                (byte)(uValue >> 32), (byte)(uValue >> 40),
                (byte)(uValue >> 48), (byte)(uValue >> 56) };
    }

    /// <summary>
    /// Convert a 64-bit unsigned integer to 8 bytes (little-endian).
    /// </summary>
    public static void UInt64ToBytesEx(ulong uValue, byte[] pb, int iOffset)
    {
      if (pb == null) { Debug.Assert(false); throw new ArgumentNullException("pb"); }
      if ((iOffset < 0) || (iOffset >= (pb.Length - 7)))
      {
        Debug.Assert(false);
        throw new ArgumentOutOfRangeException("iOffset");
      }

      pb[iOffset] = (byte)uValue;
      pb[iOffset + 1] = (byte)(uValue >> 8);
      pb[iOffset + 2] = (byte)(uValue >> 16);
      pb[iOffset + 3] = (byte)(uValue >> 24);
      pb[iOffset + 4] = (byte)(uValue >> 32);
      pb[iOffset + 5] = (byte)(uValue >> 40);
      pb[iOffset + 6] = (byte)(uValue >> 48);
      pb[iOffset + 7] = (byte)(uValue >> 56);
    }

    public static byte[] Int32ToBytes(int iValue)
    {
      return UInt32ToBytes((uint)iValue);
    }

    public static void Int32ToBytesEx(int iValue, byte[] pb, int iOffset)
    {
      UInt32ToBytesEx((uint)iValue, pb, iOffset);
    }

    public static byte[] Int64ToBytes(long lValue)
    {
      return UInt64ToBytes((ulong)lValue);
    }

    public static void Int64ToBytesEx(long lValue, byte[] pb, int iOffset)
    {
      UInt64ToBytesEx((ulong)lValue, pb, iOffset);
    }

    public static uint RotateLeft32(uint u, int nBits)
    {
      return ((u << nBits) | (u >> (32 - nBits)));
    }

    public static uint RotateRight32(uint u, int nBits)
    {
      return ((u >> nBits) | (u << (32 - nBits)));
    }

    public static ulong RotateLeft64(ulong u, int nBits)
    {
      return ((u << nBits) | (u >> (64 - nBits)));
    }

    public static ulong RotateRight64(ulong u, int nBits)
    {
      return ((u >> nBits) | (u << (64 - nBits)));
    }

    private static void AddVersionComponent(ref ulong uVersion, int iValue)
    {
      if (iValue < 0) iValue = 0;
      else if (iValue > 0xFFFF) { Debug.Assert(false); iValue = 0xFFFF; }

      uVersion = (uVersion << 16) | (uint)iValue;
    }

    internal static ulong VersionToUInt64(Version v)
    {
      if (v == null) { Debug.Assert(false); return 0; }

      ulong u = 0;
      AddVersionComponent(ref u, v.Major);
      AddVersionComponent(ref u, v.Minor);
      AddVersionComponent(ref u, v.Build);
      AddVersionComponent(ref u, v.Revision);
      return u;
    }

    public static bool ArraysEqual(byte[] x, byte[] y)
    {
      // Return false if one of them is null (not comparable)!
      if ((x == null) || (y == null)) { Debug.Assert(false); return false; }

      int cb = x.Length;
      if (cb != y.Length) return false;

      for (int i = 0; i < cb; ++i)
      {
        if (x[i] != y[i]) return false;
      }

      return true;
    }

    public static void XorArray(byte[] pbSource, int iSourceOffset,
        byte[] pbBuffer, int iBufferOffset, int cb)
    {
      if (pbSource == null) throw new ArgumentNullException("pbSource");
      if (iSourceOffset < 0) throw new ArgumentOutOfRangeException("iSourceOffset");
      if (pbBuffer == null) throw new ArgumentNullException("pbBuffer");
      if (iBufferOffset < 0) throw new ArgumentOutOfRangeException("iBufferOffset");
      if (cb < 0) throw new ArgumentOutOfRangeException("cb");
      if (iSourceOffset > (pbSource.Length - cb))
        throw new ArgumentOutOfRangeException("cb");
      if (iBufferOffset > (pbBuffer.Length - cb))
        throw new ArgumentOutOfRangeException("cb");

      for (int i = 0; i < cb; ++i)
        pbBuffer[iBufferOffset + i] ^= pbSource[iSourceOffset + i];
    }

    /// <summary>
    /// Fast 32-bit hash (e.g. for hash tables).
    /// The algorithm might change in the future; do not store
    /// the hashes for later use.
    /// </summary>
    public static uint Hash32(byte[] pb, int iOffset, int cb)
    {
      const ulong hI = 0x4295DC458269ED9DUL;
      const uint hI32 = (uint)(hI >> 32);

      if (pb == null) { Debug.Assert(false); return hI32; }
      if (iOffset < 0) { Debug.Assert(false); return hI32; }
      if (cb < 0) { Debug.Assert(false); return hI32; }

      int m = iOffset + cb;
      if ((m < 0) || (m > pb.Length)) { Debug.Assert(false); return hI32; }

      int m4 = iOffset + (cb & ~3), cbR = cb & 3;
      ulong h = hI;

      for (int i = iOffset; i < m4; i += 4)
        h = (pb[i] ^ ((ulong)pb[i + 1] << 8) ^ ((ulong)pb[i + 2] << 16) ^
            ((ulong)pb[i + 3] << 24) ^ h) * 0x5EA4A1E35C8ACDA3UL;

      switch (cbR)
      {
        case 1:
          Debug.Assert(m4 == (m - 1));
          h = (pb[m4] ^ h) * 0x54A1CC5970AF27BBUL;
          break;
        case 2:
          Debug.Assert(m4 == (m - 2));
          h = (pb[m4] ^ ((ulong)pb[m4 + 1] << 8) ^ h) *
              0x6C45CB2537A4271DUL;
          break;
        case 3:
          Debug.Assert(m4 == (m - 3));
          h = (pb[m4] ^ ((ulong)pb[m4 + 1] << 8) ^
              ((ulong)pb[m4 + 2] << 16) ^ h) * 0x59B8E8939E19695DUL;
          break;
        default:
          Debug.Assert(m4 == m);
          break;
      }

      Debug.Assert((cb != 0) || ((uint)(h >> 32) == hI32));
      return (uint)(h >> 32);
    }

    internal static uint Hash32Ex<T>(T[] v, int iOffset, int c)
    {
      const ulong hI = 0x4295DC458269ED9DUL;
      const uint hI32 = (uint)(hI >> 32);

      if (v == null) { Debug.Assert(false); return hI32; }
      if (iOffset < 0) { Debug.Assert(false); return hI32; }
      if (c < 0) { Debug.Assert(false); return hI32; }

      int m = iOffset + c;
      if ((m < 0) || (m > v.Length)) { Debug.Assert(false); return hI32; }

      ulong h = hI;

      for (int i = iOffset; i < m; ++i)
        h = (h ^ (uint)v[i].GetHashCode()) * 0x5EA4A1E35C8ACDA3UL;

      Debug.Assert((c != 0) || ((uint)(h >> 32) == hI32));
      return (uint)(h >> 32);
    }

    internal static ulong Hash64(int[] v, int iOffset, int ci)
    {
      ulong h = 0x4295DC458269ED9DUL;

      if (v == null) { Debug.Assert(false); return h; }
      if (iOffset < 0) { Debug.Assert(false); return h; }
      if (ci < 0) { Debug.Assert(false); return h; }

      int m = iOffset + ci;
      if ((m < 0) || (m > v.Length)) { Debug.Assert(false); return h; }

      for (int i = iOffset; i < m; ++i)
        h = (h ^ (uint)v[i]) * 0x5EA4A1E35C8ACDA3UL;

      return ((h ^ (h >> 32)) * 0x59B8E8939E19695DUL);
    }

    public static void CopyStream(Stream sSource, Stream sTarget)
    {
      Debug.Assert((sSource != null) && (sTarget != null));
      if (sSource == null) throw new ArgumentNullException("sSource");
      if (sTarget == null) throw new ArgumentNullException("sTarget");

      const int cbBuf = 4096;
      byte[] pbBuf = new byte[cbBuf];

      while (true)
      {
        int cbRead = sSource.Read(pbBuf, 0, cbBuf);
        if (cbRead == 0) break;

        sTarget.Write(pbBuf, 0, cbRead);
      }

      // Do not close any of the streams
    }

    public static byte[] Read(Stream s)
    {
      if (s == null) throw new ArgumentNullException("s");

      using (MemoryStream ms = new MemoryStream())
      {
        CopyStream(s, ms);
        return ms.ToArray();
      }
    }

    public static byte[] Read(Stream s, int nCount)
    {
      if (s == null) throw new ArgumentNullException("s");
      if (nCount < 0) throw new ArgumentOutOfRangeException("nCount");

      byte[] pb = new byte[nCount];
      int iOffset = 0;
      while (nCount > 0)
      {
        int iRead = s.Read(pb, iOffset, nCount);
        if (iRead == 0) break;

        iOffset += iRead;
        nCount -= iRead;
      }

      if (iOffset != pb.Length)
      {
        byte[] pbPart = new byte[iOffset];
        Array.Copy(pb, pbPart, iOffset);
        return pbPart;
      }

      return pb;
    }

    internal static string ReadString(Stream s, Encoding enc)
    {
      if (s == null) throw new ArgumentNullException("s");
      if (enc == null) throw new ArgumentNullException("enc");

      using (StreamReader sr = new StreamReader(s, enc, true))
      {
        return sr.ReadToEnd();
      }
    }

    public static void Write(Stream s, byte[] pbData)
    {
      if (s == null) { Debug.Assert(false); return; }
      if (pbData == null) { Debug.Assert(false); return; }

      Debug.Assert(pbData.Length >= 0);
      if (pbData.Length > 0) s.Write(pbData, 0, pbData.Length);
    }

    public static byte[] Compress(byte[] pbData)
    {
      if (pbData == null) throw new ArgumentNullException("pbData");
      if (pbData.Length == 0) return pbData;

      using (MemoryStream msSource = new MemoryStream(pbData, false))
      {
        using (MemoryStream msCompressed = new MemoryStream())
        {
          using (GZipStream gz = new GZipStream(msCompressed,
              CompressionMode.Compress))
          {
            CopyStream(msSource, gz);
          }

          return msCompressed.ToArray();
        }
      }
    }

    public static byte[] Decompress(byte[] pbCompressed)
    {
      if (pbCompressed == null) throw new ArgumentNullException("pbCompressed");
      if (pbCompressed.Length == 0) return pbCompressed;

      using (MemoryStream msData = new MemoryStream())
      {
        using (MemoryStream msCompressed = new MemoryStream(pbCompressed, false))
        {
          using (GZipStream gz = new GZipStream(msCompressed,
              CompressionMode.Decompress))
          {
            CopyStream(gz, msData);
          }
        }

        return msData.ToArray();
      }
    }

    public static int IndexOf<T>(T[] vHaystack, T[] vNeedle)
        where T : IEquatable<T>
    {
      if (vHaystack == null) throw new ArgumentNullException("vHaystack");
      if (vNeedle == null) throw new ArgumentNullException("vNeedle");
      if (vNeedle.Length == 0) return 0;

      int cN = vNeedle.Length;
      int iMax = vHaystack.Length - cN;

      for (int i = 0; i <= iMax; ++i)
      {
        bool bFound = true;
        for (int m = 0; m < cN; ++m)
        {
          if (!vHaystack[i + m].Equals(vNeedle[m]))
          {
            bFound = false;
            break;
          }
        }
        if (bFound) return i;
      }

      return -1;
    }

    public static T[] Mid<T>(T[] v, int iOffset, int iLength)
    {
      if (v == null) throw new ArgumentNullException("v");
      if (iOffset < 0) throw new ArgumentOutOfRangeException("iOffset");
      if (iLength < 0) throw new ArgumentOutOfRangeException("iLength");
      if ((iOffset + iLength) > v.Length) throw new ArgumentException();

      T[] r = new T[iLength];
      Array.Copy(v, iOffset, r, 0, iLength);
      return r;
    }

    public static IEnumerable<T> Union<T>(IEnumerable<T> a, IEnumerable<T> b,
        IEqualityComparer<T> cmp)
    {
      if (a == null) throw new ArgumentNullException("a");
      if (b == null) throw new ArgumentNullException("b");

      Dictionary<T, bool> d = ((cmp != null) ?
          (new Dictionary<T, bool>(cmp)) : (new Dictionary<T, bool>()));

      foreach (T ta in a)
      {
        if (d.ContainsKey(ta)) continue; // Prevent duplicates

        d[ta] = true;
        yield return ta;
      }

      foreach (T tb in b)
      {
        if (d.ContainsKey(tb)) continue; // Prevent duplicates

        d[tb] = true;
        yield return tb;
      }

      yield break;
    }

    public static IEnumerable<T> Intersect<T>(IEnumerable<T> a, IEnumerable<T> b,
        IEqualityComparer<T> cmp)
    {
      if (a == null) throw new ArgumentNullException("a");
      if (b == null) throw new ArgumentNullException("b");

      Dictionary<T, bool> d = ((cmp != null) ?
          (new Dictionary<T, bool>(cmp)) : (new Dictionary<T, bool>()));

      foreach (T tb in b) { d[tb] = true; }

      foreach (T ta in a)
      {
        if (d.Remove(ta)) // Prevent duplicates
          yield return ta;
      }

      yield break;
    }

    public static IEnumerable<T> Except<T>(IEnumerable<T> a, IEnumerable<T> b,
        IEqualityComparer<T> cmp)
    {
      if (a == null) throw new ArgumentNullException("a");
      if (b == null) throw new ArgumentNullException("b");

      Dictionary<T, bool> d = ((cmp != null) ?
          (new Dictionary<T, bool>(cmp)) : (new Dictionary<T, bool>()));

      foreach (T tb in b) { d[tb] = true; }

      foreach (T ta in a)
      {
        if (d.ContainsKey(ta)) continue;

        d[ta] = true; // Prevent duplicates
        yield return ta;
      }

      yield break;
    }

    internal static IEnumerable<T> Distinct<T, TKey>(IEnumerable<T> s,
        GFunc<T, TKey> fGetKey, bool bPreferLast)
    {
      if (s == null) throw new ArgumentNullException("s");
      if (fGetKey == null) throw new ArgumentNullException("fGetKey");

      Dictionary<TKey, bool> d = new Dictionary<TKey, bool>();

      if (bPreferLast)
      {
        List<T> l = new List<T>(s);
        int n = l.Count;
        bool[] v = new bool[n];

        for (int i = n - 1; i >= 0; --i)
        {
          TKey k = fGetKey(l[i]);
          if (!d.ContainsKey(k)) { d[k] = true; v[i] = true; }
        }

        for (int i = 0; i < n; ++i)
        {
          if (v[i]) yield return l[i];
        }
      }
      else
      {
        foreach (T t in s)
        {
          TKey k = fGetKey(t);
          if (!d.ContainsKey(k)) { d[k] = true; yield return t; }
        }
      }

      yield break;
    }

    internal static bool ListsEqual<T>(List<T> a, List<T> b)
        where T : class, IEquatable<T>
    {
      if (object.ReferenceEquals(a, b)) return true;
      if ((a == null) || (b == null)) return false;

      int n = a.Count;
      if (n != b.Count) return false;

      for (int i = 0; i < n; ++i)
      {
        T tA = a[i], tB = b[i];

        if (tA == null)
        {
          if (tB != null) return false;
        }
        else if (tB == null) return false;
        else if (!tA.Equals(tB)) return false;
      }

      return true;
    }

    internal static int Count(byte[] pb, byte bt)
    {
      if (pb == null) { Debug.Assert(false); return 0; }

      int cb = pb.Length, r = 0;
      for (int i = 0; i < cb; ++i)
      {
        if (pb[i] == bt) ++r;
      }

      return r;
    }

    [MethodImpl(MioNoOptimize)]
    internal static void DisposeIfPossible(object o)
    {
      if (o == null) { Debug.Assert(false); return; }

      IDisposable d = (o as IDisposable);
      if (d != null) d.Dispose();
    }

    internal static object GetEnumValue(Type tEnum, string strName)
    {
      if (tEnum == null) { Debug.Assert(false); return null; }
      if (!tEnum.IsEnum) { Debug.Assert(false); return null; }
      if (string.IsNullOrEmpty(strName)) { Debug.Assert(false); return null; }

      return ((Array.IndexOf<string>(Enum.GetNames(tEnum), strName) >= 0) ?
          Enum.Parse(tEnum, strName) : null);
    }

    internal static T ConvertObject<T>(object o, T tDefault)
    {
      if (o == null) return tDefault;

      try
      {
        if (o is T) return (T)o;
        return (T)Convert.ChangeType(o, typeof(T));
      }
      catch (Exception) { Debug.Assert(false); }

      try { return (T)o; }
      catch (Exception) { Debug.Assert(false); }

      return tDefault;
    }

    internal static T BytesToStruct<T>(byte[] pb, int iOffset)
        where T : struct
    {
      if (pb == null) throw new ArgumentNullException("pb");
      if (iOffset < 0) throw new ArgumentOutOfRangeException("iOffset");

      int cb = Marshal.SizeOf(typeof(T));
      if (cb <= 0) { Debug.Assert(false); return default(T); }

      if (iOffset > (pb.Length - cb)) throw new ArgumentOutOfRangeException("iOffset");

      IntPtr p = Marshal.AllocCoTaskMem(cb);
      if (p == IntPtr.Zero) throw new OutOfMemoryException();

      object o;
      try
      {
        Marshal.Copy(pb, iOffset, p, cb);
        o = Marshal.PtrToStructure(p, typeof(T));
      }
      finally { Marshal.FreeCoTaskMem(p); }

      return (T)o;
    }

    internal static byte[] StructToBytes<T>(ref T t)
        where T : struct
    {
      int cb = Marshal.SizeOf(typeof(T));
      if (cb <= 0) { Debug.Assert(false); return MemUtil.EmptyByteArray; }

      byte[] pb = new byte[cb];

      IntPtr p = Marshal.AllocCoTaskMem(cb);
      if (p == IntPtr.Zero) throw new OutOfMemoryException();

      try
      {
        Marshal.StructureToPtr(t, p, false);
        Marshal.Copy(p, pb, 0, cb);
      }
      finally { Marshal.FreeCoTaskMem(p); }

      return pb;
    }

    internal static IntPtr AddPtr(IntPtr p, long cb)
    {
      // IntPtr.operator+ and IntPtr.Add are not available in .NET 2.0

      if (IntPtr.Size >= 8)
        return new IntPtr(unchecked(p.ToInt64() + cb));
      return new IntPtr(unchecked(p.ToInt32() + (int)cb));
    }

    // Cf. Array.Empty<T>() of .NET 4.6
    private static class EmptyArrayEx<T>
    {
      internal static readonly T[] Instance = new T[0];
    }
    internal static T[] EmptyArray<T>()
    {
      return EmptyArrayEx<T>.Instance;
    }
  }

  internal sealed class ArrayHelperEx<T> : IEqualityComparer<T[]>, IComparer<T[]>
      where T : IEquatable<T>, IComparable<T>
  {
    public int GetHashCode(T[] obj)
    {
      if (obj == null) { Debug.Assert(false); throw new ArgumentNullException("obj"); }

      return (int)MemUtil.Hash32Ex<T>(obj, 0, obj.Length);
    }

    public bool Equals(T[] x, T[] y)
    {
      if (object.ReferenceEquals(x, y)) return true;
      if ((x == null) || (y == null)) return false;

      int n = x.Length;
      if (n != y.Length) return false;

      for (int i = 0; i < n; ++i)
      {
        if (!x[i].Equals(y[i])) return false;
      }

      return true;
    }

    public int Compare(T[] x, T[] y)
    {
      if (object.ReferenceEquals(x, y)) return 0;
      if (x == null) return -1;
      if (y == null) return 1;

      int n = x.Length, m = y.Length;
      if (n != m) return ((n < m) ? -1 : 1);

      for (int i = 0; i < n; ++i)
      {
        T tX = x[i], tY = y[i];
        if (!tX.Equals(tY)) return tX.CompareTo(tY);
      }

      return 0;
    }
  }
}
