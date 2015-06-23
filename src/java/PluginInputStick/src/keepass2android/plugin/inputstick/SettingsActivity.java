package keepass2android.plugin.inputstick;

import keepass2android.pluginsdk.AccessManager;
import keepass2android.pluginsdk.Strings;
import sheetrock.panda.changelog.ChangeLog;
import android.annotation.TargetApi;
import android.app.AlertDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.res.Configuration;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.preference.ListPreference;
import android.preference.Preference;
import android.preference.Preference.OnPreferenceChangeListener;
import android.preference.Preference.OnPreferenceClickListener;
import android.preference.PreferenceActivity;
import android.preference.PreferenceFragment;
import android.preference.PreferenceManager;
import android.widget.CheckBox;

/**
 * A {@link PreferenceActivity} that presents a set of application settings. On
 * handset devices, settings are presented as a single list. On tablets,
 * settings are split by category, with category headers shown to the left of
 * the list of settings.
 * <p>
 * See <a href="http://developer.android.com/design/patterns/settings.html">
 * Android Design: Settings</a> for design guidelines and the <a
 * href="http://developer.android.com/guide/topics/ui/settings.html">Settings
 * API Guide</a> for more information on developing a Settings UI.
 */

@SuppressWarnings("deprecation")
public class SettingsActivity extends PreferenceActivity {
	/**
	 * Determines whether to always show the simplified settings UI, where
	 * settings are presented in a single list. When false, settings are shown
	 * as a master/detail two-pane view on tablets. When true, a single pane is
	 * shown on tablets.
	 */
	private static final boolean ALWAYS_SIMPLE_PREFS = false;
	
	private Preference prefShowSecondary;
	private Preference prefSecondaryKbdLayout;
	
	private Preference prefAutoconnectTimeout;
	
	private Preference prefSettings;
	private Preference prefMacSetup;
	private Preference prefTabEnter;
	
	private Preference prefUserPass;
	private Preference prefUserPassEnter;
	private Preference prefMasked;
	private Preference prefType;
	private Preference prefTypeSlow;
	
	private Preference prefSecondaryUserPass;
	private Preference prefSecondaryUserPassEnter;
	private Preference prefSecondaryMasked;
	private Preference prefSecondaryType;
	private Preference prefSecondaryTypeSlow;
	
	private boolean displayReloadInfo;
	private OnPreferenceClickListener reloadInfoListener = new OnPreferenceClickListener() {
		@Override
		public boolean onPreferenceClick(Preference preference) {
			displayReloadInfo = true;
			return false;
		}
	};

	@Override
	protected void onPostCreate(Bundle savedInstanceState) {
		super.onPostCreate(savedInstanceState);

		setupSimplePreferencesScreen();
		
		if (getIntent().getBooleanExtra(Const.EXTRA_CHANGELOG, false)) {
			ChangeLog cl = new ChangeLog(this);
			cl.getLogDialog().show();
		} else {		
			//first run ever?			
			final SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(this);
			if (prefs.getBoolean("display_configuration_message", true)) {
				AlertDialog.Builder alert = new AlertDialog.Builder(this);
				
				alert.setTitle(R.string.configuration_title);
				alert.setMessage(R.string.configuration_message);
				alert.setPositiveButton("OK", new DialogInterface.OnClickListener() {
					public void onClick(DialogInterface dialog, int whichButton) {
						//disable cfg message ONLY after clicking OK button
						prefs.edit().putBoolean("display_configuration_message", false).apply();
					}
				});
				alert.show();
			}		
		}
	}

	/**
	 * Shows the simplified settings UI if the device configuration if the
	 * device configuration dictates that a simplified, single-pane UI should be
	 * shown.
	 */
	
