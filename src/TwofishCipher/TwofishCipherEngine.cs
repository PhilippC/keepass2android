/*
  Twofish Cipher for KeePass Password Safe
  Copyright (C) 2009-2010 SEG Tech <me@gogogadgetscott.info>

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
using System.Security.Cryptography;
using System.Diagnostics;

using KeePassLib;
using KeePassLib.Cryptography.Cipher;

using TwofishCipher.Crypto;
	
namespace TwofishCipher
{
	public sealed class TwofishCipherEngine : ICipherEngine
	{
		private const CipherMode m_rCipherMode = CipherMode.CBC;
		private const PaddingMode m_rCipherPadding = PaddingMode.PKCS7;
		
		private PwUuid m_uuidCipher;

		private static readonly byte[] TwofishCipherUuidBytes = new byte[]{
			0xAD, 0x68, 0xF2, 0x9F, 0x57, 0x6F, 0x4B, 0xB9, 
			0xA3, 0x6A, 0xD4, 0x7A, 0xF9, 0x65, 0x34, 0x6C
		};
		
		public TwofishCipherEngine()
		{
			m_uuidCipher = new PwUuid(TwofishCipherUuidBytes);
		}

		public PwUuid CipherUuid
		{
			get
			{
				Debug.Assert(m_uuidCipher != null);
				return m_uuidCipher;
			}
		}

		public string DisplayName
		{
			get { return "Twofish (256-Bit Key)"; }
		}

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
			ValidateArguments(s, bEncrypt, pbKey, pbIV);

			Twofish f = new Twofish();
		
			byte[] pbLocalIV = new byte[16];
			Array.Copy(pbIV, pbLocalIV, 16);
			f.IV = pbLocalIV;

			byte[] pbLocalKey = new byte[32];
			Array.Copy(pbKey, pbLocalKey, 32);
			f.KeySize = 256;
			f.Key = pbLocalKey;

			f.Mode = m_rCipherMode;
			f.Padding = m_rCipherPadding;

			ICryptoTransform iTransform = (bEncrypt ? f.CreateEncryptor() : f.CreateDecryptor());
			Debug.Assert(iTransform != null);
			if(iTransform == null) throw new SecurityException("Unable to create Twofish transform!");

			return new CryptoStream(s, iTransform, bEncrypt ? CryptoStreamMode.Write :
				CryptoStreamMode.Read);
		}

		public Stream EncryptStream(Stream sPlainText, byte[] pbKey, byte[] pbIV)
		{
			return CreateStream(sPlainText, true, pbKey, pbIV);
		}

		public Stream DecryptStream(Stream sEncrypted, byte[] pbKey, byte[] pbIV)
		{
			return CreateStream(sEncrypted, false, pbKey, pbIV);
		}
		
	}
}
