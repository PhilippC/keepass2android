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

// This implementation is based on the official reference C
// implementation by Daniel Dinu and Dmitry Khovratovich (CC0 1.0).

// Relative iterations (* = B2ROUND_ARRAYS \\ G_INLINED):
//     * | false true
// ------+-----------
// false |  8885 9618
//  true |  9009 9636
#define ARGON2_B2ROUND_ARRAYS
#define ARGON2_G_INLINED

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using KeePassLib.Cryptography.Hash;
using KeePassLib.Utility;

namespace KeePassLib.Cryptography.KeyDerivation
{
	public sealed partial class Argon2Kdf : KdfEngine
	{
		private const ulong NbBlockSize = 1024;
		private const ulong NbBlockSizeInQW = NbBlockSize / 8UL;
		private const ulong NbSyncPoints = 4;

		private const int NbPreHashDigestLength = 64;
		private const int NbPreHashSeedLength = NbPreHashDigestLength + 8;

#if ARGON2_B2ROUND_ARRAYS
		private static int[][] g_vFBCols = null;
		private static int[][] g_vFBRows = null;
#endif

		private sealed class Argon2Ctx
		{
			public uint Version = 0;

			public ulong Lanes = 0;
			public ulong TCost = 0;
			public ulong MCost = 0;
			public ulong MemoryBlocks = 0;
			public ulong SegmentLength = 0;
			public ulong LaneLength = 0;

			public ulong[] Mem = null;
		}

		private sealed class Argon2ThreadInfo
		{
			public Argon2Ctx Context = null;
			public ManualResetEvent Finished = new ManualResetEvent(false);

			public ulong Pass = 0;
			public ulong Lane = 0;
			public ulong Slice = 0;
			public ulong Index = 0;

			public void Release()
			{
				if(this.Finished != null)
				{
					this.Finished.Close();
					this.Finished = null;
				}
				else { Debug.Assert(false); }
			}
		}

