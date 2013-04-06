
package keepass2android.kbbridge;
import java.util.HashMap;
public class KeyboardData {
	public static HashMap<String, String> availableFields = new HashMap<String, String>();
	public static String entryName;
	 
	public static void clear()
	{
		 availableFields.clear();
	}
}
