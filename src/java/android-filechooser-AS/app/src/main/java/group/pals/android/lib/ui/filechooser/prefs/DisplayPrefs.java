/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.prefs;

import group.pals.android.lib.ui.filechooser.FileChooserActivity.ViewType;
import group.pals.android.lib.ui.filechooser.R;
import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileContract.BaseFile;
import android.content.Context;

/**
 * Display preferences.
 * 
 * @author Hai Bison
 * @since v4.3 beta
 */
public class DisplayPrefs extends Prefs {

    /**
     * Delay time for waiting for other threads inside a thread... This is in
     * milliseconds.
     */
    public static final int DELAY_TIME_WAITING_THREADS = 10;

    /**
     * Delay time for waiting for very short animation, in milliseconds.
     */
    public static final int DELAY_TIME_FOR_VERY_SHORT_ANIMATION = 199;

    /**
     * Delay time for waiting for short animation, in milliseconds.
     */
    public static final int DELAY_TIME_FOR_SHORT_ANIMATION = 499;

    /**
     * Delay time for waiting for simple animation, in milliseconds.
     */
    public static final int DELAY_TIME_FOR_SIMPLE_ANIMATION = 999;

    /**
     * Gets view type.
     * 
     * @param c
     *            {@link Context}
     * @return {@link ViewType}
     */
    public static ViewType getViewType(Context c) {
        return ViewType.LIST.ordinal() == p(c).getInt(
                c.getString(R.string.afc_pkey_display_view_type),
                c.getResources().getInteger(
                        R.integer.afc_pkey_display_view_type_def)) ? ViewType.LIST
                : ViewType.GRID;
    }

    /**
     * Sets view type.
     * 
     * @param c
     *            {@link Context}
     * @param v
     *            {@link ViewType}, if {@code null}, default value will be used.
     */
    public static void setViewType(Context c, ViewType v) {
        String key = c.getString(R.string.afc_pkey_display_view_type);
        if (v == null)
            p(c).edit()
                    .putInt(key,
                            c.getResources().getInteger(
                                    R.integer.afc_pkey_display_view_type_def))
                    .commit();
        else
            p(c).edit().putInt(key, v.ordinal()).commit();
    }

    /**
     * Gets sort type.
     * 
     * @param c
     *            {@link Context}
     * @return one of {@link BaseFile#SORT_BY_MODIFICATION_TIME},
     *         {@link BaseFile#SORT_BY_NAME}, {@link BaseFile#SORT_BY_SIZE}.
     */
    public static int getSortType(Context c) {
        return p(c).getInt(
                c.getString(R.string.afc_pkey_display_sort_type),
                c.getResources().getInteger(
                        R.integer.afc_pkey_display_sort_type_def));
    }

    /**
     * Sets {@link SortType}
     * 
     * @param c
     *            {@link Context}
     * @param v
     *            one of {@link BaseFile#SORT_BY_MODIFICATION_TIME},
     *            {@link BaseFile#SORT_BY_NAME}, {@link BaseFile#SORT_BY_SIZE}.,
     *            if {@code null}, default value will be used.
     */
    public static void setSortType(Context c, Integer v) {
        String key = c.getString(R.string.afc_pkey_display_sort_type);
        if (v == null)
            p(c).edit()
                    .putInt(key,
                            c.getResources().getInteger(
                                    R.integer.afc_pkey_display_sort_type_def))
                    .commit();
        else
            p(c).edit().putInt(key, v).commit();
    }

    /**
     * Gets sort ascending.
     * 
     * @param c
     *            {@link Context}
     * @return {@code true} if sort is ascending, {@code false} otherwise.
     */
    public static boolean isSortAscending(Context c) {
        return p(c).getBoolean(
                c.getString(R.string.afc_pkey_display_sort_ascending),
                c.getResources().getBoolean(
                        R.bool.afc_pkey_display_sort_ascending_def));
    }

    /**
     * Sets sort ascending.
     * 
     * @param c
     *            {@link Context}
     * @param v
     *            {@link Boolean}, if {@code null}, default value will be used.
     */
    public static void setSortAscending(Context c, Boolean v) {
        if (v == null)
            v = c.getResources().getBoolean(
                    R.bool.afc_pkey_display_sort_ascending_def);
        p(c).edit()
                .putBoolean(
                        c.getString(R.string.afc_pkey_display_sort_ascending),
                        v).commit();
    }

