/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2013 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Security.Cryptography;
using System.Diagnostics;

using KeePassLib.Cryptography;
using KeePassLib.Utility;

#if KeePassLibSD
using KeePassLibSD;
#endif

namespace KeePassLib.Security
{
	/// <summary>
	/// Represents a protected binary, i.e. a byte array that is encrypted
	/// in memory. A <c>ProtectedBinary</c> object is immutable and
	/// thread-safe.
	/// </summary>
	public sealed class ProtectedBinary : IEquatable<ProtectedBinary>
	{
		private const int PmBlockSize = 16;

		// In-memory protection is supported only on Windows 2000 SP3 and
		// higher.
		private static bool m_bProtectionSupported;

		private byte[] m_pbData; // Never null

		// The real length of the data. This value can be different than
		// m_pbData.Length, as the length of m_pbData always is a multiple
		// of PmBlockSize (required for fast in-memory protection).
		private uint m_uDataLen;

		private bool m_bProtected;

		private object m_objSync = new object();

		/// <summary>
		/// A flag specifying whether the <c>ProtectedBinary</c> object has
		/// turned on in-memory protection or not.
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

		static ProtectedBinary()
		{
			try // Test whether ProtectedMemory is supported
			{
				byte[] pbDummy = new byte[PmBlockSize * 2];
				ProtectedMemory.Protect(pbDummy, MemoryProtectionScope.SameProcess);
				m_bProtectionSupported = true;
			}
			catch(Exception) // Windows 98 / ME
			{
				m_bProtectionSupported = false;
			}
		}

		/// <summary>
		/// Construct a new, empty protected binary data object. Protection
		/// is disabled.
		/// </summary>
		public ProtectedBinary()
		{
			Init(false, new byte[0]);
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
			Init(bEnableProtection, pbData);
		}

		/// <summary>
		/// Construct a new protected binary data object. Copy the data from
		/// a <c>XorredBuffer</c> object.
		/// </summary>
		/// <param name="bEnableProtection">Enable protection or not.</param>
		/// <param name="xbProtected"><c>XorredBuffer</c> object used to
		/// initialize the <c>ProtectedBinary</c> object.</param>
		/// <exception cref="System.ArgumentNullException">Thrown if the input
		/// parameter is <c>null</c>.</exception>
		public ProtectedBinary(bool bEnableProtection, XorredBuffer xbProtected)
		{
			Debug.Assert(xbProtected != null); if(xbProtected == null) throw new ArgumentNullException("xbProtected");

			byte[] pb = xbProtected.ReadPlainText();
			Init(bEnableProtection, pb);
			MemUtil.ZeroByteArray(pb);
		}

		private void Init(bool bEnableProtection, byte[] pbData)
		{
			if(pbData == null) throw new ArgumentNullException("pbData");

			m_bProtected = bEnableProtection;
			m_uDataLen = (uint)pbData.Length;

			int nBlocks = (int)m_uDataLen / PmBlockSize;
			if((nBlocks * PmBlockSize) < (int)m_uDataLen) ++nBlocks;
			Debug.Assert((nBlocks * PmBlockSize) >= (int)m_uDataLen);

			m_pbData = new byte[nBlocks * PmBlockSize];
			Array.Copy(pbData, m_pbData, (int)m_uDataLen);

			// Data size must be > 0, otherwise 'Protect' throws
			if(m_bProtected && m_bProtectionSupported && (m_uDataLen > 0))
				ProtectedMemory.Protect(m_pbData, MemoryProtectionScope.SameProcess);
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
			if(m_uDataLen == 0) return new byte[0];

			byte[] pbReturn = new byte[m_uDataLen];

			if(m_bProtected && m_bProtectionSupported)
			{
				lock(m_objSync)
				{
					ProtectedMemory.Unprotect(m_pbData, MemoryProtectionScope.SameProcess);
					Array.Copy(m_pbData, pbReturn, (int)m_uDataLen);
					ProtectedMemory.Protect(m_pbData, MemoryProtectionScope.SameProcess);
				}
			}
			else Array.Copy(m_pbData, pbReturn, (int)m_uDataLen);

			return pbReturn;
		}

		/// <summary>
		/// Read the protected data and return it protected with a sequence
		/// of bytes generated by a random stream.
		/// </summary>
		/// <param name="crsRandomSource">Random number source.</param>
		/// <returns>Protected data.</returns>
		/// <exception cref="System.ArgumentNullException">Thrown if the input
		/// parameter is <c>null</c>.</exception>
		public byte[] ReadXorredData(CryptoRandomStream crsRandomSource)
		{
			Debug.Assert(crsRandomSource != null);
			if(crsRandomSource == null) throw new ArgumentNullException("crsRandomSource");

			byte[] pbData = ReadData();
			uint uLen = (uint)pbData.Length;

			byte[] randomPad = crsRandomSource.GetRandomBytes(uLen);
			Debug.Assert(randomPad.Length == uLen);

			for(uint i = 0; i < uLen; ++i)
				pbData[i] ^= randomPad[i];

			return pbData;
		}

		public override int GetHashCode()
		{
			int h = (m_bProtected ? 0x7B11D289 : 0);

			byte[] pb = ReadData();
			unchecked
			{
				for(int i = 0; i < pb.Length; ++i)
					h = (h << 3) + h + (int)pb[i];
			}
			MemUtil.ZeroByteArray(pb);

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
