/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils;

import group.pals.android.lib.ui.filechooser.R;
import group.pals.android.lib.ui.filechooser.prefs.DisplayPrefs.FileTimeDisplay;

import java.util.Calendar;

import android.content.Context;

/**
 * Date utilities.
 * 
 * @author Hai Bison
 * @since v4.7 beta
 */
public class DateUtils {

    /**
     * Used with format methods of {@link android.text.format.DateUtils}. For
     * example: "10:01 AM".
     */
    @SuppressWarnings("deprecation")
    public static final int FORMAT_SHORT_TIME = android.text.format.DateUtils.FORMAT_12HOUR
            | android.text.format.DateUtils.FORMAT_SHOW_TIME;

    /**
     * Used with format methods of {@link android.text.format.DateUtils}. For
     * example: "Oct 01".
     */
    public static final int FORMAT_MONTH_AND_DAY = android.text.format.DateUtils.FORMAT_ABBREV_MONTH
            | android.text.format.DateUtils.FORMAT_SHOW_DATE
            | android.text.format.DateUtils.FORMAT_NO_YEAR;

    /**
     * Used with format methods of {@link android.text.format.DateUtils}. For
     * example: "2012".
     */
    public static final int FORMAT_YEAR = android.text.format.DateUtils.FORMAT_SHOW_YEAR;

    /**
     * Formats date.
     * 
     * @param context
     *            {@link Context}.
     * @param millis
     *            time in milliseconds.
     * @param fileTimeDisplay
     *            {@link FileTimeDisplay}.
     * @return the formatted string
     */
    public static String formatDate(Context context, long millis,
            FileTimeDisplay fileTimeDisplay) {
        Calendar cal = Calendar.getInstance();
        cal.setTimeInMillis(millis);
        return formatDate(context, cal, fileTimeDisplay);
    }// formatDate()

    /**
     * Formats date.
     * 
     * @param context
     *            {@link Context}.
     * @param date
     *            {@link Calendar}.
     * @param fileTimeDisplay
     *            {@link FileTimeDisplay}.
     * @return the formatted string, for local human reading.
     */
    public static String formatDate(Context context, Calendar date,
            FileTimeDisplay fileTimeDisplay) {
        final Calendar yesterday = Calendar.getInstance();
        yesterday.add(Calendar.DAY_OF_YEAR, -1);

        String res;

        if (android.text.format.DateUtils.isToday(date.getTimeInMillis())) {
            res = android.text.format.DateUtils.formatDateTime(context,
                    date.getTimeInMillis(), FORMAT_SHORT_TIME);
        }// today
        else if (date.get(Calendar.YEAR) == yesterday.get(Calendar.YEAR)
                && date.get(Calendar.DAY_OF_YEAR) == yesterday
                        .get(Calendar.DAY_OF_YEAR)) {
            res = String.format(
                    "%s, %s",
                    context.getString(R.string.afc_yesterday),
                    android.text.format.DateUtils.formatDateTime(context,
                            date.getTimeInMillis(), FORMAT_SHORT_TIME));
        }// yesterday
        else if (date.get(Calendar.YEAR) == yesterday.get(Calendar.YEAR)) {
            if (fileTimeDisplay.showTimeForOldDaysThisYear)
                res = android.text.format.DateUtils.formatDateTime(context,
                        date.getTimeInMillis(), FORMAT_SHORT_TIME
                                | FORMAT_MONTH_AND_DAY);
            else
                res = android.text.format.DateUtils.formatDateTime(context,
                        date.getTimeInMillis(), FORMAT_MONTH_AND_DAY);
        }// this year
        else {
            if (fileTimeDisplay.showTimeForOldDays)
                res = android.text.format.DateUtils.formatDateTime(context,
                        date.getTimeInMillis(), FORMAT_SHORT_TIME
                                | FORMAT_MONTH_AND_DAY | FORMAT_YEAR);
            else
                res = android.text.format.DateUtils.formatDateTime(context,
                        date.getTimeInMillis(), FORMAT_MONTH_AND_DAY
                                | FORMAT_YEAR);
        }// other years (maybe older or newer than this year)

        return res;
    }// formatDate()

}
