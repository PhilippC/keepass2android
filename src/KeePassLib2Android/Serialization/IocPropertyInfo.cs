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
using System.Text;

using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
	public sealed class IocPropertyInfo
	{
		private readonly string m_strName;
		public string Name
		{
			get { return m_strName; }
		}

		private readonly Type m_t;
		public Type Type
		{
			get { return m_t; }
		}

		private string m_strDisplayName;
		public string DisplayName
		{
			get { return m_strDisplayName; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strDisplayName = value;
			}
		}

		private List<string> m_lProtocols = new List<string>();
		public IEnumerable<string> Protocols
		{
			get { return m_lProtocols; }
		}

		public IocPropertyInfo(string strName, Type t, string strDisplayName,
			string[] vProtocols)
		{
			if(strName == null) throw new ArgumentNullException("strName");
			if(t == null) throw new ArgumentNullException("t");
			if(strDisplayName == null) throw new ArgumentNullException("strDisplayName");

			m_strName = strName;
			m_t = t;
			m_strDisplayName = strDisplayName;

			AddProtocols(vProtocols);
		}

		public void AddProtocols(string[] v)
		{
			if(v == null) { Debug.Assert(false); return; }

			foreach(string strProtocol in v)
			{
				if(strProtocol == null) continue;

				string str = strProtocol.Trim();
				if(str.Length == 0) continue;

				bool bFound = false;
				foreach(string strEx in m_lProtocols)
				{
					if(strEx.Equals(str, StrUtil.CaseIgnoreCmp))
					{
						bFound = true;
						break;
					}
				}

				if(!bFound) m_lProtocols.Add(str);
			}
		}
	}
}
