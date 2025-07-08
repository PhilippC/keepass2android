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

namespace KeePassLib.Cryptography.PasswordGenerator
{
    public sealed class PwCharSet : IEquatable<PwCharSet>
    {
        public static readonly string UpperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public static readonly string LowerCase = "abcdefghijklmnopqrstuvwxyz";
        public static readonly string Digits = "0123456789";

        public static readonly string UpperConsonants = "BCDFGHJKLMNPQRSTVWXYZ";
        public static readonly string LowerConsonants = "bcdfghjklmnpqrstvwxyz";
        public static readonly string UpperVowels = "AEIOU";
        public static readonly string LowerVowels = "aeiou";

        public static readonly string Punctuation = ",.;:";
        public static readonly string Brackets = @"[]{}()<>";

        public static readonly string Special = "!\"#$%&'*+,./:;=?@\\^`|~";
        public static readonly string PrintableAsciiSpecial = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

        public static readonly string UpperHex = "0123456789ABCDEF";
        public static readonly string LowerHex = "0123456789abcdef";

        public static readonly string LookAlike = "O0Il1|";

        /// <summary>
        /// Latin-1 Supplement except U+00A0 (NBSP) and U+00AD (SHY).
        /// </summary>
        public static readonly string Latin1S =
            "\u00A1\u00A2\u00A3\u00A4\u00A5\u00A6\u00A7" +
            "\u00A8\u00A9\u00AA\u00AB\u00AC\u00AE\u00AF" +
            "\u00B0\u00B1\u00B2\u00B3\u00B4\u00B5\u00B6\u00B7" +
            "\u00B8\u00B9\u00BA\u00BB\u00BC\u00BD\u00BE\u00BF" +
            "\u00C0\u00C1\u00C2\u00C3\u00C4\u00C5\u00C6\u00C7" +
            "\u00C8\u00C9\u00CA\u00CB\u00CC\u00CD\u00CE\u00CF" +
            "\u00D0\u00D1\u00D2\u00D3\u00D4\u00D5\u00D6\u00D7" +
            "\u00D8\u00D9\u00DA\u00DB\u00DC\u00DD\u00DE\u00DF" +
            "\u00E0\u00E1\u00E2\u00E3\u00E4\u00E5\u00E6\u00E7" +
            "\u00E8\u00E9\u00EA\u00EB\u00EC\u00ED\u00EE\u00EF" +
            "\u00F0\u00F1\u00F2\u00F3\u00F4\u00F5\u00F6\u00F7" +
            "\u00F8\u00F9\u00FA\u00FB\u00FC\u00FD\u00FE\u00FF";

        // internal static readonly string MenuAccels = PwCharSet.LowerCase + PwCharSet.Digits;

        [Obsolete]
        public static string SpecialChars { get { return PwCharSet.Special; } }
        [Obsolete]
        public static string HighAnsiChars { get { return PwCharSet.Latin1S; } }

        private readonly List<char> m_lChars = new List<char>();
        private readonly byte[] m_vTab = new byte[0x10000 / 8];

        /// <summary>
        /// Create a new, empty character set.
        /// </summary>
        public PwCharSet()
        {
            Debug.Assert(PwCharSet.Latin1S.Length == (16 * 6 - 2));
        }

        public PwCharSet(string strCharSet)
        {
            Add(strCharSet);
        }

        /// <summary>
        /// Number of characters in this set.
        /// </summary>
        public uint Size
        {
            get { return (uint)m_lChars.Count; }
        }

        /// <summary>
        /// Get a character of the set using an index.
        /// </summary>
        /// <param name="uPos">Index of the character to get.</param>
        /// <returns>Character at the specified position. If the index is invalid,
        /// an <c>ArgumentOutOfRangeException</c> is thrown.</returns>
        public char this[uint uPos]
        {
            get
            {
                if (uPos >= (uint)m_lChars.Count)
                    throw new ArgumentOutOfRangeException("uPos");

                return m_lChars[(int)uPos];
            }
        }

        public bool Equals(PwCharSet other)
        {
            if (object.ReferenceEquals(other, this)) return true;
            if (object.ReferenceEquals(other, null)) return false;

            if (m_lChars.Count != other.m_lChars.Count) return false;

            return MemUtil.ArraysEqual(m_vTab, other.m_vTab);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PwCharSet);
        }

        public override int GetHashCode()
        {
            return (int)MemUtil.Hash32(m_vTab, 0, m_vTab.Length);
        }

        /// <summary>
        /// Remove all characters from this set.
        /// </summary>
        public void Clear()
        {
            m_lChars.Clear();
            Array.Clear(m_vTab, 0, m_vTab.Length);
        }

        public bool Contains(char ch)
        {
            return (((m_vTab[ch / 8] >> (ch % 8)) & 1) != char.MinValue);
        }

        public bool Contains(string strCharacters)
        {
            Debug.Assert(strCharacters != null);
            if (strCharacters == null) throw new ArgumentNullException("strCharacters");

            foreach (char ch in strCharacters)
            {
                if (!Contains(ch)) return false;
            }

            return true;
        }

        /// <summary>
        /// Add characters to the set.
        /// </summary>
        /// <param name="ch">Character to add.</param>
        public void Add(char ch)
        {
            if (ch == char.MinValue) { Debug.Assert(false); return; }

            if (!Contains(ch))
            {
                m_lChars.Add(ch);
                m_vTab[ch / 8] |= (byte)(1 << (ch % 8));
            }
        }

