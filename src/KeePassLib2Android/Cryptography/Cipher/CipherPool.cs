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
using System.Text;
using System.IO;
using System.Diagnostics;

namespace KeePassLib.Cryptography.Cipher
{
	/// <summary>
	/// Pool of encryption/decryption algorithms (ciphers).
	/// </summary>
	public sealed class CipherPool
	{
		private List<ICipherEngine> m_vCiphers = new List<ICipherEngine>();
		private static CipherPool m_poolGlobal = null;

		/// <summary>
		/// Reference to the global cipher pool.
		/// </summary>
		public static CipherPool GlobalPool
		{
			get
			{
				if(m_poolGlobal != null) return m_poolGlobal;

				m_poolGlobal = new CipherPool();
				m_poolGlobal.AddCipher(new StandardAesEngine());

				return m_poolGlobal;
			}
		}

		/// <summary>
		/// Remove all cipher engines from the current pool.
		/// </summary>
		public void Clear()
		{
			m_vCiphers.Clear();
		}

		/// <summary>
		/// Add a cipher engine to the pool.
		/// </summary>
		/// <param name="csEngine">Cipher engine to add. Must not be <c>null</c>.</param>
		public void AddCipher(ICipherEngine csEngine)
		{
			Debug.Assert(csEngine != null);
			if(csEngine == null) throw new ArgumentNullException("csEngine");

			// Return if a cipher with that ID is registered already.
			for(int i = 0; i < m_vCiphers.Count; ++i)
				if(m_vCiphers[i].CipherUuid.Equals(csEngine.CipherUuid))
					return;

			m_vCiphers.Add(csEngine);
		}

		/// <summary>
		/// Get a cipher identified by its UUID.
		/// </summary>
		/// <param name="uuidCipher">UUID of the cipher to return.</param>
		/// <returns>Reference to the requested cipher. If the cipher is
		/// not found, <c>null</c> is returned.</returns>
		public ICipherEngine GetCipher(PwUuid uuidCipher)
		{
			foreach(ICipherEngine iEngine in m_vCiphers)
			{
				if(iEngine.CipherUuid.Equals(uuidCipher))
					return iEngine;
			}

			return null;
		}

		/// <summary>
		/// Get the index of a cipher. This index is temporary and should
		/// not be stored or used to identify a cipher.
		/// </summary>
		/// <param name="uuidCipher">UUID of the cipher.</param>
		/// <returns>Index of the requested cipher. Returns <c>-1</c> if
		/// the specified cipher is not found.</returns>
		public int GetCipherIndex(PwUuid uuidCipher)
		{
			for(int i = 0; i < m_vCiphers.Count; ++i)
			{
				if(m_vCiphers[i].CipherUuid.Equals(uuidCipher))
					return i;
			}

			Debug.Assert(false);
			return -1;
		}

		/// <summary>
		/// Get the index of a cipher. This index is temporary and should
		/// not be stored or used to identify a cipher.
		/// </summary>
		/// <param name="strDisplayName">Name of the cipher. Note that
		/// multiple ciphers can have the same name. In this case, the
		/// first matching cipher is returned.</param>
		/// <returns>Cipher with the specified name or <c>-1</c> if
		/// no cipher with that name is found.</returns>
		public int GetCipherIndex(string strDisplayName)
		{
			for(int i = 0; i < m_vCiphers.Count; ++i)
				if(m_vCiphers[i].DisplayName == strDisplayName)
					return i;

			Debug.Assert(false);
			return -1;
		}

		/// <summary>
		/// Get the number of cipher engines in this pool.
		/// </summary>
		public int EngineCount
		{
			get { return m_vCiphers.Count; }
		}

		/// <summary>
		/// Get the cipher engine at the specified position. Throws
		/// an exception if the index is invalid. You can use this
		/// to iterate over all ciphers, but do not use it to
		/// identify ciphers.
		/// </summary>
		/// <param name="nIndex">Index of the requested cipher engine.</param>
		/// <returns>Reference to the cipher engine at the specified
		/// position.</returns>
		public ICipherEngine this[int nIndex]
		{
			get
			{
				if((nIndex < 0) || (nIndex >= m_vCiphers.Count))
					throw new ArgumentOutOfRangeException("nIndex");

				return m_vCiphers[nIndex];
			}
		}
	}
}
