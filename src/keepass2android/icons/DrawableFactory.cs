/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Widget;
using Android.Graphics.Drawables;
using Android.Content.Res;
using KeePassLib;
using Android.Graphics;
using Android.Preferences;

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
		private readonly Dictionary<int/*icon key*/, Drawable> _standardIconMap = new Dictionary<int, Drawable>();
			
		public void AssignDrawableTo (ImageView iv, Context context, PwDatabase db, PwIcon icon, PwUuid customIconId, bool forGroup)
		{
			Drawable draw = GetIconDrawable (context, db, icon, customIconId, forGroup);
			if (draw != null)
				iv.SetImageDrawable(draw);
			else
				Kp2aLog.Log("icon not found : " + icon);
		}
			
		public Drawable GetIconDrawable (Context context, PwDatabase db, PwIcon icon, PwUuid customIconId, bool forGroup)
		{
			if ((customIconId != null) && (!customIconId.Equals(PwUuid.Zero))) {
				return GetIconDrawable (context, db, customIconId);
			}
		    return GetIconDrawable (context, icon, forGroup);
		}

		public bool IsWhiteIconSet
		{
			get
			{
				var context = Application.Context;
				string packageName = PreferenceManager.GetDefaultSharedPreferences(Application.Context).GetString("IconSetKey", context.PackageName);
				//assume that at the momemt only the built in icons are white
				return packageName == context.PackageName;
			}
		}

		private static void InitBlank(Context context)	
		{
			if (_blank == null) {
				_blank = context.Resources.GetDrawable (Resource.Drawable.ic99_blank);
				_blankWidth = _blank.IntrinsicWidth;
				_blankHeight = _blank.IntrinsicHeight;
			}
		}
			
		public Drawable GetIconDrawable (Context context, PwIcon icon, bool forGroup)
		{
			//int resId = DefaultIcons.IconToResId (icon, forGroup);
			int dictKey = GetDictionaryKey(icon, forGroup);
				
			Drawable draw;
			if (!_standardIconMap.TryGetValue(dictKey, out draw))
			{
				string packageName = PreferenceManager.GetDefaultSharedPreferences(Application.Context).GetString("IconSetKey", context.PackageName);

				Resources res = context.PackageManager.GetResourcesForApplication(packageName);

				try
				{
					int resId = GetResourceId(res, dictKey, packageName);
					if (resId == 0)
						draw = context.Resources.GetDrawable(Resource.Drawable.ic99_blank);
					else
						draw = res.GetDrawable(resId);
					
				}
				catch (System.Exception e)
				{
					Kp2aLog.Log(e.ToString());
					draw = context.Resources.GetDrawable(Resource.Drawable.ic99_blank);
				}
				
			}
			_standardIconMap[dictKey] = draw;
			return draw;
		}

		private const int FolderOffset = 10000;

		private int GetResourceId(Resources res, int dictKey, string packageName)
		{
			bool forFolder = dictKey >= FolderOffset;
			int iconId = dictKey%FolderOffset;
			var name = (forFolder? "icf" : "ic") + iconId.ToString("D2");
			int resId = res.GetIdentifier(name, "drawable", packageName);
			if ((forFolder) && (resId == 0)) //if no explicit folder icon was found, retry a normal icon
			{
				name = "ic" + iconId.ToString("D2");
				resId = res.GetIdentifier(name, "drawable", packageName);
			}
			//return what we found, may still be 0
			return resId;
		}

		private int GetDictionaryKey(PwIcon icon, bool forGroup)
		{

			return ((int)icon) + (forGroup ? FolderOffset : 0);
		}

		public Drawable GetIconDrawable(Context context, PwDatabase db, PwUuid icon)
		{
			InitBlank (context);
			if (icon.Equals(PwUuid.Zero)) {
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
					
				draw = new BitmapDrawable(context.Resources, bitmap);
				_customIconMap[icon] = draw;
			}
				
			return draw;
		}
		
		
			
		public void Clear ()
		{
			_standardIconMap.Clear ();
			_customIconMap.Clear ();
		}

	}
}

