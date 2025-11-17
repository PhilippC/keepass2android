/*
 * This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
 *
 *   Keepass2Android is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   Keepass2Android is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
 */


package keepass2android.kbbridge;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;

import android.text.TextUtils;
public class KeyboardData 
{
	public static List<StringForTyping> availableFields = new ArrayList<StringForTyping>();
	public static String entryName;
	public static String entryId;

	public static int kp2aFieldIndex = 0;
	
	public static boolean hasData()
	{
		return !TextUtils.isEmpty(entryId); 
	}
	 
	public static void clear()
	{
		 availableFields.clear();
		 entryName = entryId = "";
		kp2aFieldIndex = 0;
	}
}
