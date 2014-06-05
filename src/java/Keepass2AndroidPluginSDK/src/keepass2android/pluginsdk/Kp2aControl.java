package keepass2android.pluginsdk;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.Iterator;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import android.content.Intent;
import android.text.TextUtils;

public class Kp2aControl {
	
	/**
	 * Creates and returns an intent to launch Keepass2Android for adding an entry with the given fields.
	 * @param fields Key/Value pairs of the field values. See KeepassDefs for standard keys.
	 * @param protectedFields List of keys of the protected fields.
	 * @return Intent to start Keepass2Android.
	 * @throws JSONException
	 */
	public static Intent getAddEntryIntent(HashMap<String, String> fields, ArrayList<String> protectedFields)
	{
		return getAddEntryIntent(new JSONObject(fields).toString(), protectedFields);
	}
	
	public static Intent getAddEntryIntent(String outputData, ArrayList<String> protectedFields)
	{
		Intent startKp2aIntent = new Intent(Strings.ACTION_START_WITH_TASK);
		startKp2aIntent.addCategory(Intent.CATEGORY_DEFAULT);
		startKp2aIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
		startKp2aIntent.putExtra("KP2A_APPTASK", "CreateEntryThenCloseTask");
		startKp2aIntent.putExtra("ShowUserNotifications", "false"); //KP2A expects a StringExtra
		startKp2aIntent.putExtra(Strings.EXTRA_ENTRY_OUTPUT_DATA, outputData);
		if (protectedFields != null)
			startKp2aIntent.putStringArrayListExtra(Strings.EXTRA_PROTECTED_FIELDS_LIST, protectedFields);
		
			
		return startKp2aIntent;
	}
	
	
	/**
	 * Creates an intent to open a Password Entry matching searchText
	 * @param searchText queryString
	 * @param showUserNotifications if true, the notifications (copy to clipboard, keyboard) are displayed
	 * @param closeAfterOpen if true, the entry is opened and KP2A is immediately closed
	 * @return Intent to start KP2A with
	 */
	public static Intent getOpenEntryIntent(String searchText, boolean showUserNotifications, boolean closeAfterOpen)
	{
		Intent startKp2aIntent = new Intent(Strings.ACTION_START_WITH_TASK);
		startKp2aIntent.addCategory(Intent.CATEGORY_DEFAULT);
		startKp2aIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
		startKp2aIntent.putExtra("KP2A_APPTASK", "SearchUrlTask");
		startKp2aIntent.putExtra("ShowUserNotifications", String.valueOf(showUserNotifications));
		startKp2aIntent.putExtra("CloseAfterCreate", String.valueOf(closeAfterOpen));
		startKp2aIntent.putExtra("UrlToSearch", searchText);
		return startKp2aIntent;
	}
	
	/**
	 * Creates an intent to query a password entry from KP2A. The credentials are returned as Activity result.
	 * @param searchText Text to search for. Should be a URL or "androidapp://com.my.package."
	 * @return an Intent to start KP2A with
	 */
	public static Intent getQueryEntryIntent(String searchText)
	{
		Intent i = new Intent(Strings.ACTION_QUERY_CREDENTIALS);
		if (!TextUtils.isEmpty(searchText))
			i.putExtra(Strings.EXTRA_QUERY_STRING, searchText);
		return i;
	}
	
	/**
	 * Creates an intent to query a password entry from KP2A, matching to the current app's package . 
	 * The credentials are returned as Activity result.
	 * This requires SCOPE_QUERY_CREDENTIALS_FOR_OWN_PACKAGE.
	 * @return an Intent to start KP2A with
	 */
	public static Intent getQueryEntryIntentForOwnPackage()
	{
		return new Intent(Strings.ACTION_QUERY_CREDENTIALS_FOR_OWN_PACKAGE);
	}
	
	/**
	 * Converts the entry fields returned in an intent from a query to a hashmap. 
	 * @param intent data received in onActivityResult after getQueryEntryIntent(ForOwnPackage)
	 * @return HashMap with keys = field names (see KeepassDefs for standard keys) and values = values
	 */
	public static HashMap<String, String> getEntryFieldsFromIntent(Intent intent)  
	{
		HashMap<String, String> res = new HashMap<String, String>();
		try {
			JSONObject json = new JSONObject(intent.getStringExtra(Strings.EXTRA_ENTRY_OUTPUT_DATA));
			for(Iterator<String> iter = json.keys();iter.hasNext();) {
			    String key = iter.next();
			    String value = json.get(key).toString();
			    res.put(key, value);
			}
			
		} catch (JSONException e) {
			e.printStackTrace();
		} catch (NullPointerException e) {
			e.printStackTrace();
		} 
		return res;
	}

}
