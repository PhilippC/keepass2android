package keepass2android.pluginsdk;

import java.util.ArrayList;

import org.json.JSONArray;
import org.json.JSONException;

import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.SharedPreferences.Editor;
import android.preference.PreferenceManager;


public class AccessManager 
{
	private static final String PREF_KEY_SCOPE = "scope";
	private static final String PREF_KEY_TOKEN = "token";

	public static String stringArrayToString(ArrayList<String> values) {
	    JSONArray a = new JSONArray();
	    for (int i = 0; i < values.size(); i++) {
	        a.put(values.get(i));
	    }
	    if (!values.isEmpty()) {
	        return a.toString();
	    } else {
	        return null;
	    }
	    
	}

	public static ArrayList<String> stringToStringArray(String s) {
	    ArrayList<String> strings = new ArrayList<String>();
	    if ((s != null) && (s != "")) {
	        try {
	            JSONArray a = new JSONArray(s);
	            for (int i = 0; i < a.length(); i++) {
	                String url = a.optString(i);
	                strings.add(url);
	            }
	        } catch (JSONException e) {
	            e.printStackTrace();
	        }
	    }
	    return strings;
	}
	
	public static void storeAccessToken(Context ctx, String hostPackage, String accessToken, ArrayList<String> scopes)
	{
		 SharedPreferences prefs = getPrefsForHost(ctx, hostPackage);
		 
		 //
		 if (accessToken.equals(prefs.getString(PREF_KEY_TOKEN, "")))
		 {
			 //token already available
			 return;
		 }
		 
		 Editor edit = prefs.edit();
		 edit.putString(PREF_KEY_TOKEN, accessToken);
		 edit.putString(PREF_KEY_SCOPE, stringArrayToString(scopes));
		 edit.commit();
		 
	}

	private static SharedPreferences getPrefsForHost(Context ctx,
			String hostPackage) {
		SharedPreferences prefs = ctx.getSharedPreferences("KP2A.PluginAccess."+hostPackage, Context.MODE_PRIVATE);
		return prefs;
	}

	public static String tryGetAccessToken(Context ctx, String hostPackage, ArrayList<String> scopes) {
		
		SharedPreferences prefs = getPrefsForHost(ctx, hostPackage);
		ArrayList<String> currentScope = stringToStringArray(prefs.getString(PREF_KEY_SCOPE, ""));
		if (isSubset(scopes, currentScope))
		{
			return prefs.getString(PREF_KEY_TOKEN, null);
		}
		else
		{
			return null;
		}
	}

	public static boolean isSubset(ArrayList<String> requiredScopes,
			ArrayList<String> availableScopes) {
		for (String r: requiredScopes){
			if (availableScopes.indexOf(r)<0)
				return false;
		}
		return true;
	}

	public static void removeAccessToken(Context ctx, String hostPackage,
			String accessToken) {
		SharedPreferences prefs = getPrefsForHost(ctx, hostPackage);

		if (prefs.getString(PREF_KEY_TOKEN, "").equals(accessToken))
		{
			Editor edit = prefs.edit();
			edit.clear();
			edit.commit();

		}
	 
	}
}
