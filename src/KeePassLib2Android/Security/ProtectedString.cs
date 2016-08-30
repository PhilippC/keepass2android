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
using System.Diagnostics;
using System.Text;

using KeePassLib.Cryptography;
using KeePassLib.Utility;

#if KeePassLibSD
using KeePassLibSD;
#endif

// SecureString objects are limited to 65536 characters, don't use

namespace KeePassLib.Security
{
	/// <summary>
	/// Represents an in-memory encrypted string.
	/// <c>ProtectedString</c> objects are immutable and thread-safe.
	/// </summary>
#if (DEBUG && !KeePassLibSD)
	[DebuggerDisplay(@"{ReadString()}")]
#endif
	public sealed class ProtectedString
	{
		// Exactly one of the following will be non-null
		private ProtectedBinary m_pbUtf8 = null;
		private string m_strPlainText = null;

		private bool m_bIsProtected;

		private static readonly ProtectedString m_psEmpty = new ProtectedString();
		public static ProtectedString Empty
		{
			get { return m_psEmpty; }
		}

		/// <summary>
		/// A flag specifying whether the <c>ProtectedString</c> object
		/// has turned on memory protection or not.
		/// </summary>
		public bool IsProtected
		{
			get { return m_bIsProtected; }
		}

		public bool IsEmpty
		{
			get
			{
				ProtectedBinary pBin = m_pbUtf8; // Local ref for thread-safety
				if(pBin != null) return (pBin.Length == 0);

				Debug.Assert(m_strPlainText != null);
				return (m_strPlainText.Length == 0);
			}
		}

		private int m_nCachedLength = -1;
		public int Length
		{
			get
			{
				if(m_nCachedLength >= 0) return m_nCachedLength;

				ProtectedBinary pBin = m_pbUtf8; // Local ref for thread-safety
				if(pBin != null)
				{
					byte[] pbPlain = pBin.ReadData();
					m_nCachedLength = StrUtil.Utf8.GetCharCount(pbPlain);
					MemUtil.ZeroByteArray(pbPlain);
				}
				else
				{
					Debug.Assert(m_strPlainText != null);
					m_nCachedLength = m_strPlainText.Length;
				}

				return m_nCachedLength;
			}
		}

		/// <summary>
		/// Construct a new protected string object. Protection is
		/// disabled.
		/// </summary>
		public ProtectedString()
		{
			Init(false, string.Empty);
		}

		/// <summary>
		/// Construct a new protected string. The string is initialized
		/// to the value supplied in the parameters.
		/// </summary>
		/// <param name="bEnableProtection">If this parameter is <c>true</c>,
		/// the string will be protected in memory (encrypted). If it
		/// is <c>false</c>, the string will be stored as plain-text.</param>
		/// <param name="strValue">The initial string value.</param>
		public ProtectedString(bool bEnableProtection, string strValue)
		{
			Init(bEnableProtection, strValue);
		}

		/// <summary>
		/// Construct a new protected string. The string is initialized
		/// to the value supplied in the parameters (UTF-8 encoded string).
		/// </summary>
		/// <param name="bEnableProtection">If this parameter is <c>true</c>,
		/// the string will be protected in memory (encrypted). If it
		/// is <c>false</c>, the string will be stored as plain-text.</param>
		/// <param name="vUtf8Value">The initial string value, encoded as
		/// UTF-8 byte array. This parameter won't be modified; the caller
		/// is responsible for clearing it.</param>
		public ProtectedString(bool bEnableProtection, byte[] vUtf8Value)
		{
			Init(bEnableProtection, vUtf8Value);
		}

		/// <summary>
		/// Construct a new protected string. The string is initialized
		/// to the value passed in the <c>XorredBuffer</c> object.
		/// </summary>
		/// <param name="bEnableProtection">Enable protection or not.</param>
		/// <param name="xbProtected"><c>XorredBuffer</c> object containing the
		/// string in UTF-8 representation. The UTF-8 string must not
		/// be <c>null</c>-terminated.</param>
		public ProtectedString(bool bEnableProtection, XorredBuffer xbProtected)
		{
			Debug.Assert(xbProtected != null);
			if(xbProtected == null) throw new ArgumentNullException("xbProtected");

			byte[] pb = xbProtected.ReadPlainText();
			Init(bEnableProtection, pb);

			if(bEnableProtection) MemUtil.ZeroByteArray(pb);
		}

		private void Init(bool bEnableProtection, string str)
		{
			if(str == null) throw new ArgumentNullException("str");

			m_bIsProtected = bEnableProtection;

			// The string already is in memory and immutable,
			// protection would be useless
			m_strPlainText = str;
		}

		private void Init(bool bEnableProtection, byte[] pbUtf8)
		{
			if(pbUtf8 == null) throw new ArgumentNullException("pbUtf8");

			m_bIsProtected = bEnableProtection;

			if(bEnableProtection)
				m_pbUtf8 = new ProtectedBinary(true, pbUtf8);
			else
				m_strPlainText = StrUtil.Utf8.GetString(pbUtf8, 0, pbUtf8.Length);
		}

