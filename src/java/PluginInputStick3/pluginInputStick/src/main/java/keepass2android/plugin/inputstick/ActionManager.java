package keepass2android.plugin.inputstick;

import java.util.HashMap;

import keepass2android.pluginsdk.KeepassDefs;
import android.annotation.SuppressLint;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.preference.PreferenceManager;
import android.widget.Toast;

import com.inputstick.api.hid.HIDKeycodes;

public class ActionManager {
	
	
	private static UserPreferences userPrefs;
	private static SharedPreferences prefs;
	private static Context ctx;
	
	private static HashMap<String, String> entryFields;
	private static long lastActivityTime;
	
	public static void init(Context ctx, String entryId, HashMap<String, String> entryFields) {
		ActionManager.ctx = ctx;
		ActionManager.entryFields = entryFields;
		prefs = PreferenceManager.getDefaultSharedPreferences(ctx);
		userPrefs = new UserPreferences(prefs, entryId);
		lastActivityTime = 0;
	}
	
	public static void reloadPreferences() {
		if (userPrefs != null) {
			userPrefs.reload();
		}
	}
	
	public static UserPreferences getUserPrefs() {
		return userPrefs;
	}
	
	
	public static String getActionStringForPrimaryLayout(int id, boolean allowInputStickText) {
		return getActionString(id, userPrefs.getLayoutPrimaryDisplayCode(), allowInputStickText);
	}
	public static String getActionStringForSecondaryLayout(int id, boolean allowInputStickText) {
		return getActionString(id, userPrefs.getLayoutSecondaryDisplayCode(), allowInputStickText);
	}
	public static String getActionString(int id, boolean allowInputStickText) {
		return getActionString(id, null, allowInputStickText);
	}	
	
	private static String getActionString(int id, String layoutCode, boolean allowInputStickText) {
		String s = ctx.getString(id);
		if (layoutCode != null) {
			s += " (" + layoutCode + ")";
		}
		if ((allowInputStickText) && (userPrefs.isDisplayInputStickText())) {
			s += " (IS)";
		}
		return s;
	}
	
	public static long getLastActivityTime() {
		return lastActivityTime;
	}
	
	
	
	public static void startSettingsActivity() {
		Intent i = new Intent(ctx.getApplicationContext(), SettingsActivity.class);
		i.putExtra(Const.EXTRA_LAUNCHED_FROM_KP2A, true);				
		i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
		ctx.getApplicationContext().startActivity(i);			
	}
	public static void startShowAllActivity() {
		Intent i = new Intent(ctx.getApplicationContext(), AllActionsActivity.class);		
		i.putExtra(Const.EXTRA_MAX_TIME, System.currentTimeMillis() + Const.ACTIVITY_LOCK_TIMEOUT_MS);
		i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
		ctx.getApplicationContext().startActivity(i);		
	}
	public static void startMacSetupActivity() {
		connect();
		Intent i = new Intent(ctx.getApplicationContext(), MacSetupActivity.class);
		i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
		ctx.getApplicationContext().startActivity(i);			
	}
	
	

	
	
	
	public static void connect() {
		Intent serviceIntent = new Intent(ctx, InputStickService.class);
		serviceIntent.setAction(Const.SERVICE_CONNECT);
		ctx.startService(serviceIntent);
	}
	
	public static void disconnect() {
		Intent serviceIntent = new Intent(ctx, InputStickService.class);
		serviceIntent.setAction(Const.SERVICE_DISCONNECT);
		ctx.startService(serviceIntent);
	}
	
	
	public static void queueText(String text, String layout, int reportMultiplier) {
		Bundle b = new Bundle();
		b.putString(Const.EXTRA_ACTION, Const.ACTION_TYPE);		
		b.putString(Const.EXTRA_TEXT, text);
		b.putString(Const.EXTRA_LAYOUT, layout);
		b.putInt(Const.EXTRA_REPORT_MULTIPLIER, reportMultiplier);		
		sendToService(b);	
	}

	public static void queueText(String text, String layout) {
		queueText(text, layout, userPrefs.getReportMultiplier());
	}
	
	public static void queueDelay(int value) {	
		Bundle b = new Bundle();
		b.putString(Const.EXTRA_ACTION, Const.ACTION_DELAY);		
		b.putInt(Const.EXTRA_DELAY, value);
		sendToService(b);
	}
	
	public static void queueKey(byte modifier, byte key) {
		Bundle b = new Bundle();
		b.putString(Const.EXTRA_ACTION, Const.ACTION_KEY_PRESS);		
		b.putByte(Const.EXTRA_MODIFIER, modifier);
		b.putByte(Const.EXTRA_KEY, key);
		b.putInt(Const.EXTRA_REPORT_MULTIPLIER, userPrefs.getReportMultiplier());	
		sendToService(b);		
	}
	
	public static void sendToService(Bundle b) {
		lastActivityTime = System.currentTimeMillis();
		Intent serviceIntent = new Intent(ctx, InputStickService.class);
		serviceIntent.setAction(Const.SERVICE_EXEC);
		serviceIntent.putExtras(b);
		ctx.startService(serviceIntent);
	}

	
	
	
	
