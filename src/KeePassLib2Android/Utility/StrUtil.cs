/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2021 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

#if !KeePassUAP
using System.Drawing;
using System.Security.Cryptography;
#endif

using KeePassLib.Collections;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Native;
using KeePassLib.Security;

namespace KeePassLib.Utility
{
	/// <summary>
	/// Character stream class.
	/// </summary>
	public sealed class CharStream
	{
		private readonly string m_str;
		private int m_iPos = 0;

		public long Position
		{
			get { return m_iPos; }
			set
			{
				if ((value < 0) || (value > int.MaxValue))
					throw new ArgumentOutOfRangeException("value");
				m_iPos = (int)value;
			}
		}

		public CharStream(string str)
		{
			if (str == null) { Debug.Assert(false); throw new ArgumentNullException("str"); }

			m_str = str;
		}

		public char ReadChar()
		{
			if (m_iPos >= m_str.Length) return char.MinValue;

			return m_str[m_iPos++];
		}

		public char ReadChar(bool bSkipWhiteSpace)
		{
			if (!bSkipWhiteSpace) return ReadChar();

			while (true)
			{
				char ch = ReadChar();
				if ((ch != ' ') && (ch != '\t') && (ch != '\r') && (ch != '\n'))
					return ch;
			}
		}

		public char PeekChar()
		{
			if (m_iPos >= m_str.Length) return char.MinValue;

			return m_str[m_iPos];
		}

		public char PeekChar(bool bSkipWhiteSpace)
		{
			if (!bSkipWhiteSpace) return PeekChar();

			int i = m_iPos;
			while (i < m_str.Length)
			{
				char ch = m_str[i];
				if ((ch != ' ') && (ch != '\t') && (ch != '\r') && (ch != '\n'))
					return ch;
				++i;
			}

			return char.MinValue;
		}
	}

	public enum StrEncodingType
	{
		Unknown = 0,
		Default,
		Ascii,
		Utf7,
		Utf8,
		Utf16LE,
		Utf16BE,
		Utf32LE,
		Utf32BE
	}

	public sealed class StrEncodingInfo
	{
		private readonly StrEncodingType m_type;
		public StrEncodingType Type
		{
			get { return m_type; }
		}

		private readonly string m_strName;
		public string Name
		{
			get { return m_strName; }
		}

		private readonly Encoding m_enc;
		public Encoding Encoding
		{
			get { return m_enc; }
		}

		private readonly uint m_cbCodePoint;
		/// <summary>
		/// Size of a character in bytes.
		/// </summary>
		public uint CodePointSize
		{
			get { return m_cbCodePoint; }
		}

		private readonly byte[] m_vSig;
		/// <summary>
		/// Start signature of the text (byte order mark).
		/// May be <c>null</c> or empty, if no signature is known.
		/// </summary>
		public byte[] StartSignature
		{
			get { return m_vSig; }
		}

		public StrEncodingInfo(StrEncodingType t, string strName, Encoding enc,
			uint cbCodePoint, byte[] vStartSig)
		{
			if (strName == null) throw new ArgumentNullException("strName");
			if (enc == null) throw new ArgumentNullException("enc");
			if (cbCodePoint <= 0) throw new ArgumentOutOfRangeException("cbCodePoint");

			m_type = t;
			m_strName = strName;
			m_enc = enc;
			m_cbCodePoint = cbCodePoint;
			m_vSig = vStartSig;
		}
	}

	/// <summary>
	/// A class containing various string helper methods.
	/// </summary>
	public static class StrUtil
	{
		public static readonly StringComparison CaseIgnoreCmp = StringComparison.OrdinalIgnoreCase;

		public static StringComparer CaseIgnoreComparer
		{
			get { return StringComparer.OrdinalIgnoreCase; }
		}

		private static bool m_bRtl = false;
		public static bool RightToLeft
		{
			get { return m_bRtl; }
			set { m_bRtl = value; }
		}

		private static UTF8Encoding m_encUtf8 = null;
		public static UTF8Encoding Utf8
		{
			get
			{
				if (m_encUtf8 == null) m_encUtf8 = new UTF8Encoding(false, false);
				return m_encUtf8;
			}
		}

		private static List<StrEncodingInfo> m_lEncs = null;
		public static IEnumerable<StrEncodingInfo> Encodings
		{
			get
			{
				if (m_lEncs != null) return m_lEncs;

				List<StrEncodingInfo> l = new List<StrEncodingInfo>();

				l.Add(new StrEncodingInfo(StrEncodingType.Default,
#if KeePassUAP
					"Unicode (UTF-8)", StrUtil.Utf8, 1, new byte[] { 0xEF, 0xBB, 0xBF }));
#else
#if !KeePassLibSD
					Encoding.Default.EncodingName,
#else
					Encoding.Default.WebName,
#endif
					Encoding.Default,
					(uint)Encoding.Default.GetBytes("a").Length, null));
#endif

				l.Add(new StrEncodingInfo(StrEncodingType.Ascii,
					"ASCII", Encoding.ASCII, 1, null));
				l.Add(new StrEncodingInfo(StrEncodingType.Utf7,
					"Unicode (UTF-7)", Encoding.UTF7, 1, null));
				l.Add(new StrEncodingInfo(StrEncodingType.Utf8,
					"Unicode (UTF-8)", StrUtil.Utf8, 1, new byte[] { 0xEF, 0xBB, 0xBF }));
				l.Add(new StrEncodingInfo(StrEncodingType.Utf16LE,
					"Unicode (UTF-16 LE)", new UnicodeEncoding(false, false),
					2, new byte[] { 0xFF, 0xFE }));
				l.Add(new StrEncodingInfo(StrEncodingType.Utf16BE,
					"Unicode (UTF-16 BE)", new UnicodeEncoding(true, false),
					2, new byte[] { 0xFE, 0xFF }));

#if !KeePassLibSD
				l.Add(new StrEncodingInfo(StrEncodingType.Utf32LE,
					"Unicode (UTF-32 LE)", new UTF32Encoding(false, false),
					4, new byte[] { 0xFF, 0xFE, 0x0, 0x0 }));
				l.Add(new StrEncodingInfo(StrEncodingType.Utf32BE,
					"Unicode (UTF-32 BE)", new UTF32Encoding(true, false),
					4, new byte[] { 0x0, 0x0, 0xFE, 0xFF }));
#endif