		private static byte[] Argon2d(byte[] pbMsg, byte[] pbSalt, uint uParallel,
			ulong uMem, ulong uIt, int cbOut, uint uVersion, byte[] pbSecretKey,
			byte[] pbAssocData)
		{
			pbSecretKey = (pbSecretKey ?? MemUtil.EmptyByteArray);
			pbAssocData = (pbAssocData ?? MemUtil.EmptyByteArray);

#if ARGON2_B2ROUND_ARRAYS
			InitB2RoundIndexArrays();
#endif

			Argon2Ctx ctx = new Argon2Ctx();
			ctx.Version = uVersion;

			ctx.Lanes = uParallel;
			ctx.TCost = uIt;
			ctx.MCost = uMem / NbBlockSize;
			ctx.MemoryBlocks = Math.Max(ctx.MCost, 2UL * NbSyncPoints * ctx.Lanes);

			ctx.SegmentLength = ctx.MemoryBlocks / (ctx.Lanes * NbSyncPoints);
			ctx.MemoryBlocks = ctx.SegmentLength * ctx.Lanes * NbSyncPoints;

			ctx.LaneLength = ctx.SegmentLength * NbSyncPoints;

			Debug.Assert(NbBlockSize == (NbBlockSizeInQW *
#if KeePassUAP
				(ulong)Marshal.SizeOf<ulong>()
#else
				(ulong)Marshal.SizeOf(typeof(ulong))
#endif
				));
			ctx.Mem = new ulong[ctx.MemoryBlocks * NbBlockSizeInQW];

			Blake2b h = new Blake2b();

			// Initial hash
			Debug.Assert(h.HashSize == (NbPreHashDigestLength * 8));
			byte[] pbBuf = new byte[4];
			MemUtil.UInt32ToBytesEx(uParallel, pbBuf, 0);
			h.TransformBlock(pbBuf, 0, pbBuf.Length, pbBuf, 0);
			MemUtil.UInt32ToBytesEx((uint)cbOut, pbBuf, 0);
			h.TransformBlock(pbBuf, 0, pbBuf.Length, pbBuf, 0);
			MemUtil.UInt32ToBytesEx((uint)ctx.MCost, pbBuf, 0);
			h.TransformBlock(pbBuf, 0, pbBuf.Length, pbBuf, 0);
			MemUtil.UInt32ToBytesEx((uint)uIt, pbBuf, 0);
			h.TransformBlock(pbBuf, 0, pbBuf.Length, pbBuf, 0);
			MemUtil.UInt32ToBytesEx(uVersion, pbBuf, 0);
			h.TransformBlock(pbBuf, 0, pbBuf.Length, pbBuf, 0);
			MemUtil.UInt32ToBytesEx(0, pbBuf, 0); // Argon2d type = 0
			h.TransformBlock(pbBuf, 0, pbBuf.Length, pbBuf, 0);
			MemUtil.UInt32ToBytesEx((uint)pbMsg.Length, pbBuf, 0);
			h.TransformBlock(pbBuf, 0, pbBuf.Length, pbBuf, 0);
			h.TransformBlock(pbMsg, 0, pbMsg.Length, pbMsg, 0);
			MemUtil.UInt32ToBytesEx((uint)pbSalt.Length, pbBuf, 0);
			h.TransformBlock(pbBuf, 0, pbBuf.Length, pbBuf, 0);
			h.TransformBlock(pbSalt, 0, pbSalt.Length, pbSalt, 0);
			MemUtil.UInt32ToBytesEx((uint)pbSecretKey.Length, pbBuf, 0);
			h.TransformBlock(pbBuf, 0, pbBuf.Length, pbBuf, 0);
			h.TransformBlock(pbSecretKey, 0, pbSecretKey.Length, pbSecretKey, 0);
			MemUtil.UInt32ToBytesEx((uint)pbAssocData.Length, pbBuf, 0);
			h.TransformBlock(pbBuf, 0, pbBuf.Length, pbBuf, 0);
			h.TransformBlock(pbAssocData, 0, pbAssocData.Length, pbAssocData, 0);
			h.TransformFinalBlock(MemUtil.EmptyByteArray, 0, 0);
			byte[] pbH0 = h.Hash;
			Debug.Assert(pbH0.Length == 64);

			byte[] pbBlockHash = new byte[NbPreHashSeedLength];
			Array.Copy(pbH0, pbBlockHash, pbH0.Length);
			MemUtil.ZeroByteArray(pbH0);

			FillFirstBlocks(ctx, pbBlockHash, h);
			MemUtil.ZeroByteArray(pbBlockHash);

			FillMemoryBlocks(ctx);

			byte[] pbOut = FinalHash(ctx, cbOut, h);

			h.Clear();
			MemUtil.ZeroArray<ulong>(ctx.Mem);
			return pbOut;
		}

		private static void LoadBlock(ulong[] pqDst, ulong uDstOffset, byte[] pbIn)
		{
			// for(ulong i = 0; i < NbBlockSizeInQW; ++i)
			//	pqDst[uDstOffset + i] = MemUtil.BytesToUInt64(pbIn, (int)(i << 3));

			Debug.Assert((uDstOffset + NbBlockSizeInQW - 1UL) <= (ulong)int.MaxValue);
			int iDstOffset = (int)uDstOffset;
			for(int i = 0; i < (int)NbBlockSizeInQW; ++i)
				pqDst[iDstOffset + i] = MemUtil.BytesToUInt64(pbIn, i << 3);
		}

		private static void StoreBlock(byte[] pbDst, ulong[] pqSrc)
		{
			for(int i = 0; i < (int)NbBlockSizeInQW; ++i)
				MemUtil.UInt64ToBytesEx(pqSrc[i], pbDst, i << 3);
		}

		private static void CopyBlock(ulong[] vDst, ulong uDstOffset, ulong[] vSrc,
			ulong uSrcOffset)
		{
			// for(ulong i = 0; i < NbBlockSizeInQW; ++i)
			//	vDst[uDstOffset + i] = vSrc[uSrcOffset + i];

			// Debug.Assert((uDstOffset + NbBlockSizeInQW - 1UL) <= (ulong)int.MaxValue);
			// Debug.Assert((uSrcOffset + NbBlockSizeInQW - 1UL) <= (ulong)int.MaxValue);
			// int iDstOffset = (int)uDstOffset;
			// int iSrcOffset = (int)uSrcOffset;
			// for(int i = 0; i < (int)NbBlockSizeInQW; ++i)
			//	vDst[iDstOffset + i] = vSrc[iSrcOffset + i];

#if KeePassUAP
			Array.Copy(vSrc, (int)uSrcOffset, vDst, (int)uDstOffset,
				(int)NbBlockSizeInQW);
#else
			Array.Copy(vSrc, (long)uSrcOffset, vDst, (long)uDstOffset,
				(long)NbBlockSizeInQW);
#endif
		}

