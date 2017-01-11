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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Serialization;

using KeePassLib.Interfaces;
using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
	public enum IOCredSaveMode
	{
		/// <summary>
		/// Do not remember user name or password.
		/// </summary>
		NoSave = 0,

		/// <summary>
		/// Remember the user name only, not the password.
		/// </summary>
		UserNameOnly,

		/// <summary>
		/// Save both user name and password.
		/// </summary>
		SaveCred
	}

	public enum IOCredProtMode
	{
		None = 0,
		Obf
	}

	/* public enum IOFileFormatHint
	{
		None = 0,
		Deprecated
	} */

	public sealed class IOConnectionInfo : IDeepCloneable<IOConnectionInfo>
	{
		// private IOFileFormatHint m_ioHint = IOFileFormatHint.None;

		private string m_strUrl = string.Empty;
		public string Path
		{
			get { return m_strUrl; }
			set
			{
				Debug.Assert(value != null);
				if(value == null) throw new ArgumentNullException("value");

				m_strUrl = value;
			}
		}

		private string m_strUser = string.Empty;
		[DefaultValue("")]
		public string UserName
		{
			get { return m_strUser; }
			set
			{
				Debug.Assert(value != null);
				if(value == null) throw new ArgumentNullException("value");

				m_strUser = value;
			}
		}

		private string m_strPassword = string.Empty;
		[DefaultValue("")]
		public string Password
		{
			get { return m_strPassword; }
			set
			{
				Debug.Assert(value != null);
				if(value == null) throw new ArgumentNullException("value");

				m_strPassword = value;
			}
		}

		private IOCredProtMode m_ioCredProtMode = IOCredProtMode.None;
		public IOCredProtMode CredProtMode
		{
			get { return m_ioCredProtMode; }
			set { m_ioCredProtMode = value; }
		}

		private IOCredSaveMode m_ioCredSaveMode = IOCredSaveMode.NoSave;
		public IOCredSaveMode CredSaveMode
		{
			get { return m_ioCredSaveMode; }
			set { m_ioCredSaveMode = value; }
		}

		private bool m_bComplete = false;
		[XmlIgnore]
		public bool IsComplete // Credentials etc. fully specified
		{
			get { return m_bComplete; }
			set { m_bComplete = value; }
		}

		/* public IOFileFormatHint FileFormatHint
		{
			get { return m_ioHint; }
			set { m_ioHint = value; }
		} */

		private IocProperties m_props = new IocProperties();
		[XmlIgnore]
		public IocProperties Properties
		{
			get { return m_props; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_props = value;
			}
		}

		/// <summary>
		/// For serialization only; use <c>Properties</c> in code.
		/// </summary>
		[DefaultValue("")]
		public string PropertiesEx
		{
			get { return m_props.Serialize(); }
			set
			{
				if(value == null) throw new ArgumentNullException("value");

				IocProperties p = IocProperties.Deserialize(value);
				Debug.Assert(p != null);
				m_props = (p ?? new IocProperties());
			}
		}

		public IOConnectionInfo CloneDeep()
		{
			IOConnectionInfo ioc = (IOConnectionInfo)this.MemberwiseClone();
			ioc.m_props = m_props.CloneDeep();
			return ioc;
		}

#if DEBUG // For debugger display only
		public override string ToString()
		{
			return GetDisplayName();
		}
#endif

		/*
		/// <summary>
		/// Serialize the current connection info to a string. Credentials
		/// are serialized based on the <c>CredSaveMode</c> property.
		/// </summary>
		/// <param name="iocToCompile">Input object to be serialized.</param>
		/// <returns>Serialized object as string.</returns>
		public static string SerializeToString(IOConnectionInfo iocToCompile)
		{
			Debug.Assert(iocToCompile != null);
			if(iocToCompile == null) throw new ArgumentNullException("iocToCompile");

			string strUrl = iocToCompile.Path;
			string strUser = TransformUnreadable(iocToCompile.UserName, true);
			string strPassword = TransformUnreadable(iocToCompile.Password, true);

			string strAll = strUrl + strUser + strPassword + "CUN";
			char chSep = StrUtil.GetUnusedChar(strAll);
			if(chSep == char.MinValue) throw new FormatException();

			StringBuilder sb = new StringBuilder();
			sb.Append(chSep);
			sb.Append(strUrl);
			sb.Append(chSep);

			if(iocToCompile.CredSaveMode == IOCredSaveMode.SaveCred)
			{
				sb.Append('C');
				sb.Append(chSep);
				sb.Append(strUser);
				sb.Append(chSep);
				sb.Append(strPassword);
			}
			else if(iocToCompile.CredSaveMode == IOCredSaveMode.UserNameOnly)
			{
				sb.Append('U');
				sb.Append(chSep);
				sb.Append(strUser);
				sb.Append(chSep);
			}
			else // Don't remember credentials
			{
				sb.Append('N');
				sb.Append(chSep);
				sb.Append(chSep);
			}

			return sb.ToString();
		}

		public static IOConnectionInfo UnserializeFromString(string strToDecompile)
		{
			Debug.Assert(strToDecompile != null);
			if(strToDecompile == null) throw new ArgumentNullException("strToDecompile");
			if(strToDecompile.Length <= 1) throw new ArgumentException();

			char chSep = strToDecompile[0];
			string[] vParts = strToDecompile.Substring(1, strToDecompile.Length -
				1).Split(new char[]{ chSep });
			if(vParts.Length < 4) throw new ArgumentException();

			IOConnectionInfo s = new IOConnectionInfo();
			s.Path = vParts[0];

			if(vParts[1] == "C")
				s.CredSaveMode = IOCredSaveMode.SaveCred;
			else if(vParts[1] == "U")
				s.CredSaveMode = IOCredSaveMode.UserNameOnly;
			else
				s.CredSaveMode = IOCredSaveMode.NoSave;

			s.UserName = TransformUnreadable(vParts[2], false);
			s.Password = TransformUnreadable(vParts[3], false);
			return s;
		}
		*/

		/*
		/// <summary>
		/// Very simple string protection. Doesn't really encrypt the input
		/// string, only encodes it that it's not readable on the first glance.
		/// </summary>
		/// <param name="strToEncode">The string to encode/decode.</param>
		/// <param name="bEncode">If <c>true</c>, the string will be encoded,
		/// otherwise it'll be decoded.</param>
		/// <returns>Encoded/decoded string.</returns>
		private static string TransformUnreadable(string strToEncode, bool bEncode)
		{
			Debug.Assert(strToEncode != null);
			if(strToEncode == null) throw new ArgumentNullException("strToEncode");

			if(bEncode)
			{
				byte[] pbUtf8 = StrUtil.Utf8.GetBytes(strToEncode);

				unchecked
				{
					for(int iPos = 0; iPos < pbUtf8.Length; ++iPos)
						pbUtf8[iPos] += (byte)(iPos * 11);
				}

				return Convert.ToBase64String(pbUtf8);
			}
			else // Decode
			{
				byte[] pbBase = Convert.FromBase64String(strToEncode);

				unchecked
				{
					for(int iPos = 0; iPos < pbBase.Length; ++iPos)
						pbBase[iPos] -= (byte)(iPos * 11);
				}

				return StrUtil.Utf8.GetString(pbBase, 0, pbBase.Length);
			}
		}
		*/

		public string GetDisplayName()
		{
			string str = m_strUrl;

			if(m_strUser.Length > 0)
				str += (" (" + m_strUser + ")");

			return str;
		}

		public bool IsEmpty()
		{
			return (m_strUrl.Length == 0);
		}

		public static IOConnectionInfo FromPath(string strPath)
		{
			IOConnectionInfo ioc = new IOConnectionInfo();

			ioc.Path = strPath;
			ioc.CredSaveMode = IOCredSaveMode.NoSave;

			return ioc;
		}

		public bool CanProbablyAccess()
		{
			if(IsLocalFile()) return File.Exists(m_strUrl);

			return true;
		}

		public bool IsLocalFile()
		{
			// Not just ":/", see e.g. AppConfigEx.ChangePathRelAbs
			return (m_strUrl.IndexOf("://") < 0);
		}

		public void ClearCredentials(bool bDependingOnRememberMode)
		{
			if((bDependingOnRememberMode == false) ||
				(m_ioCredSaveMode == IOCredSaveMode.NoSave))
			{
				m_strUser = string.Empty;
			}

			if((bDependingOnRememberMode == false) ||
				(m_ioCredSaveMode == IOCredSaveMode.NoSave) ||
				(m_ioCredSaveMode == IOCredSaveMode.UserNameOnly))
			{
				m_strPassword = string.Empty;
			}
		}

		public void Obfuscate(bool bObf)
		{
			if(bObf && (m_ioCredProtMode == IOCredProtMode.None))
			{
				m_strPassword = StrUtil.Obfuscate(m_strPassword);
				m_ioCredProtMode = IOCredProtMode.Obf;
			}
			else if(!bObf && (m_ioCredProtMode == IOCredProtMode.Obf))
			{
				m_strPassword = StrUtil.Deobfuscate(m_strPassword);
				m_ioCredProtMode = IOCredProtMode.None;
			}
		}
	}
}
