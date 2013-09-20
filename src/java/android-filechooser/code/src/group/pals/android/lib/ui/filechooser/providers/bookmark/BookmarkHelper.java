/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers.bookmark;

import group.pals.android.lib.ui.filechooser.prefs.Prefs;
import group.pals.android.lib.ui.filechooser.providers.DbUtils;
import android.content.Context;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;
import android.os.Build;

/**
 * Database for bookmark.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class BookmarkHelper extends SQLiteOpenHelper {

    @SuppressWarnings("unused")
    private static final String CLASSNAME = BookmarkHelper.class.getName();

    private static final String DB_FILENAME = "Bookmarks.sqlite";
    private static final int DB_VERSION = 1;

    // Database creation SQL statements

    /**
     * @since v5.1 beta
     */
    private static final String PATTERN_DB_CREATOR = String
            .format("CREATE VIRTUAL TABLE " + BookmarkContract.TABLE_NAME
                    + " USING %%s(" + BookmarkContract.COLUMN_CREATE_TIME + ","
                    + BookmarkContract.COLUMN_MODIFICATION_TIME + ","
                    + BookmarkContract.COLUMN_PROVIDER_ID + ","
                    + BookmarkContract.COLUMN_URI + ","
                    + BookmarkContract.COLUMN_NAME + ",tokenize=porter);");

    public BookmarkHelper(Context context) {
        // always use application context
        super(context.getApplicationContext(), Prefs.genDatabaseFilename(
                context, DB_FILENAME), null, DB_VERSION);
    }// BookmarkHelper()

    @Override
    public void onCreate(SQLiteDatabase db) {
        db.execSQL(String
                .format(PATTERN_DB_CREATOR,
                        Build.VERSION.SDK_INT < Build.VERSION_CODES.HONEYCOMB ? DbUtils.SQLITE_FTS3
                                : DbUtils.SQLITE_FTS4));
    }// onCreate()

    @Override
    public void onUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
        // TODO
    }// onUpgrade()
}
