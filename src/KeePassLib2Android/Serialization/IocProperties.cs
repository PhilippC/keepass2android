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
using System.Globalization;
using System.Text;
using System.Xml;

using KeePassLib.Interfaces;
using KeePassLib.Utility;

using StrDict = System.Collections.Generic.Dictionary<string, string>;

namespace KeePassLib.Serialization
{
	public interface IHasIocProperties
	{
		IocProperties IOConnectionProperties { get; set; }
	}

	public sealed class IocProperties : IDeepCloneable<IocProperties>
	{
		private StrDict m_dict = new StrDict();

		public IocProperties()
		{
		}

		public IocProperties CloneDeep()
		{
			IocProperties p = new IocProperties();
			p.m_dict = new StrDict(m_dict);
			return p;
		}

		public string Get(string strKey)
		{
			if(string.IsNullOrEmpty(strKey)) return null;

			foreach(KeyValuePair<string, string> kvp in m_dict)
			{
				if(kvp.Key.Equals(strKey, StrUtil.CaseIgnoreCmp))
					return kvp.Value;
			}

			return null;
		}

		public void Set(string strKey, string strValue)
		{
			if(string.IsNullOrEmpty(strKey)) { Debug.Assert(false); return; }

			foreach(KeyValuePair<string, string> kvp in m_dict)
			{
				if(kvp.Key.Equals(strKey, StrUtil.CaseIgnoreCmp))
				{
					if(string.IsNullOrEmpty(strValue)) m_dict.Remove(kvp.Key);
					else m_dict[kvp.Key] = strValue;
					return;
				}
			}

			if(!string.IsNullOrEmpty(strValue)) m_dict[strKey] = strValue;
		}

		public bool? GetBool(string strKey)
		{
			string str = Get(strKey);
			if(string.IsNullOrEmpty(str)) return null;

			return StrUtil.StringToBool(str);
		}

		public void SetBool(string strKey, bool? ob)
		{
			if(ob.HasValue) Set(strKey, (ob.Value ? "1" : "0"));
			else Set(strKey, null);
		}

		public long? GetLong(string strKey)
		{
			string str = Get(strKey);
			if(string.IsNullOrEmpty(str)) return null;

			long l;
			if(StrUtil.TryParseLongInvariant(str, out l)) return l;
			Debug.Assert(false);
			return null;
		}

		public void SetLong(string strKey, long? ol)
		{
			if(ol.HasValue)
				Set(strKey, ol.Value.ToString(NumberFormatInfo.InvariantInfo));
			else Set(strKey, null);
		}

		public string Serialize()
		{
			if(m_dict.Count == 0) return string.Empty;

			StringBuilder sbAll = new StringBuilder();
			foreach(KeyValuePair<string, string> kvp in m_dict)
			{
				sbAll.Append(kvp.Key);
				sbAll.Append(kvp.Value);
			}

			string strAll = sbAll.ToString();
			char chSepOuter = ';';
			if(strAll.IndexOf(chSepOuter) >= 0)
				chSepOuter = StrUtil.GetUnusedChar(strAll);

			strAll += chSepOuter;
			char chSepInner = '=';
			if(strAll.IndexOf(chSepInner) >= 0)
				chSepInner = StrUtil.GetUnusedChar(strAll);

			StringBuilder sb = new StringBuilder();
			sb.Append(chSepOuter);
			sb.Append(chSepInner);

			foreach(KeyValuePair<string, string> kvp in m_dict)
			{
				sb.Append(chSepOuter);
				sb.Append(kvp.Key);
				sb.Append(chSepInner);
				sb.Append(kvp.Value);
			}

			return sb.ToString();
		}

		public static IocProperties Deserialize(string strSerialized)
		{
			IocProperties p = new IocProperties();
			if(string.IsNullOrEmpty(strSerialized)) return p; // No assert

			char chSepOuter = strSerialized[0];
			string[] v = strSerialized.Substring(1).Split(new char[] { chSepOuter });
			if((v == null) || (v.Length < 2)) { Debug.Assert(false); return p; }

			string strMeta = v[0];
			if(string.IsNullOrEmpty(strMeta)) { Debug.Assert(false); return p; }

			char chSepInner = strMeta[0];
			char[] vSepInner = new char[] { chSepInner };

			for(int i = 1; i < v.Length; ++i)
			{
				string strProp = v[i];
				if(string.IsNullOrEmpty(strProp)) { Debug.Assert(false); continue; }

				string[] vProp = strProp.Split(vSepInner);
				if((vProp == null) || (vProp.Length < 2)) { Debug.Assert(false); continue; }
				Debug.Assert(vProp.Length == 2);

				p.Set(vProp[0], vProp[1]);
			}

			return p;
		}

		public void CopyTo(IocProperties p)
		{
			if(p == null) { Debug.Assert(false); return; }

			foreach(KeyValuePair<string, string> kvp in m_dict)
			{
				p.m_dict[kvp.Key] = kvp.Value;
			}
		}
	}
}
