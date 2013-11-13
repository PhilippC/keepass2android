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
using System.Text;
using System.Security;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;

using KeePassLib.Utility;

namespace KeePassLib.Native
{
	internal static class NativeMethods
	{
		internal const int MAX_PATH = 260;

		/* [DllImport("KeePassNtv32.dll", EntryPoint = "TransformKey")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool TransformKey32(IntPtr pBuf256,
			IntPtr pKey256, UInt64 uRounds);

		[DllImport("KeePassNtv64.dll", EntryPoint = "TransformKey")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool TransformKey64(IntPtr pBuf256,
			IntPtr pKey256, UInt64 uRounds);

		internal static bool TransformKey(IntPtr pBuf256, IntPtr pKey256,
			UInt64 uRounds)
		{
			if(Marshal.SizeOf(typeof(IntPtr)) == 8)
				return TransformKey64(pBuf256, pKey256, uRounds);
			else
				return TransformKey32(pBuf256, pKey256, uRounds);
		}

		[DllImport("KeePassNtv32.dll", EntryPoint = "TransformKeyTimed")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool TransformKeyTimed32(IntPtr pBuf256,
			IntPtr pKey256, ref UInt64 puRounds, UInt32 uSeconds);

		[DllImport("KeePassNtv64.dll", EntryPoint = "TransformKeyTimed")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool TransformKeyTimed64(IntPtr pBuf256,
			IntPtr pKey256, ref UInt64 puRounds, UInt32 uSeconds);

		internal static bool TransformKeyTimed(IntPtr pBuf256, IntPtr pKey256,
			ref UInt64 puRounds, UInt32 uSeconds)
		{
			if(Marshal.SizeOf(typeof(IntPtr)) == 8)
				return TransformKeyTimed64(pBuf256, pKey256, ref puRounds, uSeconds);
			else
				return TransformKeyTimed32(pBuf256, pKey256, ref puRounds, uSeconds);
		} */

		[DllImport("KeePassLibC32.dll", EntryPoint = "TransformKey256")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool TransformKey32(IntPtr pBuf256,
			IntPtr pKey256, UInt64 uRounds);

		[DllImport("KeePassLibC64.dll", EntryPoint = "TransformKey256")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool TransformKey64(IntPtr pBuf256,
			IntPtr pKey256, UInt64 uRounds);

		internal static bool TransformKey(IntPtr pBuf256, IntPtr pKey256,
			UInt64 uRounds)
		{
			if(Marshal.SizeOf(typeof(IntPtr)) == 8)
				return TransformKey64(pBuf256, pKey256, uRounds);
			else
				return TransformKey32(pBuf256, pKey256, uRounds);
		}

		[DllImport("KeePassLibC32.dll", EntryPoint = "TransformKeyBenchmark256")]
		private static extern UInt64 TransformKeyBenchmark32(UInt32 uTimeMs);

		[DllImport("KeePassLibC64.dll", EntryPoint = "TransformKeyBenchmark256")]
		private static extern UInt64 TransformKeyBenchmark64(UInt32 uTimeMs);

		internal static UInt64 TransformKeyBenchmark(UInt32 uTimeMs)
		{
			if(Marshal.SizeOf(typeof(IntPtr)) == 8)
				return TransformKeyBenchmark64(uTimeMs);
			else
				return TransformKeyBenchmark32(uTimeMs);
		}

#if (!KeePassLibSD && !KeePassRT)
		[DllImport("ShlWApi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
		internal static extern int StrCmpLogicalW(string x, string y);

		[DllImport("ShlWApi.dll", CharSet = CharSet.Auto)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool PathRelativePathTo([Out] StringBuilder pszPath,
			[In] string pszFrom, [In] uint dwAttrFrom, [In] string pszTo,
			[In] uint dwAttrTo);
#endif

		private static bool? m_bSupportsLogicalCmp = null;

		private static void TestNaturalComparisonsSupport()
		{
#if (KeePassLibSD || KeePassRT)
#warning No native natural comparisons supported.
			m_bSupportsLogicalCmp = false;
#else
			try
			{
				StrCmpLogicalW("0", "0"); // Throws exception if unsupported
				m_bSupportsLogicalCmp = true;
			}
			catch(Exception) { m_bSupportsLogicalCmp = false; }
#endif
		}

		internal static bool SupportsStrCmpNaturally
		{
			get
			{
				if(m_bSupportsLogicalCmp.HasValue == false)
					TestNaturalComparisonsSupport();

				return m_bSupportsLogicalCmp.Value;
			}
		}

		internal static int StrCmpNaturally(string x, string y)
		{
			if(m_bSupportsLogicalCmp.HasValue == false) TestNaturalComparisonsSupport();
			if(m_bSupportsLogicalCmp.Value == false) return 0;

#if (KeePassLibSD || KeePassRT)
#warning No native natural comparisons supported.
			return x.CompareTo(y);
#else
			return StrCmpLogicalW(x, y);
#endif
		}

		internal static string GetUserRuntimeDir()
		{
#if !KeePassLibSD
#if KeePassRT
			string strRtDir = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
#else
			string strRtDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
			if(string.IsNullOrEmpty(strRtDir))
				strRtDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			if(string.IsNullOrEmpty(strRtDir))
			{
				Debug.Assert(false);
				return Path.GetTempPath(); // Not UrlUtil (otherwise cyclic)
			}
#endif

			strRtDir = UrlUtil.EnsureTerminatingSeparator(strRtDir, false);
			strRtDir += PwDefs.ShortProductName;

			return strRtDir;
#else
			return Path.GetTempPath();
#endif
		}
	}
}
