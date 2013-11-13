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
using System.IO;

namespace KeePassLib.Cryptography.Cipher
{
	/// <summary>
	/// Interface of an encryption/decryption class.
	/// </summary>
	public interface ICipherEngine
	{
		/// <summary>
		/// UUID of the engine. If you want to write an engine/plugin,
		/// please contact the KeePass team to obtain a new UUID.
		/// </summary>
		PwUuid CipherUuid
		{
			get;
		}

		/// <summary>
		/// String displayed in the list of available encryption/decryption
		/// engines in the GUI.
		/// </summary>
		string DisplayName
		{
			get;
		}

		/// <summary>
		/// Encrypt a stream.
		/// </summary>
		/// <param name="sPlainText">Stream to read the plain-text from.</param>
		/// <param name="pbKey">Key to use.</param>
		/// <param name="pbIV">Initialization vector.</param>
		/// <returns>Stream, from which the encrypted data can be read.</returns>
		Stream EncryptStream(Stream sPlainText, byte[] pbKey, byte[] pbIV);

		/// <summary>
		/// Decrypt a stream.
		/// </summary>
		/// <param name="sEncrypted">Stream to read the encrypted data from.</param>
		/// <param name="pbKey">Key to use.</param>
		/// <param name="pbIV">Initialization vector.</param>
		/// <returns>Stream, from which the decrypted data can be read.</returns>
		Stream DecryptStream(Stream sEncrypted, byte[] pbKey, byte[] pbIV);
	}
}
