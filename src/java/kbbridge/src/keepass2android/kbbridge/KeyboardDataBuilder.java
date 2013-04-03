package keepass2android.kbbridge;
import java.util.HashMap;
public class KeyboardDataBuilder {
	 private HashMap<String, String> availableFields = new HashMap<String, String>();
	 
	 public void addPair(String displayName, String valueToType)
	 {
		 availableFields.put(displayName, valueToType);
	 }
	 
	 public void commit()
	 {
	 	KeyboardData.availableFields = this.availableFields;
	 }
}