        /// <summary>
        /// Add characters to the set.
        /// </summary>
        /// <param name="strCharSet">String containing characters to add.</param>
        public void Add(string strCharSet)
        {
            Debug.Assert(strCharSet != null);
            if (strCharSet == null) throw new ArgumentNullException("strCharSet");

            foreach (char ch in strCharSet)
                Add(ch);
        }

        public void Add(string strCharSet1, string strCharSet2)
        {
            Add(strCharSet1);
            Add(strCharSet2);
        }

        public void Add(string strCharSet1, string strCharSet2, string strCharSet3)
        {
            Add(strCharSet1);
            Add(strCharSet2);
            Add(strCharSet3);
        }

        public void AddRange(char chMin, char chMax)
        {
            for (char ch = chMin; ch < chMax; ++ch)
                Add(ch);

            Add(chMax);
        }

        public bool AddCharSet(char chCharSetIdentifier)
        {
            bool bResult = true;

            switch (chCharSetIdentifier)
            {
                case 'a': Add(PwCharSet.LowerCase, PwCharSet.Digits); break;
                case 'A':
                    Add(PwCharSet.LowerCase, PwCharSet.UpperCase,
                    PwCharSet.Digits); break;
                case 'U': Add(PwCharSet.UpperCase, PwCharSet.Digits); break;
                case 'c': Add(PwCharSet.LowerConsonants); break;
                case 'C':
                    Add(PwCharSet.LowerConsonants,
                    PwCharSet.UpperConsonants); break;
                case 'z': Add(PwCharSet.UpperConsonants); break;
                case 'd': Add(PwCharSet.Digits); break; // Digit
                case 'h': Add(PwCharSet.LowerHex); break;
                case 'H': Add(PwCharSet.UpperHex); break;
                case 'l': Add(PwCharSet.LowerCase); break;
                case 'L': Add(PwCharSet.LowerCase, PwCharSet.UpperCase); break;
                case 'u': Add(PwCharSet.UpperCase); break;
                case 'p': Add(PwCharSet.Punctuation); break;
                case 'b': Add(PwCharSet.Brackets); break;
                case 's': Add(PwCharSet.PrintableAsciiSpecial); break;
                case 'S':
                    Add(PwCharSet.UpperCase, PwCharSet.LowerCase);
                    Add(PwCharSet.Digits, PwCharSet.PrintableAsciiSpecial); break;
                case 'v': Add(PwCharSet.LowerVowels); break;
                case 'V': Add(PwCharSet.LowerVowels, PwCharSet.UpperVowels); break;
                case 'Z': Add(PwCharSet.UpperVowels); break;
                case 'x': Add(PwCharSet.Latin1S); break;
                default: bResult = false; break;
            }

            return bResult;
        }

        public bool Remove(char ch)
        {
            m_vTab[ch / 8] &= (byte)(~(1 << (ch % 8)));
            return m_lChars.Remove(ch);
        }

        public bool Remove(string strCharacters)
        {
            Debug.Assert(strCharacters != null);
            if (strCharacters == null) throw new ArgumentNullException("strCharacters");

            bool bResult = true;
            foreach (char ch in strCharacters)
            {
                if (!Remove(ch)) bResult = false;
            }

            return bResult;
        }

        public bool RemoveIfAllExist(string strCharacters)
        {
            Debug.Assert(strCharacters != null);
            if (strCharacters == null) throw new ArgumentNullException("strCharacters");

            if (!Contains(strCharacters))
                return false;

            return Remove(strCharacters);
        }

        /// <summary>
        /// Convert the character set to a string containing all its characters.
        /// </summary>
        /// <returns>String containing all character set characters.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(m_lChars.Count);
            foreach (char ch in m_lChars)
                sb.Append(ch);

            return sb.ToString();
        }

        public string PackAndRemoveCharRanges()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(RemoveIfAllExist(PwCharSet.UpperCase) ? 'U' : '_');
            sb.Append(RemoveIfAllExist(PwCharSet.LowerCase) ? 'L' : '_');
            sb.Append(RemoveIfAllExist(PwCharSet.Digits) ? 'D' : '_');
            sb.Append(RemoveIfAllExist(PwCharSet.Special) ? 'S' : '_');
            sb.Append(RemoveIfAllExist(PwCharSet.Punctuation) ? 'P' : '_');
            sb.Append(RemoveIfAllExist("-") ? 'm' : '_');
            sb.Append(RemoveIfAllExist("_") ? 'u' : '_');
            sb.Append(RemoveIfAllExist(" ") ? 's' : '_');
            sb.Append(RemoveIfAllExist(PwCharSet.Brackets) ? 'B' : '_');
            sb.Append(RemoveIfAllExist(PwCharSet.Latin1S) ? 'H' : '_');

            return sb.ToString();
        }

        public void UnpackCharRanges(string strRanges)
        {
            if (strRanges == null) { Debug.Assert(false); return; }
            if (strRanges.Length < 10) { Debug.Assert(false); return; }

            if (strRanges[0] != '_') Add(PwCharSet.UpperCase);
            if (strRanges[1] != '_') Add(PwCharSet.LowerCase);
            if (strRanges[2] != '_') Add(PwCharSet.Digits);
            if (strRanges[3] != '_') Add(PwCharSet.Special);
            if (strRanges[4] != '_') Add(PwCharSet.Punctuation);
            if (strRanges[5] != '_') Add('-');
            if (strRanges[6] != '_') Add('_');
            if (strRanges[7] != '_') Add(' ');
            if (strRanges[8] != '_') Add(PwCharSet.Brackets);
            if (strRanges[9] != '_') Add(PwCharSet.Latin1S);
        }
    }
}