    /**
     * Checks setting of showing time for old days in this year. Default is
     * {@code false}.
     * 
     * @param c
     *            {@link Context}.
     * @return {@code true} or {@code false}.
     * @since v4.7 beta
     */
    public static boolean isShowTimeForOldDaysThisYear(Context c) {
        return p(c)
                .getBoolean(
                        c.getString(R.string.afc_pkey_display_show_time_for_old_days_this_year),
                        c.getResources()
                                .getBoolean(
                                        R.bool.afc_pkey_display_show_time_for_old_days_this_year_def));
    }

    /**
     * Enables or disables showing time of old days in this year.
     * 
     * @param c
     *            {@link Context}.
     * @param v
     *            your preferred flag. If {@code null}, default will be used (
     *            {@code false}).
     * @since v4.7 beta
     */
    public static void setShowTimeForOldDaysThisYear(Context c, Boolean v) {
        if (v == null)
            v = c.getResources()
                    .getBoolean(
                            R.bool.afc_pkey_display_show_time_for_old_days_this_year_def);
        p(c).edit()
                .putBoolean(
                        c.getString(R.string.afc_pkey_display_show_time_for_old_days_this_year),
                        v).commit();
    }

    /**
     * Checks setting of showing time for old days in last year and older.
     * Default is {@code false}.
     * 
     * @param c
     *            {@link Context}.
     * @return {@code true} or {@code false}.
     * @since v4.7 beta
     */
    public static boolean isShowTimeForOldDays(Context c) {
        return p(c).getBoolean(
                c.getString(R.string.afc_pkey_display_show_time_for_old_days),
                c.getResources().getBoolean(
                        R.bool.afc_pkey_display_show_time_for_old_days_def));
    }

    /**
     * Enables or disables showing time of old days in last year and older.
     * 
     * @param c
     *            {@link Context}.
     * @param v
     *            your preferred flag. If {@code null}, default will be used (
     *            {@code false}).
     * @since v4.7 beta
     */
    public static void setShowTimeForOldDays(Context c, Boolean v) {
        if (v == null)
            v = c.getResources().getBoolean(
                    R.bool.afc_pkey_display_show_time_for_old_days_def);
        p(c).edit()
                .putBoolean(
                        c.getString(R.string.afc_pkey_display_show_time_for_old_days),
                        v).commit();
    }

    /**
     * Checks if remembering last location is enabled or not.
     * 
     * @param c
     *            {@link Context}.
     * @return {@code true} if remembering last location is enabled.
     * @since v4.7 beta
     */
    public static boolean isRememberLastLocation(Context c) {
        return false; //KP2A: don't allow to remember because of different protocols
    }

    /**
     * Enables or disables remembering last location.
     * 
     * @param c
     *            {@link Context}.
     * @param v
     *            your preferred flag. If {@code null}, default will be used (
     *            {@code true}).
     * @since v4.7 beta
     */
    public static void setRememberLastLocation(Context c, Boolean v) {
        if (v == null)
            v = c.getResources().getBoolean(
                    R.bool.afc_pkey_display_remember_last_location_def);
        p(c).edit()
                .putBoolean(
                        c.getString(R.string.afc_pkey_display_remember_last_location),
                        v).commit();
    }

    /**
     * Gets last location.
     * 
     * @param c
     *            {@link Context}.
     * @return the last location, or {@code null} if not available.
     * @since v4.7 beta
     */
    public static String getLastLocation(Context c) {
        return p(c).getString(
                c.getString(R.string.afc_pkey_display_last_location), null);
    }

    /**
     * Sets last location.
     * 
     * @param c
     *            {@link Context}.
     * @param v
     *            the last location.
     */
    public static void setLastLocation(Context c, String v) {
        p(c).edit()
                .putString(
                        c.getString(R.string.afc_pkey_display_last_location), v)
                .commit();
    }

    /*
     * HELPER CLASSES
     */

    /**
     * File time display options.
     * 
     * @author Hai Bison
     * @see DisplayPrefs#isShowTimeForOldDaysThisYear(Context)
     * @see DisplayPrefs#isShowTimeForOldDays(Context)
     * @since v4.9 beta
     */
    public static class FileTimeDisplay {

        public boolean showTimeForOldDaysThisYear;
        public boolean showTimeForOldDays;

        /**
         * Creates new instance.
         * 
         * @param showTimeForOldDaysThisYear
         * @param showTimeForOldDays
         */
        public FileTimeDisplay(boolean showTimeForOldDaysThisYear,
                boolean showTimeForOldDays) {
            this.showTimeForOldDaysThisYear = showTimeForOldDaysThisYear;
            this.showTimeForOldDays = showTimeForOldDays;
        }// FileTimeDisplay()
    }// FileTimeDisplay

}
