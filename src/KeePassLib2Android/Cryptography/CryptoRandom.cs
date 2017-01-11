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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

#if !KeePassUAP
using System.Drawing;
using System.Security.Cryptography;
using System.Windows.Forms;
#endif

using KeePassLib.Native;
using KeePassLib.Utility;

namespace KeePassLib.Cryptography
{
	/// <summary>
	/// Cryptographically secure pseudo-random number generator.
	/// The returned values are unpredictable and cannot be reproduced.
	/// <c>CryptoRandom</c> is a singleton class.
	/// </summary>
	public sealed class CryptoRandom
	{
		private byte[] m_pbEntropyPool = new byte[64];
		private ulong m_uCounter;
		private RNGCryptoServiceProvider m_rng = new RNGCryptoServiceProvider();
		private ulong m_uGeneratedBytesCount = 0;

		private static object g_oSyncRoot = new object();
		private object m_oSyncRoot = new object();

		private static CryptoRandom g_pInstance = null;
		public static CryptoRandom Instance
		{
			get
			{
				CryptoRandom cr;
				lock(g_oSyncRoot)
				{
					cr = g_pInstance;
					if(cr == null)
					{
						cr = new CryptoRandom();
						g_pInstance = cr;
					}
				}

				return cr;
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
			// Random rWeak = new Random(); // Based on tick count
			// byte[] pb = new byte[8];
			// rWeak.NextBytes(pb);
			// m_uCounter = MemUtil.BytesToUInt64(pb);
			m_uCounter = (ulong)DateTime.UtcNow.ToBinary();

			AddEntropy(GetSystemData());
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
			if(pbEntropy.Length > 64)
			{
#if KeePassLibSD
				using(SHA256Managed shaNew = new SHA256Managed())
#else
				using(SHA512Managed shaNew = new SHA512Managed())
#endif
				{
					pbNewData = shaNew.ComputeHash(pbEntropy);
				}
			}

			lock(m_oSyncRoot)
			{
				int cbPool = m_pbEntropyPool.Length;
				int cbNew = pbNewData.Length;

				byte[] pbCmp = new byte[cbPool + cbNew];
				Array.Copy(m_pbEntropyPool, pbCmp, cbPool);
				Array.Copy(pbNewData, 0, pbCmp, cbPool, cbNew);

				MemUtil.ZeroByteArray(m_pbEntropyPool);

#if KeePassLibSD
				using(SHA256Managed shaPool = new SHA256Managed())
#else
				using(SHA512Managed shaPool = new SHA512Managed())
#endif
				{
					m_pbEntropyPool = shaPool.ComputeHash(pbCmp);
				}

				MemUtil.ZeroByteArray(pbCmp);
			}
		}

		private static byte[] GetSystemData()
		{
			MemoryStream ms = new MemoryStream();
			byte[] pb;

			pb = MemUtil.Int32ToBytes(Environment.TickCount);
			MemUtil.Write(ms, pb);

			pb = MemUtil.Int64ToBytes(DateTime.UtcNow.ToBinary());
			MemUtil.Write(ms, pb);

#if !KeePassLibSD
			// In try-catch for systems without GUI;
			// https://sourceforge.net/p/keepass/discussion/329221/thread/20335b73/
			try
			{
				Point pt = Cursor.Position;
				pb = MemUtil.Int32ToBytes(pt.X);
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int32ToBytes(pt.Y);
				MemUtil.Write(ms, pb);
			}
			catch(Exception) { }
#endif

			pb = MemUtil.UInt32ToBytes((uint)NativeLib.GetPlatformID());
			MemUtil.Write(ms, pb);

			try
			{
#if KeePassUAP
				string strOS = EnvironmentExt.OSVersion.VersionString;
#else
				string strOS = Environment.OSVersion.VersionString;
#endif
				AddStrHash(ms, strOS);

				pb = MemUtil.Int32ToBytes(Environment.ProcessorCount);
				MemUtil.Write(ms, pb);

#if !KeePassUAP
				AddStrHash(ms, Environment.CommandLine);

				pb = MemUtil.Int64ToBytes(Environment.WorkingSet);
				MemUtil.Write(ms, pb);
#endif
			}
			catch(Exception) { Debug.Assert(false); }

			try
			{
				foreach(DictionaryEntry de in Environment.GetEnvironmentVariables())
				{
					AddStrHash(ms, (de.Key as string));
					AddStrHash(ms, (de.Value as string));
				}
			}
			catch(Exception) { Debug.Assert(false); }

#if KeePassUAP
			pb = DiagnosticsExt.GetProcessEntropy();
			MemUtil.Write(ms, pb);
#elif !KeePassLibSD
			Process p = null;
			try
			{
				p = Process.GetCurrentProcess();

				pb = MemUtil.Int64ToBytes(p.Handle.ToInt64());
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int32ToBytes(p.HandleCount);
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int32ToBytes(p.Id);
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int64ToBytes(p.NonpagedSystemMemorySize64);
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int64ToBytes(p.PagedMemorySize64);
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int64ToBytes(p.PagedSystemMemorySize64);
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int64ToBytes(p.PeakPagedMemorySize64);
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int64ToBytes(p.PeakVirtualMemorySize64);
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int64ToBytes(p.PeakWorkingSet64);
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int64ToBytes(p.PrivateMemorySize64);
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int64ToBytes(p.StartTime.ToBinary());
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int64ToBytes(p.VirtualMemorySize64);
				MemUtil.Write(ms, pb);
				pb = MemUtil.Int64ToBytes(p.WorkingSet64);
				MemUtil.Write(ms, pb);

				// Not supported in Mono 1.2.6:
				// pb = MemUtil.UInt32ToBytes((uint)p.SessionId);
				// MemUtil.Write(ms, pb);
			}
			catch(Exception) { Debug.Assert(NativeLib.IsUnix()); }
			finally
			{
				try { if(p != null) p.Dispose(); }
				catch(Exception) { Debug.Assert(false); }
			}
#endif

			try
			{
				CultureInfo ci = CultureInfo.CurrentCulture;
				if(ci != null)
				{
					pb = MemUtil.Int32ToBytes(ci.GetHashCode());
					MemUtil.Write(ms, pb);
				}
				else { Debug.Assert(false); }
			}
			catch(Exception) { Debug.Assert(false); }

			pb = Guid.NewGuid().ToByteArray();
			MemUtil.Write(ms, pb);

			byte[] pbAll = ms.ToArray();
			ms.Close();
			return pbAll;
		}

		private static void AddStrHash(Stream s, string str)
		{
			if(s == null) { Debug.Assert(false); return; }
			if(string.IsNullOrEmpty(str)) return;

			byte[] pbUtf8 = StrUtil.Utf8.GetBytes(str);
			byte[] pbHash = CryptoUtil.HashSha256(pbUtf8);
			MemUtil.Write(s, pbHash);
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

			byte[] pbCmp;
			lock(m_oSyncRoot)
			{
				m_uCounter += 0x74D8B29E4D38E161UL; // Prime number
				byte[] pbCounter = MemUtil.UInt64ToBytes(m_uCounter);

				byte[] pbCspRandom = GetCspData();

				int cbPool = m_pbEntropyPool.Length;
				int cbCtr = pbCounter.Length;
				int cbCsp = pbCspRandom.Length;

				pbCmp = new byte[cbPool + cbCtr + cbCsp];
				Array.Copy(m_pbEntropyPool, pbCmp, cbPool);
				Array.Copy(pbCounter, 0, pbCmp, cbPool, cbCtr);
				Array.Copy(pbCspRandom, 0, pbCmp, cbPool + cbCtr, cbCsp);

				MemUtil.ZeroByteArray(pbCspRandom);

				m_uGeneratedBytesCount += 32;
			}

			byte[] pbRet = CryptoUtil.HashSha256(pbCmp);
			MemUtil.ZeroByteArray(pbCmp);
			return pbRet;
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
			if(uRequestedBytes == 0) return MemUtil.EmptyByteArray;
			if(uRequestedBytes > (uint)int.MaxValue)
			{
				Debug.Assert(false);
				throw new ArgumentOutOfRangeException("uRequestedBytes");
			}

			int cbRem = (int)uRequestedBytes;
			byte[] pbRes = new byte[cbRem];
			int iPos = 0;

			while(cbRem != 0)
			{
				byte[] pbRandom256 = GenerateRandom256();
				Debug.Assert(pbRandom256.Length == 32);

				int cbCopy = Math.Min(cbRem, pbRandom256.Length);
				Array.Copy(pbRandom256, 0, pbRes, iPos, cbCopy);

				MemUtil.ZeroByteArray(pbRandom256);

				iPos += cbCopy;
				cbRem -= cbCopy;
			}

			Debug.Assert(iPos == pbRes.Length);
			return pbRes;
		}
	}
}
