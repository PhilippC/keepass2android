/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Graphics.Drawables;
using Android.Content.Res;
using KeePassLib;
using Android.Graphics;

namespace keepass2android
{
	public class DrawableFactory
	{
		private static Drawable blank = null;
		private static int blankWidth = -1;
		private static int blankHeight = -1;
			
		/** customIconMap
	 *  Cache for icon drawable. 
	 *  Keys: UUID, Values: Drawables
	 */
		private Dictionary<PwUuid, Drawable> customIconMap = new Dictionary<PwUuid, Drawable>(new PwUuidEqualityComparer());
			
		/** standardIconMap
	 *  Cache for icon drawable. 
	 *  Keys: Integer, Values: Drawables
	 */
		private Dictionary<int/*resId*/, Drawable> standardIconMap = new Dictionary<int, Drawable>();
			
		public void assignDrawableTo (ImageView iv, Resources res, PwDatabase db, PwIcon icon, PwUuid customIconId)
		{
			Drawable draw = getIconDrawable (res, db, icon, customIconId);
			iv.SetImageDrawable (draw);
		}
			
		public Drawable getIconDrawable (Resources res, PwDatabase db, PwIcon icon, PwUuid customIconId)
		{
			if (customIconId != PwUuid.Zero) {
				return getIconDrawable (res, db, customIconId);
			} else {
				return getIconDrawable (res, icon);
			}
		}
			
		private static void initBlank (Resources res)	
		{
			if (blank == null) {
				blank = res.GetDrawable (Resource.Drawable.ic99_blank);
				blankWidth = blank.IntrinsicWidth;
				blankHeight = blank.IntrinsicHeight;
			}
		}
			
		public Drawable getIconDrawable (Resources res, PwIcon icon)
		{
			int resId = Icons.iconToResId (icon);
				
			Drawable draw;
			if (!standardIconMap.TryGetValue(resId, out draw))
			{
				draw = res.GetDrawable(resId);
				standardIconMap[resId] = draw;
			}
				
			return draw;
		}
			
		public Drawable getIconDrawable (Resources res, PwDatabase db, PwUuid icon)
		{
			initBlank (res);
			if (icon == PwUuid.Zero) {
				return blank;
			}
			Drawable draw = null;
			if (!customIconMap.TryGetValue(icon, out draw)) 
			{
				Bitmap bitmap = db.GetCustomIcon(icon);
					
				// Could not understand custom icon
				if (bitmap == null) {
					return blank;
				}
					
				bitmap = resize (bitmap);
					
				draw = BitmapDrawableCompat.getBitmapDrawable (res, bitmap);
				customIconMap[icon] = draw;
			}
				
			return draw;
		}
			
		/** Resize the custom icon to match the built in icons
	 * @param bitmap
	 * @return
	 */
		private Bitmap resize (Bitmap bitmap)
		{
			int width = bitmap.Width;
			int height = bitmap.Height;
				
			if (width == blankWidth && height == blankHeight) {
				return bitmap;
			}
				
			return Bitmap.CreateScaledBitmap (bitmap, blankWidth, blankHeight, true);
		}
			
		public void Clear ()
		{
			standardIconMap.Clear ();
			customIconMap.Clear ();
		}

	}
}

