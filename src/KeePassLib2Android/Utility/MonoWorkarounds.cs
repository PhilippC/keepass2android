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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;


using KeePassLib.Native;

namespace KeePassLib.Utility
{
	public static class MonoWorkarounds
	{
		private static Dictionary<uint, bool> m_dForceReq = new Dictionary<uint, bool>();
		private static Thread m_thFixClip = null;


		private static bool? m_bReq = null;
		public static bool IsRequired()
		{
			if(!m_bReq.HasValue) m_bReq = NativeLib.IsUnix();
			return m_bReq.Value;
		}

		// 1219:
		//   Mono prepends byte order mark (BOM) to StdIn.
		//   https://sourceforge.net/p/keepass/bugs/1219/
		// 1245:
		//   Key events not raised while Alt is down, and nav keys out of order.
		//   https://sourceforge.net/p/keepass/bugs/1245/
		// 1254:
		//   NumericUpDown bug: text is drawn below up/down buttons.
		//   https://sourceforge.net/p/keepass/bugs/1254/
		// 1354:
		//   Finalizer of NotifyIcon throws on Unity.
		//   https://sourceforge.net/p/keepass/bugs/1354/
		// 1358:
		//   FileDialog crashes when ~/.recently-used is invalid.
		//   https://sourceforge.net/p/keepass/bugs/1358/
		// 1366:
		//   Drawing bug when scrolling a RichTextBox.
		//   https://sourceforge.net/p/keepass/bugs/1366/
		// 1378:
		//   Mono doesn't implement Microsoft.Win32.SystemEvents events.
		//   https://sourceforge.net/p/keepass/bugs/1378/
		//   https://github.com/mono/mono/blob/master/mcs/class/System/Microsoft.Win32/SystemEvents.cs
		// 1418:
		//   Minimizing a form while loading it doesn't work.
		//   https://sourceforge.net/p/keepass/bugs/1418/
		// 2139:
		//   Shortcut keys are ignored.
		//   https://sourceforge.net/p/keepass/feature-requests/2139/
		// 2140:
		//   Explicit control focusing is ignored.
		//   https://sourceforge.net/p/keepass/feature-requests/2140/
		// 5795:
		//   Text in input field is incomplete.
		//   https://bugzilla.xamarin.com/show_bug.cgi?id=5795
		//   https://sourceforge.net/p/keepass/discussion/329220/thread/d23dc88b/
		// 10163:
		//   WebRequest GetResponse call missing, breaks WebDAV due to no PUT.
		//   https://bugzilla.xamarin.com/show_bug.cgi?id=10163
		//   https://sourceforge.net/p/keepass/bugs/1117/
		//   https://sourceforge.net/p/keepass/discussion/329221/thread/9422258c/
		//   https://github.com/mono/mono/commit/8e67b8c2fc7cb66bff7816ebf7c1039fb8cfc43b
		//   https://bugzilla.xamarin.com/show_bug.cgi?id=1512
		//   https://sourceforge.net/p/keepass/patches/89/
		// 12525:
		//   PictureBox not rendered when bitmap height >= control height.
		//   https://bugzilla.xamarin.com/show_bug.cgi?id=12525
		//   https://sourceforge.net/p/keepass/discussion/329220/thread/54f61e9a/
		// 586901:
		//   RichTextBox doesn't handle Unicode string correctly.
		//   https://bugzilla.novell.com/show_bug.cgi?id=586901
		// 620618:
		//   ListView column headers not drawn.
		//   https://bugzilla.novell.com/show_bug.cgi?id=620618
		// 649266:
		//   Calling Control.Hide doesn't remove the application from taskbar.
		//   https://bugzilla.novell.com/show_bug.cgi?id=649266
		// 686017:
		//   Minimum sizes must be enforced.
		//   http://bugs.debian.org/cgi-bin/bugreport.cgi?bug=686017
		// 801414:
		//   Mono recreates the main window incorrectly.
		//   https://bugs.launchpad.net/ubuntu/+source/keepass2/+bug/801414
		// 891029:
		//   Increase tab control height, otherwise Mono throws exceptions.
		//   https://sourceforge.net/projects/keepass/forums/forum/329221/topic/4519750
		//   https://bugs.launchpad.net/ubuntu/+source/keepass2/+bug/891029
		// 836428016:
		//   ListView group header selection unsupported.
		//   https://sourceforge.net/p/keepass/discussion/329221/thread/31dae0f0/
		// 3574233558:
		//   Problems with minimizing windows, no content rendered.
		//   https://sourceforge.net/p/keepass/discussion/329220/thread/d50a79d6/
		//   https://bugs.launchpad.net/ubuntu/+source/keepass2/+bug/801414
		// 891029:
		//   Increase tab control height, otherwise Mono throws exceptions.
		//   https://sourceforge.net/projects/keepass/forums/forum/329221/topic/4519750
		//   https://bugs.launchpad.net/ubuntu/+source/keepass2/+bug/891029
		// 836428016:
		//   ListView group header selection unsupported.
		//   https://sourceforge.net/p/keepass/discussion/329221/thread/31dae0f0/
		// 3574233558:
		//   Problems with minimizing windows, no content rendered.
		//   https://sourceforge.net/p/keepass/discussion/329220/thread/d50a79d6/
		public static bool IsRequired(uint uBugID)
		{
			if(!MonoWorkarounds.IsRequired()) return false;

			bool bForce;
			if(m_dForceReq.TryGetValue(uBugID, out bForce)) return bForce;

			ulong v = NativeLib.MonoVersion;
			if(v != 0)
			{
				if(uBugID == 10163)
					return (v >= 0x0002000B00000000UL); // >= 2.11
			}

			return true;
		}

		internal static void SetEnabled(string strIDs, bool bEnabled)
		{
			if(string.IsNullOrEmpty(strIDs)) return;

			string[] vIDs = strIDs.Split(new char[] { ',' });
			foreach(string strID in vIDs)
			{
				if(string.IsNullOrEmpty(strID)) continue;

				uint uID;
				if(StrUtil.TryParseUInt(strID.Trim(), out uID))
					m_dForceReq[uID] = bEnabled;
			}
		}

		internal static void Initialize()
		{
			Terminate();

			// m_fOwnWindow = fOwnWindow;

			
		}

		internal static void Terminate()
		{
			if(m_thFixClip != null)
			{
				try { m_thFixClip.Abort(); }
				catch(Exception) { Debug.Assert(false); }

				m_thFixClip = null;
			}
		}

		/// <summary>
		/// Ensure that the file ~/.recently-used is valid (in order to
		/// prevent Mono's FileDialog from crashing).
		/// </summary>
		internal static void EnsureRecentlyUsedValid()
		{
			if(!MonoWorkarounds.IsRequired(1358)) return;

			try
			{
				string strFile = Environment.GetFolderPath(
					Environment.SpecialFolder.Personal);
				strFile = UrlUtil.EnsureTerminatingSeparator(strFile, false);
				strFile += ".recently-used";

				if(File.Exists(strFile))
				{
					try
					{
						// Mono's WriteRecentlyUsedFiles method also loads the
						// XML file using XmlDocument
						XmlDocument xd = new XmlDocument();
						xd.Load(strFile);
					}
					catch(Exception) // The XML file is invalid
					{
						File.Delete(strFile);
					}
				}
			}
			catch(Exception) { Debug.Assert(false); }
		}

	}
}
