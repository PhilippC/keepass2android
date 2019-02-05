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
