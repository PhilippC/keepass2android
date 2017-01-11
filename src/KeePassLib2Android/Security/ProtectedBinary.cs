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
using System.Threading;

#if !KeePassUAP
using System.Security.Cryptography;
#endif

using KeePassLib.Cryptography;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Native;
using KeePassLib.Utility;

#if KeePassLibSD
using KeePassLibSD;
#endif

namespace KeePassLib.Security
{
	[Flags]
	public enum PbCryptFlags
	{
		None = 0,
		Encrypt = 1,
		Decrypt = 2
	}

	public delegate void PbCryptDelegate(byte[] pbData, PbCryptFlags cf,
		long lID);

	/// <summary>
	/// Represents a protected binary, i.e. a byte array that is encrypted
	/// in memory. A <c>ProtectedBinary</c> object is immutable and
	/// thread-safe.
	/// </summary>
	public sealed class ProtectedBinary : IEquatable<ProtectedBinary>
	{
		private const int BlockSize = 16;

		private static PbCryptDelegate g_fExtCrypt = null;
		/// <summary>
		/// A plugin can provide a custom memory protection method
		/// by assigning a non-null delegate to this property.
		/// </summary>
		public static PbCryptDelegate ExtCrypt
		{
			get { return g_fExtCrypt; }
			set { g_fExtCrypt = value; }
		}

		// Local copy of the delegate that was used for encryption,
		// in order to allow correct decryption even when the global
		// delegate changes
		private PbCryptDelegate m_fExtCrypt = null;

		private enum PbMemProt
		{
			None = 0,
			ProtectedMemory,
			ChaCha20,
			ExtCrypt
		}

		// ProtectedMemory is supported only on Windows 2000 SP3 and higher
#if !KeePassLibSD
		private static bool? g_obProtectedMemorySupported = null;
#endif
		private static bool ProtectedMemorySupported
		{
			get
			{
#if KeePassLibSD
				return false;
#else
				bool? ob = g_obProtectedMemorySupported;
				if(ob.HasValue) return ob.Value;

				// Mono does not implement any encryption for ProtectedMemory;
				// https://sourceforge.net/p/keepass/feature-requests/1907/
				if(NativeLib.IsUnix())
				{
					g_obProtectedMemorySupported = false;
					return false;
				}

				ob = false;
				try // Test whether ProtectedMemory is supported
				{
					// BlockSize * 3 in order to test encryption for multiple
					// blocks, but not introduce a power of 2 as factor
					byte[] pb = new byte[ProtectedBinary.BlockSize * 3];
					for(int i = 0; i < pb.Length; ++i) pb[i] = (byte)i;

					ProtectedMemory.Protect(pb, MemoryProtectionScope.SameProcess);

					for(int i = 0; i < pb.Length; ++i)
					{
						if(pb[i] != (byte)i) { ob = true; break; }
					}
				}
				catch(Exception) { } // Windows 98 / ME

				g_obProtectedMemorySupported = ob;
				return ob.Value;
#endif
			}
		}

		private static long g_lCurID = 0;
		private long m_lID;

		private byte[] m_pbData; // Never null

		// The real length of the data; this value can be different from
		// m_pbData.Length, as the length of m_pbData always is a multiple
		// of BlockSize (required for ProtectedMemory)
		private uint m_uDataLen;

		private bool m_bProtected; // Protection requested by the caller

		private PbMemProt m_mp = PbMemProt.None; // Actual protection

		private object m_objSync = new object();

		private static byte[] g_pbKey32 = null;

		/// <summary>
		/// A flag specifying whether the <c>ProtectedBinary</c> object has
		/// turned on memory protection or not.
		/// </summary>
		public bool IsProtected
		{
			get { return m_bProtected; }
		}

		/// <summary>
		/// Length of the stored data.
		/// </summary>
		public uint Length
		{
			get { return m_uDataLen; }
		}

		/// <summary>
		/// Construct a new, empty protected binary data object.
		/// Protection is disabled.
		/// </summary>
		public ProtectedBinary()
		{
			Init(false, MemUtil.EmptyByteArray, 0, 0);
		}

