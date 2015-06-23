package keepass2android.plugin.inputstick;

import keepass2android.pluginsdk.KeepassDefs;
import keepass2android.pluginsdk.PluginAccessException;
import keepass2android.pluginsdk.Strings;
import sheetrock.panda.changelog.ChangeLog;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.preference.PreferenceManager;

public class ActionReceiver extends keepass2android.pluginsdk.PluginActionBroadcastReceiver {

	private static final String ACTION_MASKED_PASSWORD = "masked_password";
	private static final String ACTION_SETTINGS = "settings";
	private static final String ACTION_USER_PASS = "user_pass";
	private static final String ACTION_USER_PASS_ENTER = "user_pass_enter";
	private static final String ACTION_MAC_SETUP = "mac_setup";			
	
	private static final int IC = R.drawable.ic_launcher;
	
	private static long lastTypingTime = 0;
	private static final long AUTOCONNECT_TIMEOUT = 600000; //10min, 

	private static boolean enterAfterURL;
	
	@Override
	protected void openEntry(OpenEntryAction oe) {
		Context ctx = oe.getContext();
		SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(ctx);
		try {	
			enterAfterURL = prefs.getBoolean("enter_after_url", true);
			boolean showSecondary = prefs.getBoolean("show_secondary", false);
			String layoutPrimary = prefs.getString("kbd_layout", "en-US"); //layout code used when typing
			String layoutSecondary = prefs.getString("secondary_kbd_layout", "en-US");
			
			String layoutPrimaryDisplayCode = null;  //only for displaying layout code
			String layoutSecondaryDisplayCode = null;
			if (showSecondary) {
				//display layout code only if secondary layout is enabled
				layoutPrimaryDisplayCode = layoutPrimary;
				layoutSecondaryDisplayCode = layoutSecondary;
			}
			Bundle b;
			String displayText;	
			
			boolean showType = prefs.getBoolean("show_field_type", true);
			boolean showTypeSlow = prefs.getBoolean("show_field_type_slow", false);
			boolean showTypeSec = prefs.getBoolean("show_field_type_secondary", false);
			boolean showTypeSlowSec = prefs.getBoolean("show_field_type_slow_secondary", false);
			for (String field: oe.getEntryFields().keySet()) {			
				if (showType) {
					displayText = getActionString(ctx, R.string.action_type, layoutPrimaryDisplayCode, true);
					b = new Bundle();
					b.putString(Const.EXTRA_LAYOUT, layoutPrimary);
					oe.addEntryFieldAction("keepass2android.plugin.inputstick.type", Strings.PREFIX_STRING + field, displayText, IC, b);
				}
				if (showTypeSlow) {
					displayText = getActionString(ctx, R.string.action_type_slow, layoutPrimaryDisplayCode, true);
					b = new Bundle();
					b.putString(Const.EXTRA_LAYOUT, layoutPrimary);
					b.putString(Const.EXTRA_PARAMS, Const.PARAM_SLOW_TYPING);
					oe.addEntryFieldAction("keepass2android.plugin.inputstick.typeslow", Strings.PREFIX_STRING + field, displayText, IC, b);
				}
						
				if (showSecondary) {
					if (showTypeSec) {					
						displayText = getActionString(ctx, R.string.action_type, layoutSecondaryDisplayCode, true);
						b = new Bundle();
						b.putString(Const.EXTRA_LAYOUT, layoutSecondary);
						oe.addEntryFieldAction("keepass2android.plugin.inputstick.typesecondary", Strings.PREFIX_STRING + field, displayText, IC, b);
					}
					if (showTypeSlowSec) {					
						displayText = getActionString(ctx, R.string.action_type_slow, layoutSecondaryDisplayCode, true);
						b = new Bundle();
						b.putString(Const.EXTRA_LAYOUT, layoutSecondary);
						b.putString(Const.EXTRA_PARAMS, Const.PARAM_SLOW_TYPING);
						oe.addEntryFieldAction("keepass2android.plugin.inputstick.typeslowsecondary", Strings.PREFIX_STRING + field, displayText, IC, b);
					}
				}
			}
			
			
			
			
			//GENERAL
			if (prefs.getBoolean("show_settings", true)) {
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, ACTION_SETTINGS);
				oe.addEntryAction(getActionString(ctx, R.string.action_open_settings, null, true), IC, b);
			}
			
			if (prefs.getBoolean("show_mac_setup", true)) {
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, ACTION_MAC_SETUP);
				oe.addEntryAction(getActionString(ctx, R.string.action_open_mac_setup, null, true), IC, b);
			}			
			if (prefs.getBoolean("show_tab_enter", true)) {
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, "\t");
				oe.addEntryAction(getActionString(ctx, R.string.action_type_tab, null, true), IC, b);
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, "\n");
				oe.addEntryAction(getActionString(ctx, R.string.action_type_enter, null, true), IC, b);
			}

			//ENTRY SCOPE
			if (prefs.getBoolean("show_user_pass", true)) {
				displayText = getActionString(ctx, R.string.action_type_user_tab_pass, layoutPrimaryDisplayCode, true);
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, ACTION_USER_PASS);
				b.putString(Const.EXTRA_LAYOUT, layoutPrimary);
				oe.addEntryAction(displayText, IC, b);
			}			
			if (prefs.getBoolean("show_user_pass_enter", true)) {
				displayText = getActionString(ctx, R.string.action_type_user_tab_pass_enter, layoutPrimaryDisplayCode, true);
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, ACTION_USER_PASS_ENTER);
				b.putString(Const.EXTRA_LAYOUT, layoutPrimary);
				oe.addEntryAction(displayText, IC, b);
			}				
			if (prefs.getBoolean("show_masked", true)) {
				displayText = getActionString(ctx, R.string.action_masked_password, layoutPrimaryDisplayCode, true);
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, ACTION_MASKED_PASSWORD);
				b.putString(Const.EXTRA_LAYOUT, layoutPrimary);
				oe.addEntryAction(displayText, IC, b);
			}	
			
			if (showSecondary) {
				if (prefs.getBoolean("show_user_pass_secondary", true)) {
					displayText = getActionString(ctx, R.string.action_type_user_tab_pass, layoutSecondaryDisplayCode, true);
					b = new Bundle();
					b.putString(Const.EXTRA_TEXT, ACTION_USER_PASS);
					b.putString(Const.EXTRA_LAYOUT, layoutSecondary);
					oe.addEntryAction(displayText, IC, b);					
				}
				if (prefs.getBoolean("show_user_pass_enter_secondary", false)) {
					displayText = getActionString(ctx, R.string.action_type_user_tab_pass_enter, layoutSecondaryDisplayCode, true);
					b = new Bundle();
					b.putString(Const.EXTRA_TEXT, ACTION_USER_PASS_ENTER);
					b.putString(Const.EXTRA_LAYOUT, layoutSecondary);
					oe.addEntryAction(displayText, IC, b);	
				}				
				if (prefs.getBoolean("show_masked_secondary", false)) {
					displayText = getActionString(ctx, R.string.action_masked_password, layoutSecondaryDisplayCode, true);
					b = new Bundle();
					b.putString(Const.EXTRA_TEXT, ACTION_MASKED_PASSWORD);
					b.putString(Const.EXTRA_LAYOUT, layoutSecondary);
					oe.addEntryAction(displayText, IC, b);				
				}
			}			
		} catch (PluginAccessException e) {
			e.printStackTrace();
		}
				
		if (prefs.getBoolean("autoconnect", true)) {
			typeText(ctx, "", "en-US");
		} else {
			int autoconnectTimeout = (int)AUTOCONNECT_TIMEOUT;
			try {
				autoconnectTimeout = Integer.parseInt(prefs.getString("autoconnect_timeout", "600000"));
			} catch (Exception e) {	
				autoconnectTimeout = (int)AUTOCONNECT_TIMEOUT;
			}	
			
			if ((lastTypingTime != 0) && ((System.currentTimeMillis() - autoconnectTimeout) < lastTypingTime)) {
				//System.out.println("AUTOCONNECT (NO TIMEOUT)");
				typeText(ctx, "", "en-US");
			} 
		}	

		ChangeLog cl = new ChangeLog(ctx.getApplicationContext());
	    if (cl.firstRun()) {
			Intent i = new Intent(ctx.getApplicationContext(), SettingsActivity.class);
			i.putExtra(Const.EXTRA_CHANGELOG, true);
			i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
			ctx.getApplicationContext().startActivity(i);	
	    }
	}
	
	private String getActionString(Context ctx, int id, String layoutCode, boolean addInputStickInfo) {
		String s = ctx.getString(id);
		if (layoutCode != null) {
			s += " (" + layoutCode + ")";
		}
		if (addInputStickInfo) {
			s += " (InputStick)";
		}
		return s;
	}
	
	@Override 
	protected void closeEntryView(CloseEntryViewAction closeEntryView) {
		SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(closeEntryView.getContext());
		boolean doNotDisconnect = prefs.getBoolean("do_not_disconnect", false);
		if ( !doNotDisconnect) {		
			Intent serviceIntent = new Intent(closeEntryView.getContext(), InputStickService.class);
			serviceIntent.setAction(InputStickService.DISCONNECT);
			closeEntryView.getContext().startService(serviceIntent);
		}	
	};
	
	@Override
	protected void actionSelected(ActionSelectedAction actionSelected) {
		Context ctx = actionSelected.getContext();
		String layoutName = actionSelected.getActionData().getString(Const.EXTRA_LAYOUT, "en-US");
		String params = actionSelected.getActionData().getString(Const.EXTRA_PARAMS, null);
		
		if (actionSelected.isEntryAction()) {
			String text = actionSelected.getActionData().getString(Const.EXTRA_TEXT);
						
			if (ACTION_MASKED_PASSWORD.equals(text)) {
				typeText(ctx, "", "en-US"); //will connect if not already connected				
				Intent i = new Intent(ctx.getApplicationContext(), MaskedPasswordActivity.class);
				i.putExtra(Const.EXTRA_TEXT, actionSelected.getEntryFields().get(KeepassDefs.PasswordField));
				i.putExtra(Const.EXTRA_LAYOUT, layoutName);
				i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
				ctx.getApplicationContext().startActivity(i);				
			} else if (ACTION_SETTINGS.equals(text)) {
				Intent i = new Intent(ctx.getApplicationContext(), SettingsActivity.class);
				i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
				ctx.getApplicationContext().startActivity(i);				
			} else if (ACTION_USER_PASS.equals(text)) {
				typeText(ctx, actionSelected.getEntryFields().get(KeepassDefs.UserNameField), layoutName);
				typeText(ctx, "\t", layoutName);
				typeText(ctx, actionSelected.getEntryFields().get(KeepassDefs.PasswordField), layoutName);
			} else if (ACTION_USER_PASS_ENTER.equals(text)) {
				typeText(ctx, actionSelected.getEntryFields().get(KeepassDefs.UserNameField), layoutName);
				typeText(ctx, "\t", layoutName);
				typeText(ctx, actionSelected.getEntryFields().get(KeepassDefs.PasswordField), layoutName);
				typeText(ctx, "\n", layoutName);				
			} else if (ACTION_MAC_SETUP.equals(text)) {
				typeText(ctx, "", "en-US"); //will connect if not already connected
				Intent i = new Intent(ctx.getApplicationContext(), MacSetupActivity.class);
				i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK | Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
				ctx.getApplicationContext().startActivity(i);					
			} else {
				typeText(ctx, text, layoutName);
			}			
		} else {
			String fieldKey = actionSelected.getFieldId().substring(Strings.PREFIX_STRING.length());
			String text = actionSelected.getEntryFields().get(fieldKey);
			typeText(ctx, text, layoutName, params);	

			if ((enterAfterURL) && ("URL".equals(fieldKey))) {
				typeText(ctx, "\n", layoutName, params);	
			}			
		}
	}

	private void typeText(Context ctx, String text, String layout) {
		typeText(ctx, text, layout, null);
	}
	
	private void typeText(Context ctx, String text, String layout, String params) {
		if ( !("".equals(text))) { //only if text is actually being typed			
			lastTypingTime = System.currentTimeMillis();
		}
		Intent serviceIntent = new Intent(ctx, InputStickService.class);
		serviceIntent.setAction(InputStickService.TYPE);
		Bundle b = new Bundle();
		b.putString(Const.EXTRA_TEXT, text);
		b.putString(Const.EXTRA_LAYOUT, layout);
		b.putString(Const.EXTRA_PARAMS, params);
		serviceIntent.putExtras(b);
		ctx.startService(serviceIntent);		
	}
		

	@Override
	protected void entryOutputModified(EntryOutputModifiedAction eom) {	
		Context ctx = eom.getContext();
		SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(ctx);
		try {
			boolean showSecondary = prefs.getBoolean("show_secondary", false);
			String layoutPrimary = prefs.getString("kbd_layout", "en-US");
			String layoutSecondary = prefs.getString("secondary_kbd_layout", "en-US");
			
			String layoutPrimaryDisplayCode = null;  //only for displaying layout code
			String layoutSecondaryDisplayCode = null;
			if (showSecondary) {
				//display layout code only if secondary layout is enabled
				layoutPrimaryDisplayCode = layoutPrimary;
				layoutSecondaryDisplayCode = layoutSecondary;
			}
			
			Bundle b;
			String displayText;		
			
			if (prefs.getBoolean("show_field_type", true)) {
				displayText = getActionString(ctx, R.string.action_type, layoutPrimaryDisplayCode, true);				
				b = new Bundle();
				b.putString(Const.EXTRA_LAYOUT, layoutPrimary);			
				eom.addEntryFieldAction("keepass2android.plugin.inputstick.type", eom.getModifiedFieldId(), displayText, IC, b);
			}
			
			if (prefs.getBoolean("show_field_type_slow", false)) {
				displayText = getActionString(ctx, R.string.action_type_slow, layoutPrimaryDisplayCode, true);				
				b = new Bundle();
				b.putString(Const.EXTRA_LAYOUT, layoutPrimary);
				b.putString(Const.EXTRA_PARAMS, Const.PARAM_SLOW_TYPING);
				eom.addEntryFieldAction("keepass2android.plugin.inputstick.typeslow", eom.getModifiedFieldId(), displayText, IC, b);
			}
			
			if (showSecondary) {
				if (prefs.getBoolean("show_field_type_secondary", false)) {
					displayText = getActionString(ctx, R.string.action_type, layoutSecondaryDisplayCode, true);				
					b = new Bundle();
					b.putString(Const.EXTRA_LAYOUT, layoutSecondary);
					eom.addEntryFieldAction("keepass2android.plugin.inputstick.typesecondary", eom.getModifiedFieldId(), displayText, IC, b);
				}
				if (prefs.getBoolean("show_field_type_slow_secondary", false)) {
					displayText = getActionString(ctx, R.string.action_type, layoutSecondaryDisplayCode, true);				
					b = new Bundle();
					b.putString(Const.EXTRA_LAYOUT, layoutSecondary);
					b.putString(Const.EXTRA_PARAMS, Const.PARAM_SLOW_TYPING);
					eom.addEntryFieldAction("keepass2android.plugin.inputstick.typeslowsecondary", eom.getModifiedFieldId(), displayText, IC, b);				
				}		
			}
		} catch (PluginAccessException e) {
			e.printStackTrace();
		}
	}

}
