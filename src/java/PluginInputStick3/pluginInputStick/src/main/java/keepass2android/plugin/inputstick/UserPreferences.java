package keepass2android.plugin.inputstick;

import android.content.SharedPreferences;

public class UserPreferences {		
	
	private SharedPreferences prefs;

	private String layoutPrimary;
	private String layoutSecondary;
	private String layoutPrimaryDisplayCode;
	private String layoutSecondaryDisplayCode;

	private boolean showSecondary;
	private boolean enterAfterURL;
	private boolean autoConnect;
	private int autoConnectTimeout;
	private boolean disconnectOnClose;
	private int reportMultiplier;
	
	private String macro;
	private String entryId;
	
	//clipboard	
	private boolean clipboardLaunchAuthenticator;
	private boolean clipboardAutoDisable;
	private boolean clipboardAutoEnter;

	private boolean displayInputstickText;
	//general
	private boolean showSettings;
	private boolean showConnectionOptions;
	private boolean showMacSetup;
	private boolean showTabEnter;
	private boolean showMacroAddEdit;	
	//entry
	private boolean showUserPass;
	private boolean showUserPassEnter;
	private boolean showMasked;
	private boolean showMacro;
	private boolean showClipboard;
	private boolean showUserPassSec;
	private boolean showUserPassEnterSec;
	private boolean showMaskedSec;
	private boolean showMacroSec;
	private boolean showClipboardSec;
	//item
	private boolean showType;
	private boolean showTypeSlow;
	private boolean showTypeSec;
	private boolean showTypeSlowSec;	
	

	public UserPreferences(SharedPreferences prefs, String id) {
		this.prefs = prefs;
		entryId = id;
		reload();
	}
	
	public void reload() {
		getMacro();		 //is always reloaded

		layoutPrimary = prefs.getString("kbd_layout", "en-US");
		layoutSecondary = prefs.getString("secondary_kbd_layout", "en-US");
		showSecondary = prefs.getBoolean("show_secondary", false);	
		
		if (showSecondary) {
			// display layout codes only if secondary layout is enabled
			layoutPrimaryDisplayCode = layoutPrimary;
			layoutSecondaryDisplayCode = layoutSecondary;
		}
		enterAfterURL = prefs.getBoolean("enter_after_url", false);
		
		reportMultiplier = 1;
		try {
			reportMultiplier = Integer.parseInt(prefs.getString("typing_speed", "1"));
		} catch (Exception e) {	
			reportMultiplier = 1;
		}
		
		disconnectOnClose = !prefs.getBoolean("do_not_disconnect", false);				
		autoConnectTimeout = Const.DEFAULT_AUTOCONNECT_TIMEOUT_MS;
		try {
			autoConnectTimeout = Integer.parseInt(prefs.getString("autoconnect_timeout", "600000"));
		} catch (Exception e) {
			autoConnectTimeout = Const.DEFAULT_AUTOCONNECT_TIMEOUT_MS;
		}

		displayInputstickText = prefs.getBoolean("display_inputstick_text", true);
		String tmp;
		tmp = prefs.getString("items_general", "settings|osx|tab_enter|macro");
		showSettings = tmp.contains(SettingsActivity.ITEM_SETTINGS);
		showConnectionOptions = tmp.contains(SettingsActivity.ITEM_CONNECTION);	
		showMacSetup = tmp.contains(SettingsActivity.ITEM_MAC_SETUP);
		showTabEnter = tmp.contains(SettingsActivity.ITEM_TAB_ENTER);	
		showMacroAddEdit = tmp.contains(SettingsActivity.ITEM_MACRO);		
		
		
		
				
		tmp = prefs.getString(SettingsActivity.ITEMS_ENTRY_PRIMARY, "username_and_password|username_password_enter|masked_password|macro|clipboard");
		showUserPass = tmp.contains(SettingsActivity.ITEM_USER_PASSWORD);
		showUserPassEnter = tmp.contains(SettingsActivity.ITEM_USER_PASSWORD_ENTER);
		showMasked = tmp.contains(SettingsActivity.ITEM_MASKED);
		showMacro = tmp.contains(SettingsActivity.ITEM_MACRO);	
		showClipboard = tmp.contains(SettingsActivity.ITEM_CLIPBOARD);	
		
		if (showSecondary) {
			tmp = prefs.getString(SettingsActivity.ITEMS_ENTRY_SECONDARY, "username_and_password|username_password_enter|masked_password|macro|clipboard");
			showUserPassSec = tmp.contains(SettingsActivity.ITEM_USER_PASSWORD);
			showUserPassEnterSec = tmp.contains(SettingsActivity.ITEM_USER_PASSWORD_ENTER);
			showMaskedSec = tmp.contains(SettingsActivity.ITEM_MASKED);
			showMacroSec = tmp.contains(SettingsActivity.ITEM_MACRO);
			showClipboardSec = tmp.contains(SettingsActivity.ITEM_CLIPBOARD);	
		}
				
		tmp = prefs.getString(SettingsActivity.ITEMS_FIELD_PRIMARY, "type_normal|type_slow");
		showType = tmp.contains(SettingsActivity.ITEM_TYPE);
		showTypeSlow = tmp.contains(SettingsActivity.ITEM_TYPE_SLOW);
		
		if (showSecondary) {
			tmp = prefs.getString(SettingsActivity.ITEMS_FIELD_SECONDARY, "type_normal|type_slow");
			showTypeSec = tmp.contains(SettingsActivity.ITEM_TYPE);
			showTypeSlowSec = tmp.contains(SettingsActivity.ITEM_TYPE_SLOW);
		}		
		
		clipboardLaunchAuthenticator = prefs.getBoolean("clipboard_launch_authenticator", true);
		clipboardAutoDisable = prefs.getBoolean("clipboard_auto_disable", true);
		clipboardAutoEnter = prefs.getBoolean("clipboard_auto_enter", false);
		
	}
	

