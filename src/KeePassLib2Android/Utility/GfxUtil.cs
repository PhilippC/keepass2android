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
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;

namespace KeePassLib.Utility
{
	public static class GfxUtil
	{
		public static Image LoadImage(byte[] pb)
		{
			if(pb == null) throw new ArgumentNullException("pb");

			MemoryStream ms = new MemoryStream(pb, false);
			try { return LoadImagePriv(ms); }
			catch(Exception)
			{
				Image imgIco = TryLoadIco(pb);
				if(imgIco != null) return imgIco;
				throw;
			}
			finally { ms.Close(); }
		}

		private static Image LoadImagePriv(Stream s)
		{
			// Image.FromStream wants the stream to be open during
			// the whole lifetime of the image; as we can't guarantee
			// this, we make a copy of the image
			Image imgSrc = null;
			try
			{
#if !KeePassLibSD
				imgSrc = Image.FromStream(s);
				Bitmap bmp = new Bitmap(imgSrc.Width, imgSrc.Height,
					PixelFormat.Format32bppArgb);

				try
				{
					bmp.SetResolution(imgSrc.HorizontalResolution,
						imgSrc.VerticalResolution);
					Debug.Assert(bmp.Size == imgSrc.Size);
				}
				catch(Exception) { Debug.Assert(false); }
#else
				imgSrc = new Bitmap(s);
				Bitmap bmp = new Bitmap(imgSrc.Width, imgSrc.Height);
#endif

				using(Graphics g = Graphics.FromImage(bmp))
				{
					g.Clear(Color.Transparent);
					g.DrawImage(imgSrc, 0, 0);
				}

				return bmp;
			}
			finally { if(imgSrc != null) imgSrc.Dispose(); }
		}

		private static Image TryLoadIco(byte[] pb)
		{
#if !KeePassLibSD
			MemoryStream ms = new MemoryStream(pb, false);
			try { return (new Icon(ms)).ToBitmap(); }
			catch(Exception) { }
			finally { ms.Close(); }
#endif

			return null;
		}
	}
}