		private static void XorBlock(ulong[] vDst, ulong uDstOffset, ulong[] vSrc,
			ulong uSrcOffset)
		{
			// for(ulong i = 0; i < NbBlockSizeInQW; ++i)
			//	vDst[uDstOffset + i] ^= vSrc[uSrcOffset + i];

			Debug.Assert((uDstOffset + NbBlockSizeInQW - 1UL) <= (ulong)int.MaxValue);
			Debug.Assert((uSrcOffset + NbBlockSizeInQW - 1UL) <= (ulong)int.MaxValue);
			int iDstOffset = (int)uDstOffset;
			int iSrcOffset = (int)uSrcOffset;
			for(int i = 0; i < (int)NbBlockSizeInQW; ++i)
				vDst[iDstOffset + i] ^= vSrc[iSrcOffset + i];
		}

		private static void Blake2bLong(byte[] pbOut, int cbOut,
			byte[] pbIn, int cbIn, Blake2b h)
		{
			Debug.Assert((h != null) && (h.HashSize == (64 * 8)));

			byte[] pbOutLen = new byte[4];
			MemUtil.UInt32ToBytesEx((uint)cbOut, pbOutLen, 0);

			if(cbOut <= 64)
			{
				Blake2b hOut = ((cbOut == 64) ? h : new Blake2b(cbOut));
				if(cbOut == 64) hOut.Initialize();

				hOut.TransformBlock(pbOutLen, 0, pbOutLen.Length, pbOutLen, 0);
				hOut.TransformBlock(pbIn, 0, cbIn, pbIn, 0);
				hOut.TransformFinalBlock(MemUtil.EmptyByteArray, 0, 0);

				Array.Copy(hOut.Hash, pbOut, cbOut);

				if(cbOut < 64) hOut.Clear();
				return;
			}

			h.Initialize();
			h.TransformBlock(pbOutLen, 0, pbOutLen.Length, pbOutLen, 0);
			h.TransformBlock(pbIn, 0, cbIn, pbIn, 0);
			h.TransformFinalBlock(MemUtil.EmptyByteArray, 0, 0);

			byte[] pbOutBuffer = new byte[64];
			Array.Copy(h.Hash, pbOutBuffer, pbOutBuffer.Length);

			int ibOut = 64 / 2;
			Array.Copy(pbOutBuffer, pbOut, ibOut);
			int cbToProduce = cbOut - ibOut;

			h.Initialize();
			while(cbToProduce > 64)
			{
				byte[] pbHash = h.ComputeHash(pbOutBuffer);
				Array.Copy(pbHash, pbOutBuffer, 64);

				Array.Copy(pbHash, 0, pbOut, ibOut, 64 / 2);
				ibOut += 64 / 2;
				cbToProduce -= 64 / 2;

				MemUtil.ZeroByteArray(pbHash);
			}

			using(Blake2b hOut = new Blake2b(cbToProduce))
			{
				byte[] pbHash = hOut.ComputeHash(pbOutBuffer);
				Array.Copy(pbHash, 0, pbOut, ibOut, cbToProduce);

				MemUtil.ZeroByteArray(pbHash);
			}

			MemUtil.ZeroByteArray(pbOutBuffer);
		}

#if !ARGON2_G_INLINED
		private static ulong BlaMka(ulong x, ulong y)
		{
			ulong xy = (x & 0xFFFFFFFFUL) * (y & 0xFFFFFFFFUL);
			return (x + y + (xy << 1));
		}

