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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

using KeePassLib.Resources;

namespace KeePassLib.Cryptography.Cipher
{
	public sealed class ChaCha20Engine : ICipherEngine2
	{
		private PwUuid m_uuid = new PwUuid(new byte[] {
			0xD6, 0x03, 0x8A, 0x2B, 0x8B, 0x6F, 0x4C, 0xB5,
			0xA5, 0x24, 0x33, 0x9A, 0x31, 0xDB, 0xB5, 0x9A
		});

		public PwUuid CipherUuid
		{
			get { return m_uuid; }
		}

		public string DisplayName
		{
			get
			{
				return ("ChaCha20 (" + KLRes.KeyBits.Replace(@"{PARAM}",
					"256") + ", RFC 7539)");
			}
		}

		public int KeyLength
		{
			get { return 32; }
		}

		public int IVLength
		{
			get { return 12; } // 96 bits
		}

		public Stream EncryptStream(Stream sPlainText, byte[] pbKey, byte[] pbIV)
		{
			return new ChaCha20Stream(sPlainText, true, pbKey, pbIV);
		}

		public Stream DecryptStream(Stream sEncrypted, byte[] pbKey, byte[] pbIV)
		{
			return new ChaCha20Stream(sEncrypted, false, pbKey, pbIV);
		}
	}

	internal sealed class ChaCha20Stream : Stream
	{
		private Stream m_sBase;
		private readonly bool m_bWriting;
		private ChaCha20Cipher m_c;

		private byte[] m_pbBuffer = null;

		public override bool CanRead
		{
			get { return !m_bWriting; }
		}

		public override bool CanSeek
		{
			get { return false; }
		}

		public override bool CanWrite
		{
			get { return m_bWriting; }
		}

		public override long Length
		{
			get { Debug.Assert(false); throw new NotSupportedException(); }
		}

		public override long Position
		{
			get { Debug.Assert(false); throw new NotSupportedException(); }
			set { Debug.Assert(false); throw new NotSupportedException(); }
		}

		public ChaCha20Stream(Stream sBase, bool bWriting, byte[] pbKey32,
			byte[] pbIV12)
		{
			if(sBase == null) throw new ArgumentNullException("sBase");

			m_sBase = sBase;
			m_bWriting = bWriting;
			m_c = new ChaCha20Cipher(pbKey32, pbIV12);
		}

		protected override void Dispose(bool bDisposing)
		{
			if(bDisposing)
			{
				if(m_sBase != null)
				{
					m_c.Dispose();
					m_c = null;

					m_sBase.Close();
					m_sBase = null;
				}

				m_pbBuffer = null;
			}

			base.Dispose(bDisposing);
		}

		public override void Flush()
		{
			Debug.Assert(m_sBase != null);
			if(m_bWriting && (m_sBase != null)) m_sBase.Flush();
		}

		public override long Seek(long lOffset, SeekOrigin soOrigin)
		{
			Debug.Assert(false);
			throw new NotImplementedException();
		}

		public override void SetLength(long lValue)
		{
			Debug.Assert(false);
			throw new NotImplementedException();
		}

		public override int Read(byte[] pbBuffer, int iOffset, int nCount)
		{
			if(m_bWriting) throw new InvalidOperationException();

			int cbRead = m_sBase.Read(pbBuffer, iOffset, nCount);
			m_c.Decrypt(pbBuffer, iOffset, cbRead);
			return cbRead;
		}

		public override void Write(byte[] pbBuffer, int iOffset, int nCount)
		{
			if(nCount < 0) throw new ArgumentOutOfRangeException("nCount");
			if(nCount == 0) return;

			if(!m_bWriting) throw new InvalidOperationException();

			if((m_pbBuffer == null) || (m_pbBuffer.Length < nCount))
				m_pbBuffer = new byte[nCount];
			Array.Copy(pbBuffer, iOffset, m_pbBuffer, 0, nCount);

			m_c.Encrypt(m_pbBuffer, 0, nCount);
			m_sBase.Write(m_pbBuffer, 0, nCount);
		}
	}
}
