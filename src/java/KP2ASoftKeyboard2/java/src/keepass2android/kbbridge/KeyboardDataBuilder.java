package keepass2android.kbbridge;
import java.util.ArrayList;
import java.util.HashMap;
public class KeyboardDataBuilder {
	 private ArrayList<StringForTyping> availableFields = new ArrayList<StringForTyping>();
	 
	 public void addPair(String displayName, String valueToType)
	 {
		 StringForTyping pair = new StringForTyping();
		 pair.displayName = displayName;
		 pair.value = valueToType;
		 availableFields.add(pair);
	 }
	 
	 public void commit()
	 {
	 	KeyboardData.availableFields = this.availableFields;
	 }
}
