/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers.history;

import group.pals.android.lib.ui.filechooser.prefs.Prefs;
import group.pals.android.lib.ui.filechooser.providers.DbUtils;
import android.content.Context;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;
import android.os.Build;

/**
 * SQLite helper for history database.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class HistoryHelper extends SQLiteOpenHelper {

    private static final String DB_FILENAME = "History.sqlite";
    private static final int DB_VERSION = 1;

    /**
     * @since v5.1 beta
     */
    private static final String PATTERN_DB_CREATOR_V3 = String
            .format("CREATE VIRTUAL TABLE " + HistoryContract.TABLE_NAME
                    + " USING %%s(" + HistoryContract.COLUMN_CREATE_TIME + ","
                    + HistoryContract.COLUMN_MODIFICATION_TIME + ","
                    + HistoryContract.COLUMN_PROVIDER_ID + ","
                    + HistoryContract.COLUMN_FILE_TYPE + ","
                    + HistoryContract.COLUMN_URI + ",tokenize=porter);");

    public HistoryHelper(Context context) {
        // always use application context
        super(context.getApplicationContext(), Prefs
                .genDatabaseFilename(DB_FILENAME), null, DB_VERSION);
    }// HistoryHelper()

    @Override
    public void onCreate(SQLiteDatabase db) {
        db.execSQL(String
                .format(PATTERN_DB_CREATOR_V3,
                        Build.VERSION.SDK_INT < Build.VERSION_CODES.HONEYCOMB ? DbUtils.SQLITE_FTS3
                                : DbUtils.SQLITE_FTS4));
    }// onCreate()

    @Override
    public void onUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
        // TODO
    }// onUpgrade()

}
