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
using System.Diagnostics;

using KeePassLib.Utility;

namespace KeePassLib.Cryptography
{
	public static class PopularPasswords
	{
		private static Dictionary<int, Dictionary<string, bool>> m_dicts =
			new Dictionary<int, Dictionary<string, bool>>();

		internal static int MaxLength
		{
			get
			{
				Debug.Assert(m_dicts.Count > 0); // Should be initialized

				int iMaxLen = 0;
				foreach(int iLen in m_dicts.Keys)
				{
					if(iLen > iMaxLen) iMaxLen = iLen;
				}

				return iMaxLen;
			}
		}

		internal static bool ContainsLength(int nLength)
		{
			Dictionary<string, bool> dDummy;
			return m_dicts.TryGetValue(nLength, out dDummy);
		}

		public static bool IsPopularPassword(char[] vPassword)
		{
			ulong uDummy;
			return IsPopularPassword(vPassword, out uDummy);
		}

		public static bool IsPopularPassword(char[] vPassword, out ulong uDictSize)
		{
			if(vPassword == null) throw new ArgumentNullException("vPassword");
			if(vPassword.Length == 0) { uDictSize = 0; return false; }

			string str = new string(vPassword);

			try { return IsPopularPasswordPriv(str, out uDictSize); }
			catch(Exception) { Debug.Assert(false); }

			uDictSize = 0;
			return false;
		}

		private static bool IsPopularPasswordPriv(string str, out ulong uDictSize)
		{
			Debug.Assert(m_dicts.Count > 0); // Should be initialized with data

			Dictionary<string, bool> d;
			if(!m_dicts.TryGetValue(str.Length, out d))
			{
				uDictSize = 0;
				return false;
			}

			uDictSize = (ulong)d.Count;
			return d.ContainsKey(str);
		}

		public static void Add(byte[] pbData, bool bGZipped)
		{
			try
			{
				if(bGZipped)
					pbData = MemUtil.Decompress(pbData);

				string strData = StrUtil.Utf8.GetString(pbData, 0, pbData.Length);
				if(string.IsNullOrEmpty(strData)) { Debug.Assert(false); return; }

				if(!char.IsWhiteSpace(strData[strData.Length - 1]))
					strData += "\n";

				StringBuilder sb = new StringBuilder();
				for(int i = 0; i < strData.Length; ++i)
				{
					char ch = strData[i];

					if(char.IsWhiteSpace(ch))
					{
						int cc = sb.Length;
						if(cc > 0)
						{
							string strWord = sb.ToString();
							Debug.Assert(strWord.Length == cc);

							Dictionary<string, bool> d;
							if(!m_dicts.TryGetValue(cc, out d))
							{
								d = new Dictionary<string, bool>();
								m_dicts[cc] = d;
							}

							d[strWord] = true;
							sb.Remove(0, cc);
						}
					}
					else sb.Append(char.ToLower(ch));
				}
			}
			catch(Exception) { Debug.Assert(false); }
		}
	}
}