				m_lEncs = l;
				return l;
			}
		}

		// public static string RtfPar
		// {
		//	// get { return (m_bRtl ? "\\rtlpar " : "\\par "); }
		//	get { return "\\par "; }
		// }

		// /// <summary>
		// /// Convert a string into a valid RTF string.
		// /// </summary>
		// /// <param name="str">Any string.</param>
		// /// <returns>RTF-encoded string.</returns>
		// public static string MakeRtfString(string str)
		// {
		//	Debug.Assert(str != null); if(str == null) throw new ArgumentNullException("str");
		//	str = str.Replace("\\", "\\\\");
		//	str = str.Replace("\r", string.Empty);
		//	str = str.Replace("{", "\\{");
		//	str = str.Replace("}", "\\}");
		//	str = str.Replace("\n", StrUtil.RtfPar);
		//	StringBuilder sbEncoded = new StringBuilder();
		//	for(int i = 0; i < str.Length; ++i)
		//	{
		//		char ch = str[i];
		//		if((int)ch >= 256)
		//			sbEncoded.Append(StrUtil.RtfEncodeChar(ch));
		//		else sbEncoded.Append(ch);
		//	}
		//	return sbEncoded.ToString();
		// }

		public static string RtfEncodeChar(char ch)
		{
			// Unicode character values must be encoded using
			// 16-bit numbers (decimal); Unicode values greater
			// than 32767 must be expressed as negative numbers
			short sh = (short)ch;
			return ("\\u" + sh.ToString(NumberFormatInfo.InvariantInfo) + "?");
		}

		internal static bool RtfIsURtf(string str)
		{
			if (str == null) { Debug.Assert(false); return false; }

			const string p = "{\\urtf"; // Typically "{\\urtf1\\ansi\\ansicpg65001"
			return (str.StartsWith(p) && (str.Length > p.Length) &&
				char.IsDigit(str[p.Length]));
		}

		public static string RtfFix(string strRtf)
		{
			if (strRtf == null) { Debug.Assert(false); return string.Empty; }

			string str = strRtf;

			// Workaround for .NET bug: the Rtf property of a RichTextBox
			// can return an RTF text starting with "{\\urtf", but
			// setting such an RTF text throws an exception (the setter
			// checks for the RTF text to start with "{\\rtf");
			// https://sourceforge.net/p/keepass/discussion/329221/thread/7788872f/
			// https://www.microsoft.com/en-us/download/details.aspx?id=10725
			// https://msdn.microsoft.com/en-us/library/windows/desktop/bb774284.aspx
			// https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/RichTextBox.cs
			if (RtfIsURtf(str)) str = str.Remove(2, 1); // Remove the 'u'

			return str;
		}

		internal static string RtfFilterText(string strText)
		{
			if (strText == null) { Debug.Assert(false); return string.Empty; }

			// A U+FFFC character causes the rest of the text to be lost.
			// With '?', the string length, substring indices and
			// character visibility remain the same.
			// More special characters (unproblematic) in rich text boxes:
			// https://docs.microsoft.com/en-us/windows/win32/api/richedit/ns-richedit-gettextex
			return strText.Replace('\uFFFC', '?');
		}

		internal static bool ContainsHighChar(string str)
		{
			if (str == null) { Debug.Assert(false); return false; }

			for (int i = 0; i < str.Length; ++i)
			{
				if (str[i] > '\u00FF') return true;
			}

			return false;
		}

		/// <summary>
		/// Convert a string to a HTML sequence representing that string.
		/// </summary>
		/// <param name="str">String to convert.</param>
		/// <returns>String, HTML-encoded.</returns>
		public static string StringToHtml(string str)
		{
			return StringToHtml(str, false);
		}

		internal static string StringToHtml(string str, bool bNbsp)
		{
			Debug.Assert(str != null); if (str == null) throw new ArgumentNullException("str");

			str = str.Replace(@"&", @"&amp;"); // Must be first
			str = str.Replace(@"<", @"&lt;");
			str = str.Replace(@">", @"&gt;");
			str = str.Replace("\"", @"&quot;");
			str = str.Replace("\'", @"&#39;");

			if (bNbsp) str = str.Replace(" ", @"&nbsp;"); // Before <br />

			str = NormalizeNewLines(str, false);
			str = str.Replace("\n", @"<br />" + MessageService.NewLine);

			return str;
		}

		public static string XmlToString(string str)
		{
			Debug.Assert(str != null); if (str == null) throw new ArgumentNullException("str");

			str = str.Replace(@"&amp;", @"&");
			str = str.Replace(@"&lt;", @"<");
			str = str.Replace(@"&gt;", @">");
			str = str.Replace(@"&quot;", "\"");
			str = str.Replace(@"&#39;", "\'");

			return str;
		}

		public static string ReplaceCaseInsensitive(string strString, string strFind,
			string strNew)
		{
			Debug.Assert(strString != null); if (strString == null) return strString;
			Debug.Assert(strFind != null); if (strFind == null) return strString;
			Debug.Assert(strNew != null); if (strNew == null) return strString;

			string str = strString;

			int nPos = 0;
			while (nPos < str.Length)
			{
				nPos = str.IndexOf(strFind, nPos, StringComparison.OrdinalIgnoreCase);
				if (nPos < 0) break;

				str = str.Remove(nPos, strFind.Length);
				str = str.Insert(nPos, strNew);

				nPos += strNew.Length;
			}

			return str;
		}

		// /// <summary>
		// /// Initialize an RTF document based on given font face and size.
		// /// </summary>
		// /// <param name="sb"><c>StringBuilder</c> to put the generated RTF into.</param>
		// /// <param name="strFontFace">Face name of the font to use.</param>
		// /// <param name="fFontSize">Size of the font to use.</param>
		// public static void InitRtf(StringBuilder sb, string strFontFace, float fFontSize)
		// {
		//	Debug.Assert(sb != null); if(sb == null) throw new ArgumentNullException("sb");
		//	Debug.Assert(strFontFace != null); if(strFontFace == null) throw new ArgumentNullException("strFontFace");
		//	sb.Append("{\\rtf1");
		//	if(m_bRtl) sb.Append("\\fbidis");
		//	sb.Append("\\ansi\\ansicpg");
		//	sb.Append(Encoding.Default.CodePage);
		//	sb.Append("\\deff0{\\fonttbl{\\f0\\fswiss MS Sans Serif;}{\\f1\\froman\\fcharset2 Symbol;}{\\f2\\fswiss ");
		//	sb.Append(strFontFace);
		//	sb.Append(";}{\\f3\\fswiss Arial;}}");
		//	sb.Append("{\\colortbl\\red0\\green0\\blue0;}");
		//	if(m_bRtl) sb.Append("\\rtldoc");
		//	sb.Append("\\deflang1031\\pard\\plain\\f2\\cf0 ");
		//	sb.Append("\\fs");
		//	sb.Append((int)(fFontSize * 2));
		//	if(m_bRtl) sb.Append("\\rtlpar\\qr\\rtlch ");
		// }

		// /// <summary>
		// /// Convert a simple HTML string to an RTF string.
		// /// </summary>
		// /// <param name="strHtmlString">Input HTML string.</param>
		// /// <returns>RTF string representing the HTML input string.</returns>
		// public static string SimpleHtmlToRtf(string strHtmlString)
		// {
		//	StringBuilder sb = new StringBuilder();
		//	StrUtil.InitRtf(sb, "Microsoft Sans Serif", 8.25f);
		//	sb.Append(" ");
		//	string str = MakeRtfString(strHtmlString);
		//	str = str.Replace("<b>", "\\b ");
		//	str = str.Replace("</b>", "\\b0 ");
		//	str = str.Replace("<i>", "\\i ");
		//	str = str.Replace("</i>", "\\i0 ");
		//	str = str.Replace("<u>", "\\ul ");
		//	str = str.Replace("</u>", "\\ul0 ");
		//	str = str.Replace("<br />", StrUtil.RtfPar);
		//	sb.Append(str);
		//	return sb.ToString();
		// }

		/// <summary>
		/// Convert a <c>Color</c> to a HTML color identifier string.
		/// </summary>
		/// <param name="color">Color to convert.</param>
		/// <param name="bEmptyIfTransparent">If this is <c>true</c>, an empty string
		/// is returned if the color is transparent.</param>
		/// <returns>HTML color identifier string.</returns>
		public static string ColorToUnnamedHtml(Color color, bool bEmptyIfTransparent)
		{
			if (bEmptyIfTransparent && (color.A != 255))
				return string.Empty;

			StringBuilder sb = new StringBuilder();
			byte bt;

			sb.Append('#');

			bt = (byte)(color.R >> 4);
			if (bt < 10) sb.Append((char)('0' + bt)); else sb.Append((char)('A' - 10 + bt));
			bt = (byte)(color.R & 0x0F);
			if (bt < 10) sb.Append((char)('0' + bt)); else sb.Append((char)('A' - 10 + bt));

			bt = (byte)(color.G >> 4);
			if (bt < 10) sb.Append((char)('0' + bt)); else sb.Append((char)('A' - 10 + bt));
			bt = (byte)(color.G & 0x0F);
			if (bt < 10) sb.Append((char)('0' + bt)); else sb.Append((char)('A' - 10 + bt));

			bt = (byte)(color.B >> 4);
			if (bt < 10) sb.Append((char)('0' + bt)); else sb.Append((char)('A' - 10 + bt));
			bt = (byte)(color.B & 0x0F);
			if (bt < 10) sb.Append((char)('0' + bt)); else sb.Append((char)('A' - 10 + bt));

			return sb.ToString();
		}

		/// <summary>
		/// Format an exception and convert it to a string.
		/// </summary>
		/// <param name="excp"><c>Exception</c> to convert/format.</param>
		/// <returns>String representing the exception.</returns>
		public static string FormatException(Exception excp)
		{
			string strText = string.Empty;

			if (!string.IsNullOrEmpty(excp.Message))
				strText += excp.Message + MessageService.NewLine;
#if !KeePassLibSD
			if (!string.IsNullOrEmpty(excp.Source))
				strText += excp.Source + MessageService.NewLine;
#endif
			if (!string.IsNullOrEmpty(excp.StackTrace))
				strText += excp.StackTrace + MessageService.NewLine;
#if !KeePassLibSD
#if !KeePassUAP
			if (excp.TargetSite != null)
				strText += excp.TargetSite.ToString() + MessageService.NewLine;
#endif

			if (excp.Data != null)
			{
				strText += MessageService.NewLine;
				foreach (DictionaryEntry de in excp.Data)
					strText += @"'" + de.Key + @"' -> '" + de.Value + @"'" +
						MessageService.NewLine;
			}
#endif

			if (excp.InnerException != null)
			{
				strText += MessageService.NewLine + "Inner:" + MessageService.NewLine;
				if (!string.IsNullOrEmpty(excp.InnerException.Message))
					strText += excp.InnerException.Message + MessageService.NewLine;
#if !KeePassLibSD
				if (!string.IsNullOrEmpty(excp.InnerException.Source))
					strText += excp.InnerException.Source + MessageService.NewLine;
#endif
				if (!string.IsNullOrEmpty(excp.InnerException.StackTrace))
					strText += excp.InnerException.StackTrace + MessageService.NewLine;
#if !KeePassLibSD
#if !KeePassUAP
				if (excp.InnerException.TargetSite != null)
					strText += excp.InnerException.TargetSite.ToString();
#endif

				if (excp.InnerException.Data != null)
				{
					strText += MessageService.NewLine;
					foreach (DictionaryEntry de in excp.InnerException.Data)
						strText += @"'" + de.Key + @"' -> '" + de.Value + @"'" +
							MessageService.NewLine;
				}
#endif
			}

			return strText;
		}

		public static bool TryParseUShort(string str, out ushort u)
		{
#if !KeePassLibSD
			return ushort.TryParse(str, out u);
#else
			try { u = ushort.Parse(str); return true; }
			catch(Exception) { u = 0; return false; }
#endif
		}

		public static bool TryParseInt(string str, out int n)
		{
#if !KeePassLibSD
			return int.TryParse(str, out n);
#else
			try { n = int.Parse(str); return true; }
			catch(Exception) { n = 0; }
			return false;
#endif
		}

		public static bool TryParseIntInvariant(string str, out int n)
		{
#if !KeePassLibSD
			return int.TryParse(str, NumberStyles.Integer,
				NumberFormatInfo.InvariantInfo, out n);
#else
			try
			{
				n = int.Parse(str, NumberStyles.Integer,
					NumberFormatInfo.InvariantInfo);
				return true;
			}
			catch(Exception) { n = 0; }
			return false;
#endif
		}

		public static bool TryParseUInt(string str, out uint u)
		{
#if !KeePassLibSD
			return uint.TryParse(str, out u);
#else
			try { u = uint.Parse(str); return true; }
			catch(Exception) { u = 0; }
			return false;
#endif
		}

		public static bool TryParseUIntInvariant(string str, out uint u)
		{
#if !KeePassLibSD
			return uint.TryParse(str, NumberStyles.Integer,
				NumberFormatInfo.InvariantInfo, out u);
#else
			try
			{
				u = uint.Parse(str, NumberStyles.Integer,
					NumberFormatInfo.InvariantInfo);
				return true;
			}
			catch(Exception) { u = 0; }
			return false;
#endif
		}

		public static bool TryParseLong(string str, out long n)
		{
#if !KeePassLibSD
			return long.TryParse(str, out n);
#else
			try { n = long.Parse(str); return true; }
			catch(Exception) { n = 0; }
			return false;
#endif
		}

		public static bool TryParseLongInvariant(string str, out long n)
		{
#if !KeePassLibSD
			return long.TryParse(str, NumberStyles.Integer,
				NumberFormatInfo.InvariantInfo, out n);
#else
			try
			{
				n = long.Parse(str, NumberStyles.Integer,
					NumberFormatInfo.InvariantInfo);
				return true;
			}
			catch(Exception) { n = 0; }
			return false;
#endif
		}

		public static bool TryParseULong(string str, out ulong u)
		{
#if !KeePassLibSD
			return ulong.TryParse(str, out u);
#else
			try { u = ulong.Parse(str); return true; }
			catch(Exception) { u = 0; }
			return false;
#endif
		}

		public static bool TryParseULongInvariant(string str, out ulong u)
		{
#if !KeePassLibSD
			return ulong.TryParse(str, NumberStyles.Integer,
				NumberFormatInfo.InvariantInfo, out u);
#else
			try
			{
				u = ulong.Parse(str, NumberStyles.Integer,
					NumberFormatInfo.InvariantInfo);
				return true;
			}
			catch(Exception) { u = 0; }
			return false;
#endif
		}

		public static bool TryParseDateTime(string str, out DateTime dt)
		{
#if !KeePassLibSD
			return DateTime.TryParse(str, out dt);
#else
			try { dt = DateTime.Parse(str); return true; }
			catch(Exception) { dt = DateTime.UtcNow; }
			return false;
#endif
		}

		public static string CompactString3Dots(string strText, int cchMax)
		{
			Debug.Assert(strText != null);
			if (strText == null) throw new ArgumentNullException("strText");
			Debug.Assert(cchMax >= 0);
			if (cchMax < 0) throw new ArgumentOutOfRangeException("cchMax");

			if (strText.Length <= cchMax) return strText;

			if (cchMax == 0) return string.Empty;
			if (cchMax <= 3) return new string('.', cchMax);

			return (strText.Substring(0, cchMax - 3) + "...");
		}

		private static readonly char[] g_vDots = new char[] { '.', '\u2026' };
		private static readonly char[] g_vDotsWS = new char[] { '.', '\u2026',
			' ', '\t', '\r', '\n' };
		internal static string TrimDots(string strText, bool bTrimWhiteSpace)
		{
			if (strText == null) { Debug.Assert(false); return string.Empty; }

			return strText.Trim(bTrimWhiteSpace ? g_vDotsWS : g_vDots);
		}

		public static string GetStringBetween(string strText, int nStartIndex,
			string strStart, string strEnd)
		{
			int nTemp;
			return GetStringBetween(strText, nStartIndex, strStart, strEnd, out nTemp);
		}

		public static string GetStringBetween(string strText, int nStartIndex,
			string strStart, string strEnd, out int nInnerStartIndex)
		{
			if (strText == null) throw new ArgumentNullException("strText");
			if (strStart == null) throw new ArgumentNullException("strStart");
			if (strEnd == null) throw new ArgumentNullException("strEnd");

			nInnerStartIndex = -1;

			int nIndex = strText.IndexOf(strStart, nStartIndex);
			if (nIndex < 0) return string.Empty;

			nIndex += strStart.Length;

			int nEndIndex = strText.IndexOf(strEnd, nIndex);
			if (nEndIndex < 0) return string.Empty;

			nInnerStartIndex = nIndex;
			return strText.Substring(nIndex, nEndIndex - nIndex);
		}

		/// <summary>
		/// Removes all characters that are not valid XML characters,
		/// according to https://www.w3.org/TR/xml/#charsets .
		/// </summary>
		/// <param name="strText">Source text.</param>
		/// <returns>Text containing only valid XML characters.</returns>
		public static string SafeXmlString(string strText)
		{
			Debug.Assert(strText != null); // No throw
			if (string.IsNullOrEmpty(strText)) return strText;

			int nLength = strText.Length;
			StringBuilder sb = new StringBuilder(nLength);

			for (int i = 0; i < nLength; ++i)
			{
				char ch = strText[i];

				if (((ch >= '\u0020') && (ch <= '\uD7FF')) ||
					(ch == '\u0009') || (ch == '\u000A') || (ch == '\u000D') ||
					((ch >= '\uE000') && (ch <= '\uFFFD')))
					sb.Append(ch);
				else if ((ch >= '\uD800') && (ch <= '\uDBFF')) // High surrogate
				{
					if ((i + 1) < nLength)
					{
						char chLow = strText[i + 1];
						if ((chLow >= '\uDC00') && (chLow <= '\uDFFF')) // Low sur.
						{
							sb.Append(ch);
							sb.Append(chLow);
							++i;
						}
						else { Debug.Assert(false); } // Low sur. invalid
					}
					else { Debug.Assert(false); } // Low sur. missing
				}

				Debug.Assert((ch < '\uDC00') || (ch > '\uDFFF')); // Lonely low sur.
			}

			return sb.ToString();
		}

		/* private static Regex g_rxNaturalSplit = null;
		public static int CompareNaturally(string strX, string strY)
		{
			Debug.Assert(strX != null);
			if(strX == null) throw new ArgumentNullException("strX");
			Debug.Assert(strY != null);
			if(strY == null) throw new ArgumentNullException("strY");

			if(NativeMethods.SupportsStrCmpNaturally)
				return NativeMethods.StrCmpNaturally(strX, strY);

			if(g_rxNaturalSplit == null)
				g_rxNaturalSplit = new Regex(@"([0-9]+)", RegexOptions.Compiled);

			string[] vPartsX = g_rxNaturalSplit.Split(strX);
			string[] vPartsY = g_rxNaturalSplit.Split(strY);

			int n = Math.Min(vPartsX.Length, vPartsY.Length);
			for(int i = 0; i < n; ++i)
			{
				string strPartX = vPartsX[i], strPartY = vPartsY[i];
				int iPartCompare;

#if KeePassLibSD
				try
				{
					ulong uX = ulong.Parse(strPartX);
					ulong uY = ulong.Parse(strPartY);
					iPartCompare = uX.CompareTo(uY);
				}
				catch(Exception) { iPartCompare = string.Compare(strPartX, strPartY, true); }
#else
				ulong uX, uY;
				if(ulong.TryParse(strPartX, out uX) && ulong.TryParse(strPartY, out uY))
					iPartCompare = uX.CompareTo(uY);
				else iPartCompare = string.Compare(strPartX, strPartY, true);
#endif

				if(iPartCompare != 0) return iPartCompare;
			}

			if(vPartsX.Length == vPartsY.Length) return 0;
			if(vPartsX.Length < vPartsY.Length) return -1;
			return 1;
		} */

		public static int CompareNaturally(string strX, string strY)
		{
			Debug.Assert(strX != null);
			if (strX == null) throw new ArgumentNullException("strX");
			Debug.Assert(strY != null);
			if (strY == null) throw new ArgumentNullException("strY");

			if (NativeMethods.SupportsStrCmpNaturally)
				return NativeMethods.StrCmpNaturally(strX, strY);

			int cX = strX.Length;
			int cY = strY.Length;
			if (cX == 0) return ((cY == 0) ? 0 : -1);
			if (cY == 0) return 1;

			char chFirstX = strX[0];
			char chFirstY = strY[0];
			bool bExpNum = ((chFirstX >= '0') && (chFirstX <= '9'));
			bool bExpNumY = ((chFirstY >= '0') && (chFirstY <= '9'));
			if (bExpNum != bExpNumY) return string.Compare(strX, strY, true);

			int pX = 0;
			int pY = 0;
			while ((pX < cX) && (pY < cY))
			{
				Debug.Assert(((strX[pX] >= '0') && (strX[pX] <= '9')) == bExpNum);
				Debug.Assert(((strY[pY] >= '0') && (strY[pY] <= '9')) == bExpNum);

				int pExclX = pX + 1;
				while (pExclX < cX)
				{
					char ch = strX[pExclX];
					bool bChNum = ((ch >= '0') && (ch <= '9'));
					if (bChNum != bExpNum) break;
					++pExclX;
				}

				int pExclY = pY + 1;
				while (pExclY < cY)
				{
					char ch = strY[pExclY];
					bool bChNum = ((ch >= '0') && (ch <= '9'));
					if (bChNum != bExpNum) break;
					++pExclY;
				}

				string strPartX = strX.Substring(pX, pExclX - pX);
				string strPartY = strY.Substring(pY, pExclY - pY);

				bool bStrCmp = true;
				if (bExpNum)
				{
					// 2^64 - 1 = 18446744073709551615 has length 20
					if ((strPartX.Length <= 19) && (strPartY.Length <= 19))
					{
						ulong uX, uY;
						if (ulong.TryParse(strPartX, out uX) && ulong.TryParse(strPartY, out uY))
						{
							if (uX < uY) return -1;
							if (uX > uY) return 1;

							bStrCmp = false;
						}
						else { Debug.Assert(false); }
					}
					else
					{
						double dX, dY;
						if (double.TryParse(strPartX, out dX) && double.TryParse(strPartY, out dY))
						{
							if (dX < dY) return -1;
							if (dX > dY) return 1;

							bStrCmp = false;
						}
						else { Debug.Assert(false); }
					}
				}
				if (bStrCmp)
				{
					int c = string.Compare(strPartX, strPartY, true);
					if (c != 0) return c;
				}

				bExpNum = !bExpNum;
				pX = pExclX;
				pY = pExclY;
			}

			if (pX >= cX)
			{
				Debug.Assert(pX == cX);
				if (pY >= cY) { Debug.Assert(pY == cY); return 0; }
				return -1;
			}

			Debug.Assert(pY == cY);
			return 1;
		}

		public static string RemoveAccelerator(string strMenuText)
		{
			if (strMenuText == null) throw new ArgumentNullException("strMenuText");

			string str = strMenuText;

			for (char ch = 'A'; ch <= 'Z'; ++ch)
			{
				string strEnhAcc = @"(&" + ch.ToString() + ")";
				if (str.IndexOf(strEnhAcc) >= 0)
				{
					str = str.Replace(" " + strEnhAcc, string.Empty);
					str = str.Replace(strEnhAcc, string.Empty);
				}
			}

			str = str.Replace(@"&", string.Empty);

			return str;
		}

		public static string AddAccelerator(string strMenuText,
			List<char> lAvailKeys)
		{
			if (strMenuText == null) { Debug.Assert(false); return string.Empty; }
			if (lAvailKeys == null) { Debug.Assert(false); return strMenuText; }

			for (int i = 0; i < strMenuText.Length; ++i)
			{
				char ch = char.ToLowerInvariant(strMenuText[i]);

				for (int j = 0; j < lAvailKeys.Count; ++j)
				{
					if (char.ToLowerInvariant(lAvailKeys[j]) == ch)
					{
						lAvailKeys.RemoveAt(j);
						return strMenuText.Insert(i, @"&");
					}
				}
			}

			return strMenuText;
		}

		public static string EncodeMenuText(string strText)
		{
			if (strText == null) throw new ArgumentNullException("strText");

			return strText.Replace(@"&", @"&&");
		}

		public static string EncodeToolTipText(string strText)
		{
			if (strText == null) throw new ArgumentNullException("strText");

			return strText.Replace(@"&", @"&&&");
		}

		public static bool IsHexString(string str, bool bStrict)
		{
			if (str == null) throw new ArgumentNullException("str");

			foreach (char ch in str)
			{
				if ((ch >= '0') && (ch <= '9')) continue;
				if ((ch >= 'a') && (ch <= 'f')) continue;
				if ((ch >= 'A') && (ch <= 'F')) continue;

				if (bStrict) return false;

				if ((ch == ' ') || (ch == '\t') || (ch == '\r') || (ch == '\n'))
					continue;

				return false;
			}

			return true;
		}

		public static bool IsHexString(byte[] pbUtf8, bool bStrict)
		{
			if (pbUtf8 == null) throw new ArgumentNullException("pbUtf8");

			for (int i = 0; i < pbUtf8.Length; ++i)
			{
				byte bt = pbUtf8[i];
				if ((bt >= (byte)'0') && (bt <= (byte)'9')) continue;
				if ((bt >= (byte)'a') && (bt <= (byte)'f')) continue;
				if ((bt >= (byte)'A') && (bt <= (byte)'F')) continue;

				if (bStrict) return false;

				if ((bt == (byte)' ') || (bt == (byte)'\t') ||
					(bt == (byte)'\r') || (bt == (byte)'\n'))
					continue;

				return false;
			}

			return true;
		}

