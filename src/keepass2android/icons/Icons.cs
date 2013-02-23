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
using System.Reflection;
using KeePassLib;
using Android.Util;

namespace keepass2android
{
	
	public class Icons {
		private static Dictionary<PwIcon,int> icons = null;
		
		private static void buildList() {
			if (icons == null) {
				icons = new Dictionary<PwIcon,int>();

				FieldInfo[] fields = typeof(Resource.Drawable).GetFields(BindingFlags.Static | BindingFlags.Public);
				for (int i = 0; i < fields.Length; i++) {
					String fieldName = fields[i].Name;

					if (fieldName.StartsWith("ic") && (fieldName.Length >= 4))
					{

						String sNum = fieldName.Substring(2, 2);
						int num;
						if (int.TryParse(sNum,out num) && (num < (int)PwIcon.Count))
						{
						
						int resId;
						try {
							resId = (int)fields[i].GetValue(null);
						} catch (Exception) {
							continue;
						}
						
						icons[(PwIcon)num] = resId;
						}
					}
				}
			}	
		}
		
		public static int iconToResId(PwIcon iconId) {
			buildList();
			int resId = Resource.Drawable.ic99_blank;
			icons.TryGetValue(iconId, out resId);
			return resId;
		}
		
		public static int count() {
			buildList();
			
			return icons.Count;
		}
		
	}

}

