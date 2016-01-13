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
    private ArrayList<Loc> mAvailableLanguages = new ArrayList<Loc>();

    private static final String[] WHITELIST_LANGUAGES = {
        "cs", "da", "de", "en_GB", "en_US", "es", "es_US", "fr", "it", "nb", "nl", "pl", "pt",
        "ru", "tr"
    };
    
    private static final String[] WEAK_WHITELIST_LANGUAGES = {
        "cs", "da", "de", "en_GB", "en_US", "es", "es_US", "fr", "it", "nb", "nl", "pl", "pt",
        "ru", "tr", "en"
    };

    private static boolean isWhitelisted(String lang, boolean strict) {
        for (String s : (strict? WHITELIST_LANGUAGES : WEAK_WHITELIST_LANGUAGES)) {
            if (s.equalsIgnoreCase(lang)) {
                return true;
            }
            if ((!strict) && (s.length()==2) && lang.toLowerCase(Locale.US).startsWith(s))
            {
            	return true;
            }
        }
        return false;
    }

    private static class Loc implements Comparable<Object> {
        static Collator sCollator = Collator.getInstance();

        String label;
        Locale locale;

        public Loc(String label, Locale locale) {
            this.label = label;
            this.locale = locale;
        }

        @Override
        public String toString() {
            return this.label;
        }

        public int compareTo(Object o) {
            return sCollator.compare(this.label, ((Loc) o).label);
        }
    }

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

    ArrayList<Loc> getUniqueLocales(boolean strict) {
        String[] locales = getAssets().getLocales();
        Arrays.sort(locales);
        ArrayList<Loc> uniqueLocales = new ArrayList<Loc>();

        final int origSize = locales.length;
        Loc[] preprocess = new Loc[origSize];
        int finalSize = 0;
        for (int i = 0 ; i < origSize; i++ ) {
            String s = locales[i];
            
            int len = s.length();
            final Locale l;
            final String language;
            if (len == 5) {
                language = s.substring(0, 2);
                String country = s.substring(3, 5);
                l = new Locale(language, country);
            } else if (len == 2) {
                language = s;
                l = new Locale(language);
            } else {
            	android.util.Log.d("KP2AK", "locale "+s+" has unexpected length.");
                continue;
            }
            // Exclude languages that are not relevant to LatinIME
            if (!isWhitelisted(s, strict)) 
        	{
            	android.util.Log.d("KP2AK", "locale "+s+" is not white-listed");
            	continue;
        	}

            android.util.Log.d("KP2AK", "adding locale "+s);
            if (finalSize == 0) {
                preprocess[finalSize++] =
                        new Loc(LanguageSwitcher.toTitleCase(l.getDisplayName(l), l), l);
            } else {
                // check previous entry:
                //  same lang and a country -> upgrade to full name and
                //    insert ours with full name
                //  diff lang -> insert ours with lang-only name
                if (preprocess[finalSize-1].locale.getLanguage().equals(
                        language)) {
                    preprocess[finalSize-1].label = LanguageSwitcher.toTitleCase(
                            preprocess[finalSize-1].locale.getDisplayName(),
                            preprocess[finalSize-1].locale);
                    preprocess[finalSize++] =
                            new Loc(LanguageSwitcher.toTitleCase(l.getDisplayName(), l), l);
                } else {
                    String displayName;
                    if (s.equals("zz_ZZ")) {
                    } else {
                        displayName = LanguageSwitcher.toTitleCase(l.getDisplayName(l), l);
                        preprocess[finalSize++] = new Loc(displayName, l);
                    }
                }
            }
        }
        for (int i = 0; i < finalSize ; i++) {
            uniqueLocales.add(preprocess[i]);
        }
        return uniqueLocales;
    }
}
