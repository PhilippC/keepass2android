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
using System.Diagnostics;
using System.Drawing;
using System.IO;

using KeePassLib.Utility;

namespace KeePassLib
{
	/// <summary>
	/// Custom icon. <c>PwCustomIcon</c> objects are immutable.
	/// </summary>
	public sealed class PwCustomIcon
	{
		private PwUuid m_pwUuid;
		private byte[] m_pbImageDataPng;
		private Image m_pCachedImage;

		public PwUuid Uuid
		{
			get { return m_pwUuid; }
		}

		public byte[] ImageDataPng
		{
			get { return m_pbImageDataPng; }
		}

		public Image Image
		{
			get { return m_pCachedImage; }
		}

		public PwCustomIcon(PwUuid pwUuid, byte[] pbImageDataPng)
		{
			Debug.Assert(pwUuid != null);
			if(pwUuid == null) throw new ArgumentNullException("pwUuid");
			Debug.Assert(pwUuid != PwUuid.Zero);
			if(pwUuid == PwUuid.Zero) throw new ArgumentException("pwUuid == 0");

			Debug.Assert(pbImageDataPng != null);
			if(pbImageDataPng == null) throw new ArgumentNullException("pbImageDataPng");

			m_pwUuid = pwUuid;
			m_pbImageDataPng = pbImageDataPng;

#if !KeePassLibSD
			// MemoryStream ms = new MemoryStream(m_pbImageDataPng, false);
			// m_pCachedImage = Image.FromStream(ms);
			// ms.Close();
			m_pCachedImage = GfxUtil.LoadImage(m_pbImageDataPng);
#else
			m_pCachedImage = null;
#endif
		}
	}
}
