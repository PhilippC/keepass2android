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
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

#if (!KeePassLibSD && !KeePassRT)
using System.Security.AccessControl;
#endif

using KeePassLib.Native;
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

			// Prevent transactions for FTP URLs under .NET 4.0 in order to
			// avoid/workaround .NET bug 621450:
			// https://connect.microsoft.com/VisualStudio/feedback/details/621450/problem-renaming-file-on-ftp-server-using-ftpwebrequest-in-net-framework-4-0-vs2010-only
			if(m_iocBase.Path.StartsWith("ftp:", StrUtil.CaseIgnoreCmp) &&
				(Environment.Version.Major >= 4) && !NativeLib.IsUnix())
				m_bTransacted = false;

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

#if (!KeePassLibSD && !KeePassRT)
			FileSecurity bkSecurity = null;
			bool bEfsEncrypted = false;
#endif

			if(IOConnection.FileExists(m_iocBase))
			{
#if (!KeePassLibSD && !KeePassRT)
				if(m_iocBase.IsLocalFile())
				{
					try
					{
						FileAttributes faBase = File.GetAttributes(m_iocBase.Path);
						bEfsEncrypted = ((long)(faBase & FileAttributes.Encrypted) != 0);

						DateTime tCreation = File.GetCreationTime(m_iocBase.Path);
						bkSecurity = File.GetAccessControl(m_iocBase.Path);

						File.SetCreationTime(m_iocTemp.Path, tCreation);
					}
					catch(Exception) { Debug.Assert(false); }
				}
#endif

				IOConnection.DeleteFile(m_iocBase);
			}

			IOConnection.RenameFile(m_iocTemp, m_iocBase);

#if (!KeePassLibSD && !KeePassRT)
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
	}
}