	public static void typeUsernameAndPassword(String layoutName, boolean addEnter) {
		queueText(entryFields.get(KeepassDefs.UserNameField), layoutName);
		queueDelay(5);
		queueKey(HIDKeycodes.NONE, HIDKeycodes.KEY_TAB);
		queueDelay(5);
		queueText(entryFields.get(KeepassDefs.PasswordField), layoutName);
		if (addEnter) {
			queueDelay(5);
			queueKey(HIDKeycodes.NONE, HIDKeycodes.KEY_ENTER);
		}
	}
	
	
	
	public static void runMacro(String layoutName) {		
		String macro = userPrefs.getMacro();				
		if ((macro != null) && (macro.length() > 0)) {
			boolean runInBackground = macro.startsWith(MacroHelper.MACRO_BACKGROUND_EXEC_STRING);
			String actions[] = macro.split("%");
			connect();
			if (runInBackground) {	
				for (String s : actions) {
					runMacroAction(layoutName, s);
				}								
			} else {
				Intent i = new Intent(ctx.getApplicationContext(), MacroExecuteActivity.class);
				i.putExtra(Const.EXTRA_MAX_TIME, System.currentTimeMillis() + Const.ACTIVITY_LOCK_TIMEOUT_MS);
				i.putExtra(Const.EXTRA_MACRO_ACTIONS, actions);
				i.putExtra(Const.EXTRA_LAYOUT, layoutName);
				i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
				ctx.getApplicationContext().startActivity(i);	
			}			
		} else {
			addEditMacro(true);
		}
	}
	
	
	@SuppressLint("DefaultLocale")
	public static void runMacroAction(String layoutName, String s) {
		String tmp, param;
		if ((s != null) && (s.length() > 0)) {
			tmp = s.toLowerCase();
			//no parameter
			if (tmp.startsWith(MacroHelper.MACRO_ACTION_PASSWORD)) {
				queueText(entryFields.get(KeepassDefs.PasswordField), layoutName);
			} else if (tmp.startsWith(MacroHelper.MACRO_ACTION_USER_NAME)) {
				queueText(entryFields.get(KeepassDefs.UserNameField), layoutName);
			} else if (tmp.startsWith(MacroHelper.MACRO_ACTION_URL)) {
				queueText(entryFields.get(KeepassDefs.UrlField), layoutName);
			} else if (tmp.startsWith(MacroHelper.MACRO_ACTION_PASSWORD_MASKED)) {
				openMaskedPassword(layoutName, false);
			} else if (tmp.startsWith(MacroHelper.MACRO_ACTION_CLIPBOARD)) {
				clipboardTyping(layoutName);
			} else {
				//get parameter
				param = MacroHelper.getParam(s);
				if ((param != null) && (param.length() > 0)) { 					
					if (tmp.startsWith(MacroHelper.MACRO_ACTION_TYPE)) {						
						queueText(param, layoutName);
					}
					if (tmp.startsWith(MacroHelper.MACRO_ACTION_DELAY)) {
						queueDelay(MacroHelper.getDelay(param));
					}
					if (tmp.startsWith(MacroHelper.MACRO_ACTION_KEY)) {
						queueKey(MacroHelper.getModifiers(param), MacroHelper.getKey(param));
					}		
				}
			}
		}
	}
	
	
	
	public static void clipboardTyping(String layoutName) {
		connect(); //in case not connected already
		if (userPrefs.isClipboardLaunchAuthenticator()) {
			Intent launchIntent = ctx.getPackageManager().getLaunchIntentForPackage("com.google.android.apps.authenticator2");
			if (launchIntent != null) {
				ctx.getApplicationContext().startActivity(launchIntent);
			} else {
				Toast.makeText(ctx, R.string.text_authenticator_app_not_found, Toast.LENGTH_LONG).show();
			}						
		}
		
		Intent i = new Intent(ctx, ClipboardService.class);
		i.putExtra(Const.EXTRA_LAYOUT, layoutName);
		ctx.startService(i);
	}
	
	
	public static void addEditMacro(boolean showEmptyMacroError) {
		Intent i = new Intent(ctx.getApplicationContext(), MacroActivity.class);
		i.putExtra(Const.EXTRA_MACRO, userPrefs.getMacro());
		i.putExtra(Const.EXTRA_ENTRY_ID, userPrefs.getEntryId());		
		if (showEmptyMacroError) {
			i.putExtra(Const.EXTRA_MACRO_RUN_BUT_EMPTY, showEmptyMacroError);		
		}
		i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
		ctx.getApplicationContext().startActivity(i);			
	}
	
	public static void openMaskedPassword(String layoutName, boolean addClearFlags) {
		connect(); //in case not connected already
		Intent i = new Intent(ctx.getApplicationContext(), MaskedPasswordActivity.class);
		i.putExtra(Const.EXTRA_TEXT, entryFields.get(KeepassDefs.PasswordField));
		i.putExtra(Const.EXTRA_LAYOUT, layoutName);
		i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
		if (addClearFlags) {
			i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP);
		}
		ctx.getApplicationContext().startActivity(i);		
	}		
	
	
}
