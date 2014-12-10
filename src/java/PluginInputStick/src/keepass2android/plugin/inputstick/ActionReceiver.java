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
			String layoutPrimary = prefs.getString("kbd_layout", "en-US");
			String layoutSecondary = prefs.getString("secondary_kbd_layout", "en-US");
			Bundle b;
			String displayText;			
			for (String field: oe.getEntryFields().keySet()) {
				
				displayText = ctx.getString(R.string.action_input_stick);
				if (showSecondary) {
					displayText += " (" + layoutPrimary + ")";
				} 				
				b = new Bundle();
				b.putString(Const.EXTRA_LAYOUT, layoutPrimary);
				oe.addEntryFieldAction("keepass2android.plugin.inputstick.type", Strings.PREFIX_STRING + field, displayText, IC, b);
								
				if (showSecondary) {					
					displayText = oe.getContext().getString(R.string.action_input_stick) + " (" + layoutSecondary + ")";
					b = new Bundle();
					b.putString(Const.EXTRA_LAYOUT, layoutSecondary);
					oe.addEntryFieldAction("keepass2android.plugin.inputstick.typesecondary", Strings.PREFIX_STRING + field, displayText, IC, b);		
				}
			}
			
			if (prefs.getBoolean("show_tab_enter", true)) {
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, "\t");
				oe.addEntryAction(ctx.getString(R.string.action_type_tab), IC, b);
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, "\n");
				oe.addEntryAction(ctx.getString(R.string.action_type_enter), IC, b);
			}
			
			if (prefs.getBoolean("show_user_pass", true)) {
				displayText = ctx.getString(R.string.action_type_user_tab_pass);
				if (showSecondary) {
					displayText += " (" + layoutPrimary + ")";
				} 				
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, ACTION_USER_PASS);
				b.putString(Const.EXTRA_LAYOUT, layoutPrimary);
				oe.addEntryAction(displayText, IC, b);
				
				if (showSecondary) {	
					displayText = oe.getContext().getString(R.string.action_type_user_tab_pass) + " (" + layoutSecondary + ")";
					b = new Bundle();
					b.putString(Const.EXTRA_TEXT, ACTION_USER_PASS);
					b.putString(Const.EXTRA_LAYOUT, layoutSecondary);
					oe.addEntryAction(displayText, IC, b);	
				}
			}
			
			if (prefs.getBoolean("show_user_pass_enter", false)) {
				displayText = ctx.getString(R.string.action_type_user_tab_pass_enter);
				if (showSecondary) {
					displayText += " (" + layoutPrimary + ")";
				} 				
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, ACTION_USER_PASS_ENTER);
				b.putString(Const.EXTRA_LAYOUT, layoutPrimary);
				oe.addEntryAction(displayText, IC, b);
				
				if (showSecondary) {	
					displayText = oe.getContext().getString(R.string.action_type_user_tab_pass_enter) + " (" + layoutSecondary + ")";
					b = new Bundle();
					b.putString(Const.EXTRA_TEXT, ACTION_USER_PASS_ENTER);
					b.putString(Const.EXTRA_LAYOUT, layoutSecondary);
					oe.addEntryAction(displayText, IC, b);	
				}
			}			
			
			if (prefs.getBoolean("show_masked", true)) {
				displayText = ctx.getString(R.string.action_masked_password);
				if (showSecondary) {
					displayText += " (" + layoutPrimary + ")";
				} 				
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, ACTION_MASKED_PASSWORD);
				b.putString(Const.EXTRA_LAYOUT, layoutPrimary);
				oe.addEntryAction(displayText, IC, b);
				
				if (showSecondary) {	
					displayText = oe.getContext().getString(R.string.action_masked_password) + " (" + layoutSecondary + ")";
					b = new Bundle();
					b.putString(Const.EXTRA_TEXT, ACTION_MASKED_PASSWORD);
					b.putString(Const.EXTRA_LAYOUT, layoutSecondary);
					oe.addEntryAction(displayText, IC, b);
				}
			}	
			
			if (prefs.getBoolean("show_settings", true)) {
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, ACTION_SETTINGS);
				oe.addEntryAction(ctx.getString(R.string.action_open_settings), IC, b);
			}
			
			if (prefs.getBoolean("show_mac_setup", true)) {
				b = new Bundle();
				b.putString(Const.EXTRA_TEXT, ACTION_MAC_SETUP);
				oe.addEntryAction(ctx.getString(R.string.action_open_mac_setup), IC, b);
			}				
		} catch (PluginAccessException e) {
			e.printStackTrace();
		}
				
		//typeText(oe.getContext(), "en-US");
		if (prefs.getBoolean("autoconnect", true)) {
			typeText(ctx, "", "en-US");
		} else {
			if ((lastTypingTime != 0) && ((System.currentTimeMillis() - AUTOCONNECT_TIMEOUT) < lastTypingTime)) {
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
	
	@Override 
	protected void closeEntryView(CloseEntryViewAction closeEntryView) {
		Intent serviceIntent = new Intent(closeEntryView.getContext(), InputStickService.class);
		serviceIntent.setAction(InputStickService.DISCONNECT);
		closeEntryView.getContext().startService(serviceIntent);
		//System.out.println("CLOSE ENTRY");
	};
	
	@Override
	protected void actionSelected(ActionSelectedAction actionSelected) {
		Context ctx = actionSelected.getContext();
		String layoutName = actionSelected.getActionData().getString(Const.EXTRA_LAYOUT, "en-US");
		
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
			typeText(ctx, text, layoutName);
			
			if ((enterAfterURL) && ("URL".equals(fieldKey))) {
				typeText(ctx, "\n", layoutName);	
			}			
		}
	}

	
	private void typeText(Context ctx, String text, String layout) {
		lastTypingTime = System.currentTimeMillis();
		
		Intent serviceIntent = new Intent(ctx, InputStickService.class);
		serviceIntent.setAction(InputStickService.TYPE);
		Bundle b = new Bundle();
		b.putString(Const.EXTRA_TEXT, text);
		b.putString(Const.EXTRA_LAYOUT, layout);
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
			Bundle b;
			String displayText;												
			
			displayText = ctx.getString(R.string.action_input_stick);
			if (showSecondary) {
				displayText += " (" + layoutPrimary + ")";
			} 				
			b = new Bundle();
			b.putString(Const.EXTRA_LAYOUT, layoutPrimary);
			eom.addEntryFieldAction("keepass2android.plugin.inputstick.type", eom.getModifiedFieldId(), displayText, IC, null);
							
			if (showSecondary) {					
				displayText = ctx.getString(R.string.action_input_stick) + " (" + layoutSecondary + ")";
				b = new Bundle();
				b.putString(Const.EXTRA_LAYOUT, layoutSecondary);
				eom.addEntryFieldAction("keepass2android.plugin.inputstick.typesecondary", eom.getModifiedFieldId(), displayText, IC, null);
			}						
		} catch (PluginAccessException e) {
			e.printStackTrace();
		}
	}

}
