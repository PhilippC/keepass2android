/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2012 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Threading;
using System.Diagnostics;

using KeePassLib.Cryptography;
using KeePassLib.Resources;
using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
	public sealed class FileLockException : Exception
	{
		private readonly string m_strMsg;

		public override string Message
		{
			get { return m_strMsg; }
		}

		public FileLockException(string strBaseFile, string strUser)
		{
			StringBuilder sb = new StringBuilder();

			if(!string.IsNullOrEmpty(strBaseFile))
			{
				sb.Append(strBaseFile);
				sb.Append(MessageService.NewParagraph);
			}

			sb.Append(KLRes.FileLockedWrite);
			sb.Append(MessageService.NewLine);

			if(!string.IsNullOrEmpty(strUser)) sb.Append(strUser);
			else sb.Append("?");

			sb.Append(MessageService.NewParagraph);
			sb.Append(KLRes.TryAgainSecs);

			m_strMsg = sb.ToString();
		}
	}

	public sealed class FileLock : IDisposable
	{
		private const string LockFileExt = ".lock";
		private const string LockFileHeader = "KeePass Lock File";

		private IOConnectionInfo m_iocLockFile;

		private sealed class LockFileInfo
		{
			public readonly string ID;
			public readonly DateTime Time;
			public readonly string UserName;
			public readonly string Machine;
			public readonly string Domain;

			private LockFileInfo(string strID, string strTime, string strUserName,
				string strMachine, string strDomain)
			{
				this.ID = (strID ?? string.Empty).Trim();

				DateTime dt;
				if(TimeUtil.TryDeserializeUtc(strTime.Trim(), out dt))
					this.Time = dt;
				else
				{
					Debug.Assert(false);
					this.Time = DateTime.UtcNow;
				}

				this.UserName = (strUserName ?? string.Empty).Trim();
				this.Machine = (strMachine ?? string.Empty).Trim();
				this.Domain = (strDomain ?? string.Empty).Trim();

				if(this.Domain.Equals(this.Machine, StrUtil.CaseIgnoreCmp))
					this.Domain = string.Empty;
			}

			public string GetOwner()
			{
				StringBuilder sb = new StringBuilder();
				sb.Append((this.UserName.Length > 0) ? this.UserName : "?");

				bool bMachine = (this.Machine.Length > 0);
				bool bDomain = (this.Domain.Length > 0);
				if(bMachine || bDomain)
				{
					sb.Append(" (");
					sb.Append(this.Machine);
					if(bMachine && bDomain) sb.Append(" @ ");
					sb.Append(this.Domain);
					sb.Append(")");
				}

				return sb.ToString();
			}

			public static LockFileInfo Load(IOConnectionInfo iocLockFile)
			{
				Stream s = null;
				try
				{
					s = IOConnection.OpenRead(iocLockFile);
					if(s == null) return null;
					StreamReader sr = new StreamReader(s, StrUtil.Utf8);
					string str = sr.ReadToEnd();
					sr.Close();
					if(str == null) { Debug.Assert(false); return null; }

					str = StrUtil.NormalizeNewLines(str, false);
					string[] v = str.Split('\n');
					if((v == null) || (v.Length < 6)) { Debug.Assert(false); return null; }

					if(!v[0].StartsWith(LockFileHeader)) { Debug.Assert(false); return null; }
					return new LockFileInfo(v[1], v[2], v[3], v[4], v[5]);
				}
				catch(FileNotFoundException) { }
				catch(Exception) { Debug.Assert(false); }
				finally { if(s != null) s.Close(); }

				return null;
			}

			// Throws on error
			public static LockFileInfo Create(IOConnectionInfo iocLockFile)
			{
				LockFileInfo lfi;
				Stream s = null;
				try
				{
					byte[] pbID = CryptoRandom.Instance.GetRandomBytes(16);
					string strTime = TimeUtil.SerializeUtc(DateTime.Now);

#if !KeePassLibSD
					lfi = new LockFileInfo(Convert.ToBase64String(pbID), strTime,
						Environment.UserName, Environment.MachineName,
						Environment.UserDomainName);
#else
					lfi = new LockFileInfo(Convert.ToBase64String(pbID), strTime,
						string.Empty, string.Empty, string.Empty);
#endif

					StringBuilder sb = new StringBuilder();
#if !KeePassLibSD
					sb.AppendLine(LockFileHeader);
					sb.AppendLine(lfi.ID);
					sb.AppendLine(strTime);
					sb.AppendLine(lfi.UserName);
					sb.AppendLine(lfi.Machine);
					sb.AppendLine(lfi.Domain);
#else
					sb.Append(LockFileHeader + MessageService.NewLine);
					sb.Append(lfi.ID + MessageService.NewLine);
					sb.Append(strTime + MessageService.NewLine);
					sb.Append(lfi.UserName + MessageService.NewLine);
					sb.Append(lfi.Machine + MessageService.NewLine);
					sb.Append(lfi.Domain + MessageService.NewLine);
#endif

					byte[] pbFile = StrUtil.Utf8.GetBytes(sb.ToString());

					s = IOConnection.OpenWrite(iocLockFile);
					if(s == null) throw new IOException(iocLockFile.GetDisplayName());
					s.Write(pbFile, 0, pbFile.Length);
				}
				finally { if(s != null) s.Close(); }

				return lfi;
			}
		}

		public FileLock(IOConnectionInfo iocBaseFile)
		{
			if(iocBaseFile == null) throw new ArgumentNullException("strBaseFile");

			m_iocLockFile = iocBaseFile.CloneDeep();
			m_iocLockFile.Path += LockFileExt;

			LockFileInfo lfiEx = LockFileInfo.Load(m_iocLockFile);
			if(lfiEx != null)
			{
				m_iocLockFile = null; // Otherwise Dispose deletes the existing one
				throw new FileLockException(iocBaseFile.GetDisplayName(),
					lfiEx.GetOwner());
			}

			LockFileInfo.Create(m_iocLockFile);
		}

		~FileLock()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool bDisposing)
		{
			if(m_iocLockFile == null) return;

			bool bFileDeleted = false;
			for(int r = 0; r < 5; ++r)
			{
				// if(!OwnLockFile()) { bFileDeleted = true; break; }

				try
				{
					IOConnection.DeleteFile(m_iocLockFile);
					bFileDeleted = true;
				}
				catch(Exception) { Debug.Assert(false); }

				if(bFileDeleted) break;

				if(bDisposing) Thread.Sleep(50);
			}

			if(bDisposing && !bFileDeleted)
				IOConnection.DeleteFile(m_iocLockFile); // Possibly with exception

			m_iocLockFile = null;
		}

		// private bool OwnLockFile()
		// {
		//	if(m_iocLockFile == null) { Debug.Assert(false); return false; }
		//	if(m_strLockID == null) { Debug.Assert(false); return false; }
		//	LockFileInfo lfi = LockFileInfo.Load(m_iocLockFile);
		//	if(lfi == null) return false;
		//	return m_strLockID.Equals(lfi.ID);
		// }
	}
}