	private void setupSimplePreferencesScreen() {
		if (!isSimplePreferences(this)) {
			return;
		}

		// In the simplified UI, fragments are not used at all and we instead
		// use the older PreferenceActivity APIs.

		// Add 'general' preferences.
		addPreferencesFromResource(R.xml.pref_general);

		bindPreferenceSummaryToValue(findPreference("kbd_layout"));
		bindPreferenceSummaryToValue(findPreference("secondary_kbd_layout"));
		bindPreferenceSummaryToValue(findPreference("typing_speed"));
		bindPreferenceSummaryToValue(findPreference("autoconnect_timeout"));
		

		
		Preference pref;
		
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
				ChangeLog cl = new ChangeLog(SettingsActivity.this);
				cl.getFullLogDialog().show();				
				return true;
			}
		});
		
		pref = (Preference)findPreference("show_help_webpage_key");
		pref.setOnPreferenceClickListener(new OnPreferenceClickListener() {
			@Override
			public boolean onPreferenceClick(Preference preference) {
				Intent browserIntent = new Intent(Intent.ACTION_VIEW, Uri.parse("http://www.inputstick.com/help"));
				startActivity(browserIntent);					
				return true;
			}
		});
		
		
		pref = (Preference) findPreference("show_secondary");
		pref.setOnPreferenceChangeListener(new OnPreferenceChangeListener() {
			@Override
			public boolean onPreferenceChange(Preference preference, Object newValue) {
				setSecondaryLayoutEnabled((Boolean)newValue);
        		return true;
			}
        });
		
		pref = (Preference) findPreference("autoconnect");
		pref.setOnPreferenceChangeListener(new OnPreferenceChangeListener() {
			@Override
			public boolean onPreferenceChange(Preference preference, Object newValue) {
				setAutoconnectTimeoutEnabled((Boolean)newValue);
        		return true;
			}
        });
		
		prefAutoconnectTimeout = (Preference) findPreference("autoconnect_timeout");
		prefShowSecondary = (Preference) findPreference("show_secondary");
		
		prefSettings = (Preference) findPreference("show_settings");		
		prefMacSetup = (Preference) findPreference("show_mac_setup");		
		prefTabEnter = (Preference) findPreference("show_tab_enter");
		
		prefUserPass = (Preference) findPreference("show_user_pass");		
		prefUserPassEnter = (Preference) findPreference("show_user_pass_enter");		
		prefMasked = (Preference) findPreference("show_masked");
		prefType = (Preference) findPreference("show_field_type");
		prefTypeSlow = (Preference) findPreference("show_field_type_slow");		
		
		prefSecondaryKbdLayout = findPreference("secondary_kbd_layout");
		prefSecondaryUserPass = findPreference("show_user_pass_secondary");
		prefSecondaryUserPassEnter = findPreference("show_user_pass_enter_secondary");
		prefSecondaryMasked = findPreference("show_masked_secondary");
		prefSecondaryType = findPreference("show_field_type_secondary");
		prefSecondaryTypeSlow = findPreference("show_field_type_slow_secondary");
		
		
		
		prefShowSecondary.setOnPreferenceClickListener(reloadInfoListener);
		
		prefSettings.setOnPreferenceClickListener(reloadInfoListener);
		prefMacSetup.setOnPreferenceClickListener(reloadInfoListener);
		prefTabEnter.setOnPreferenceClickListener(reloadInfoListener);

		prefUserPass.setOnPreferenceClickListener(reloadInfoListener);
		prefUserPassEnter.setOnPreferenceClickListener(reloadInfoListener);
		prefMasked.setOnPreferenceClickListener(reloadInfoListener);
		prefType.setOnPreferenceClickListener(reloadInfoListener);		
		prefTypeSlow.setOnPreferenceClickListener(reloadInfoListener);		

		prefSecondaryUserPass.setOnPreferenceClickListener(reloadInfoListener);
		prefSecondaryUserPassEnter.setOnPreferenceClickListener(reloadInfoListener);
		prefSecondaryMasked.setOnPreferenceClickListener(reloadInfoListener);
		prefSecondaryType.setOnPreferenceClickListener(reloadInfoListener);
		prefSecondaryTypeSlow.setOnPreferenceClickListener(reloadInfoListener);		


		SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(this);
		setSecondaryLayoutEnabled(prefs.getBoolean("show_secondary", false));		
		setAutoconnectTimeoutEnabled(prefs.getBoolean("autoconnect", true));	
	}
	
	@Override
	protected void onResume() {
		displayReloadInfo = false;
		Preference enablePref = findPreference("enable_plugin_pref");
		if (AccessManager.getAllHostPackages(SettingsActivity.this).isEmpty()) {
			enablePref.setSummary("");
		} else {
			enablePref.setSummary(R.string.enabled);
		}					
		
		super.onResume();
	}
	
	@Override
	public void onBackPressed() {
		if (displayReloadInfo) {
			final SharedPreferences sharedPref = PreferenceManager.getDefaultSharedPreferences(this);
			if (sharedPref.getBoolean("show_reload_warning", true)) {		
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
				super.onBackPressed();
			}
		} else {
			super.onBackPressed();
		}
	}
	
	
	private void setAutoconnectTimeoutEnabled(boolean enabled) {
		prefAutoconnectTimeout.setEnabled( !enabled); // <<<<<<<<<<< show this pref only if autoconnect is DISABLED
	}
	
	private void setSecondaryLayoutEnabled(boolean enabled) {
		prefSecondaryKbdLayout.setEnabled(enabled);
		prefSecondaryUserPass.setEnabled(enabled);
		prefSecondaryUserPassEnter.setEnabled(enabled);
		prefSecondaryMasked.setEnabled(enabled);
		prefSecondaryType.setEnabled(enabled);
		prefSecondaryTypeSlow.setEnabled(enabled);
	}

	/** {@inheritDoc} */
	@Override
	public boolean onIsMultiPane() {
		return isXLargeTablet(this) && !isSimplePreferences(this);
	}

	/**
	 * Helper method to determine if the device has an extra-large screen. For
	 * example, 10" tablets are extra-large.
	 */
	private static boolean isXLargeTablet(Context context) {
		return (context.getResources().getConfiguration().screenLayout & Configuration.SCREENLAYOUT_SIZE_MASK) >= Configuration.SCREENLAYOUT_SIZE_XLARGE;
	}

	/**
	 * Determines whether the simplified settings UI should be shown. This is
	 * true if this is forced via {@link #ALWAYS_SIMPLE_PREFS}, or the device
	 * doesn't have newer APIs like {@link PreferenceFragment}, or the device
	 * doesn't have an extra-large screen. In these cases, a single-pane
	 * "simplified" settings UI should be shown.
	 */
	private static boolean isSimplePreferences(Context context) {
		return ALWAYS_SIMPLE_PREFS
				|| Build.VERSION.SDK_INT < Build.VERSION_CODES.HONEYCOMB
				|| !isXLargeTablet(context);
	}

	

	/**
	 * A preference value change listener that updates the preference's summary
	 * to reflect its new value.
	 */
	private static Preference.OnPreferenceChangeListener sBindPreferenceSummaryToValueListener = new Preference.OnPreferenceChangeListener() {
		@Override
		public boolean onPreferenceChange(Preference preference, Object value) {
			String stringValue = value.toString();

			if (preference instanceof ListPreference) {
				// For list preferences, look up the correct display value in
				// the preference's 'entries' list.
				ListPreference listPreference = (ListPreference) preference;
				int index = listPreference.findIndexOfValue(stringValue);

				// Set the summary to reflect the new value.
				preference
						.setSummary(index >= 0 ? listPreference.getEntries()[index]
								: null);


			} else {
				// For all other preferences, set the summary to the value's
				// simple string representation.
				preference.setSummary(stringValue);
			}
			return true;
		}
	};

	/**
	 * Binds a preference's summary to its value. More specifically, when the
	 * preference's value is changed, its summary (line of text below the
	 * preference title) is updated to reflect the value. The summary is also
	 * immediately updated upon calling this method. The exact display format is
	 * dependent on the type of preference.
	 * 
	 * @see #sBindPreferenceSummaryToValueListener
	 */
	private static void bindPreferenceSummaryToValue(Preference preference) {
		// Set the listener to watch for value changes.
		preference
				.setOnPreferenceChangeListener(sBindPreferenceSummaryToValueListener);

		// Trigger the listener immediately with the preference's
		// current value.
		sBindPreferenceSummaryToValueListener.onPreferenceChange(
				preference,
				PreferenceManager.getDefaultSharedPreferences(
						preference.getContext()).getString(preference.getKey(),
						""));
	}

	/**
	 * This fragment shows general preferences only. It is used when the
	 * activity is showing a two-pane settings UI.
	 */
	@TargetApi(Build.VERSION_CODES.HONEYCOMB)
	public static class GeneralPreferenceFragment extends PreferenceFragment {
		@Override
		public void onCreate(Bundle savedInstanceState) {
			super.onCreate(savedInstanceState);
			addPreferencesFromResource(R.xml.pref_general);

			// Bind the summaries of EditText/List/Dialog/Ringtone preferences
			// to their values. When their values change, their summaries are
			// updated to reflect the new value, per the Android Design
			// guidelines.
			//bindPreferenceSummaryToValue(findPreference("example_text"));
			//bindPreferenceSummaryToValue(findPreference("example_list"));
		}
	}

}