	public String getLayoutPrimary() {
		return layoutPrimary;
	}

	public String getLayoutSecondary() {		
		return layoutSecondary;
	}

	public String getLayoutPrimaryDisplayCode() {
		return layoutPrimaryDisplayCode;
	}

	public String getLayoutSecondaryDisplayCode() {
		return layoutSecondaryDisplayCode;
	}

	public boolean isShowSecondary() {
		return showSecondary;
	}

	public boolean isEnterAfterURL() {		
		return enterAfterURL;
	}

	public boolean isAutoConnect() {
		return autoConnect;
	}

	public int getAutoConnectTimeout() {
		return autoConnectTimeout;
	}

	public boolean isDisconnectOnClose() {
		return disconnectOnClose;
	}
	
	public int getReportMultiplier() {
		return reportMultiplier;
	}
	
	
	
	public boolean isDisplayInputStickText() {
		return displayInputstickText;
	}
	
	public boolean isShowSettings() {
		return showSettings;
	}
	public boolean isShowConnectionOptions() {
		return showConnectionOptions;
	}
	public boolean isShowMacSetup() {
		return showMacSetup;
	}
	public boolean isShowTabEnter() {
		return showTabEnter;
	}
	public boolean isShowMacroAddEdit() {
		return showMacroAddEdit;
	}
	
	public boolean isShowClipboard(boolean isPrimary) {
		if (isPrimary) {
			return showClipboard;
		} else {
			return showClipboardSec;
		}		
	}	
	
	public boolean isShowUserPass(boolean isPrimary) {
		if (isPrimary) {
			return showUserPass;
		} else {
			return showUserPassSec;
		}
	}
	
	public boolean isShowUserPassEnter(boolean isPrimary) {
		if (isPrimary) {
			return showUserPassEnter;
		} else {
			return showUserPassEnterSec;
		}
	}
	
	public boolean isShowMasked(boolean isPrimary) {
		if (isPrimary) {
			return showMasked;
		} else {
			return showMaskedSec;
		}
	}
	
	public boolean isShowType(boolean isPrimary) {
		if (isPrimary) {
			return showType;
		} else {
			return showTypeSec;
		}
	}
	
	public boolean isShowTypeSlow(boolean isPrimary) {
		if (isPrimary) {
			return showTypeSlow;
		} else {
			return showTypeSlowSec;
		}
	}

	public boolean isShowMacro(boolean isPrimary) {
		if (isPrimary) {
			return showMacro;
		} else {
			return showMacroSec;
		}
	}
	
	public String getMacro() {
		macro = prefs.getString(Const.MACRO_PREF_PREFIX + entryId, null);
		return macro;
	}
	
	public String getEntryId() {
		return entryId;
	}
	
	
	public boolean isClipboardLaunchAuthenticator() {
		return clipboardLaunchAuthenticator;
	}
	public boolean isClipboardAutoDisable() {
		return clipboardAutoDisable;
	}
	public boolean isClipboardAutoEnter() {
		return clipboardAutoEnter;
	}	
	
	
}
