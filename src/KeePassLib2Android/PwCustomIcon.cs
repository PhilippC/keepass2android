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
using System.Collections.Generic;
using System.Diagnostics;

#if !KeePassUAP
using System.Drawing;
#endif

using KeePassLib.Utility;

namespace KeePassLib
{
	public sealed class PwCustomIcon
	{
		// Recommended maximum sizes, not obligatory
		internal const int MaxWidth = 128;
		internal const int MaxHeight = 128;

		private readonly PwUuid m_uuid;
		private readonly byte[] m_pbImageDataPng;

		private string m_strName = string.Empty;
		private DateTime? m_odtLastMod = null;

		private Dictionary<long, Android.Graphics.Bitmap> m_dImageCache = new Dictionary<long, Android.Graphics.Bitmap>();

		public PwUuid Uuid
		{
			get { return m_uuid; }
		}

		public byte[] ImageDataPng
		{
			get { return m_pbImageDataPng; }
			// When allowing 'set', do not copy the cache in 'Clone'
		}

		public string Name
		{
			get { return m_strName; }
			set
			{
				if (value == null) throw new ArgumentNullException("value");
				m_strName = value;
			}
		}

		public DateTime? LastModificationTime
		{
			get { return m_odtLastMod; }
			set { m_odtLastMod = value; }
		}

		[Obsolete("Use GetImage instead.")]
		public Android.Graphics.Bitmap Image
		{
			get { return GetImage(); } // Backward compatibility
        }

		public PwCustomIcon(PwUuid pu, byte[] pbImageDataPng)
		{
			if (pu == null) { Debug.Assert(false); throw new ArgumentNullException("pu"); }
			if (pu.Equals(PwUuid.Zero)) { Debug.Assert(false); throw new ArgumentOutOfRangeException("pu"); }
			if (pbImageDataPng == null) { Debug.Assert(false); throw new ArgumentNullException("pbImageDataPng"); }

			m_uuid = pu;
			m_pbImageDataPng = pbImageDataPng;
		}

		private static long GetKey(int w, int h)
		{
			return (((long)w << 32) ^ (long)h);
		}

		/// <summary>
		/// Get the icon as an <c>Image</c> (original size).
		/// </summary>
		public Android.Graphics.Bitmap GetImage()
		{
			const long lKey = -1;

            Android.Graphics.Bitmap img;
			if (m_dImageCache.TryGetValue(lKey, out img)) return img;

			try { img = GfxUtil.LoadImage(m_pbImageDataPng); }
			catch (Exception) { Debug.Assert(false); }

			m_dImageCache[lKey] = img;
			return img;
		}

		internal PwCustomIcon Clone()
		{
			PwCustomIcon ico = new PwCustomIcon(m_uuid, m_pbImageDataPng);

			ico.m_strName = m_strName;
			ico.m_odtLastMod = m_odtLastMod;

			ico.m_dImageCache = m_dImageCache; // Same image data

			return ico;
		}
	}
}
