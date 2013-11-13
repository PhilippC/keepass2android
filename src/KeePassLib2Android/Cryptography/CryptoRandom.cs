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
using System.Security;
using System.Security.Cryptography;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;

using KeePassLib.Native;
using KeePassLib.Utility;

namespace KeePassLib.Cryptography
{
	/// <summary>
	/// Cryptographically strong random number generator. The returned values
	/// are unpredictable and cannot be reproduced.
	/// <c>CryptoRandom</c> is a singleton class.
	/// </summary>
	public sealed class CryptoRandom
	{
		private byte[] m_pbEntropyPool = new byte[64];
		private uint m_uCounter;
		private RNGCryptoServiceProvider m_rng = new RNGCryptoServiceProvider();
		private ulong m_uGeneratedBytesCount = 0;

		private object m_oSyncRoot = new object();

		private static CryptoRandom m_pInstance = null;
		public static CryptoRandom Instance
		{
			get
			{
				if(m_pInstance != null) return m_pInstance;

				m_pInstance = new CryptoRandom();
				return m_pInstance;
			}
		}

		/// <summary>
		/// Get the number of random bytes that this instance generated so far.
		/// Note that this number can be higher than the number of random bytes
		/// actually requested using the <c>GetRandomBytes</c> method.
		/// </summary>
		public ulong GeneratedBytesCount
		{
			get
			{
				ulong u;
				lock(m_oSyncRoot) { u = m_uGeneratedBytesCount; }
				return u;
			}
		}

		/// <summary>
		/// Event that is triggered whenever the internal <c>GenerateRandom256</c>
		/// method is called to generate random bytes.
		/// </summary>
		public event EventHandler GenerateRandom256Pre;

		private CryptoRandom()
		{
			Random r = new Random();
			m_uCounter = (uint)r.Next();

			AddEntropy(GetSystemData(r));
			AddEntropy(GetCspData());
		}

		/// <summary>
		/// Update the internal seed of the random number generator based
		/// on entropy data.
		/// This method is thread-safe.
		/// </summary>
		/// <param name="pbEntropy">Entropy bytes.</param>
		public void AddEntropy(byte[] pbEntropy)
		{
			if(pbEntropy == null) { Debug.Assert(false); return; }
			if(pbEntropy.Length == 0) { Debug.Assert(false); return; }

			byte[] pbNewData = pbEntropy;
			if(pbEntropy.Length >= 64)
			{
#if !KeePassLibSD
				SHA512Managed shaNew = new SHA512Managed();
#else
				SHA256Managed shaNew = new SHA256Managed();
#endif
				pbNewData = shaNew.ComputeHash(pbEntropy);
			}

			MemoryStream ms = new MemoryStream();
			lock(m_oSyncRoot)
			{
				ms.Write(m_pbEntropyPool, 0, m_pbEntropyPool.Length);
				ms.Write(pbNewData, 0, pbNewData.Length);

				byte[] pbFinal = ms.ToArray();
#if !KeePassLibSD
				Debug.Assert(pbFinal.Length == (64 + pbNewData.Length));
				SHA512Managed shaPool = new SHA512Managed();
#else
				SHA256Managed shaPool = new SHA256Managed();
#endif
				m_pbEntropyPool = shaPool.ComputeHash(pbFinal);
			}
			ms.Close();
		}

