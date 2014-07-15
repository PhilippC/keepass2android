package keepass2android.pluginsdk;

import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.Set;

import org.json.JSONArray;
import org.json.JSONException;

import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.SharedPreferences.Editor;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.preference.PreferenceManager;
import android.text.TextUtils;
import android.util.Log;
import android.view.View;
import android.widget.PopupMenu;


public class AccessManager 
{
	
	private static final String _tag = "Kp2aPluginSDK";
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
	    if (TextUtils.isEmpty(s) == false) {
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
		  
		 Editor edit = prefs.edit();
		 edit.putString(PREF_KEY_TOKEN, accessToken);
		 String scopesString = stringArrayToString(scopes);
		 edit.putString(PREF_KEY_SCOPE, scopesString);
		 edit.commit();
		 Log.d(_tag, "stored access token " + accessToken.substring(0, 4)+"... for "+scopes.size()+" scopes ("+scopesString+").");
		 
		 SharedPreferences hostPrefs = ctx.getSharedPreferences("KP2A.PluginAccess.hosts", Context.MODE_PRIVATE);
		 if (!hostPrefs.contains(hostPackage))
		 {
			 hostPrefs.edit().putString(hostPackage, "").commit();
		 }
		 
		 
	}
	
	public static void preparePopup(Object popupMenu)
	{
		try
		{
			Field[] fields = popupMenu.getClass().getDeclaredFields();
			for (Field field : fields) {
				if ("mPopup".equals(field.getName())) {
					field.setAccessible(true);
					Object menuPopupHelper = field.get(popupMenu);
					Class<?> classPopupHelper = Class.forName(menuPopupHelper
							.getClass().getName());
					Method setForceIcons = classPopupHelper.getMethod(
							"setForceShowIcon", boolean.class);
					setForceIcons.invoke(menuPopupHelper, true);
					break;
				}
			}
			
		}
		catch (Exception e)
		{
			e.printStackTrace();
		}
	}

	private static SharedPreferences getPrefsForHost(Context ctx,
			String hostPackage) {
		SharedPreferences prefs = ctx.getSharedPreferences("KP2A.PluginAccess."+hostPackage, Context.MODE_PRIVATE);
		return prefs;
	}

	public static String tryGetAccessToken(Context ctx, String hostPackage, ArrayList<String> scopes) {
		
		if (TextUtils.isEmpty(hostPackage))
		{
			Log.d(_tag, "hostPackage is empty!");
			return null;
		}
		Log.d(_tag, "trying to find prefs for "+hostPackage);
		SharedPreferences prefs = getPrefsForHost(ctx, hostPackage);
		String scopesString = prefs.getString(PREF_KEY_SCOPE, "");
		Log.d(_tag, "available scopes: "+ scopesString);
		ArrayList<String> currentScope = stringToStringArray(scopesString);
		if (isSubset(scopes, currentScope))
		{
			return prefs.getString(PREF_KEY_TOKEN, null);
		}
		else
		{
			Log.d(_tag, "looks like scope changed. Access token invalid.");
			return null;
		}
	}

	public static boolean isSubset(ArrayList<String> requiredScopes,
			ArrayList<String> availableScopes) {
		for (String r: requiredScopes){
			if (availableScopes.indexOf(r)<0)
			{
				Log.d(_tag, "Scope "+r+" not available. "+availableScopes.size());
				return false;
			}
		}
		return true;
	}

	public static void removeAccessToken(Context ctx, String hostPackage,
			String accessToken) {
		SharedPreferences prefs = getPrefsForHost(ctx, hostPackage);

		Log.d(_tag, "removing AccessToken.");
		if (prefs.getString(PREF_KEY_TOKEN, "").equals(accessToken))
		{
			Editor edit = prefs.edit();
			edit.clear();
			edit.commit();

		}
		
		SharedPreferences hostPrefs = ctx.getSharedPreferences("KP2A.PluginAccess.hosts", Context.MODE_PRIVATE);
		if (hostPrefs.contains(hostPackage))
		{
			hostPrefs.edit().remove(hostPackage).commit();
		}
	 
	}
	
	public static Set<String> getAllHostPackages(Context ctx)
	{
		SharedPreferences prefs = ctx.getSharedPreferences("KP2A.PluginAccess.hosts", Context.MODE_PRIVATE);
		Set<String> result = new HashSet<String>();
		for (String host: prefs.getAll().keySet())
		{
			try
			{
				PackageInfo info = ctx.getPackageManager().getPackageInfo(host, PackageManager.GET_META_DATA);
				//if we get here, the package is still there
				result.add(host);
			}
			catch (PackageManager.NameNotFoundException e)
			{
				//host gone. ignore.
			}
		}
		return result;
		
	}
	
	

	/**
	 * Returns a valid access token or throws PluginAccessException
	 */
	public static String getAccessToken(Context context, String hostPackage,
			ArrayList<String> scopes) throws PluginAccessException {
		String accessToken = tryGetAccessToken(context, hostPackage, scopes);
		if (accessToken == null)
			throw new PluginAccessException(hostPackage, scopes);
		return accessToken;
	}
}
