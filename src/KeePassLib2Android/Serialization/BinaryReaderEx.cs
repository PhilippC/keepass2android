/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2012 Dominik Reichl <dominik.reichl@t-online.de>

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

using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
	public sealed class BinaryReaderEx
	{
		private Stream m_s;
		private Encoding m_enc;

		private string m_strReadExcp;
		public string ReadExceptionText
		{
			get { return m_strReadExcp; }
			set { m_strReadExcp = value; }
		}

		private Stream m_sCopyTo = null;
		/// <summary>
		/// If this property is set to a non-null stream, all data that
		/// is read from the input stream is automatically written to
		/// the copy stream (before returning the read data).
		/// </summary>
		public Stream CopyDataTo
		{
			get { return m_sCopyTo; }
			set { m_sCopyTo = value; }
		}

		public BinaryReaderEx(Stream input, Encoding encoding,
			string strReadExceptionText)
		{
			if(input == null) throw new ArgumentNullException("input");

			m_s = input;
			m_enc = encoding;
			m_strReadExcp = strReadExceptionText;
		}

		public byte[] ReadBytes(int nCount)
		{
			try
			{
				byte[] pb = MemUtil.Read(m_s, nCount);
				if((pb == null) || (pb.Length != nCount))
				{
					if(m_strReadExcp != null) throw new IOException(m_strReadExcp);
					else throw new EndOfStreamException();
				}

				if(m_sCopyTo != null) m_sCopyTo.Write(pb, 0, pb.Length);
				return pb;
			}
			catch(Exception)
			{
				if(m_strReadExcp != null) throw new IOException(m_strReadExcp);
				else throw;
			}
		}

		public byte ReadByte()
		{
			byte[] pb = ReadBytes(1);
			return pb[0];
		}
	}
}
