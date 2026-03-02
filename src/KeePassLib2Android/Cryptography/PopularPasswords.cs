/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2025 Dominik Reichl <dominik.reichl@t-online.de>

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

namespace KeePassLib.Cryptography
{
  public static class PopularPasswords
  {
    private static readonly Dictionary<int, Dictionary<char[], bool>> g_dicts =
        new Dictionary<int, Dictionary<char[], bool>>();

    internal static int MaxLength
    {
      get
      {
        Debug.Assert(g_dicts.Count > 0); // Should be initialized

        int iMaxLen = 0;
        foreach (int iLen in g_dicts.Keys)
        {
          if (iLen > iMaxLen) iMaxLen = iLen;
        }

        return iMaxLen;
      }
    }

    internal static bool ContainsLength(int nLength)
    {
      Dictionary<char[], bool> dDummy;
      return g_dicts.TryGetValue(nLength, out dDummy);
    }

    public static bool IsPopularPassword(char[] vPassword)
    {
      ulong uDummy;
      return IsPopularPassword(vPassword, out uDummy);
    }

    public static bool IsPopularPassword(char[] vPassword, out ulong uDictSize)
    {
      if (vPassword == null) throw new ArgumentNullException("vPassword");
      if (vPassword.Length == 0) { uDictSize = 0; return false; }

#if DEBUG
      Array.ForEach(vPassword, ch => Debug.Assert(ch == char.ToLower(ch)));
#endif

      try { return IsPopularPasswordPriv(vPassword, out uDictSize); }
      catch (Exception) { Debug.Assert(false); }

      uDictSize = 0;
      return false;
    }

    private static bool IsPopularPasswordPriv(char[] vPassword, out ulong uDictSize)
    {
      Debug.Assert(g_dicts.Count > 0); // Should be initialized with data

      Dictionary<char[], bool> d;
      if (!g_dicts.TryGetValue(vPassword.Length, out d))
      {
        uDictSize = 0;
        return false;
      }

      uDictSize = (ulong)d.Count;
      return d.ContainsKey(vPassword);
    }

    public static void Add(byte[] pbData, bool bGZipped)
    {
      try
      {
        if (bGZipped)
          pbData = MemUtil.Decompress(pbData);

        string strData = StrUtil.Utf8.GetString(pbData, 0, pbData.Length);
        if (string.IsNullOrEmpty(strData)) { Debug.Assert(false); return; }

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i <= strData.Length; ++i)
        {
          char ch = ((i == strData.Length) ? ' ' : strData[i]);

          if (char.IsWhiteSpace(ch))
          {
            int cc = sb.Length;
            if (cc > 0)
            {
              char[] vWord = new char[cc];
              sb.CopyTo(0, vWord, 0, cc);

              Dictionary<char[], bool> d;
              if (!g_dicts.TryGetValue(cc, out d))
              {
                d = new Dictionary<char[], bool>(MemUtil.ArrayHelperExOfChar);
                g_dicts[cc] = d;
              }

              d[vWord] = true;
              sb.Remove(0, cc);
            }
          }
          else sb.Append(char.ToLower(ch));
        }
      }
      catch (Exception) { Debug.Assert(false); }
    }
  }
}
