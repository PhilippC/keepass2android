package keepass2android.plugin.inputstick;

import keepass2android.pluginsdk.AccessManager;
import keepass2android.pluginsdk.Strings;
import sheetrock.panda.changelog.ChangeLog;
import android.app.AlertDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.SharedPreferences.OnSharedPreferenceChangeListener;
import android.net.Uri;
import android.os.Bundle;
import android.preference.ListPreference;
import android.preference.Preference;
import android.preference.Preference.OnPreferenceChangeListener;
import android.preference.Preference.OnPreferenceClickListener;
import android.preference.PreferenceActivity;
import android.preference.PreferenceManager;
import android.widget.CheckBox;
import android.widget.Toast;


@SuppressWarnings("deprecation")
public class SettingsActivity extends PreferenceActivity implements OnSharedPreferenceChangeListener {	
	
	
	public static final String ITEMS_GENERAL = "items_general";
	public static final String ITEMS_ENTRY_PRIMARY = "items_entry_primary";
	public static final String ITEMS_FIELD_PRIMARY = "items_field_primary";
	public static final String ITEMS_ENTRY_SECONDARY = "items_entry_secondary";
	public static final String ITEMS_FIELD_SECONDARY = "items_field_secondary";
	
	//IMPORTANT: checked using .contains() !
	//username_password_only
	//general
	public static final String ITEM_SETTINGS = "settings";
	public static final String ITEM_CONNECTION = "con_disc";
	public static final String ITEM_MAC_SETUP = "osx";
	public static final String ITEM_TAB_ENTER = "tab_enter";
	public static final String ITEM_MACRO = "macro";	
	//entry
	public static final String ITEM_USER_PASSWORD = "username_and_password";
	public static final String ITEM_USER_PASSWORD_ENTER = "username_password_enter";
	public static final String ITEM_MASKED = "masked_password";
	public static final String ITEM_CLIPBOARD = "clipboard";
	
	//field
	public static final String ITEM_TYPE = "type_normal";
	public static final String ITEM_TYPE_SLOW = "type_slow";
	
	private static final String[] PREFS_UI_GENERAL = {"show_settings", "show_mac_setup", "show_tab_enter"};	
	private static final String[] PREFS_UI_ENTRY_PRIMARY = {"show_user_pass", "show_user_pass_enter", "show_masked"};
	private static final String[] PREFS_UI_FIELD_PRIMARY = {"show_field_type", "show_field_type_slow"};		
	private static final String[] PREFS_UI_ENTRY_SECONDARY = {"show_user_pass_secondary", "show_user_pass_enter_secondary", "show_masked_secondary"};
	private static final String[] PREFS_UI_FIELD_SECONDARY = {"show_field_type_secondary", "show_field_type_slow_secondary"};		
	
	private static final String[] NAMES_UI_GENERAL = {ITEM_SETTINGS, ITEM_MAC_SETUP, ITEM_TAB_ENTER};
	private static final String[] NAMES_UI_ENTRY = {ITEM_USER_PASSWORD, ITEM_USER_PASSWORD_ENTER, ITEM_MASKED};	
	private static final String[] NAMES_UI_FIELD = {ITEM_TYPE, ITEM_TYPE_SLOW};	
	
	
	private SharedPreferences sharedPref;	
	
	private Preference prefShowSecondary;
	private Preference prefSecondaryKbdLayout;
	
	private Preference prefAutoconnectTimeout;	
	private Preference prefUiEntrySecondary;
	private Preference prefUiFieldSecondary;
	
	private boolean displayReloadInfo;
	private OnPreferenceClickListener reloadInfoListener = new OnPreferenceClickListener() {
		@Override
		public boolean onPreferenceClick(Preference preference) {
			displayReloadInfo = true;
			return false;
		}
	};

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		sharedPref = PreferenceManager.getDefaultSharedPreferences(this);		
						
		convertOldUiPreferences(sharedPref, this);			
		
		addPreferencesFromResource(R.layout.activity_settings);
		setupSimplePreferencesScreen();				
		
