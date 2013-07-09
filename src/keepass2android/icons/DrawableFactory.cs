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

using System.Collections.Generic;
using Android.Widget;
using Android.Graphics.Drawables;
using Android.Content.Res;
using KeePassLib;
using Android.Graphics;

namespace keepass2android
{
	/// <summary>
	/// Factory to create password icons
	/// </summary>
	public class DrawableFactory: IDrawableFactory
	{
		private static Drawable _blank;
		private static int _blankWidth = -1;
		private static int _blankHeight = -1;
			
		/** customIconMap
	 *  Cache for icon drawable. 
	 *  Keys: UUID, Values: Drawables
	 */
		private readonly Dictionary<PwUuid, Drawable> _customIconMap = new Dictionary<PwUuid, Drawable>(new PwUuidEqualityComparer());
			
		/** standardIconMap
	 *  Cache for icon drawable. 
	 *  Keys: Integer, Values: Drawables
	 */
		private readonly Dictionary<int/*resId*/, Drawable> _standardIconMap = new Dictionary<int, Drawable>();
			
		public void AssignDrawableTo (ImageView iv, Resources res, PwDatabase db, PwIcon icon, PwUuid customIconId)
		{
			Drawable draw = GetIconDrawable (res, db, icon, customIconId);
			iv.SetImageDrawable (draw);
		}
			
		public Drawable GetIconDrawable (Resources res, PwDatabase db, PwIcon icon, PwUuid customIconId)
		{
			if (!customIconId.EqualsValue(PwUuid.Zero)) {
				return GetIconDrawable (res, db, customIconId);
			}
		    return GetIconDrawable (res, icon);
		}

		private static void InitBlank (Resources res)	
		{
			if (_blank == null) {
				_blank = res.GetDrawable (Resource.Drawable.ic99_blank);
				_blankWidth = _blank.IntrinsicWidth;
				_blankHeight = _blank.IntrinsicHeight;
			}
		}
			
		public Drawable GetIconDrawable (Resources res, PwIcon icon)
		{
			int resId = Icons.IconToResId (icon);
				
			Drawable draw;
			if (!_standardIconMap.TryGetValue(resId, out draw))
			{
				draw = res.GetDrawable(resId);
				_standardIconMap[resId] = draw;
			}
				
			return draw;
		}
			
		public Drawable GetIconDrawable (Resources res, PwDatabase db, PwUuid icon)
		{
			InitBlank (res);
			if (icon.EqualsValue(PwUuid.Zero)) {
				return _blank;
			}
			Drawable draw;
			if (!_customIconMap.TryGetValue(icon, out draw)) 
			{
				Bitmap bitmap = db.GetCustomIcon(icon);
					
				// Could not understand custom icon
				if (bitmap == null) {
					return _blank;
				}
					
				bitmap = resize (bitmap);

				draw = new BitmapDrawable(res, bitmap);
				_customIconMap[icon] = draw;
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
				
			if (width == _blankWidth && height == _blankHeight) {
				return bitmap;
			}
				
			return Bitmap.CreateScaledBitmap (bitmap, _blankWidth, _blankHeight, true);
		}
			
		public void Clear ()
		{
			_standardIconMap.Clear ();
			_customIconMap.Clear ();
		}

	}
}

