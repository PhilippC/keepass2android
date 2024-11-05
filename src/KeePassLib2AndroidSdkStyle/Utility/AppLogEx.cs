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

#if !KeePassLibSD
using System.IO.Compression;
#endif

namespace KeePassLib.Utility
{
	/// <summary>
	/// Application-wide logging services.
	/// </summary>
	public static class AppLogEx
	{
		private static StreamWriter m_swOut = null;

		public static void Open(string strPrefix)
		{
			// Logging is not enabled in normal builds of KeePass!
			/*
			AppLogEx.Close();

			Debug.Assert(strPrefix != null);
			if(strPrefix == null) strPrefix = "Log";

			try
			{
				string strDirSep = string.Empty;
				strDirSep += UrlUtil.LocalDirSepChar;

				string strTemp = UrlUtil.GetTempPath();
				if(!strTemp.EndsWith(strDirSep))
					strTemp += strDirSep;

				string strPath = strTemp + strPrefix + "-";
				Debug.Assert(strPath.IndexOf('/') < 0);

				DateTime dtNow = DateTime.UtcNow;
				string strTime = dtNow.ToString("s");
				strTime = strTime.Replace('T', '-');
				strTime = strTime.Replace(':', '-');

				strPath += strTime + "-" + Environment.TickCount.ToString(
					NumberFormatInfo.InvariantInfo) + ".log.gz";

				FileStream fsOut = new FileStream(strPath, FileMode.Create,
					FileAccess.Write, FileShare.None);
				GZipStream gz = new GZipStream(fsOut, CompressionMode.Compress);
				m_swOut = new StreamWriter(gz);

				AppLogEx.Log("Started logging on " + dtNow.ToString("s") + ".");
			}
			catch(Exception) { Debug.Assert(false); }
			*/
		}

		public static void Close()
		{
			if(m_swOut == null) return;

			m_swOut.Close();
			m_swOut = null;
		}

		public static void Log(string strText)
		{
			if(m_swOut == null) return;

			if(strText == null) m_swOut.WriteLine();
			else m_swOut.WriteLine(strText);
		}

		public static void Log(Exception ex)
		{
			if(m_swOut == null) return;

			if(ex == null) m_swOut.WriteLine();
			else m_swOut.WriteLine(ex.ToString());
		}
	}
}
