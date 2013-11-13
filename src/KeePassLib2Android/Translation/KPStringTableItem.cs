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
using System.Xml.Serialization;

namespace KeePassLib.Translation
{
	public sealed class KPStringTableItem
	{
		private string m_strName = string.Empty;
		public string Name
		{
			get { return m_strName; }
			set { m_strName = value; }
		}

		private string m_strValue = string.Empty;
		public string Value
		{
			get { return m_strValue; }
			set { m_strValue = value; }
		}

		private string m_strEnglish = string.Empty;
		[XmlIgnore]
		public string ValueEnglish
		{
			get { return m_strEnglish; }
			set { m_strEnglish = value; }
		}
	}
}