#if !KeePassLibSD
		private static readonly char[] g_vPatternPartsSep = new char[] { '*' };
		public static bool SimplePatternMatch(string strPattern, string strText,
			StringComparison sc)
		{
			if (strPattern == null) throw new ArgumentNullException("strPattern");
			if (strText == null) throw new ArgumentNullException("strText");

			if (strPattern.IndexOf('*') < 0) return strText.Equals(strPattern, sc);

			string[] vPatternParts = strPattern.Split(g_vPatternPartsSep,
				StringSplitOptions.RemoveEmptyEntries);
			if (vPatternParts == null) { Debug.Assert(false); return true; }
			if (vPatternParts.Length == 0) return true;

			if (strText.Length == 0) return false;

			if ((strPattern[0] != '*') && !strText.StartsWith(vPatternParts[0], sc))
				return false;
			if ((strPattern[strPattern.Length - 1] != '*') && !strText.EndsWith(
				vPatternParts[vPatternParts.Length - 1], sc))
				return false;

			int iOffset = 0;
			for (int i = 0; i < vPatternParts.Length; ++i)
			{
				string strPart = vPatternParts[i];

				int iFound = strText.IndexOf(strPart, iOffset, sc);
				if (iFound < iOffset) return false;

				iOffset = iFound + strPart.Length;
				if (iOffset == strText.Length) return (i == (vPatternParts.Length - 1));
			}

			return true;
		}
