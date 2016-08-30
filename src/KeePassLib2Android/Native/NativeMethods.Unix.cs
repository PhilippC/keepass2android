/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2016 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

#if !KeePassUAP
using System.Windows.Forms;
#endif

namespace KeePassLib.Native
{
	internal static partial class NativeMethods
	{
#if (!KeePassLibSD && !KeePassUAP)
		[StructLayout(LayoutKind.Sequential)]
		private struct XClassHint
		{
			public IntPtr res_name;
			public IntPtr res_class;
		}

		[DllImport("libX11")]
		private static extern int XSetClassHint(IntPtr display, IntPtr window, IntPtr class_hints);

		private static Type m_tXplatUIX11 = null;
		private static Type GetXplatUIX11Type(bool bThrowOnError)
		{
			if(m_tXplatUIX11 == null)
			{
				// CheckState is in System.Windows.Forms
				string strTypeCS = typeof(CheckState).AssemblyQualifiedName;
				string strTypeX11 = strTypeCS.Replace("CheckState", "XplatUIX11");
				m_tXplatUIX11 = Type.GetType(strTypeX11, bThrowOnError, true);
			}

			return m_tXplatUIX11;
		}

		private static Type m_tHwnd = null;
		private static Type GetHwndType(bool bThrowOnError)
		{
			if(m_tHwnd == null)
			{
				// CheckState is in System.Windows.Forms
				string strTypeCS = typeof(CheckState).AssemblyQualifiedName;
				string strTypeHwnd = strTypeCS.Replace("CheckState", "Hwnd");
				m_tHwnd = Type.GetType(strTypeHwnd, bThrowOnError, true);
			}

			return m_tHwnd;
		}

		internal static void SetWmClass(Form f, string strName, string strClass)
		{
			if(f == null) { Debug.Assert(false); return; }

			// The following crashes under Mac OS X (SIGSEGV in native code,
			// not just an exception), thus skip it when we're on Mac OS X;
			// https://sourceforge.net/projects/keepass/forums/forum/329221/topic/5860588
			if(NativeLib.GetPlatformID() == PlatformID.MacOSX) return;

			try
			{
				Type tXplatUIX11 = GetXplatUIX11Type(true);
				FieldInfo fiDisplayHandle = tXplatUIX11.GetField("DisplayHandle",
					BindingFlags.NonPublic | BindingFlags.Static);
				IntPtr hDisplay = (IntPtr)fiDisplayHandle.GetValue(null);

				Type tHwnd = GetHwndType(true);
				MethodInfo miObjectFromHandle = tHwnd.GetMethod("ObjectFromHandle",
					BindingFlags.Public | BindingFlags.Static);
				object oHwnd = miObjectFromHandle.Invoke(null, new object[] { f.Handle });

				FieldInfo fiWholeWindow = tHwnd.GetField("whole_window",
					BindingFlags.NonPublic | BindingFlags.Instance);
				IntPtr hWindow = (IntPtr)fiWholeWindow.GetValue(oHwnd);

				XClassHint xch = new XClassHint();
				xch.res_name = Marshal.StringToCoTaskMemAnsi(strName ?? string.Empty);
				xch.res_class = Marshal.StringToCoTaskMemAnsi(strClass ?? string.Empty);
				IntPtr pXch = Marshal.AllocCoTaskMem(Marshal.SizeOf(xch));
				Marshal.StructureToPtr(xch, pXch, false);

				XSetClassHint(hDisplay, hWindow, pXch);

				Marshal.FreeCoTaskMem(pXch);
				Marshal.FreeCoTaskMem(xch.res_name);
				Marshal.FreeCoTaskMem(xch.res_class);
			}
			catch(Exception) { Debug.Assert(false); }
		}
#endif
	}
}
