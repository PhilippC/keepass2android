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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using KeePassLib.Utility;

namespace KeePassLib.Native
{
	internal static partial class NativeMethods
	{
		internal const int MAX_PATH = 260;

		// internal const uint TF_SFT_SHOWNORMAL = 0x00000001;
		// internal const uint TF_SFT_HIDDEN = 0x00000008;

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

#if !KeePassUAP
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
			if(NativeLib.PointerSize == 8)
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
			if(NativeLib.PointerSize == 8)
				return TransformKeyBenchmark64(uTimeMs);
			return TransformKeyBenchmark32(uTimeMs);
		}
#endif

		/* [DllImport("KeePassLibC32.dll", EntryPoint = "TF_ShowLangBar")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool TF_ShowLangBar32(UInt32 dwFlags);

		[DllImport("KeePassLibC64.dll", EntryPoint = "TF_ShowLangBar")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool TF_ShowLangBar64(UInt32 dwFlags);

		internal static bool TfShowLangBar(uint dwFlags)
		{
			if(Marshal.SizeOf(typeof(IntPtr)) == 8)
				return TF_ShowLangBar64(dwFlags);
			return TF_ShowLangBar32(dwFlags);
		} */

#if (!KeePassLibSD && !KeePassUAP)
		[DllImport("ShlWApi.dll", CharSet = CharSet.Auto)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool PathRelativePathTo([Out] StringBuilder pszPath,
			[In] string pszFrom, uint dwAttrFrom, [In] string pszTo, uint dwAttrTo);

		[DllImport("ShlWApi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
		private static extern int StrCmpLogicalW(string x, string y);

		private static bool? m_obSupportsLogicalCmp = null;

		private static void TestNaturalComparisonsSupport()
		{
			try
			{
				StrCmpLogicalW("0", "0"); // Throws exception if unsupported
				m_obSupportsLogicalCmp = true;
			}
			catch(Exception) { m_obSupportsLogicalCmp = false; }
		}
#endif

		internal static bool SupportsStrCmpNaturally
		{
			get
			{
#if (!KeePassLibSD && !KeePassUAP)
				if(!m_obSupportsLogicalCmp.HasValue)
					TestNaturalComparisonsSupport();

				return m_obSupportsLogicalCmp.Value;
#else
				return false;
#endif
			}
		}

		internal static int StrCmpNaturally(string x, string y)
		{
#if (!KeePassLibSD && !KeePassUAP)
			if(!NativeMethods.SupportsStrCmpNaturally)
			{
				Debug.Assert(false);
				return string.Compare(x, y, true);
			}

			return StrCmpLogicalW(x, y);
#else
			Debug.Assert(false);
			return string.Compare(x, y, true);
#endif
		}

		internal static string GetUserRuntimeDir()
		{
#if KeePassLibSD
			return Path.GetTempPath();
#else
#if KeePassUAP
			string strRtDir = EnvironmentExt.AppDataLocalFolderPath;
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
#endif
		}
	}
}