		private static void G(ulong[] v, int a, int b, int c, int d)
		{
			ulong va = v[a], vb = v[b], vc = v[c], vd = v[d];

			va = BlaMka(va, vb);
			vd = MemUtil.RotateRight64(vd ^ va, 32);
			vc = BlaMka(vc, vd);
			vb = MemUtil.RotateRight64(vb ^ vc, 24);
			va = BlaMka(va, vb);
			vd = MemUtil.RotateRight64(vd ^ va, 16);
			vc = BlaMka(vc, vd);
			vb = MemUtil.RotateRight64(vb ^ vc, 63);

			v[a] = va;
			v[b] = vb;
			v[c] = vc;
			v[d] = vd;
		}
#else
		private static void G(ulong[] v, int a, int b, int c, int d)
		{
			ulong va = v[a], vb = v[b], vc = v[c], vd = v[d];

			ulong xy = (va & 0xFFFFFFFFUL) * (vb & 0xFFFFFFFFUL);
			va += vb + (xy << 1);

			vd = MemUtil.RotateRight64(vd ^ va, 32);

			xy = (vc & 0xFFFFFFFFUL) * (vd & 0xFFFFFFFFUL);
			vc += vd + (xy << 1);

			vb = MemUtil.RotateRight64(vb ^ vc, 24);

			xy = (va & 0xFFFFFFFFUL) * (vb & 0xFFFFFFFFUL);
			va += vb + (xy << 1);

			vd = MemUtil.RotateRight64(vd ^ va, 16);

			xy = (vc & 0xFFFFFFFFUL) * (vd & 0xFFFFFFFFUL);
			vc += vd + (xy << 1);

			vb = MemUtil.RotateRight64(vb ^ vc, 63);

			v[a] = va;
			v[b] = vb;
			v[c] = vc;
			v[d] = vd;
		}
#endif

#if ARGON2_B2ROUND_ARRAYS
		private static void Blake2RoundNoMsg(ulong[] pbR, int[] v)
		{
			G(pbR, v[0], v[4], v[8], v[12]);
			G(pbR, v[1], v[5], v[9], v[13]);
			G(pbR, v[2], v[6], v[10], v[14]);
			G(pbR, v[3], v[7], v[11], v[15]);
			G(pbR, v[0], v[5], v[10], v[15]);
			G(pbR, v[1], v[6], v[11], v[12]);
			G(pbR, v[2], v[7], v[8], v[13]);
			G(pbR, v[3], v[4], v[9], v[14]);
		}
#else
		private static void Blake2RoundNoMsgCols16i(ulong[] pbR, int i)
		{
			G(pbR, i,     i + 4, i +  8, i + 12);
			G(pbR, i + 1, i + 5, i +  9, i + 13);
			G(pbR, i + 2, i + 6, i + 10, i + 14);
			G(pbR, i + 3, i + 7, i + 11, i + 15);
			G(pbR, i,     i + 5, i + 10, i + 15);
			G(pbR, i + 1, i + 6, i + 11, i + 12);
			G(pbR, i + 2, i + 7, i +  8, i + 13);
			G(pbR, i + 3, i + 4, i +  9, i + 14);
		}

		private static void Blake2RoundNoMsgRows2i(ulong[] pbR, int i)
		{
			G(pbR, i,      i + 32, i + 64, i +  96);
			G(pbR, i +  1, i + 33, i + 65, i +  97);
			G(pbR, i + 16, i + 48, i + 80, i + 112);
			G(pbR, i + 17, i + 49, i + 81, i + 113);
			G(pbR, i,      i + 33, i + 80, i + 113);
			G(pbR, i +  1, i + 48, i + 81, i +  96);
			G(pbR, i + 16, i + 49, i + 64, i +  97);
			G(pbR, i + 17, i + 32, i + 65, i + 112);
		}
#endif

		private static void FillFirstBlocks(Argon2Ctx ctx, byte[] pbBlockHash,
			Blake2b h)
		{
			byte[] pbBlock = new byte[NbBlockSize];

			for(ulong l = 0; l < ctx.Lanes; ++l)
			{
				MemUtil.UInt32ToBytesEx(0, pbBlockHash, NbPreHashDigestLength);
				MemUtil.UInt32ToBytesEx((uint)l, pbBlockHash, NbPreHashDigestLength + 4);

				Blake2bLong(pbBlock, (int)NbBlockSize, pbBlockHash,
					NbPreHashSeedLength, h);
				LoadBlock(ctx.Mem, l * ctx.LaneLength * NbBlockSizeInQW, pbBlock);

				MemUtil.UInt32ToBytesEx(1, pbBlockHash, NbPreHashDigestLength);

				Blake2bLong(pbBlock, (int)NbBlockSize, pbBlockHash,
					NbPreHashSeedLength, h);
				LoadBlock(ctx.Mem, (l * ctx.LaneLength + 1UL) * NbBlockSizeInQW, pbBlock);
			}

			MemUtil.ZeroByteArray(pbBlock);
		}

