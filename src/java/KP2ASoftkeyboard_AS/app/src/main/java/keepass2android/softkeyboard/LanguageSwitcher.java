/*
 * Copyright (C) 2010 Google Inc.
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

import android.content.SharedPreferences;
import android.content.SharedPreferences.Editor;
import android.preference.PreferenceManager;
import android.text.TextUtils;

import java.util.Locale;

/**
 * Keeps track of list of selected input languages and the current
 * input language that the user has selected.
 */
public class LanguageSwitcher {

    private Locale[] mLocales;
    private KP2AKeyboard mIme;
    private String[] mSelectedLanguageArray;
    private String   mSelectedLanguages;
    private int      mCurrentIndex = 0;
    private String   mDefaultInputLanguage;
    private Locale   mDefaultInputLocale;
    private Locale   mSystemLocale;

    public LanguageSwitcher(KP2AKeyboard ime) {
        mIme = ime;
        mLocales = new Locale[0];
    }

    public Locale[]  getLocales() {
        return mLocales;
    }

    public int getLocaleCount() {
        return mLocales.length;
    }

    /**
     * Loads the currently selected input languages from shared preferences.
     * @param sp
     * @return whether there was any change
     */
    public boolean loadLocales(SharedPreferences sp) {
        String selectedLanguages = sp.getString(KP2AKeyboard.PREF_SELECTED_LANGUAGES, null);
        String currentLanguage   = sp.getString(KP2AKeyboard.PREF_INPUT_LANGUAGE, null);
        if (selectedLanguages == null || selectedLanguages.length() < 1) {
            loadDefaults();
            if (mLocales.length == 0) {
                return false;
            }
            mLocales = new Locale[0];
            return true;
        }
        if (selectedLanguages.equals(mSelectedLanguages)) {
            return false;
        }
        mSelectedLanguageArray = selectedLanguages.split(",");
        mSelectedLanguages = selectedLanguages; // Cache it for comparison later
        constructLocales();
        mCurrentIndex = 0;
        if (currentLanguage != null) {
            // Find the index
            mCurrentIndex = 0;
            for (int i = 0; i < mLocales.length; i++) {
                if (mSelectedLanguageArray[i].equals(currentLanguage)) {
                    mCurrentIndex = i;
                    break;
                }
            }
            // If we didn't find the index, use the first one
        }
        return true;
    }

    private void loadDefaults() {
        mDefaultInputLocale = mIme.getResources().getConfiguration().locale;
        String country = mDefaultInputLocale.getCountry();
        mDefaultInputLanguage = mDefaultInputLocale.getLanguage() +
                (TextUtils.isEmpty(country) ? "" : "_" + country);
    }

    private void constructLocales() {
        mLocales = new Locale[mSelectedLanguageArray.length];
        for (int i = 0; i < mLocales.length; i++) {
            final String lang = mSelectedLanguageArray[i];
            mLocales[i] = new Locale(lang.substring(0, 2),
                    lang.length() > 4 ? lang.substring(3, 5) : "");
        }
    }

    /**
     * Returns the currently selected input language code, or the display language code if
     * no specific locale was selected for input.
     */
    public String getInputLanguage() {
        if (getLocaleCount() == 0) return mDefaultInputLanguage;

        return mSelectedLanguageArray[mCurrentIndex];
    }
    
    /**
     * Returns the list of enabled language codes.
     */
    public String[] getEnabledLanguages() {
        return mSelectedLanguageArray;
    }

    /**
     * Returns the currently selected input locale, or the display locale if no specific
     * locale was selected for input.
     * @return
     */
    public Locale getInputLocale() {
        if (getLocaleCount() == 0) return mDefaultInputLocale;

        return mLocales[mCurrentIndex];
    }

    /**
     * Returns the next input locale in the list. Wraps around to the beginning of the
     * list if we're at the end of the list.
     * @return
     */
    public Locale getNextInputLocale() {
        if (getLocaleCount() == 0) return mDefaultInputLocale;

        return mLocales[(mCurrentIndex + 1) % mLocales.length];
    }

    /**
     * Sets the system locale (display UI) used for comparing with the input language.
     * @param locale the locale of the system
     */
    public void setSystemLocale(Locale locale) {
        mSystemLocale = locale;
    }

    /**
     * Returns the system locale.
     * @return the system locale
     */
    public Locale getSystemLocale() {
        return mSystemLocale;
    }

    /**
     * Returns the previous input locale in the list. Wraps around to the end of the
     * list if we're at the beginning of the list.
     * @return
     */
    public Locale getPrevInputLocale() {
        if (getLocaleCount() == 0) return mDefaultInputLocale;

        return mLocales[(mCurrentIndex - 1 + mLocales.length) % mLocales.length];
    }

    public void reset() {
        mCurrentIndex = 0;
    }

    public void next() {
        mCurrentIndex++;
        if (mCurrentIndex >= mLocales.length) mCurrentIndex = 0; // Wrap around
    }

    public void prev() {
        mCurrentIndex--;
        if (mCurrentIndex < 0) mCurrentIndex = mLocales.length - 1; // Wrap around
    }

    public void persist() {
        SharedPreferences sp = PreferenceManager.getDefaultSharedPreferences(mIme);
        Editor editor = sp.edit();
        editor.putString(KP2AKeyboard.PREF_INPUT_LANGUAGE, getInputLanguage());
        SharedPreferencesCompat.apply(editor);
    }

    public static String toTitleCase(String s, Locale locale) {
        if (s.length() == 0) {
            return s;
        }

        return s.toUpperCase(locale).charAt(0) + s.substring(1);
    }
}
