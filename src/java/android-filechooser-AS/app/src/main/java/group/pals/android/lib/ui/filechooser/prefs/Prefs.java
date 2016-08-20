/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.prefs;

import group.pals.android.lib.ui.filechooser.utils.Sys;
import android.annotation.TargetApi;
import android.content.Context;
import android.content.SharedPreferences;
import android.os.Build;
import android.preference.PreferenceActivity;
import android.preference.PreferenceFragment;
import android.preference.PreferenceManager;

/**
 * Convenient class for working with preferences.
 * 
 * @author Hai Bison
 * @since v4.3 beta
 */
public class Prefs {

    /**
     * This unique ID is used for storing preferences.
     * 
     * @since v4.9 beta
     */
    public static final String UID = "9795e88b-2ab4-4b81-a548-409091a1e0c6";

    /**
     * Generates global preference filename of this library.
     * 
     * @return the global preference filename.
     */
    public static final String genPreferenceFilename() {
        return String.format("%s_%s", Sys.LIB_NAME, UID);
    }

    /**
     * Generates global database filename.
     * 
     * @param name
     *            the database filename.
     * @return the global database filename.
     */
    public static final String genDatabaseFilename(String name) {
        return String.format("%s_%s_%s", Sys.LIB_NAME, UID, name);
    }

    /**
     * Gets new {@link SharedPreferences}
     * 
     * @param context
     *            the context.
     * @return {@link SharedPreferences}
     */
    @TargetApi(Build.VERSION_CODES.HONEYCOMB)
    public static SharedPreferences p(Context context) {
        // always use application context
        return context.getApplicationContext().getSharedPreferences(
                genPreferenceFilename(), Context.MODE_MULTI_PROCESS);
    }

    /**
     * Setup {@code pm} to use global unique filename and global access mode.
     * You must use this method if you let the user change preferences via UI
     * (such as {@link PreferenceActivity}, {@link PreferenceFragment}...).
     * 
     * @param pm
     *            {@link PreferenceManager}.
     * @since v4.9 beta
     */
    @TargetApi(Build.VERSION_CODES.HONEYCOMB)
    public static void setupPreferenceManager(PreferenceManager pm) {
        pm.setSharedPreferencesMode(Context.MODE_MULTI_PROCESS);
        pm.setSharedPreferencesName(genPreferenceFilename());
    }// setupPreferenceManager()

}