		private static ulong IndexAlpha(Argon2Ctx ctx, Argon2ThreadInfo ti,
			uint uPseudoRand, bool bSameLane)
		{
			ulong uRefAreaSize;
			if(ti.Pass == 0)
			{
				if(ti.Slice == 0)
				{
					Debug.Assert(ti.Index > 0);
					uRefAreaSize = ti.Index - 1UL;
				}
				else
				{
					if(bSameLane)
						uRefAreaSize = ti.Slice * ctx.SegmentLength +
							ti.Index - 1UL;
					else
						uRefAreaSize = ti.Slice * ctx.SegmentLength -
							((ti.Index == 0UL) ? 1UL : 0UL);
				}
			}
			else
			{
				if(bSameLane)
					uRefAreaSize = ctx.LaneLength - ctx.SegmentLength +
						ti.Index - 1UL;
				else
					uRefAreaSize = ctx.LaneLength - ctx.SegmentLength -
						((ti.Index == 0) ? 1UL : 0UL);
			}
			Debug.Assert(uRefAreaSize <= (ulong)uint.MaxValue);

			ulong uRelPos = uPseudoRand;
			uRelPos = (uRelPos * uRelPos) >> 32;
			uRelPos = uRefAreaSize - 1UL - ((uRefAreaSize * uRelPos) >> 32);

			ulong uStart = 0;
			if(ti.Pass != 0)
				uStart = (((ti.Slice + 1UL) == NbSyncPoints) ? 0UL :
					((ti.Slice + 1UL) * ctx.SegmentLength));
			Debug.Assert(uStart <= (ulong)uint.MaxValue);

			Debug.Assert(ctx.LaneLength <= (ulong)uint.MaxValue);
			return ((uStart + uRelPos) % ctx.LaneLength);
		}

		private static void FillMemoryBlocks(Argon2Ctx ctx)
		{
			int np = (int)ctx.Lanes;
			Argon2ThreadInfo[] v = new Argon2ThreadInfo[np];

			for(ulong r = 0; r < ctx.TCost; ++r)
			{
				for(ulong s = 0; s < NbSyncPoints; ++s)
				{
					for(int l = 0; l < np; ++l)
					{
						Argon2ThreadInfo ti = new Argon2ThreadInfo();
						ti.Context = ctx;

						ti.Pass = r;
						ti.Lane = (ulong)l;
						ti.Slice = s;

						if(!ThreadPool.QueueUserWorkItem(FillSegmentThr, ti))
						{
							Debug.Assert(false);
							throw new OutOfMemoryException();
						}

						v[l] = ti;
					}

					for(int l = 0; l < np; ++l)
					{
						v[l].Finished.WaitOne();
						v[l].Release();
					}
				}
			}
		}

