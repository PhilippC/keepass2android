
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
