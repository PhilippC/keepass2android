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


using KeePassLib;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePass.Util.Spr
{
	/// <summary>
	/// String placeholders and field reference replacement engine.
	/// </summary>
	public static partial class SprEngine
	{
		// Legacy, for backward compatibility only; see PickChars
		private static string ReplacePickPw(string strText, SprContext ctx,
			uint uRecursionLevel)
		{
			if(ctx.Entry == null) { Debug.Assert(false); return strText; }

			string str = strText;

			while(true)
			{
				const string strStart = @"{PICKPASSWORDCHARS";

				int iStart = str.IndexOf(strStart, StrUtil.CaseIgnoreCmp);
				if(iStart < 0) break;

				int iEnd = str.IndexOf('}', iStart);
				if(iEnd < 0) break;

				string strPlaceholder = str.Substring(iStart, iEnd - iStart + 1);

				string strParam = str.Substring(iStart + strStart.Length,
					iEnd - (iStart + strStart.Length));
				string[] vParams = strParam.Split(new char[] { ':' });

				uint uCharCount = 0;
				if(vParams.Length >= 2) uint.TryParse(vParams[1], out uCharCount);

				str = ReplacePickPwPlaceholder(str, strPlaceholder, uCharCount,
					ctx, uRecursionLevel);
			}

			return str;
		}

		private static string ReplacePickPwPlaceholder(string str,
			string strPlaceholder, uint uCharCount, SprContext ctx,
			uint uRecursionLevel)
		{
			if(str.IndexOf(strPlaceholder, StrUtil.CaseIgnoreCmp) < 0) return str;

			ProtectedString ps = ctx.Entry.Strings.Get(PwDefs.PasswordField);
			if(ps != null)
			{
				string strPassword = ps.ReadString();

				string strPick = SprEngine.CompileInternal(strPassword,
					ctx.WithoutContentTransformations(), uRecursionLevel + 1);

				if(!string.IsNullOrEmpty(strPick))
				{
					ProtectedString psPick = new ProtectedString(false, strPick);
					string strPicked = string.Empty;

					str = StrUtil.ReplaceCaseInsensitive(str, strPlaceholder,
						SprEngine.TransformContent(strPicked, ctx));
				}
			}

			return StrUtil.ReplaceCaseInsensitive(str, strPlaceholder, string.Empty);
		}


		private static string ConvertToDownArrows(string str, int iOffset,
			string strLayout)
		{
			if(string.IsNullOrEmpty(str)) return string.Empty;

			StringBuilder sb = new StringBuilder();
			for(int i = 0; i < str.Length; ++i)
			{
				// if((sb.Length > 0) && !string.IsNullOrEmpty(strSep)) sb.Append(strSep);

				char ch = str[i];

				int? iDowns = null;
				if(strLayout.Length == 0)
				{
					if((ch >= '0') && (ch <= '9')) iDowns = (int)ch - '0';
					else if((ch >= 'a') && (ch <= 'z')) iDowns = (int)ch - 'a';
					else if((ch >= 'A') && (ch <= 'Z')) iDowns = (int)ch - 'A';
				}
				else if(strLayout.Equals("0a", StrUtil.CaseIgnoreCmp))
				{
					if((ch >= '0') && (ch <= '9')) iDowns = (int)ch - '0';
					else if((ch >= 'a') && (ch <= 'z')) iDowns = (int)ch - 'a' + 10;
					else if((ch >= 'A') && (ch <= 'Z')) iDowns = (int)ch - 'A' + 10;
				}
				else if(strLayout.Equals("a0", StrUtil.CaseIgnoreCmp))
				{
					if((ch >= '0') && (ch <= '9')) iDowns = (int)ch - '0' + 26;
					else if((ch >= 'a') && (ch <= 'z')) iDowns = (int)ch - 'a';
					else if((ch >= 'A') && (ch <= 'Z')) iDowns = (int)ch - 'A';
				}
				else if(strLayout.Equals("1a", StrUtil.CaseIgnoreCmp))
				{
					if((ch >= '1') && (ch <= '9')) iDowns = (int)ch - '1';
					else if(ch == '0') iDowns = 9;
					else if((ch >= 'a') && (ch <= 'z')) iDowns = (int)ch - 'a' + 10;
					else if((ch >= 'A') && (ch <= 'Z')) iDowns = (int)ch - 'A' + 10;
				}
				else if(strLayout.Equals("a1", StrUtil.CaseIgnoreCmp))
				{
					if((ch >= '1') && (ch <= '9')) iDowns = (int)ch - '1' + 26;
					else if(ch == '0') iDowns = 9 + 26;
					else if((ch >= 'a') && (ch <= 'z')) iDowns = (int)ch - 'a';
					else if((ch >= 'A') && (ch <= 'Z')) iDowns = (int)ch - 'A';
				}

				if(!iDowns.HasValue) continue;

				for(int j = 0; j < (iOffset + iDowns); ++j) sb.Append(@"{DOWN}");
			}

			return sb.ToString();
		}
	}
}