		private static void FillSegmentThr(object o)
		{
			Argon2ThreadInfo ti = (o as Argon2ThreadInfo);
			if(ti == null) { Debug.Assert(false); return; }

			try
			{
				Argon2Ctx ctx = ti.Context;
				if(ctx == null) { Debug.Assert(false); return; }

				Debug.Assert(ctx.Version >= MinVersion);
				bool bCanXor = (ctx.Version >= 0x13U);

				ulong uStart = 0;
				if((ti.Pass == 0) && (ti.Slice == 0)) uStart = 2;

				ulong uCur = (ti.Lane * ctx.LaneLength) + (ti.Slice *
					ctx.SegmentLength) + uStart;

				ulong uPrev = (((uCur % ctx.LaneLength) == 0) ?
					(uCur + ctx.LaneLength - 1UL) : (uCur - 1UL));

				ulong[] pbR = new ulong[NbBlockSizeInQW];
				ulong[] pbTmp = new ulong[NbBlockSizeInQW];

				for(ulong i = uStart; i < ctx.SegmentLength; ++i)
				{
					if((uCur % ctx.LaneLength) == 1)
						uPrev = uCur - 1UL;

					ulong uPseudoRand = ctx.Mem[uPrev * NbBlockSizeInQW];
					ulong uRefLane = (uPseudoRand >> 32) % ctx.Lanes;
					if((ti.Pass == 0) && (ti.Slice == 0))
						uRefLane = ti.Lane;

					ti.Index = i;
					ulong uRefIndex = IndexAlpha(ctx, ti, (uint)uPseudoRand,
						(uRefLane == ti.Lane));

					ulong uRefBlockIndex = (ctx.LaneLength * uRefLane +
						uRefIndex) * NbBlockSizeInQW;
					ulong uCurBlockIndex = uCur * NbBlockSizeInQW;

					FillBlock(ctx.Mem, uPrev * NbBlockSizeInQW, uRefBlockIndex,
						uCurBlockIndex, ((ti.Pass != 0) && bCanXor), pbR, pbTmp);

					++uCur;
					++uPrev;
				}

				MemUtil.ZeroArray<ulong>(pbR);
				MemUtil.ZeroArray<ulong>(pbTmp);
			}
			catch(Exception) { Debug.Assert(false); }

			try { ti.Finished.Set(); }
			catch(Exception) { Debug.Assert(false); }
		}

#if ARGON2_B2ROUND_ARRAYS
		private static void InitB2RoundIndexArrays()
		{
			int[][] vCols = g_vFBCols;
			if(vCols == null)
			{
				vCols = new int[8][];
				Debug.Assert(vCols.Length == 8);
				int e = 0;
				for(int i = 0; i < 8; ++i)
				{
					vCols[i] = new int[16];
					for(int j = 0; j < 16; ++j)
					{
						vCols[i][j] = e;
						++e;
					}
				}

				g_vFBCols = vCols;
			}

			int[][] vRows = g_vFBRows;
			if(vRows == null)
			{
				vRows = new int[8][];
				for(int i = 0; i < 8; ++i)
				{
					vRows[i] = new int[16];
					for(int j = 0; j < 16; ++j)
					{
						int jh = j / 2;
						vRows[i][j] = (2 * i) + (16 * jh) + (j & 1);
					}
				}

				g_vFBRows = vRows;
			}
		}
#endif

		private static void FillBlock(ulong[] pMem, ulong uPrev, ulong uRef,
			ulong uNext, bool bXor, ulong[] pbR, ulong[] pbTmp)
		{
			CopyBlock(pbR, 0, pMem, uRef);
			XorBlock(pbR, 0, pMem, uPrev);
			CopyBlock(pbTmp, 0, pbR, 0);
			if(bXor) XorBlock(pbTmp, 0, pMem, uNext);

#if ARGON2_B2ROUND_ARRAYS
			int[][] vCols = g_vFBCols;
			int[][] vRows = g_vFBRows;
			for(int i = 0; i < 8; ++i)
				Blake2RoundNoMsg(pbR, vCols[i]);
			for(int i = 0; i < 8; ++i)
				Blake2RoundNoMsg(pbR, vRows[i]);
#else
			for(int i = 0; i < (8 * 16); i += 16)
				Blake2RoundNoMsgCols16i(pbR, i);
			for(int i = 0; i < (8 * 2); i += 2)
				Blake2RoundNoMsgRows2i(pbR, i);
#endif

			CopyBlock(pMem, uNext, pbTmp, 0);
			XorBlock(pMem, uNext, pbR, 0);
		}

		private static byte[] FinalHash(Argon2Ctx ctx, int cbOut, Blake2b h)
		{
			ulong[] pqBlockHash = new ulong[NbBlockSizeInQW];
			CopyBlock(pqBlockHash, 0, ctx.Mem, (ctx.LaneLength - 1UL) *
				NbBlockSizeInQW);
			for(ulong l = 1; l < ctx.Lanes; ++l)
				XorBlock(pqBlockHash, 0, ctx.Mem, (l * ctx.LaneLength +
					ctx.LaneLength - 1UL) * NbBlockSizeInQW);

			byte[] pbBlockHashBytes = new byte[NbBlockSize];
			StoreBlock(pbBlockHashBytes, pqBlockHash);

			byte[] pbOut = new byte[cbOut];
			Blake2bLong(pbOut, cbOut, pbBlockHashBytes, (int)NbBlockSize, h);

			MemUtil.ZeroArray<ulong>(pqBlockHash);
			MemUtil.ZeroByteArray(pbBlockHashBytes);
			return pbOut;
		}
	}
}