		/// <summary>
		/// Construct a new protected binary data object.
		/// </summary>
		/// <param name="bEnableProtection">If this paremeter is <c>true</c>,
		/// the data will be encrypted in memory. If it is <c>false</c>, the
		/// data is stored in plain-text in the process memory.</param>
		/// <param name="pbData">Value of the protected object.
		/// The input parameter is not modified and
		/// <c>ProtectedBinary</c> doesn't take ownership of the data,
		/// i.e. the caller is responsible for clearing it.</param>
		public ProtectedBinary(bool bEnableProtection, byte[] pbData)
		{
			if(pbData == null) throw new ArgumentNullException("pbData");

			Init(bEnableProtection, pbData, 0, pbData.Length);
		}

		/// <summary>
		/// Construct a new protected binary data object.
		/// </summary>
		/// <param name="bEnableProtection">If this paremeter is <c>true</c>,
		/// the data will be encrypted in memory. If it is <c>false</c>, the
		/// data is stored in plain-text in the process memory.</param>
		/// <param name="pbData">Value of the protected object.
		/// The input parameter is not modified and
		/// <c>ProtectedBinary</c> doesn't take ownership of the data,
		/// i.e. the caller is responsible for clearing it.</param>
		/// <param name="iOffset">Offset for <paramref name="pbData" />.</param>
		/// <param name="cbSize">Size for <paramref name="pbData" />.</param>
		public ProtectedBinary(bool bEnableProtection, byte[] pbData,
			int iOffset, int cbSize)
		{
			Init(bEnableProtection, pbData, iOffset, cbSize);
		}

		/// <summary>
		/// Construct a new protected binary data object. Copy the data from
		/// a <c>XorredBuffer</c> object.
		/// </summary>
		/// <param name="bEnableProtection">Enable protection or not.</param>
		/// <param name="xbProtected"><c>XorredBuffer</c> object used to
		/// initialize the <c>ProtectedBinary</c> object.</param>
		public ProtectedBinary(bool bEnableProtection, XorredBuffer xbProtected)
		{
			Debug.Assert(xbProtected != null);
			if(xbProtected == null) throw new ArgumentNullException("xbProtected");

			byte[] pb = xbProtected.ReadPlainText();
			Init(bEnableProtection, pb, 0, pb.Length);

			if(bEnableProtection) MemUtil.ZeroByteArray(pb);
		}

		private void Init(bool bEnableProtection, byte[] pbData, int iOffset,
			int cbSize)
		{
			if(pbData == null) throw new ArgumentNullException("pbData");
			if(iOffset < 0) throw new ArgumentOutOfRangeException("iOffset");
			if(cbSize < 0) throw new ArgumentOutOfRangeException("cbSize");
			if(iOffset > (pbData.Length - cbSize))
				throw new ArgumentOutOfRangeException("cbSize");

#if KeePassLibSD
			m_lID = ++g_lCurID;
#else
			m_lID = Interlocked.Increment(ref g_lCurID);
#endif

			m_bProtected = bEnableProtection;
			m_uDataLen = (uint)cbSize;

			const int bs = ProtectedBinary.BlockSize;
			int nBlocks = cbSize / bs;
			if((nBlocks * bs) < cbSize) ++nBlocks;
			Debug.Assert((nBlocks * bs) >= cbSize);

			m_pbData = new byte[nBlocks * bs];
			Array.Copy(pbData, iOffset, m_pbData, 0, cbSize);

			Encrypt();
		}

		private void Encrypt()
		{
			Debug.Assert(m_mp == PbMemProt.None);

			// Nothing to do if caller didn't request protection
			if(!m_bProtected) return;

			// ProtectedMemory.Protect throws for data size == 0
			if(m_pbData.Length == 0) return;

			PbCryptDelegate f = g_fExtCrypt;
			if(f != null)
			{
				f(m_pbData, PbCryptFlags.Encrypt, m_lID);

				m_fExtCrypt = f;
				m_mp = PbMemProt.ExtCrypt;
				return;
			}

			if(ProtectedBinary.ProtectedMemorySupported)
			{
				ProtectedMemory.Protect(m_pbData, MemoryProtectionScope.SameProcess);

				m_mp = PbMemProt.ProtectedMemory;
				return;
			}

			byte[] pbKey32 = g_pbKey32;
			if(pbKey32 == null)
			{
				pbKey32 = CryptoRandom.Instance.GetRandomBytes(32);

				byte[] pbUpd = Interlocked.Exchange<byte[]>(ref g_pbKey32, pbKey32);
				if(pbUpd != null) pbKey32 = pbUpd;
			}

			byte[] pbIV = new byte[12];
			MemUtil.UInt64ToBytesEx((ulong)m_lID, pbIV, 4);
			using(ChaCha20Cipher c = new ChaCha20Cipher(pbKey32, pbIV, true))
			{
				c.Encrypt(m_pbData, 0, m_pbData.Length);
			}
			m_mp = PbMemProt.ChaCha20;
		}

