
package keepass2android.kbbridge;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
public class KeyboardData 
{
	public static List<StringForTyping> availableFields = new ArrayList<StringForTyping>();
	public static String entryName;
	public static String entryId;
	 
	public static void clear()
	{
		 availableFields.clear();
		 entryName = entryId = "";
	}
}
