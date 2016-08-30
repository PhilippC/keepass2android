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
using System.IO;
using System.Text;

#if (!KeePassLibSD && !KeePassUAP)
using System.Security.AccessControl;
#endif

using KeePassLib.Native;
using KeePassLib.Resources;
using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
	public sealed class FileTransactionEx
	{
		private bool m_bTransacted;
		private IOConnectionInfo m_iocBase;
		private IOConnectionInfo m_iocTemp;

		private bool m_bMadeUnhidden = false;

		private const string StrTempSuffix = ".tmp";

		private static Dictionary<string, bool> g_dEnabled =
			new Dictionary<string, bool>(StrUtil.CaseIgnoreComparer);

		private static bool g_bExtraSafe = false;
		internal static bool ExtraSafe
		{
			get { return g_bExtraSafe; }
			set { g_bExtraSafe = value; }
		}

		public FileTransactionEx(IOConnectionInfo iocBaseFile)
		{
			Initialize(iocBaseFile, true);
		}

		public FileTransactionEx(IOConnectionInfo iocBaseFile, bool bTransacted)
		{
			Initialize(iocBaseFile, bTransacted);
		}

		private void Initialize(IOConnectionInfo iocBaseFile, bool bTransacted)
		{
			if(iocBaseFile == null) throw new ArgumentNullException("iocBaseFile");

			m_bTransacted = bTransacted;
			m_iocBase = iocBaseFile.CloneDeep();

			string strPath = m_iocBase.Path;

#if !KeePassUAP
			// Prevent transactions for FTP URLs under .NET 4.0 in order to
			// avoid/workaround .NET bug 621450:
			// https://connect.microsoft.com/VisualStudio/feedback/details/621450/problem-renaming-file-on-ftp-server-using-ftpwebrequest-in-net-framework-4-0-vs2010-only
			if(strPath.StartsWith("ftp:", StrUtil.CaseIgnoreCmp) &&
				(Environment.Version.Major >= 4) && !NativeLib.IsUnix())
				m_bTransacted = false;
			else
			{
#endif
				foreach(KeyValuePair<string, bool> kvp in g_dEnabled)
				{
					if(strPath.StartsWith(kvp.Key, StrUtil.CaseIgnoreCmp))
					{
						m_bTransacted = kvp.Value;
						break;
					}
				}
#if !KeePassUAP
			}
#endif

			if(m_bTransacted)
			{
				m_iocTemp = m_iocBase.CloneDeep();
				m_iocTemp.Path += StrTempSuffix;
			}
			else m_iocTemp = m_iocBase;
		}

		public Stream OpenWrite()
		{
			if(!m_bTransacted) m_bMadeUnhidden = UrlUtil.UnhideFile(m_iocTemp.Path);
			else // m_bTransacted
			{
				try { IOConnection.DeleteFile(m_iocTemp); }
				catch(Exception) { }
			}

			return IOConnection.OpenWrite(m_iocTemp);
		}

		public void CommitWrite()
		{
			if(m_bTransacted) CommitWriteTransaction();
			else // !m_bTransacted
			{
				if(m_bMadeUnhidden) UrlUtil.HideFile(m_iocTemp.Path, true); // Hide again
			}
		}

		private void CommitWriteTransaction()
		{
			bool bMadeUnhidden = UrlUtil.UnhideFile(m_iocBase.Path);

#if (!KeePassLibSD && !KeePassUAP)
			FileSecurity bkSecurity = null;
			bool bEfsEncrypted = false;
#endif

			if(g_bExtraSafe)
			{
				if(!IOConnection.FileExists(m_iocTemp))
					throw new FileNotFoundException(m_iocTemp.Path +
						MessageService.NewLine + KLRes.FileSaveFailed);
			}

			if(IOConnection.FileExists(m_iocBase))
			{
#if !KeePassLibSD
				if(m_iocBase.IsLocalFile())
				{
					try
					{
#if !KeePassUAP
						FileAttributes faBase = File.GetAttributes(m_iocBase.Path);
						bEfsEncrypted = ((long)(faBase & FileAttributes.Encrypted) != 0);
#endif
						DateTime tCreation = File.GetCreationTime(m_iocBase.Path);
						File.SetCreationTime(m_iocTemp.Path, tCreation);
#if !KeePassUAP
						// May throw with Mono
						bkSecurity = File.GetAccessControl(m_iocBase.Path);
#endif
					}
					catch(Exception) { Debug.Assert(NativeLib.IsUnix()); }
				}
#endif

				IOConnection.DeleteFile(m_iocBase);
			}

			IOConnection.RenameFile(m_iocTemp, m_iocBase);

#if (!KeePassLibSD && !KeePassUAP)
			if(m_iocBase.IsLocalFile())
			{
				try
				{
					if(bEfsEncrypted)
					{
						try { File.Encrypt(m_iocBase.Path); }
						catch(Exception) { Debug.Assert(false); }
					}

					if(bkSecurity != null)
						File.SetAccessControl(m_iocBase.Path, bkSecurity);
				}
				catch(Exception) { Debug.Assert(false); }
			}
#endif

			if(bMadeUnhidden) UrlUtil.HideFile(m_iocBase.Path, true); // Hide again
		}

		// For plugins
		public static void Configure(string strPrefix, bool? obTransacted)
		{
			if(string.IsNullOrEmpty(strPrefix)) { Debug.Assert(false); return; }

			if(obTransacted.HasValue)
				g_dEnabled[strPrefix] = obTransacted.Value;
			else g_dEnabled.Remove(strPrefix);
		}
	}
}