		/// <summary>
		/// Convert the protected string to a normal string object.
		/// Be careful with this function, the returned string object
		/// isn't protected anymore and stored in plain-text in the
		/// process memory.
		/// </summary>
		/// <returns>Plain-text string. Is never <c>null</c>.</returns>
		public string ReadString()
		{
			if(m_strPlainText != null) return m_strPlainText;

			byte[] pb = ReadUtf8();
			string str = ((pb.Length == 0) ? string.Empty :
				StrUtil.Utf8.GetString(pb, 0, pb.Length));
			// No need to clear pb

			// As the text is now visible in process memory anyway,
			// there's no need to protect it anymore
			m_strPlainText = str;
			m_pbUtf8 = null; // Thread-safe order

			return str;
		}

		/// <summary>
		/// Read out the string and return a byte array that contains the
		/// string encoded using UTF-8. The returned string is not protected
		/// anymore!
		/// </summary>
		/// <returns>Plain-text UTF-8 byte array.</returns>
		public byte[] ReadUtf8()
		{
			ProtectedBinary pBin = m_pbUtf8; // Local ref for thread-safety
			if(pBin != null) return pBin.ReadData();

			return StrUtil.Utf8.GetBytes(m_strPlainText);
		}

		/// <summary>
		/// Read the protected string and return it protected with a sequence
		/// of bytes generated by a random stream.
		/// </summary>
		/// <param name="crsRandomSource">Random number source.</param>
		/// <returns>Protected string.</returns>
		public byte[] ReadXorredString(CryptoRandomStream crsRandomSource)
		{
			Debug.Assert(crsRandomSource != null); if(crsRandomSource == null) throw new ArgumentNullException("crsRandomSource");

			byte[] pbData = ReadUtf8();
			uint uLen = (uint)pbData.Length;

			byte[] randomPad = crsRandomSource.GetRandomBytes(uLen);
			Debug.Assert(randomPad.Length == pbData.Length);

			for(uint i = 0; i < uLen; ++i)
				pbData[i] ^= randomPad[i];

			return pbData;
		}

		public ProtectedString WithProtection(bool bProtect)
		{
			if(bProtect == m_bIsProtected) return this;

			byte[] pb = ReadUtf8();
			ProtectedString ps = new ProtectedString(bProtect, pb);

			if(bProtect) MemUtil.ZeroByteArray(pb);
			return ps;
		}

		public ProtectedString Insert(int iStart, string strInsert)
		{
			if(iStart < 0) throw new ArgumentOutOfRangeException("iStart");
			if(strInsert == null) throw new ArgumentNullException("strInsert");
			if(strInsert.Length == 0) return this;

			// Only operate directly with strings when m_bIsProtected is
			// false, not in the case of non-null m_strPlainText, because
			// the operation creates a new sequence in memory
			if(!m_bIsProtected)
				return new ProtectedString(false, ReadString().Insert(
					iStart, strInsert));

			UTF8Encoding utf8 = StrUtil.Utf8;

			byte[] pb = ReadUtf8();
			char[] v = utf8.GetChars(pb);
			char[] vNew;

			try
			{
				if(iStart > v.Length)
					throw new ArgumentOutOfRangeException("iStart");

				char[] vIns = strInsert.ToCharArray();

				vNew = new char[v.Length + vIns.Length];
				Array.Copy(v, 0, vNew, 0, iStart);
				Array.Copy(vIns, 0, vNew, iStart, vIns.Length);
				Array.Copy(v, iStart, vNew, iStart + vIns.Length,
					v.Length - iStart);
			}
			finally
			{
				Array.Clear(v, 0, v.Length);
				MemUtil.ZeroByteArray(pb);
			}

			byte[] pbNew = utf8.GetBytes(vNew);
			ProtectedString ps = new ProtectedString(m_bIsProtected, pbNew);

			Debug.Assert(utf8.GetString(pbNew, 0, pbNew.Length) ==
				ReadString().Insert(iStart, strInsert));

			Array.Clear(vNew, 0, vNew.Length);
			MemUtil.ZeroByteArray(pbNew);
			return ps;
		}

		public ProtectedString Remove(int iStart, int nCount)
		{
			if(iStart < 0) throw new ArgumentOutOfRangeException("iStart");
			if(nCount < 0) throw new ArgumentOutOfRangeException("nCount");
			if(nCount == 0) return this;

			// Only operate directly with strings when m_bIsProtected is
			// false, not in the case of non-null m_strPlainText, because
			// the operation creates a new sequence in memory
			if(!m_bIsProtected)
				return new ProtectedString(false, ReadString().Remove(
					iStart, nCount));

			UTF8Encoding utf8 = StrUtil.Utf8;

			byte[] pb = ReadUtf8();
			char[] v = utf8.GetChars(pb);
			char[] vNew;

			try
			{
				if((iStart + nCount) > v.Length)
					throw new ArgumentException("iStart + nCount");

				vNew = new char[v.Length - nCount];
				Array.Copy(v, 0, vNew, 0, iStart);
				Array.Copy(v, iStart + nCount, vNew, iStart, v.Length -
					(iStart + nCount));
			}
			finally
			{
				Array.Clear(v, 0, v.Length);
				MemUtil.ZeroByteArray(pb);
			}

			byte[] pbNew = utf8.GetBytes(vNew);
			ProtectedString ps = new ProtectedString(m_bIsProtected, pbNew);

			Debug.Assert(utf8.GetString(pbNew, 0, pbNew.Length) ==
				ReadString().Remove(iStart, nCount));

			Array.Clear(vNew, 0, vNew.Length);
			MemUtil.ZeroByteArray(pbNew);
			return ps;
		}
	}
}