		//ask user to download new release of the plugin (force display activity only once, use only notification next time)
		if (MigrationMessageActivity.shouldShowActivity(sharedPref)) {
			startActivity(new Intent(this, MigrationMessageActivity.class));	
		}			
	}

	
	private void setupSimplePreferencesScreen() {
		Preference pref;
		
		setListSummary("kbd_layout");
		setListSummary("secondary_kbd_layout");
		setListSummary("typing_speed");
		setListSummary("autoconnect_timeout");
        		
		pref = findPreference("enable_plugin_pref");
		pref.setOnPreferenceClickListener(new OnPreferenceClickListener() {
			@Override
			public boolean onPreferenceClick(Preference preference) {
				try {
					Intent i = new Intent( Strings.ACTION_EDIT_PLUGIN_SETTINGS);
					i.putExtra(Strings.EXTRA_PLUGIN_PACKAGE, SettingsActivity.this.getPackageName());
					startActivityForResult(i, 123);
				} catch (Exception e) {
					e.printStackTrace();
				}
				return true;
			}
		});

		pref = (Preference)findPreference("show_changelog_preference_key");
		pref.setOnPreferenceClickListener(new OnPreferenceClickListener() {
			@Override
			public boolean onPreferenceClick(Preference preference) {		
				new ChangeLog(SettingsActivity.this).getFullLogDialog().show();
				return true;
			}
		});
		
		pref = (Preference)findPreference("show_help_webpage_key");
		pref.setOnPreferenceClickListener(new OnPreferenceClickListener() {
			@Override
			public boolean onPreferenceClick(Preference preference) {
				startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("http://www.inputstick.com/help")));				
				return true;
			}
		});		
		
		pref = (Preference)findPreference("show_source_key");
		pref.setOnPreferenceClickListener(new OnPreferenceClickListener() {
			@Override
			public boolean onPreferenceClick(Preference preference) {
				startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://github.com/inputstick")));
				return true;
			}
		});					

		//typing:		
		findPreference("kbd_layout").setOnPreferenceClickListener(reloadInfoListener);
		prefShowSecondary = (Preference) findPreference("show_secondary");
		prefShowSecondary.setOnPreferenceClickListener(reloadInfoListener);
		prefShowSecondary.setOnPreferenceChangeListener(new OnPreferenceChangeListener() {
			@Override
			public boolean onPreferenceChange(Preference preference, Object newValue) {
				boolean enabled = (Boolean)newValue;
				setSecondaryLayoutEnabled(enabled);
				if (enabled) {
					//check if at least one action is enabled
					boolean showMessage = true;
					String tmp;
					tmp = sharedPref.getString(SettingsActivity.ITEMS_FIELD_SECONDARY, null);
					if ((tmp != null) && (tmp.length() > 1)) {
						showMessage = false;
					}
					tmp = sharedPref.getString(SettingsActivity.ITEMS_ENTRY_SECONDARY, null);
					if ((tmp != null) && (tmp.length() > 1)) {
						showMessage = false;
					}					
					if (showMessage) {
						AlertDialog.Builder alert = new AlertDialog.Builder(SettingsActivity.this);
						
						alert.setTitle(R.string.configuration_title);
						alert.setMessage(R.string.secondary_layout_action_reminder_message);
						alert.setPositiveButton(R.string.ok, null);
						alert.show();
					}
				}
        		return true;
			}
        });
		prefSecondaryKbdLayout = findPreference("secondary_kbd_layout");
		prefSecondaryKbdLayout.setOnPreferenceClickListener(reloadInfoListener);	
		
		//connection:
		pref = (Preference) findPreference("autoconnect");
		pref.setOnPreferenceChangeListener(new OnPreferenceChangeListener() {
			@Override
			public boolean onPreferenceChange(Preference preference, Object newValue) {
				setAutoconnectTimeoutEnabled((Boolean)newValue);
				displayReloadInfo = true;
        		return true;
			}
        });
		
		prefAutoconnectTimeout = (Preference) findPreference("autoconnect_timeout");
		prefAutoconnectTimeout.setOnPreferenceClickListener(reloadInfoListener);		
		
		
		//UI:
		findPreference("display_inputstick_text").setOnPreferenceClickListener(reloadInfoListener);
		findPreference("items_general").setOnPreferenceClickListener(reloadInfoListener);
		findPreference("items_entry_primary").setOnPreferenceClickListener(reloadInfoListener);
		findPreference("items_field_primary").setOnPreferenceClickListener(reloadInfoListener);
		prefUiEntrySecondary = findPreference("items_entry_secondary");
		prefUiEntrySecondary.setOnPreferenceClickListener(reloadInfoListener);
		prefUiFieldSecondary = findPreference("items_field_secondary");
		prefUiFieldSecondary.setOnPreferenceClickListener(reloadInfoListener);						
		
		setSecondaryLayoutEnabled(sharedPref.getBoolean("show_secondary", false));		
		setAutoconnectTimeoutEnabled(sharedPref.getBoolean("autoconnect", true));		
	}
	
	@Override
	protected void onResume() {
		super.onResume();
		getPreferenceScreen().getSharedPreferences().registerOnSharedPreferenceChangeListener(this);		
		displayReloadInfo = false;
		Preference enablePref = findPreference("enable_plugin_pref");
		if (AccessManager.getAllHostPackages(SettingsActivity.this).isEmpty()) {
			enablePref.setSummary(R.string.not_configured);
		} else {
			enablePref.setSummary(R.string.enabled);
		}					
		
		MigrationMessageActivity.displayNotification(this);
	}
	
	@Override
	protected void onPause() {
		getPreferenceScreen().getSharedPreferences().unregisterOnSharedPreferenceChangeListener(this);
		ActionManager.reloadPreferences();
		MigrationMessageActivity.hideNotification(this);
		super.onPause();				
	}
	
	@Override
    public void onSharedPreferenceChanged(SharedPreferences sharedPreferences, String key) {                
        setListSummary(key);
    }
	
	private void setListSummary(String key) {
		Preference pref;		
		ListPreference listPref;
		pref = findPreference(key);
		if (pref instanceof ListPreference) {
			listPref = (ListPreference) pref;
			pref.setSummary(listPref.getEntry());
		}
	}
	
	
	@Override
	public void onBackPressed() {
		boolean kp2a = getIntent().getBooleanExtra(Const.EXTRA_LAUNCHED_FROM_KP2A, false); //show warning only if activity was launched from kp2a app, 
		boolean showWarning = sharedPref.getBoolean("show_reload_warning", true); //show warning only if user did not checked "do not show again" before
		//show warning only if it is necessary to reload entry
		if (kp2a && displayReloadInfo) {
			if (showWarning) {
				displayReloadInfo = false;
				AlertDialog.Builder alert = new AlertDialog.Builder(this);
				alert.setTitle(R.string.important_title);
				alert.setMessage(R.string.entry_reload_message);	
				
				final CheckBox cb = new CheckBox(this);
				cb.setText(R.string.do_not_remind);
				cb.setChecked(false);
				alert.setView(cb);
				
				alert.setNeutralButton(R.string.ok, new DialogInterface.OnClickListener() {
					public void onClick(DialogInterface dialog, int which) {
						SettingsActivity.this.onBackPressed();	
						if (cb.isChecked()) {
							SharedPreferences.Editor editor = sharedPref.edit();
							editor.putBoolean("show_reload_warning", false);
							editor.apply();
						}
					}
					});
				alert.show();
			} else {
				//just toast, used does not want to see dialog msg
				Toast.makeText(this, R.string.entry_reload_message, Toast.LENGTH_LONG).show();
				super.onBackPressed();
			}
		} else {
			super.onBackPressed();
		}
	}
	
	
	private void setAutoconnectTimeoutEnabled(boolean enabled) {
		prefAutoconnectTimeout.setEnabled( !enabled); // show this pref only if autoconnect is DISABLED
	}
	
	private void setSecondaryLayoutEnabled(boolean enabled) {
		prefUiEntrySecondary.setEnabled(enabled);
		prefUiFieldSecondary.setEnabled(enabled);		
		prefSecondaryKbdLayout.setEnabled(enabled);
	}

		


	//converting old preferences to new ones:	
	public static void convertOldUiPreferences(SharedPreferences sharedPref, Context ctx) {
		//if this is a fresh install, UI display preferences will be set to default values anyway 
		ChangeLog cl = new ChangeLog(ctx);
		if ( !cl.firstRunEver()) {		
			//if ITEMS_GENERAL exista -> already converted
			if ( !sharedPref.contains(ITEMS_GENERAL)) {
				final SharedPreferences.Editor editor = sharedPref.edit();	
				//editor.putBoolean("convert_old_ui_preferences", false);
				editor.putString(ITEMS_GENERAL, getMultiSelectPreferenceString(sharedPref, PREFS_UI_GENERAL, NAMES_UI_GENERAL, true, false));		
				editor.putString(ITEMS_ENTRY_PRIMARY, getMultiSelectPreferenceString(sharedPref, PREFS_UI_ENTRY_PRIMARY, NAMES_UI_ENTRY, true, true));
				editor.putString(ITEMS_FIELD_PRIMARY, getMultiSelectPreferenceString(sharedPref, PREFS_UI_FIELD_PRIMARY, NAMES_UI_FIELD, false, false));
				editor.putString(ITEMS_ENTRY_SECONDARY, getMultiSelectPreferenceString(sharedPref, PREFS_UI_ENTRY_SECONDARY, NAMES_UI_ENTRY, true, true));
				editor.putString(ITEMS_FIELD_SECONDARY, getMultiSelectPreferenceString(sharedPref, PREFS_UI_FIELD_SECONDARY, NAMES_UI_FIELD, false, false));
				editor.commit();		
			}
		} else {
			//make sure ITEMS_GENERAL exists
			if ( !sharedPref.contains(ITEMS_GENERAL)) {
				final SharedPreferences.Editor editor = sharedPref.edit();	
				editor.putString(ITEMS_GENERAL, "settings|osx|tab_enter|macro");
				editor.commit();	
			}
		}
		cl.updateVersionInPreferences();
	}
	
	private static String getMultiSelectPreferenceString(SharedPreferences sharedPref, String[] prefs, String[] names, boolean addMacro, boolean addClipboard) {
		String tmp = "";
		boolean addSeparator = false;
		if (prefs != null) {
			for (int i = 0; i < prefs.length; i++) {				
				if ((sharedPref.contains(prefs[i])) && (sharedPref.getBoolean(prefs[i], true))) {
					if (addSeparator) {
						tmp += MultiSelectListPreference.SEPARATOR; // "|"
					}
					tmp += names[i];
					addSeparator = true;
				}
			}
			if (addMacro) {
				if (addSeparator) {
					tmp += MultiSelectListPreference.SEPARATOR;
				}
				tmp += ITEM_MACRO;
				addSeparator = true;
			}
			if (addClipboard) {
				if (addSeparator) {
					tmp += MultiSelectListPreference.SEPARATOR;
				}
				tmp += ITEM_CLIPBOARD;
			}			
		}
		return tmp;
	}

	
}
