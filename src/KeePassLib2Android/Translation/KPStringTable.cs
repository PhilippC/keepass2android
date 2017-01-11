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
using System.Text;
using System.Xml.Serialization;

#if !KeePassUAP
using System.Windows.Forms;
#endif

namespace KeePassLib.Translation
{
	public sealed class KPStringTable
	{
		private string m_strName = string.Empty;
		[XmlAttribute]
		public string Name
		{
			get { return m_strName; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_strName = value;
			}
		}

		private List<KPStringTableItem> m_vItems = new List<KPStringTableItem>();

		[XmlArrayItem("Data")]
		public List<KPStringTableItem> Strings
		{
			get { return m_vItems; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				m_vItems = value;
			}
		}

		public Dictionary<string, string> ToDictionary()
		{
			Dictionary<string, string> dict = new Dictionary<string, string>();

			foreach(KPStringTableItem kpstItem in m_vItems)
			{
				if(kpstItem.Value.Length > 0)
					dict[kpstItem.Name] = kpstItem.Value;
			}

			return dict;
		}

#if (!KeePassLibSD && !KeePassUAP)
		public void ApplyTo(ToolStripItemCollection tsic)
		{
			if(tsic == null) throw new ArgumentNullException("tsic");

			Dictionary<string, string> dict = this.ToDictionary();
			if(dict.Count == 0) return;

			this.ApplyTo(tsic, dict);
		}

		private void ApplyTo(ToolStripItemCollection tsic, Dictionary<string, string> dict)
		{
			if(tsic == null) return;

			foreach(ToolStripItem tsi in tsic)
			{
				if(tsi.Text.Length == 0) continue;

				string strTrl;
				if(dict.TryGetValue(tsi.Name, out strTrl))
					tsi.Text = strTrl;

				ToolStripMenuItem tsmi = tsi as ToolStripMenuItem;
				if((tsmi != null) && (tsmi.DropDownItems != null))
					this.ApplyTo(tsmi.DropDownItems);
			}
		}
#endif
	}
}
