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

#if !KeePassUAP
using System.Security.Cryptography;
#endif

using KeePassLib.Resources;
using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
	public sealed class HmacBlockStream : Stream
	{
		private const int NbDefaultBufferSize = 1024 * 1024; // 1 MB

		private Stream m_sBase;
		private readonly bool m_bWriting;
		private readonly bool m_bVerify;
		private byte[] m_pbKey;

		private bool m_bEos = false;
		private byte[] m_pbBuffer;
		private int m_iBufferPos = 0;

		private ulong m_uBlockIndex = 0;

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

		public HmacBlockStream(Stream sBase, bool bWriting, bool bVerify,
			byte[] pbKey)
		{
			if(sBase == null) throw new ArgumentNullException("sBase");
			if(pbKey == null) throw new ArgumentNullException("pbKey");

			m_sBase = sBase;
			m_bWriting = bWriting;
			m_bVerify = bVerify;
			m_pbKey = pbKey;

			if(!m_bWriting) // Reading mode
			{
				if(!m_sBase.CanRead) throw new InvalidOperationException();

				m_pbBuffer = MemUtil.EmptyByteArray;
			}
			else // Writing mode
			{
				if(!m_sBase.CanWrite) throw new InvalidOperationException();

				m_pbBuffer = new byte[NbDefaultBufferSize];
			}
		}

		protected override void Dispose(bool disposing)
		{
			if(disposing && (m_sBase != null))
			{
				if(m_bWriting)
				{
					if(m_iBufferPos == 0) // No data left in buffer
						WriteSafeBlock(); // Write terminating block
					else
					{
						WriteSafeBlock(); // Write remaining buffered data
						WriteSafeBlock(); // Write terminating block
					}

					Flush();
				}

				m_sBase.Close();
				m_sBase = null;
			}

			base.Dispose(disposing);
		}

		public override void Flush()
		{
			Debug.Assert(m_sBase != null); // Object should not be disposed
			if(m_bWriting && (m_sBase != null)) m_sBase.Flush();
		}

		public override long Seek(long lOffset, SeekOrigin soOrigin)
		{
			Debug.Assert(false);
			throw new NotSupportedException();
		}

		public override void SetLength(long lValue)
		{
			Debug.Assert(false);
			throw new NotSupportedException();
		}

		internal static byte[] GetHmacKey64(byte[] pbKey, ulong uBlockIndex)
		{
			if(pbKey == null) throw new ArgumentNullException("pbKey");
			Debug.Assert(pbKey.Length == 64);

			// We are computing the HMAC using SHA-256, whose internal
			// block size is 512 bits; thus create a key that is 512
			// bits long (using SHA-512)

			byte[] pbBlockKey;
			using(SHA512Managed h = new SHA512Managed())
			{
				byte[] pbIndex = MemUtil.UInt64ToBytes(uBlockIndex);

				h.TransformBlock(pbIndex, 0, pbIndex.Length, pbIndex, 0);
				h.TransformBlock(pbKey, 0, pbKey.Length, pbKey, 0);
				h.TransformFinalBlock(MemUtil.EmptyByteArray, 0, 0);

				pbBlockKey = h.Hash;
			}

#if DEBUG
			byte[] pbZero = new byte[64];
			Debug.Assert((pbBlockKey.Length == 64) && !MemUtil.ArraysEqual(
				pbBlockKey, pbZero)); // Ensure we own pbBlockKey
#endif
			return pbBlockKey;
		}

		public override int Read(byte[] pbBuffer, int iOffset, int nCount)
		{
			if(m_bWriting) throw new InvalidOperationException();

			int nRemaining = nCount;
			while(nRemaining > 0)
			{
				if(m_iBufferPos == m_pbBuffer.Length)
				{
					if(!ReadSafeBlock())
						return (nCount - nRemaining); // Bytes actually read
				}

				int nCopy = Math.Min(m_pbBuffer.Length - m_iBufferPos, nRemaining);
				Debug.Assert(nCopy > 0);

				Array.Copy(m_pbBuffer, m_iBufferPos, pbBuffer, iOffset, nCopy);

				iOffset += nCopy;
				m_iBufferPos += nCopy;

				nRemaining -= nCopy;
			}

			return nCount;
		}

		private bool ReadSafeBlock()
		{
			if(m_bEos) return false; // End of stream reached already

			byte[] pbStoredHmac = MemUtil.Read(m_sBase, 32);
			if((pbStoredHmac == null) || (pbStoredHmac.Length != 32))
				throw new EndOfStreamException(KLRes.FileCorrupted + " " +
					KLRes.FileIncomplete);

			// Block index is implicit: it's used in the HMAC computation,
			// but does not need to be stored
			// byte[] pbBlockIndex = MemUtil.Read(m_sBase, 8);
			// if((pbBlockIndex == null) || (pbBlockIndex.Length != 8))
			//	throw new EndOfStreamException();
			// ulong uBlockIndex = MemUtil.BytesToUInt64(pbBlockIndex);
			// if((uBlockIndex != m_uBlockIndex) && m_bVerify)
			//	throw new InvalidDataException();
			byte[] pbBlockIndex = MemUtil.UInt64ToBytes(m_uBlockIndex);

			byte[] pbBlockSize = MemUtil.Read(m_sBase, 4);
			if((pbBlockSize == null) || (pbBlockSize.Length != 4))
				throw new EndOfStreamException(KLRes.FileCorrupted + " " +
					KLRes.FileIncomplete);
			int nBlockSize = MemUtil.BytesToInt32(pbBlockSize);
			if(nBlockSize < 0)
				throw new InvalidDataException(KLRes.FileCorrupted);

			m_iBufferPos = 0;

			m_pbBuffer = MemUtil.Read(m_sBase, nBlockSize);
			if((m_pbBuffer == null) || ((m_pbBuffer.Length != nBlockSize) && m_bVerify))
				throw new EndOfStreamException(KLRes.FileCorrupted + " " +
					KLRes.FileIncompleteExpc);

			if(m_bVerify)
			{
				byte[] pbCmpHmac;
				byte[] pbBlockKey = GetHmacKey64(m_pbKey, m_uBlockIndex);
				using(HMACSHA256 h = new HMACSHA256(pbBlockKey))
				{
					h.TransformBlock(pbBlockIndex, 0, pbBlockIndex.Length,
						pbBlockIndex, 0);
					h.TransformBlock(pbBlockSize, 0, pbBlockSize.Length,
						pbBlockSize, 0);

					if(m_pbBuffer.Length > 0)
						h.TransformBlock(m_pbBuffer, 0, m_pbBuffer.Length,
							m_pbBuffer, 0);

					h.TransformFinalBlock(MemUtil.EmptyByteArray, 0, 0);

					pbCmpHmac = h.Hash;
				}
				MemUtil.ZeroByteArray(pbBlockKey);

				if(!MemUtil.ArraysEqual(pbCmpHmac, pbStoredHmac))
					throw new InvalidDataException(KLRes.FileCorrupted);
			}

			++m_uBlockIndex;

			if(nBlockSize == 0)
			{
				m_bEos = true;
				return false; // No further data available
			}
			return true;
		}

		public override void Write(byte[] pbBuffer, int iOffset, int nCount)
		{
			if(!m_bWriting) throw new InvalidOperationException();

			while(nCount > 0)
			{
				if(m_iBufferPos == m_pbBuffer.Length)
					WriteSafeBlock();

				int nCopy = Math.Min(m_pbBuffer.Length - m_iBufferPos, nCount);
				Debug.Assert(nCopy > 0);

				Array.Copy(pbBuffer, iOffset, m_pbBuffer, m_iBufferPos, nCopy);

				iOffset += nCopy;
				m_iBufferPos += nCopy;

				nCount -= nCopy;
			}
		}

		private void WriteSafeBlock()
		{
			byte[] pbBlockIndex = MemUtil.UInt64ToBytes(m_uBlockIndex);

			int cbBlockSize = m_iBufferPos;
			byte[] pbBlockSize = MemUtil.Int32ToBytes(cbBlockSize);

			byte[] pbBlockHmac;
			byte[] pbBlockKey = GetHmacKey64(m_pbKey, m_uBlockIndex);
			using(HMACSHA256 h = new HMACSHA256(pbBlockKey))
			{
				h.TransformBlock(pbBlockIndex, 0, pbBlockIndex.Length,
					pbBlockIndex, 0);
				h.TransformBlock(pbBlockSize, 0, pbBlockSize.Length,
					pbBlockSize, 0);

				if(cbBlockSize > 0)
					h.TransformBlock(m_pbBuffer, 0, cbBlockSize, m_pbBuffer, 0);

				h.TransformFinalBlock(MemUtil.EmptyByteArray, 0, 0);

				pbBlockHmac = h.Hash;
			}
			MemUtil.ZeroByteArray(pbBlockKey);

			MemUtil.Write(m_sBase, pbBlockHmac);
			// MemUtil.Write(m_sBase, pbBlockIndex); // Implicit
			MemUtil.Write(m_sBase, pbBlockSize);
			if(cbBlockSize > 0)
				m_sBase.Write(m_pbBuffer, 0, cbBlockSize);

			++m_uBlockIndex;
			m_iBufferPos = 0;
		}
	}
}