		private void Decrypt()
		{
			if(m_pbData.Length == 0) return;

			if(m_mp == PbMemProt.ProtectedMemory)
				ProtectedMemory.Unprotect(m_pbData, MemoryProtectionScope.SameProcess);
			else if(m_mp == PbMemProt.ChaCha20)
			{
				byte[] pbIV = new byte[12];
				MemUtil.UInt64ToBytesEx((ulong)m_lID, pbIV, 4);
				using(ChaCha20Cipher c = new ChaCha20Cipher(g_pbKey32, pbIV, true))
				{
					c.Decrypt(m_pbData, 0, m_pbData.Length);
				}
			}
			else if(m_mp == PbMemProt.ExtCrypt)
				m_fExtCrypt(m_pbData, PbCryptFlags.Decrypt, m_lID);
			else { Debug.Assert(m_mp == PbMemProt.None); }

			m_mp = PbMemProt.None;
		}

		/// <summary>
		/// Get a copy of the protected data as a byte array.
		/// Please note that the returned byte array is not protected and
		/// can therefore been read by any other application.
		/// Make sure that your clear it properly after usage.
		/// </summary>
		/// <returns>Unprotected byte array. This is always a copy of the internal
		/// protected data and can therefore be cleared safely.</returns>
		public byte[] ReadData()
		{
			if(m_uDataLen == 0) return MemUtil.EmptyByteArray;

			byte[] pbReturn = new byte[m_uDataLen];

			lock(m_objSync)
			{
				Decrypt();
				Array.Copy(m_pbData, pbReturn, (int)m_uDataLen);
				Encrypt();
			}

			return pbReturn;
		}

		/// <summary>
		/// Read the protected data and return it protected with a sequence
		/// of bytes generated by a random stream.
		/// </summary>
		/// <param name="crsRandomSource">Random number source.</param>
		public byte[] ReadXorredData(CryptoRandomStream crsRandomSource)
		{
			Debug.Assert(crsRandomSource != null);
			if(crsRandomSource == null) throw new ArgumentNullException("crsRandomSource");

			byte[] pbData = ReadData();
			uint uLen = (uint)pbData.Length;

			byte[] randomPad = crsRandomSource.GetRandomBytes(uLen);
			Debug.Assert(randomPad.Length == pbData.Length);

			for(uint i = 0; i < uLen; ++i)
				pbData[i] ^= randomPad[i];

			return pbData;
		}

		private int? m_hash = null;
		public override int GetHashCode()
		{
			if(m_hash.HasValue) return m_hash.Value;

			int h = (m_bProtected ? 0x7B11D289 : 0);

			byte[] pb = ReadData();
			unchecked
			{
				for(int i = 0; i < pb.Length; ++i)
					h = (h << 3) + h + (int)pb[i];
			}
			MemUtil.ZeroByteArray(pb);

			m_hash = h;
			return h;
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as ProtectedBinary);
		}

		public bool Equals(ProtectedBinary other)
		{
			if(other == null) return false; // No assert

			if(m_bProtected != other.m_bProtected) return false;
			if(m_uDataLen != other.m_uDataLen) return false;

			byte[] pbL = ReadData();
			byte[] pbR = other.ReadData();
			bool bEq = MemUtil.ArraysEqual(pbL, pbR);
			MemUtil.ZeroByteArray(pbL);
			MemUtil.ZeroByteArray(pbR);

#if DEBUG
			if(bEq) { Debug.Assert(GetHashCode() == other.GetHashCode()); }
#endif

			return bEq;
		}
	}
}