		private static byte[] GetSystemData(Random rWeak)
		{
			MemoryStream ms = new MemoryStream();
			byte[] pb;

			pb = MemUtil.UInt32ToBytes((uint)Environment.TickCount);
			ms.Write(pb, 0, pb.Length);

			pb = TimeUtil.PackTime(DateTime.Now);
			ms.Write(pb, 0, pb.Length);

#if (!KeePassLibSD && !KeePassRT)
			// In try-catch for systems without GUI;
			// https://sourceforge.net/p/keepass/discussion/329221/thread/20335b73/
			try
			{
				Point pt = Cursor.Position;
				pb = MemUtil.UInt32ToBytes((uint)pt.X);
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt32ToBytes((uint)pt.Y);
				ms.Write(pb, 0, pb.Length);
			}
			catch(Exception) { }
#endif

			pb = MemUtil.UInt32ToBytes((uint)rWeak.Next());
			ms.Write(pb, 0, pb.Length);

			pb = MemUtil.UInt32ToBytes((uint)NativeLib.GetPlatformID());
			ms.Write(pb, 0, pb.Length);

#if (!KeePassLibSD && !KeePassRT)
			try
			{
				pb = MemUtil.UInt32ToBytes((uint)Environment.ProcessorCount);
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt64ToBytes((ulong)Environment.WorkingSet);
				ms.Write(pb, 0, pb.Length);

				Version v = Environment.OSVersion.Version;
				int nv = (v.Major << 28) + (v.MajorRevision << 24) +
					(v.Minor << 20) + (v.MinorRevision << 16) +
					(v.Revision << 12) + v.Build;
				pb = MemUtil.UInt32ToBytes((uint)nv);
				ms.Write(pb, 0, pb.Length);

				Process p = Process.GetCurrentProcess();
				pb = MemUtil.UInt64ToBytes((ulong)p.Handle.ToInt64());
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt32ToBytes((uint)p.HandleCount);
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt32ToBytes((uint)p.Id);
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt64ToBytes((ulong)p.NonpagedSystemMemorySize64);
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt64ToBytes((ulong)p.PagedMemorySize64);
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt64ToBytes((ulong)p.PagedSystemMemorySize64);
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt64ToBytes((ulong)p.PeakPagedMemorySize64);
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt64ToBytes((ulong)p.PeakVirtualMemorySize64);
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt64ToBytes((ulong)p.PeakWorkingSet64);
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt64ToBytes((ulong)p.PrivateMemorySize64);
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt64ToBytes((ulong)p.StartTime.ToBinary());
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt64ToBytes((ulong)p.VirtualMemorySize64);
				ms.Write(pb, 0, pb.Length);
				pb = MemUtil.UInt64ToBytes((ulong)p.WorkingSet64);
				ms.Write(pb, 0, pb.Length);

				// Not supported in Mono 1.2.6:
				// pb = MemUtil.UInt32ToBytes((uint)p.SessionId);
				// ms.Write(pb, 0, pb.Length);
			}
			catch(Exception) { }
#endif

			pb = Guid.NewGuid().ToByteArray();
			ms.Write(pb, 0, pb.Length);

			byte[] pbAll = ms.ToArray();
			ms.Close();
			return pbAll;
		}

		private byte[] GetCspData()
		{
			byte[] pbCspRandom = new byte[32];
			m_rng.GetBytes(pbCspRandom);
			return pbCspRandom;
		}

		private byte[] GenerateRandom256()
		{
			if(this.GenerateRandom256Pre != null)
				this.GenerateRandom256Pre(this, EventArgs.Empty);

			byte[] pbFinal;
			lock(m_oSyncRoot)
			{
				unchecked { m_uCounter += 386047; } // Prime number
				byte[] pbCounter = MemUtil.UInt32ToBytes(m_uCounter);

				byte[] pbCspRandom = GetCspData();

				MemoryStream ms = new MemoryStream();
				ms.Write(m_pbEntropyPool, 0, m_pbEntropyPool.Length);
				ms.Write(pbCounter, 0, pbCounter.Length);
				ms.Write(pbCspRandom, 0, pbCspRandom.Length);
				pbFinal = ms.ToArray();
				Debug.Assert(pbFinal.Length == (m_pbEntropyPool.Length +
					pbCounter.Length + pbCspRandom.Length));
				ms.Close();

				m_uGeneratedBytesCount += 32;
			}

			SHA256Managed sha256 = new SHA256Managed();
			return sha256.ComputeHash(pbFinal);
		}

		/// <summary>
		/// Get a number of cryptographically strong random bytes.
		/// This method is thread-safe.
		/// </summary>
		/// <param name="uRequestedBytes">Number of requested random bytes.</param>
		/// <returns>A byte array consisting of <paramref name="uRequestedBytes" />
		/// random bytes.</returns>
		public byte[] GetRandomBytes(uint uRequestedBytes)
		{
			if(uRequestedBytes == 0) return new byte[0]; // Allow zero-length array

			byte[] pbRes = new byte[uRequestedBytes];
			long lPos = 0;

			while(uRequestedBytes != 0)
			{
				byte[] pbRandom256 = GenerateRandom256();
				Debug.Assert(pbRandom256.Length == 32);

				long lCopy = (long)((uRequestedBytes < 32) ? uRequestedBytes : 32);

#if (!KeePassLibSD && !KeePassRT)
				Array.Copy(pbRandom256, 0, pbRes, lPos, lCopy);
#else
				Array.Copy(pbRandom256, 0, pbRes, (int)lPos, (int)lCopy);
#endif

				lPos += lCopy;
				uRequestedBytes -= (uint)lCopy;
			}

			Debug.Assert((int)lPos == pbRes.Length);
			return pbRes;
		}
	}
}
