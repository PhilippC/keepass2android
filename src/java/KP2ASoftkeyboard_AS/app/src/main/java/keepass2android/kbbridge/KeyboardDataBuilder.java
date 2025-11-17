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

import keepass2android.softkeyboard.IKeyboardService;
import keepass2android.softkeyboard.KP2AKeyboard;

public class KeyboardDataBuilder {
	 private ArrayList<StringForTyping> availableFields = new ArrayList<StringForTyping>();
	 
	 public void addString(String key, String displayName, String valueToType)
	 {
		 StringForTyping stringToType = new StringForTyping();
		 stringToType.key = key;
		 stringToType.displayName = displayName;
		 stringToType.value = valueToType;
		 availableFields.add(stringToType);
	 }
	 
	 public void commit()
	 {
	 	KeyboardData.availableFields = this.availableFields;
	 	KeyboardData.kp2aFieldIndex = 0;
	 	if (KP2AKeyboard.CurrentlyRunningService != null)
	 		KP2AKeyboard.CurrentlyRunningService.onNewData();
	 }
}
