package keepass2android.kbbridge;

public class StringForTyping {
	public String key; //internal identifier (PwEntry string field key)
	public String displayName; //display name for displaying the key (might be translated)
	public String value;
	
	@Override
	public StringForTyping clone(){

		StringForTyping theClone = new StringForTyping();
		theClone.key = key;
		theClone.displayName = displayName;
		theClone.value = value;
		
		return theClone;
	}
	

}
