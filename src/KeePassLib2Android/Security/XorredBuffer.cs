/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2021 Dominik Reichl <dominik.reichl@t-online.de>

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

using KeePassLib.Utility;

namespace KeePassLib.Security
{
	/// <summary>
	/// A <c>XorredBuffer</c> object stores data that is encrypted
	/// using a XOR pad.
	/// </summary>
	public sealed class XorredBuffer : IDisposable
	{
		private byte[] m_pbCT;
		private byte[] m_pbXorPad;

		public uint Length
		{
			get
			{
				if (m_pbCT == null) { Debug.Assert(false); throw new ObjectDisposedException(null); }
				return (uint)m_pbCT.Length;
			}
		}

		/// <summary>
		/// Construct a new <c>XorredBuffer</c> object.
		/// The <paramref name="pbCT" /> byte array must have the same
		/// length as the <paramref name="pbXorPad" /> byte array.
		/// The <c>XorredBuffer</c> object takes ownership of the two byte
		/// arrays, i.e. the caller must not use them afterwards.
		/// </summary>
		/// <param name="pbCT">Data with XOR pad applied.</param>
		/// <param name="pbXorPad">XOR pad that can be used to decrypt the
		/// <paramref name="pbCT" /> byte array.</param>
		public XorredBuffer(byte[] pbCT, byte[] pbXorPad)
		{
			if (pbCT == null) { Debug.Assert(false); throw new ArgumentNullException("pbCT"); }
			if (pbXorPad == null) { Debug.Assert(false); throw new ArgumentNullException("pbXorPad"); }
			if (pbCT.Length != pbXorPad.Length)
			{
				Debug.Assert(false);
				throw new ArgumentOutOfRangeException("pbXorPad");
			}

			m_pbCT = pbCT;
			m_pbXorPad = pbXorPad;
		}

#if DEBUG
		~XorredBuffer()
		{
			Debug.Assert((m_pbCT == null) && (m_pbXorPad == null));
		}
#endif

		public void Dispose()
		{
			if (m_pbCT == null) return;

			MemUtil.ZeroByteArray(m_pbCT);
			m_pbCT = null;

			MemUtil.ZeroByteArray(m_pbXorPad);
			m_pbXorPad = null;
		}

		/// <summary>
		/// Get a copy of the plain-text. The caller is responsible
		/// for clearing the byte array safely after using it.
		/// </summary>
		/// <returns>Plain-text byte array.</returns>
		public byte[] ReadPlainText()
		{
			byte[] pbCT = m_pbCT, pbX = m_pbXorPad;
			if ((pbCT == null) || (pbX == null) || (pbCT.Length != pbX.Length))
			{
				Debug.Assert(false);
				throw new ObjectDisposedException(null);
			}

			byte[] pbPT = new byte[pbCT.Length];

			for (int i = 0; i < pbPT.Length; ++i)
				pbPT[i] = (byte)(pbCT[i] ^ pbX[i]);

			return pbPT;
		}
	}
}
