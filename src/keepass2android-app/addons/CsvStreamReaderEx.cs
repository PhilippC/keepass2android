/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2018 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Diagnostics;

using KeePassLib.Utility;

namespace KeePass.DataExchange
{
    public sealed class CsvOptions
    {
        private char m_chFieldSep = ',';
        public char FieldSeparator
        {
            get { return m_chFieldSep; }
            set { m_chFieldSep = value; }
        }

        private char m_chRecSep = '\n';
        public char RecordSeparator
        {
            get { return m_chRecSep; }
            set { m_chRecSep = value; }
        }

        private char m_chTextQual = '\"';
        public char TextQualifier
        {
            get { return m_chTextQual; }
            set { m_chTextQual = value; }
        }

        private bool m_bBackEscape = true;
        public bool BackslashIsEscape
        {
            get { return m_bBackEscape; }
            set { m_bBackEscape = value; }
        }

        private bool m_bTrimFields = true;
        public bool TrimFields
        {
            get { return m_bTrimFields; }
            set { m_bTrimFields = value; }
        }

        private string m_strNewLineSeq = "\r\n";
        public string NewLineSequence
        {
            get { return m_strNewLineSeq; }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                m_strNewLineSeq = value;
            }
        }
    }

    public sealed class CsvStreamReaderEx
    {
        private CharStream m_sChars;
        private CsvOptions m_opt;

        public CsvStreamReaderEx(string strData)
        {
            Init(strData, null);
        }

        public CsvStreamReaderEx(string strData, CsvOptions opt)
        {
            Init(strData, opt);
        }

        private void Init(string strData, CsvOptions opt)
        {
            if (strData == null) throw new ArgumentNullException("strData");

            m_opt = (opt ?? new CsvOptions());

            string strInput = strData;

            // Normalize to Unix "\n" right now; the new lines are
            // converted back in the <c>AddField</c> method
            strInput = StrUtil.NormalizeNewLines(strInput, false);

            strInput = strInput.Trim(new char[] { (char)0 });

            m_sChars = new CharStream(strInput);
        }

        public string[] ReadLine()
        {
            char chFirst = m_sChars.PeekChar();
            if (chFirst == char.MinValue) return null;

            List<string> v = new List<string>();
            StringBuilder sb = new StringBuilder();
            bool bInText = false;

            char chFS = m_opt.FieldSeparator, chRS = m_opt.RecordSeparator;
            char chTQ = m_opt.TextQualifier;

            while (true)
            {
                char ch = m_sChars.ReadChar();
                if (ch == char.MinValue) break;

                Debug.Assert(ch != '\r'); // Was normalized to Unix "\n"

                if ((ch == '\\') && m_opt.BackslashIsEscape)
                {
                    char chEsc = m_sChars.ReadChar();
                    if (chEsc == char.MinValue) break;

                    if (chEsc == 'n') sb.Append('\n');
                    else if (chEsc == 'r') sb.Append('\r');
                    else if (chEsc == 't') sb.Append('\t');
                    else if (chEsc == 'u')
                    {
                        char chNum1 = m_sChars.ReadChar();
                        char chNum2 = m_sChars.ReadChar();
                        char chNum3 = m_sChars.ReadChar();
                        char chNum4 = m_sChars.ReadChar();
                        if (chNum4 != char.MinValue) // Implies the others
                        {
                            StringBuilder sbNum = new StringBuilder();
                            sbNum.Append(chNum3); // Little-endian
                            sbNum.Append(chNum4);
                            sbNum.Append(chNum1);
                            sbNum.Append(chNum2);

                            byte[] pbNum = MemUtil.HexStringToByteArray(sbNum.ToString());
                            ushort usNum = MemUtil.BytesToUInt16(pbNum);

                            sb.Append((char)usNum);
                        }
                    }
                    else sb.Append(chEsc);
                }
                else if (ch == chTQ)
                {
                    if (!bInText) bInText = true;
                    else // bInText
                    {
                        char chNext = m_sChars.PeekChar();
                        if (chNext == chTQ)
                        {
                            m_sChars.ReadChar();
                            sb.Append(chTQ);
                        }
                        else bInText = false;
                    }
                }
                else if ((ch == chRS) && !bInText) break;
                else if (bInText) sb.Append(ch);
                else if (ch == chFS)
                {
                    AddField(v, sb.ToString());
                    if (sb.Length > 0) sb.Remove(0, sb.Length);
                }
                else sb.Append(ch);
            }
            // Debug.Assert(!bInText);
            AddField(v, sb.ToString());

            return v.ToArray();
        }

        private void AddField(List<string> v, string strField)
        {
            // Escape characters might have been used to insert
            // new lines that might not conform to Unix "\n"
            strField = StrUtil.NormalizeNewLines(strField, false);

            // Transform to final form of new lines
            strField = strField.Replace("\n", m_opt.NewLineSequence);

            if (m_opt.TrimFields) strField = strField.Trim();

            v.Add(strField);
        }
    }
}
