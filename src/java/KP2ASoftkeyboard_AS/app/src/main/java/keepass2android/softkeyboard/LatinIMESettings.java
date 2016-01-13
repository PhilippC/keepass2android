/*
 * Copyright (C) 2008 The Android Open Source Project
 * Copyright (C) 2014 Philipp Crocoll <crocoapps@googlemail.com>
 * Copyright (C) 2014 Wiktor Lawski <wiktor.lawski@gmail.com>
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not
 * use this file except in compliance with the License. You may obtain a copy of
 * the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 */

package keepass2android.softkeyboard;

import java.util.ArrayList;
import java.util.Locale;

import android.app.AlertDialog;
import android.app.Dialog;
import android.app.backup.BackupManager;
import android.content.DialogInterface;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.preference.CheckBoxPreference;
import android.preference.ListPreference;
import android.preference.PreferenceActivity;
import android.preference.PreferenceGroup;
import android.preference.PreferenceManager;
import android.speech.SpeechRecognizer;
import android.text.AutoText;
import android.util.Log;

public class LatinIMESettings extends PreferenceActivity
        implements SharedPreferences.OnSharedPreferenceChangeListener,
        DialogInterface.OnDismissListener {

    private static final String QUICK_FIXES_KEY = "quick_fixes";
    private static final String PREDICTION_SETTINGS_KEY = "prediction_settings";
    
    /* package */ static final String PREF_SETTINGS_KEY = "settings_key";

    private static final String TAG = "LatinIMESettings";

    
    private CheckBoxPreference mQuickFixes;
    private ListPreference mSettingsKeyPreference;
    
    private boolean mOkClicked = false;

    @Override
    protected void onCreate(Bundle icicle) {
    	SharedPreferences prefs =
    			PreferenceManager.getDefaultSharedPreferences(this);
    	Design.updateTheme(this, prefs);

        super.onCreate(icicle);
        addPreferencesFromResource(R.xml.prefs);
        mQuickFixes = (CheckBoxPreference) findPreference(QUICK_FIXES_KEY);
        mSettingsKeyPreference = (ListPreference) findPreference(PREF_SETTINGS_KEY);
        prefs.registerOnSharedPreferenceChangeListener(this);

        
    }

    @Override
    protected void onResume() {
        super.onResume();
        int autoTextSize = AutoText.getSize(getListView());
        if (autoTextSize < 1) {
            ((PreferenceGroup) findPreference(PREDICTION_SETTINGS_KEY))
                    .removePreference(mQuickFixes);
        }
        
        
    }

    @Override
    protected void onDestroy() {
        getPreferenceManager().getSharedPreferences().unregisterOnSharedPreferenceChangeListener(
                this);
        super.onDestroy();
    }

    public void onSharedPreferenceChanged(SharedPreferences prefs, String key) {
        (new BackupManager(this)).dataChanged();
           
    }



    @Override
    protected Dialog onCreateDialog(int id) {
        switch (id) {
           
            default:
                Log.e(TAG, "unknown dialog " + id);
                return null;
        }
    }

    public void onDismiss(DialogInterface dialog) {
        
    }

}
