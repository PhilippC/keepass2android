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
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security;
using System.Diagnostics;

#if !KeePassRT
using System.Security.Cryptography;
#else
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.IO;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
#endif

using KeePassLib.Resources;

namespace KeePassLib.Cryptography.Cipher
{
	/// <summary>
	/// Standard AES cipher implementation.
	/// </summary>
	public sealed class StandardAesEngine : ICipherEngine
	{
#if !KeePassRT
		private const CipherMode m_rCipherMode = CipherMode.CBC;
		private const PaddingMode m_rCipherPadding = PaddingMode.PKCS7;
#endif

		private static PwUuid m_uuidAes = null;

		/// <summary>
		/// UUID of the cipher engine. This ID uniquely identifies the
		/// AES engine. Must not be used by other ciphers.
		/// </summary>
		public static PwUuid AesUuid
		{
			get
			{
				if(m_uuidAes == null)
				{
					m_uuidAes = new PwUuid(new byte[]{
						0x31, 0xC1, 0xF2, 0xE6, 0xBF, 0x71, 0x43, 0x50,
						0xBE, 0x58, 0x05, 0x21, 0x6A, 0xFC, 0x5A, 0xFF });
				}

				return m_uuidAes;
			}
		}

		/// <summary>
		/// Get the UUID of this cipher engine as <c>PwUuid</c> object.
		/// </summary>
		public PwUuid CipherUuid
		{
			get { return StandardAesEngine.AesUuid; }
		}

		/// <summary>
		/// Get a displayable name describing this cipher engine.
		/// </summary>
		public string DisplayName { get { return KLRes.EncAlgorithmAes; } }

		private static void ValidateArguments(Stream stream, bool bEncrypt, byte[] pbKey, byte[] pbIV)
		{
			Debug.Assert(stream != null); if(stream == null) throw new ArgumentNullException("stream");

			Debug.Assert(pbKey != null); if(pbKey == null) throw new ArgumentNullException("pbKey");
			Debug.Assert(pbKey.Length == 32);
			if(pbKey.Length != 32) throw new ArgumentException("Key must be 256 bits wide!");

			Debug.Assert(pbIV != null); if(pbIV == null) throw new ArgumentNullException("pbIV");
			Debug.Assert(pbIV.Length == 16);
			if(pbIV.Length != 16) throw new ArgumentException("Initialization vector must be 128 bits wide!");

			if(bEncrypt)
			{
				Debug.Assert(stream.CanWrite);
				if(stream.CanWrite == false) throw new ArgumentException("Stream must be writable!");
			}
			else // Decrypt
			{
				Debug.Assert(stream.CanRead);
				if(stream.CanRead == false) throw new ArgumentException("Encrypted stream must be readable!");
			}
		}

		private static Stream CreateStream(Stream s, bool bEncrypt, byte[] pbKey, byte[] pbIV)
		{
			StandardAesEngine.ValidateArguments(s, bEncrypt, pbKey, pbIV);

			byte[] pbLocalIV = new byte[16];
			Array.Copy(pbIV, pbLocalIV, 16);

			byte[] pbLocalKey = new byte[32];
			Array.Copy(pbKey, pbLocalKey, 32);

#if !KeePassRT
			RijndaelManaged r = new RijndaelManaged();
			if(r.BlockSize != 128) // AES block size
			{
				Debug.Assert(false);
				r.BlockSize = 128;
			}

			r.IV = pbLocalIV;
			r.KeySize = 256;
			r.Key = pbLocalKey;
			r.Mode = m_rCipherMode;
			r.Padding = m_rCipherPadding;

			ICryptoTransform iTransform = (bEncrypt ? r.CreateEncryptor() : r.CreateDecryptor());
			Debug.Assert(iTransform != null);
			if(iTransform == null) throw new SecurityException("Unable to create Rijndael transform!");

			return new CryptoStream(s, iTransform, bEncrypt ? CryptoStreamMode.Write :
				CryptoStreamMode.Read);
#else
			AesEngine aes = new AesEngine();
			CbcBlockCipher cbc = new CbcBlockCipher(aes);
			PaddedBufferedBlockCipher bc = new PaddedBufferedBlockCipher(cbc,
				new Pkcs7Padding());
			KeyParameter kp = new KeyParameter(pbLocalKey);
			ParametersWithIV prmIV = new ParametersWithIV(kp, pbLocalIV);
			bc.Init(bEncrypt, prmIV);

			IBufferedCipher cpRead = (bEncrypt ? null : bc);
			IBufferedCipher cpWrite = (bEncrypt ? bc : null);
			return new CipherStream(s, cpRead, cpWrite);
#endif
		}

		public Stream EncryptStream(Stream sPlainText, byte[] pbKey, byte[] pbIV)
		{
			return StandardAesEngine.CreateStream(sPlainText, true, pbKey, pbIV);
		}

		public Stream DecryptStream(Stream sEncrypted, byte[] pbKey, byte[] pbIV)
		{
			return StandardAesEngine.CreateStream(sEncrypted, false, pbKey, pbIV);
		}
	}
}
