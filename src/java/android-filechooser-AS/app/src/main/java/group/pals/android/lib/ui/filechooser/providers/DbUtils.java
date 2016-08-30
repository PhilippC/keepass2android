/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers;

import android.database.DatabaseUtils;

/**
 * Database utilities.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class DbUtils {

    public static final String DATE_FORMAT = "yyyy:MM:dd'T'kk:mm:ss";
    /**
     * SQLite component FTS3.
     * 
     * @since v4.6 beta
     */
    public static final String SQLITE_FTS3 = "FTS3";
    /**
     * SQLite component FTS4.
     * 
     * @since v4.6 beta
     */
    public static final String SQLITE_FTS4 = "FTS4";

    /**
     * Hidden column of FTS virtual table.
     */
    public static final String SQLITE_FTS_COLUMN_ROW_ID = "rowid";

    /**
     * Joins all columns into one statement.
     * 
     * @param cols
     *            array of columns.
     * @return E.g: "col1,col2,col3"
     */
    public static String joinColumns(String[] cols) {
        if (cols == null)
            return "";

        StringBuffer sb = new StringBuffer();
        for (String col : cols) {
            sb.append(col).append(",");
        }

        return sb.toString().replaceAll(",$", "");
    }// joinColumns()

    /**
     * Formats {@code n} to text to store to database. This method prefixes the
     * output string with {@code "0"} to make sure the results will always have
     * same length (for a {@link Long}). So it will work when comparing
     * different values as text.
     * 
     * @param n
     *            a long value.
     * @return the formatted string.
     */
    public static String formatNumber(long n) {
        return String.format("%020d", n);
    }// formatNumber()

    /**
     * Calls {@link DatabaseUtils#sqlEscapeString(String)}, then removes single
     * quotes at the begin and the end of the returned string.
     * 
     * @param value
     *            the string to escape. If {@code null}, empty string will
     *            return;
     * @return the "raw" escaped-string.
     */
    public static String rawSqlEscapeString(String value) {
        return value == null ? "" : DatabaseUtils.sqlEscapeString(value)
                .replaceFirst("(?msi)^'", "").replaceFirst("(?msi)'$", "");
    }// rawSqlEscapeString()

}
