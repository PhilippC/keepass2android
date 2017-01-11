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
using System.Text;

#if !KeePassUAP
using System.Security.Cryptography;
#endif

using KeePassLib.Utility;

namespace KeePassLib.Cryptography
{
	public static class CryptoUtil
	{
		public static byte[] HashSha256(byte[] pbData)
		{
			if(pbData == null) throw new ArgumentNullException("pbData");

			return HashSha256(pbData, 0, pbData.Length);
		}

		public static byte[] HashSha256(byte[] pbData, int iOffset, int cbCount)
		{
			if(pbData == null) throw new ArgumentNullException("pbData");

#if DEBUG
			byte[] pbCopy = new byte[pbData.Length];
			Array.Copy(pbData, pbCopy, pbData.Length);
#endif

			byte[] pbHash;
			using(SHA256Managed h = new SHA256Managed())
			{
				pbHash = h.ComputeHash(pbData, iOffset, cbCount);
			}

#if DEBUG
			// Ensure the data has not been modified
			Debug.Assert(MemUtil.ArraysEqual(pbData, pbCopy));

			Debug.Assert((pbHash != null) && (pbHash.Length == 32));
			byte[] pbZero = new byte[32];
			Debug.Assert(!MemUtil.ArraysEqual(pbHash, pbZero));
#endif

			return pbHash;
		}

		/// <summary>
		/// Create a cryptographic key of length <paramref name="cbOut" />
		/// (in bytes) from <paramref name="pbIn" />.
		/// </summary>
		public static byte[] ResizeKey(byte[] pbIn, int iInOffset,
			int cbIn, int cbOut)
		{
			if(pbIn == null) throw new ArgumentNullException("pbIn");
			if(cbOut < 0) throw new ArgumentOutOfRangeException("cbOut");

			if(cbOut == 0) return MemUtil.EmptyByteArray;

			byte[] pbHash;
			if(cbOut <= 32) pbHash = HashSha256(pbIn, iInOffset, cbIn);
			else
			{
				using(SHA512Managed h = new SHA512Managed())
				{
					pbHash = h.ComputeHash(pbIn, iInOffset, cbIn);
				}
			}

			if(cbOut == pbHash.Length) return pbHash;

			byte[] pbRet = new byte[cbOut];
			if(cbOut < pbHash.Length)
				Array.Copy(pbHash, pbRet, cbOut);
			else
			{
				int iPos = 0;
				ulong r = 0;
				while(iPos < cbOut)
				{
					Debug.Assert(pbHash.Length == 64);
					using(HMACSHA256 h = new HMACSHA256(pbHash))
					{
						byte[] pbR = MemUtil.UInt64ToBytes(r);
						byte[] pbPart = h.ComputeHash(pbR);

						int cbCopy = Math.Min(cbOut - iPos, pbPart.Length);
						Debug.Assert(cbCopy > 0);

						Array.Copy(pbPart, 0, pbRet, iPos, cbCopy);
						iPos += cbCopy;
						++r;

						MemUtil.ZeroByteArray(pbPart);
					}
				}
				Debug.Assert(iPos == cbOut);
			}

#if DEBUG
			byte[] pbZero = new byte[pbHash.Length];
			Debug.Assert(!MemUtil.ArraysEqual(pbHash, pbZero));
#endif
			MemUtil.ZeroByteArray(pbHash);
			return pbRet;
		}
	}
}
