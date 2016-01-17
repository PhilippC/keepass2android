/*
 * Copyright (C) 2008-2009 Google Inc.
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

import android.content.Context;
import android.content.SharedPreferences;
import android.content.SharedPreferences.Editor;
import android.content.res.Configuration;
import android.content.res.Resources;
import android.os.Bundle;
import android.preference.CheckBoxPreference;
import android.preference.PreferenceActivity;
import android.preference.PreferenceGroup;
import android.preference.PreferenceManager;
import android.text.TextUtils;

import java.text.Collator;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Locale;

public class InputLanguageSelection extends PreferenceActivity {

    private String mSelectedLanguages;
    private ArrayList<keepass2android.kbbridge.Loc> mAvailableLanguages = new ArrayList<keepass2android.kbbridge.Loc>();


    @Override
    protected void onCreate(Bundle icicle) {
        // Get the settings preferences
        SharedPreferences sp = PreferenceManager.getDefaultSharedPreferences(this);

        Design.updateTheme(this, sp);

    	super.onCreate(icicle);
        addPreferencesFromResource(R.xml.language_prefs);
        mSelectedLanguages = sp.getString(KP2AKeyboard.PREF_SELECTED_LANGUAGES, "");
        String[] languageList = mSelectedLanguages.split(",");
        
        //first try to get the unique locales in a strict mode (filtering most redundant layouts like English (Jamaica) etc.)
        mAvailableLanguages = getUniqueLocales(true);
        //sometimes the strict check returns only EN_US, EN_GB and ES_US. Accept more in these cases:
        if (mAvailableLanguages.size() < 5)
        {
        	mAvailableLanguages = getUniqueLocales(false);
        }
        PreferenceGroup parent = getPreferenceScreen();
        for (int i = 0; i < mAvailableLanguages.size(); i++) {
            CheckBoxPreference pref = new CheckBoxPreference(this);
            Locale locale = mAvailableLanguages.get(i).locale;
            pref.setTitle(LanguageSwitcher.toTitleCase(locale.getDisplayName(locale), locale));
            boolean checked = isLocaleIn(locale, languageList);
            pref.setChecked(checked);
            if (hasDictionary(locale, this)) {
                pref.setSummary(R.string.has_dictionary);
            }
            parent.addPreference(pref);
        }
    }

    private boolean isLocaleIn(Locale locale, String[] list) {
        String lang = get5Code(locale);
        for (int i = 0; i < list.length; i++) {
            if (lang.equalsIgnoreCase(list[i])) return true;
        }
        return false;
    }

    private boolean hasDictionary(Locale locale, Context ctx) {
        Resources res = getResources();
        Configuration conf = res.getConfiguration();
        Locale saveLocale = conf.locale;
        boolean haveDictionary = false;
        conf.locale = locale;
        res.updateConfiguration(conf, res.getDisplayMetrics());

        //somewhat a hack. But simply querying the dictionary will always return an English
        //dictionary in KP2A so if we get a dict, we wouldn't know if it's language specific 
        if (locale.getLanguage().equals("en"))
        {
        	haveDictionary = true;
        }
        else 
        {
            BinaryDictionary plug = PluginManager.getDictionary(getApplicationContext(), locale.getLanguage());
            if (plug != null) {
            	plug.close();
            	haveDictionary = true;
            }
        }
        conf.locale = saveLocale;
        res.updateConfiguration(conf, res.getDisplayMetrics());
        return haveDictionary;
    }

    private String get5Code(Locale locale) {
        String country = locale.getCountry();
        return locale.getLanguage()
                + (TextUtils.isEmpty(country) ? "" : "_" + country);
    }

    @Override
    protected void onResume() {
        super.onResume();
    }

    @Override
    protected void onPause() {
        super.onPause();
        // Save the selected languages
        String checkedLanguages = "";
        PreferenceGroup parent = getPreferenceScreen();
        int count = parent.getPreferenceCount();
        for (int i = 0; i < count; i++) {
            CheckBoxPreference pref = (CheckBoxPreference) parent.getPreference(i);
            if (pref.isChecked()) {
                Locale locale = mAvailableLanguages.get(i).locale;
                checkedLanguages += get5Code(locale) + ",";
            }
        }
        if (checkedLanguages.length() < 1) checkedLanguages = null; // Save null
        SharedPreferences sp = PreferenceManager.getDefaultSharedPreferences(this);
        Editor editor = sp.edit();
        editor.putString(KP2AKeyboard.PREF_SELECTED_LANGUAGES, checkedLanguages);
        SharedPreferencesCompat.apply(editor);
    }

    ArrayList<keepass2android.kbbridge.Loc> getUniqueLocales(boolean strict) {
        return keepass2android.kbbridge.LocaleHelper.getUniqueLocales(this, strict);
    }


}
