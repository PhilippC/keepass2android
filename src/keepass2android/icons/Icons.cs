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
using System.Reflection;
using KeePassLib;

namespace keepass2android
{

	public class Icons
	{
		private static Dictionary<PwIcon, int> _icons;

		private static void BuildList()
		{
			if (_icons == null)
			{
				_icons = new Dictionary<PwIcon, int>();

				FieldInfo[] fields = typeof(Resource.Drawable).GetFields(BindingFlags.Static | BindingFlags.Public);
				foreach (FieldInfo fieldInfo in fields)
				{
					String fieldName = fieldInfo.Name;

					if (fieldName.StartsWith("ic") && (fieldName.Length >= 4))
					{

						String sNum = fieldName.Substring(2, 2);
						int num;
						if (int.TryParse(sNum, out num) && (num < (int)PwIcon.Count))
						{

							int resId;
							try
							{
								resId = (int)fieldInfo.GetValue(null);
							}
							catch (Exception)
							{
								continue;
							}

							_icons[(PwIcon)num] = resId;
						}
					}
				}
			}
		}

		public static int IconToResId(PwIcon iconId)
		{
			BuildList();
			int resId;
			if (_icons.TryGetValue(iconId, out resId))
				return resId;
			return Resource.Drawable.ic99_blank;
		}

		public static int Count()
		{
			BuildList();

			return _icons.Count;
		}

	}

}