#endif // !KeePassLibSD

		public static bool StringToBool(string str)
		{
			if (string.IsNullOrEmpty(str)) return false; // No assert

			string s = str.Trim().ToLower();
			if (s == "true") return true;
			if (s == "yes") return true;
			if (s == "1") return true;
			if (s == "enabled") return true;
			if (s == "checked") return true;

			return false;
		}

		public static bool? StringToBoolEx(string str)
		{
			if (string.IsNullOrEmpty(str)) return null;

			string s = str.Trim().ToLower();
			if (s == "true") return true;
			if (s == "false") return false;

			return null;
		}

		public static string BoolToString(bool bValue)
		{
			return (bValue ? "true" : "false");
		}

		public static string BoolToStringEx(bool? bValue)
		{
			if (bValue.HasValue) return BoolToString(bValue.Value);
			return "null";
		}

		/// <summary>
		/// Normalize new line characters in a string. Input strings may
		/// contain mixed new line character sequences from all commonly
		/// used operating systems (i.e. \r\n from Windows, \n from Unix
		/// and \r from Mac OS.
		/// </summary>
		/// <param name="str">String with mixed new line characters.</param>
		/// <param name="bWindows">If <c>true</c>, new line characters
		/// are normalized for Windows (\r\n); if <c>false</c>, new line
		/// characters are normalized for Unix (\n).</param>
		/// <returns>String with normalized new line characters.</returns>
		public static string NormalizeNewLines(string str, bool bWindows)
		{
			if (string.IsNullOrEmpty(str)) return str;

			str = str.Replace("\r\n", "\n");
			str = str.Replace("\r", "\n");

			if (bWindows) str = str.Replace("\n", "\r\n");

			return str;
		}

		public static void NormalizeNewLines(ProtectedStringDictionary dict,
			bool bWindows)
		{
			if (dict == null) { Debug.Assert(false); return; }

			List<string> lKeys = dict.GetKeys();
			foreach (string strKey in lKeys)
			{
				ProtectedString ps = dict.Get(strKey);
				if (ps == null) { Debug.Assert(false); continue; }

				char[] v = ps.ReadChars();
				if (!IsNewLineNormalized(v, bWindows))
					dict.Set(strKey, new ProtectedString(ps.IsProtected,
						NormalizeNewLines(ps.ReadString(), bWindows)));
				MemUtil.ZeroArray<char>(v);
			}
		}

		internal static bool IsNewLineNormalized(char[] v, bool bWindows)
		{
			if (v == null) { Debug.Assert(false); return true; }

			if (bWindows)
			{
				int iFreeCr = -2; // Must be < -1 (for test "!= (i - 1)")

				for (int i = 0; i < v.Length; ++i)
				{
					char ch = v[i];

					if (ch == '\r')
					{
						if (iFreeCr >= 0) return false;
						iFreeCr = i;
					}
					else if (ch == '\n')
					{
						if (iFreeCr != (i - 1)) return false;
						iFreeCr = -2; // Consume \r
					}
				}

				return (iFreeCr < 0); // Ensure no \r at end
			}

			return (Array.IndexOf<char>(v, '\r') < 0);
		}

		public static string GetNewLineSeq(string str)
		{
			if (str == null) { Debug.Assert(false); return MessageService.NewLine; }

			int n = str.Length, nLf = 0, nCr = 0, nCrLf = 0;
			char chLast = char.MinValue;
			for (int i = 0; i < n; ++i)
			{
				char ch = str[i];

				if (ch == '\r') ++nCr;
				else if (ch == '\n')
				{
					++nLf;
					if (chLast == '\r') ++nCrLf;
				}

				chLast = ch;
			}

			nCr -= nCrLf;
			nLf -= nCrLf;

			int nMax = Math.Max(nCrLf, Math.Max(nCr, nLf));
			if (nMax == 0) return MessageService.NewLine;

			if (nCrLf == nMax) return "\r\n";
			return ((nLf == nMax) ? "\n" : "\r");
		}

		public static string AlphaNumericOnly(string str)
		{
			if (string.IsNullOrEmpty(str)) return str;

			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < str.Length; ++i)
			{
				char ch = str[i];
				if (((ch >= 'a') && (ch <= 'z')) || ((ch >= 'A') && (ch <= 'Z')) ||
					((ch >= '0') && (ch <= '9')))
					sb.Append(ch);
			}

			return sb.ToString();
		}

		public static string FormatDataSize(ulong uBytes)
		{
			const ulong uKB = 1024;
			const ulong uMB = uKB * uKB;
			const ulong uGB = uMB * uKB;
			const ulong uTB = uGB * uKB;

			if (uBytes == 0) return "0 KB";
			if (uBytes <= uKB) return "1 KB";
			if (uBytes <= uMB) return (((uBytes - 1UL) / uKB) + 1UL).ToString() + " KB";
			if (uBytes <= uGB) return (((uBytes - 1UL) / uMB) + 1UL).ToString() + " MB";
			if (uBytes <= uTB) return (((uBytes - 1UL) / uGB) + 1UL).ToString() + " GB";

			return (((uBytes - 1UL) / uTB) + 1UL).ToString() + " TB";
		}

		public static string FormatDataSizeKB(ulong uBytes)
		{
			const ulong uKB = 1024;

			if (uBytes == 0) return "0 KB";
			if (uBytes <= uKB) return "1 KB";

			return (((uBytes - 1UL) / uKB) + 1UL).ToString() + " KB";
		}

		private static readonly char[] m_vVersionSep = new char[] { '.', ',' };
		public static ulong ParseVersion(string strVersion)
		{
			if (strVersion == null) { Debug.Assert(false); return 0; }

			string[] vVer = strVersion.Split(m_vVersionSep);
			if ((vVer == null) || (vVer.Length == 0)) { Debug.Assert(false); return 0; }

			ushort uPart;
			StrUtil.TryParseUShort(vVer[0].Trim(), out uPart);
			ulong uVer = ((ulong)uPart << 48);

			if (vVer.Length >= 2)
			{
				StrUtil.TryParseUShort(vVer[1].Trim(), out uPart);
				uVer |= ((ulong)uPart << 32);
			}

			if (vVer.Length >= 3)
			{
				StrUtil.TryParseUShort(vVer[2].Trim(), out uPart);
				uVer |= ((ulong)uPart << 16);
			}

			if (vVer.Length >= 4)
			{
				StrUtil.TryParseUShort(vVer[3].Trim(), out uPart);
				uVer |= (ulong)uPart;
			}

			return uVer;
		}

		public static string VersionToString(ulong uVersion)
		{
			return VersionToString(uVersion, 1U);
		}

		[Obsolete]
		public static string VersionToString(ulong uVersion,
			bool bEnsureAtLeastTwoComp)
		{
			return VersionToString(uVersion, (bEnsureAtLeastTwoComp ? 2U : 1U));
		}

		public static string VersionToString(ulong uVersion, uint uMinComp)
		{
			StringBuilder sb = new StringBuilder();
			uint uComp = 0;

			for (int i = 0; i < 4; ++i)
			{
				if (uVersion == 0UL) break;

				ushort us = (ushort)(uVersion >> 48);

				if (sb.Length > 0) sb.Append('.');

				sb.Append(us.ToString(NumberFormatInfo.InvariantInfo));
				++uComp;

				uVersion <<= 16;
			}

			while (uComp < uMinComp)
			{
				if (sb.Length > 0) sb.Append('.');

				sb.Append('0');
				++uComp;
			}

			return sb.ToString();
		}

		private static readonly byte[] m_pbOptEnt = { 0xA5, 0x74, 0x2E, 0xEC };

		public static string EncryptString(string strPlainText)
		{
			if (string.IsNullOrEmpty(strPlainText)) return string.Empty;

			try
			{
				byte[] pbPlain = StrUtil.Utf8.GetBytes(strPlainText);
				byte[] pbEnc = CryptoUtil.ProtectData(pbPlain, m_pbOptEnt,
					DataProtectionScope.CurrentUser);

#if (!KeePassLibSD && !KeePassUAP)
				return Convert.ToBase64String(pbEnc, Base64FormattingOptions.None);
#else
				return Convert.ToBase64String(pbEnc);
#endif
			}
			catch (Exception) { Debug.Assert(false); }

			return strPlainText;
		}

		public static string DecryptString(string strCipherText)
		{
			if (string.IsNullOrEmpty(strCipherText)) return string.Empty;

			try
			{
				byte[] pbEnc = Convert.FromBase64String(strCipherText);
				byte[] pbPlain = CryptoUtil.UnprotectData(pbEnc, m_pbOptEnt,
					DataProtectionScope.CurrentUser);

				return StrUtil.Utf8.GetString(pbPlain, 0, pbPlain.Length);
			}
			catch (Exception) { Debug.Assert(false); }

			return strCipherText;
		}

		public static string SerializeIntArray(int[] vNumbers)
		{
			if (vNumbers == null) throw new ArgumentNullException("vNumbers");

			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < vNumbers.Length; ++i)
			{
				if (i > 0) sb.Append(' ');
				sb.Append(vNumbers[i].ToString(NumberFormatInfo.InvariantInfo));
			}

			return sb.ToString();
		}

		public static int[] DeserializeIntArray(string strSerialized)
		{
			if (strSerialized == null) throw new ArgumentNullException("strSerialized");
			if (strSerialized.Length == 0) return new int[0];

			string[] vParts = strSerialized.Split(' ');
			int[] v = new int[vParts.Length];

			for (int i = 0; i < vParts.Length; ++i)
			{
				int n;
				if (!TryParseIntInvariant(vParts[i], out n)) { Debug.Assert(false); }
				v[i] = n;
			}

			return v;
		}

		private static readonly char[] g_vTagSep = new char[] { ',', ';' };
		internal static string NormalizeTag(string strTag)
		{
			if (strTag == null) { Debug.Assert(false); return string.Empty; }

			strTag = strTag.Trim();

			for (int i = g_vTagSep.Length - 1; i >= 0; --i)
				strTag = strTag.Replace(g_vTagSep[i], '.');

			return strTag;
		}

		internal static void NormalizeTags(List<string> lTags)
		{
			if (lTags == null) { Debug.Assert(false); return; }

			bool bRemoveNulls = false;
			for (int i = lTags.Count - 1; i >= 0; --i)
			{
				string str = NormalizeTag(lTags[i]);

				if (string.IsNullOrEmpty(str))
				{
					lTags[i] = null;
					bRemoveNulls = true;
				}
				else lTags[i] = str;
			}

			if (bRemoveNulls)
			{
				Predicate<string> f = delegate (string str) { return (str == null); };
				lTags.RemoveAll(f);
			}

			if (lTags.Count >= 2)
			{
				// Deduplicate
				Dictionary<string, bool> d = new Dictionary<string, bool>();
				for (int i = lTags.Count - 1; i >= 0; --i)
					d[lTags[i]] = true;
				if (d.Count != lTags.Count)
				{
					lTags.Clear();
					lTags.AddRange(d.Keys);
				}

				lTags.Sort(StrUtil.CompareNaturally);
			}
		}

		internal static void AddTags(List<string> lTags, IEnumerable<string> eNewTags)
		{
			if (lTags == null) { Debug.Assert(false); return; }
			if (eNewTags == null) { Debug.Assert(false); return; }

			lTags.AddRange(eNewTags);
			NormalizeTags(lTags);
		}

		public static string TagsToString(List<string> lTags, bool bForDisplay)
		{
			if (lTags == null) throw new ArgumentNullException("lTags");

#if DEBUG
			// The input should be normalized
			foreach (string str in lTags) { Debug.Assert(NormalizeTag(str) == str); }
			List<string> l = new List<string>(lTags);
			NormalizeTags(l);
			Debug.Assert(l.Count == lTags.Count);
#endif

			int n = lTags.Count;
			if (n == 0) return string.Empty;
			if (n == 1) return (lTags[0] ?? string.Empty);

			StringBuilder sb = new StringBuilder();
			bool bFirst = true;

			foreach (string strTag in lTags)
			{
				if (string.IsNullOrEmpty(strTag)) { Debug.Assert(false); continue; }

				if (bFirst) bFirst = false;
				else
				{
					if (bForDisplay) sb.Append(", ");
					else sb.Append(';');
				}

				sb.Append(strTag);
			}

			return sb.ToString();
		}

		public static List<string> StringToTags(string strTags)
		{
			if (strTags == null) throw new ArgumentNullException("strTags");

			List<string> lTags = new List<string>();
			if (strTags.Length == 0) return lTags;

			lTags.AddRange(strTags.Split(g_vTagSep));

			NormalizeTags(lTags);
			return lTags;
		}

		public static string Obfuscate(string strPlain)
		{
			if (strPlain == null) { Debug.Assert(false); return string.Empty; }
			if (strPlain.Length == 0) return string.Empty;

			byte[] pb = StrUtil.Utf8.GetBytes(strPlain);

			Array.Reverse(pb);
			for (int i = 0; i < pb.Length; ++i) pb[i] = (byte)(pb[i] ^ 0x65);

#if (!KeePassLibSD && !KeePassUAP)
			return Convert.ToBase64String(pb, Base64FormattingOptions.None);
#else
			return Convert.ToBase64String(pb);
#endif
		}

		public static string Deobfuscate(string strObf)
		{
			if (strObf == null) { Debug.Assert(false); return string.Empty; }
			if (strObf.Length == 0) return string.Empty;

			try
			{
				byte[] pb = Convert.FromBase64String(strObf);

				for (int i = 0; i < pb.Length; ++i) pb[i] = (byte)(pb[i] ^ 0x65);
				Array.Reverse(pb);

				return StrUtil.Utf8.GetString(pb, 0, pb.Length);
			}
			catch (Exception) { Debug.Assert(false); }

			return string.Empty;
		}

		/// <summary>
		/// Split a string and include the separators in the splitted array.
		/// </summary>
		/// <param name="str">String to split.</param>
		/// <param name="vSeps">Separators.</param>
		/// <param name="bCaseSensitive">Specifies whether separators are
		/// matched case-sensitively or not.</param>
		/// <returns>Splitted string including separators.</returns>
		public static List<string> SplitWithSep(string str, string[] vSeps,
			bool bCaseSensitive)
		{
			if (str == null) throw new ArgumentNullException("str");
			if (vSeps == null) throw new ArgumentNullException("vSeps");

			List<string> v = new List<string>();
			while (true)
			{
				int minIndex = int.MaxValue, minSep = -1;
				for (int i = 0; i < vSeps.Length; ++i)
				{
					string strSep = vSeps[i];
					if (string.IsNullOrEmpty(strSep)) { Debug.Assert(false); continue; }

					int iIndex = (bCaseSensitive ? str.IndexOf(strSep) :
						str.IndexOf(strSep, StrUtil.CaseIgnoreCmp));
					if ((iIndex >= 0) && (iIndex < minIndex))
					{
						minIndex = iIndex;
						minSep = i;
					}
				}

				if (minIndex == int.MaxValue) break;

				v.Add(str.Substring(0, minIndex));
				v.Add(vSeps[minSep]);

				str = str.Substring(minIndex + vSeps[minSep].Length);
			}

			v.Add(str);
			return v;
		}

		public static string MultiToSingleLine(string strMulti)
		{
			if (strMulti == null) { Debug.Assert(false); return string.Empty; }
			if (strMulti.Length == 0) return string.Empty;

			string str = strMulti;
			str = str.Replace("\r\n", " ");
			str = str.Replace('\r', ' ');
			str = str.Replace('\n', ' ');

			return str;
		}

		public static List<string> SplitSearchTerms(string strSearch)
		{
			List<string> l = new List<string>();
			if (strSearch == null) { Debug.Assert(false); return l; }

			StringBuilder sbTerm = new StringBuilder();
			bool bQuoted = false;

			for (int i = 0; i < strSearch.Length; ++i)
			{
				char ch = strSearch[i];

				if (((ch == ' ') || (ch == '\t') || (ch == '\r') ||
					(ch == '\n')) && !bQuoted)
				{
					if (sbTerm.Length != 0)
					{
						l.Add(sbTerm.ToString());
						sbTerm.Remove(0, sbTerm.Length);
					}
				}
				else if (ch == '\"') bQuoted = !bQuoted;
				else sbTerm.Append(ch);
			}
			if (sbTerm.Length != 0) l.Add(sbTerm.ToString());

			return l;
		}

		public static int CompareLengthGt(string x, string y)
		{
			if (x.Length == y.Length) return 0;
			return ((x.Length > y.Length) ? -1 : 1);
		}

		public static bool IsDataUri(string strUri)
		{
			return IsDataUri(strUri, null);
		}

		public static bool IsDataUri(string strUri, string strReqMediaType)
		{
			if (strUri == null) { Debug.Assert(false); return false; }
			// strReqMediaType may be null

			const string strPrefix = "data:";
			if (!strUri.StartsWith(strPrefix, StrUtil.CaseIgnoreCmp))
				return false;

			int iC = strUri.IndexOf(',');
			if (iC < 0) return false;

			if (!string.IsNullOrEmpty(strReqMediaType))
			{
				int iS = strUri.IndexOf(';', 0, iC);
				int iTerm = ((iS >= 0) ? iS : iC);

				string strMedia = strUri.Substring(strPrefix.Length,
					iTerm - strPrefix.Length);
				if (!strMedia.Equals(strReqMediaType, StrUtil.CaseIgnoreCmp))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Create a data URI (according to RFC 2397).
		/// </summary>
		/// <param name="pbData">Data to encode.</param>
		/// <param name="strMediaType">Optional MIME type. If <c>null</c>,
		/// an appropriate type is used.</param>
		/// <returns>Data URI.</returns>
		public static string DataToDataUri(byte[] pbData, string strMediaType)
		{
			if (pbData == null) throw new ArgumentNullException("pbData");

			if (strMediaType == null) strMediaType = "application/octet-stream";

#if (!KeePassLibSD && !KeePassUAP)
			return ("data:" + strMediaType + ";base64," + Convert.ToBase64String(
				pbData, Base64FormattingOptions.None));
#else
			return ("data:" + strMediaType + ";base64," + Convert.ToBase64String(
				pbData));
#endif
		}

		/// <summary>
		/// Convert a data URI (according to RFC 2397) to binary data.
		/// </summary>
		/// <param name="strDataUri">Data URI to decode.</param>
		/// <returns>Decoded binary data.</returns>
		public static byte[] DataUriToData(string strDataUri)
		{
			if (strDataUri == null) throw new ArgumentNullException("strDataUri");
			if (!strDataUri.StartsWith("data:", StrUtil.CaseIgnoreCmp)) return null;

			int iSep = strDataUri.IndexOf(',');
			if (iSep < 0) return null;

			string strDesc = strDataUri.Substring(5, iSep - 5);
			bool bBase64 = strDesc.EndsWith(";base64", StrUtil.CaseIgnoreCmp);

			string strData = strDataUri.Substring(iSep + 1);

			if (bBase64) return Convert.FromBase64String(strData);

			MemoryStream ms = new MemoryStream();
			Encoding enc = Encoding.ASCII;

			string[] v = strData.Split('%');
			byte[] pb = enc.GetBytes(v[0]);
			ms.Write(pb, 0, pb.Length);
			for (int i = 1; i < v.Length; ++i)
			{
				ms.WriteByte(Convert.ToByte(v[i].Substring(0, 2), 16));
				pb = enc.GetBytes(v[i].Substring(2));
				ms.Write(pb, 0, pb.Length);
			}

			pb = ms.ToArray();
			ms.Close();
			return pb;
		}

		// https://www.iana.org/assignments/media-types/media-types.xhtml
		private static readonly string[] g_vMediaTypePfx = new string[] {
			"application/", "audio/", "example/", "font/", "image/",
			"message/", "model/", "multipart/", "text/", "video/"
		};
		internal static bool IsMediaType(string str)
		{
			if (str == null) { Debug.Assert(false); return false; }
			if (str.Length == 0) return false;

			foreach (string strPfx in g_vMediaTypePfx)
			{
				if (str.StartsWith(strPfx, StrUtil.CaseIgnoreCmp))
					return true;
			}

			return false;
		}

		internal static string GetCustomMediaType(string strFormat)
		{
			if (strFormat == null)
			{
				Debug.Assert(false);
				return "application/octet-stream";
			}

			if (IsMediaType(strFormat)) return strFormat;

			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < strFormat.Length; ++i)
			{
				char ch = strFormat[i];

				if (((ch >= 'A') && (ch <= 'Z')) || ((ch >= 'a') && (ch <= 'z')) ||
					((ch >= '0') && (ch <= '9')))
					sb.Append(ch);
				else if ((sb.Length != 0) && ((ch == '-') || (ch == '_')))
					sb.Append(ch);
				else { Debug.Assert(false); }
			}

			if (sb.Length == 0) return "application/octet-stream";

			return ("application/vnd." + PwDefs.ShortProductName +
				"." + sb.ToString());
		}

		/// <summary>
		/// Remove placeholders from a string (wrapped in '{' and '}').
		/// This doesn't remove environment variables (wrapped in '%').
		/// </summary>
		public static string RemovePlaceholders(string str)
		{
			if (str == null) { Debug.Assert(false); return string.Empty; }

			while (true)
			{
				int iPlhStart = str.IndexOf('{');
				if (iPlhStart < 0) break;

				int iPlhEnd = str.IndexOf('}', iPlhStart); // '{' might be at end
				if (iPlhEnd < 0) break;

				str = (str.Substring(0, iPlhStart) + str.Substring(iPlhEnd + 1));
			}

			return str;
		}

		public static StrEncodingInfo GetEncoding(StrEncodingType t)
		{
			foreach (StrEncodingInfo sei in StrUtil.Encodings)
			{
				if (sei.Type == t) return sei;
			}

			return null;
		}

		public static StrEncodingInfo GetEncoding(string strName)
		{
			foreach (StrEncodingInfo sei in StrUtil.Encodings)
			{
				if (sei.Name == strName) return sei;
			}

			return null;
		}

		private static string[] m_vPrefSepChars = null;
		/// <summary>
		/// Find a character that does not occur within a given text.
		/// </summary>
		public static char GetUnusedChar(string strText)
		{
			if (strText == null) { Debug.Assert(false); return '@'; }

			if (m_vPrefSepChars == null)
				m_vPrefSepChars = new string[5] {
					"@!$%#/\\:;,.*-_?",
					PwCharSet.UpperCase, PwCharSet.LowerCase,
					PwCharSet.Digits, PwCharSet.PrintableAsciiSpecial
				};

			for (int i = 0; i < m_vPrefSepChars.Length; ++i)
			{
				foreach (char ch in m_vPrefSepChars[i])
				{
					if (strText.IndexOf(ch) < 0) return ch;
				}
			}

			for (char ch = '\u00C0'; ch < char.MaxValue; ++ch)
			{
				if (strText.IndexOf(ch) < 0) return ch;
			}

			return char.MinValue;
		}

		public static char ByteToSafeChar(byte bt)
		{
			const char chDefault = '.';

			// 00-1F are C0 control chars
			if (bt < 0x20) return chDefault;

			// 20-7F are basic Latin; 7F is DEL
			if (bt < 0x7F) return (char)bt;

			// 80-9F are C1 control chars
			if (bt < 0xA0) return chDefault;

			// A0-FF are Latin-1 supplement; AD is soft hyphen
			if (bt == 0xAD) return '-';
			return (char)bt;
		}

		public static int Count(string str, string strNeedle)
		{
			if (str == null) { Debug.Assert(false); return 0; }
			if (string.IsNullOrEmpty(strNeedle)) { Debug.Assert(false); return 0; }

			int iOffset = 0, iCount = 0;
			while (iOffset < str.Length)
			{
				int p = str.IndexOf(strNeedle, iOffset);
				if (p < 0) break;

				++iCount;
				iOffset = p + 1;
			}

			return iCount;
		}

		internal static string ReplaceNulls(string str)
		{
			if (str == null) { Debug.Assert(false); return null; }

			if (str.IndexOf('\0') < 0) return str;

			// Replacing null characters by spaces is the
			// behavior of Notepad (on Windows 10)
			return str.Replace('\0', ' ');
		}

		// https://sourceforge.net/p/keepass/discussion/329220/thread/f98dece5/
		internal static string EnsureLtrPath(string strPath)
		{
			if (strPath == null) { Debug.Assert(false); return string.Empty; }

			string str = strPath;

			// U+200E = left-to-right mark
			str = str.Replace("\\", "\\\u200E");
			str = str.Replace("/", "/\u200E");
			str = str.Replace("\u200E\u200E", "\u200E"); // Remove duplicates

			return str;
		}

		internal static bool IsValid(string str)
		{
			if (str == null) { Debug.Assert(false); return false; }

			int cc = str.Length;
			for (int i = 0; i < cc; ++i)
			{
				char ch = str[i];
				if (ch == '\0') return false;

				if (char.IsLowSurrogate(ch)) return false;
				if (char.IsHighSurrogate(ch))
				{
					if (++i >= cc) return false; // High surrogate at end
					if (!char.IsLowSurrogate(str[i])) return false;

					UnicodeCategory uc2 = char.GetUnicodeCategory(str, i - 1);
					if (uc2 == UnicodeCategory.OtherNotAssigned) return false;

					continue;
				}

				UnicodeCategory uc = char.GetUnicodeCategory(ch);
				if (uc == UnicodeCategory.OtherNotAssigned) return false;
			}

			return true;
		}

		internal static string RemoveWhiteSpace(string str)
		{
			if (str == null) { Debug.Assert(false); return string.Empty; }

			int cc = str.Length;
			if (cc == 0) return string.Empty;

			StringBuilder sb = new StringBuilder();

			for (int i = 0; i < cc; ++i)
			{
				char ch = str[i];
				if (char.IsWhiteSpace(ch)) continue;
				sb.Append(ch);
			}

			return sb.ToString();
		}
	}
}
