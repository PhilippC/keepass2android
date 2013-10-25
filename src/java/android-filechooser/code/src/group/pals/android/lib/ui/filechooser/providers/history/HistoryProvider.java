/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers.history;

import group.pals.android.lib.ui.filechooser.BuildConfig;
import group.pals.android.lib.ui.filechooser.providers.BaseFileProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.DbUtils;
import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileContract.BaseFile;
import group.pals.android.lib.ui.filechooser.utils.Utils;

import java.util.Arrays;
import java.util.Date;
import java.util.HashMap;
import java.util.Map;

import android.content.ContentProvider;
import android.content.ContentUris;
import android.content.ContentValues;
import android.content.UriMatcher;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.database.MatrixCursor.RowBuilder;
import android.database.SQLException;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteQueryBuilder;
import android.net.Uri;
import android.text.TextUtils;
import android.util.Log;

/**
 * History provider.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class HistoryProvider extends ContentProvider {

    private static final String CLASSNAME = HistoryProvider.class.getName();

    /*
     * Constants used by the Uri matcher to choose an action based on the
     * pattern of the incoming URI.
     */
    /**
     * The incoming URI matches the history URI pattern.
     */
    private static final int URI_HISTORY = 1;

    /**
     * The incoming URI matches the history ID URI pattern.
     */
    private static final int URI_HISTORY_ID = 2;

    /**
     * A {@link UriMatcher} instance.
     */
    private static final UriMatcher URI_MATCHER = new UriMatcher(
            UriMatcher.NO_MATCH);

    private static final Map<String, String> MAP_COLUMNS = new HashMap<String, String>();

    static {
        MAP_COLUMNS
                .put(DbUtils.SQLITE_FTS_COLUMN_ROW_ID,
                        DbUtils.SQLITE_FTS_COLUMN_ROW_ID + " AS "
                                + HistoryContract._ID);
        MAP_COLUMNS.put(HistoryContract.COLUMN_PROVIDER_ID,
                HistoryContract.COLUMN_PROVIDER_ID);
        MAP_COLUMNS.put(HistoryContract.COLUMN_FILE_TYPE,
                HistoryContract.COLUMN_FILE_TYPE);
        MAP_COLUMNS.put(HistoryContract.COLUMN_URI, HistoryContract.COLUMN_URI);
        MAP_COLUMNS.put(HistoryContract.COLUMN_CREATE_TIME,
                HistoryContract.COLUMN_CREATE_TIME);
        MAP_COLUMNS.put(HistoryContract.COLUMN_MODIFICATION_TIME,
                HistoryContract.COLUMN_MODIFICATION_TIME);
    }// static

    private HistoryHelper mHistoryHelper;

    @Override
    public boolean onCreate() {
        mHistoryHelper = new HistoryHelper(getContext());

        URI_MATCHER.addURI(HistoryContract.getAuthority(getContext()),
                HistoryContract.PATH_HISTORY, URI_HISTORY);
        URI_MATCHER.addURI(HistoryContract.getAuthority(getContext()),
                HistoryContract.PATH_HISTORY + "/#", URI_HISTORY_ID);

        return true;
    }// onCreate()

    @Override
    public String getType(Uri uri) {
        /*
         * Chooses the MIME type based on the incoming URI pattern.
         */
        switch (URI_MATCHER.match(uri)) {
        case URI_HISTORY:
            return HistoryContract.CONTENT_TYPE;

        case URI_HISTORY_ID:
            return HistoryContract.CONTENT_ITEM_TYPE;

        default:
            throw new IllegalArgumentException("UNKNOWN URI " + uri);
        }
    }// getType()

    @Override
    public synchronized int delete(Uri uri, String selection,
            String[] selectionArgs) {
        // Opens the database object in "write" mode.
        SQLiteDatabase db = mHistoryHelper.getWritableDatabase();
        String finalWhere;

        int count;

        // Does the delete based on the incoming URI pattern.
        switch (URI_MATCHER.match(uri)) {
        /*
         * If the incoming pattern matches the general pattern for history
         * items, does a delete based on the incoming "where" columns and
         * arguments.
         */
        case URI_HISTORY:
            count = db.delete(HistoryContract.TABLE_NAME, selection,
                    selectionArgs);
            break;// URI_HISTORY

        /*
         * If the incoming URI matches a single note ID, does the delete based
         * on the incoming data, but modifies the where clause to restrict it to
         * the particular history item ID.
         */
        case URI_HISTORY_ID:
            /*
             * Starts a final WHERE clause by restricting it to the desired
             * history item ID.
             */
            finalWhere = DbUtils.SQLITE_FTS_COLUMN_ROW_ID + " = "
                    + uri.getLastPathSegment();

            /*
             * If there were additional selection criteria, append them to the
             * final WHERE clause
             */
            if (selection != null)
                finalWhere = finalWhere + " AND " + selection;

            // Performs the delete.
            count = db.delete(HistoryContract.TABLE_NAME, finalWhere,
                    selectionArgs);
            break;// URI_HISTORY_ID

        // If the incoming pattern is invalid, throws an exception.
        default:
            throw new IllegalArgumentException("UNKNOWN URI " + uri);
        }

        /*
         * Gets a handle to the content resolver object for the current context,
         * and notifies it that the incoming URI changed. The object passes this
         * along to the resolver framework, and observers that have registered
         * themselves for the provider are notified.
         */
        getContext().getContentResolver().notifyChange(uri, null);

        // Returns the number of rows deleted.
        return count;
    }// delete()

    @Override
    public synchronized Uri insert(Uri uri, ContentValues values) {
        /*
         * Validates the incoming URI. Only the full provider URI is allowed for
         * inserts.
         */
        if (URI_MATCHER.match(uri) != URI_HISTORY)
            throw new IllegalArgumentException("UNKNOWN URI " + uri);

        // Gets the current time in milliseconds
        long now = new Date().getTime();

        /*
         * If the values map doesn't contain the creation date/ modification
         * date, sets the value to the current time.
         */
        for (String col : new String[] { HistoryContract.COLUMN_CREATE_TIME,
                HistoryContract.COLUMN_MODIFICATION_TIME })
            if (!values.containsKey(col))
                values.put(col, DbUtils.formatNumber(now));

        // Opens the database object in "write" mode.
        SQLiteDatabase db = mHistoryHelper.getWritableDatabase();

        // Performs the insert and returns the ID of the new note.
        long rowId = db.insert(HistoryContract.TABLE_NAME, null, values);

        // If the insert succeeded, the row ID exists.
        if (rowId > 0) {
            /*
             * Creates a URI with the note ID pattern and the new row ID
             * appended to it.
             */
            Uri noteUri = ContentUris.withAppendedId(
                    HistoryContract.genContentIdUriBase(getContext()), rowId);

            /*
             * Notifies observers registered against this provider that the data
             * changed.
             */
            getContext().getContentResolver().notifyChange(noteUri, null);
            return noteUri;
        }

        /*
         * If the insert didn't succeed, then the rowID is <= 0. Throws an
         * exception.
         */
        throw new SQLException("Failed to insert row into " + uri);
    }// insert()

    @Override
    public synchronized Cursor query(Uri uri, String[] projection,
            String selection, String[] selectionArgs, String sortOrder) {
        if (Utils.doLog())
            Log.d(CLASSNAME, String.format(
                    "query() >> uri = %s, selection = %s, sortOrder = %s", uri,
                    selection, sortOrder));

        SQLiteQueryBuilder qb = new SQLiteQueryBuilder();
        qb.setTables(HistoryContract.TABLE_NAME);
        qb.setProjectionMap(MAP_COLUMNS);

        SQLiteDatabase db = null;
        Cursor cursor = null;

        /*
         * Choose the projection and adjust the "where" clause based on URI
         * pattern-matching.
         */
        switch (URI_MATCHER.match(uri)) {
        case URI_HISTORY: {
            if (Arrays.equals(projection,
                    new String[] { HistoryContract._COUNT })) {
                db = mHistoryHelper.getReadableDatabase();
                cursor = db.rawQuery(
                        String.format(
                                "SELECT COUNT(*) AS %s FROM %s %s",
                                HistoryContract._COUNT,
                                HistoryContract.TABLE_NAME,
                                selection != null ? String.format("WHERE %s",
                                        selection) : "").trim(), null);
            }

            break;
        }// URI_HISTORY

        /*
         * If the incoming URI is for a single history item identified by its
         * ID, chooses the history item ID projection, and appends
         * "_ID = <history-item-ID>" to the where clause, so that it selects
         * that single history item.
         */
        case URI_HISTORY_ID: {
            qb.appendWhere(DbUtils.SQLITE_FTS_COLUMN_ROW_ID + " = "
                    + uri.getLastPathSegment());

            break;
        }// URI_HISTORY_ID

        default:
            throw new IllegalArgumentException("UNKNOWN URI " + uri);
        }

        if (TextUtils.isEmpty(sortOrder))
            sortOrder = HistoryContract.DEFAULT_SORT_ORDER;

        /*
         * Opens the database object in "read" mode, since no writes need to be
         * done.
         */
        if (Utils.doLog())
            Log.d(CLASSNAME,
                    String.format("Going to SQLiteQueryBuilder >> db = %s", db));
        if (db == null) {
            db = mHistoryHelper.getReadableDatabase();
            /*
             * Performs the query. If no problems occur trying to read the
             * database, then a Cursor object is returned; otherwise, the cursor
             * variable contains null. If no records were selected, then the
             * Cursor object is empty, and Cursor.getCount() returns 0.
             */
            cursor = qb.query(db, projection, selection, selectionArgs, null,
                    null, sortOrder);
        }

        cursor = appendNameAndRealUri(cursor);
        cursor.setNotificationUri(getContext().getContentResolver(), uri);
        return cursor;
    }// query()

    @Override
    public synchronized int update(Uri uri, ContentValues values,
            String selection, String[] selectionArgs) {
        // Opens the database object in "write" mode.
        SQLiteDatabase db = mHistoryHelper.getWritableDatabase();

        int count;
        String finalWhere;

        // Does the update based on the incoming URI pattern
        switch (URI_MATCHER.match(uri)) {
        /*
         * If the incoming URI matches the general history items pattern, does
         * the update based on the incoming data.
         */
        case URI_HISTORY:
            // Does the update and returns the number of rows updated.
            count = db.update(HistoryContract.TABLE_NAME, values, selection,
                    selectionArgs);
            break;

        /*
         * If the incoming URI matches a single history item ID, does the update
         * based on the incoming data, but modifies the where clause to restrict
         * it to the particular history item ID.
         */
        case URI_HISTORY_ID:
            /*
             * Starts creating the final WHERE clause by restricting it to the
             * incoming item ID.
             */
            finalWhere = DbUtils.SQLITE_FTS_COLUMN_ROW_ID + " = "
                    + uri.getLastPathSegment();

            /*
             * If there were additional selection criteria, append them to the
             * final WHERE clause
             */
            if (selection != null)
                finalWhere = finalWhere + " AND " + selection;

            // Does the update and returns the number of rows updated.
            count = db.update(HistoryContract.TABLE_NAME, values, finalWhere,
                    selectionArgs);
            break;

        // If the incoming pattern is invalid, throws an exception.
        default:
            throw new IllegalArgumentException("UNKNOWN URI " + uri);
        }

        /*
         * Gets a handle to the content resolver object for the current context,
         * and notifies it that the incoming URI changed. The object passes this
         * along to the resolver framework, and observers that have registered
         * themselves for the provider are notified.
         */
        getContext().getContentResolver().notifyChange(uri, null);

        // Returns the number of rows updated.
        return count;
    }// update()

    private static final String[] ADDITIONAL_COLUMNS = { BaseFile.COLUMN_NAME,
            BaseFile.COLUMN_REAL_URI };

    /**
     * Appends file name and real URI into {@code cursor}.
     * 
     * @param cursor
     *            the original cursor. It will be closed when done.
     * @return the new cursor.
     */
    private Cursor appendNameAndRealUri(Cursor cursor) {
        if (cursor == null || cursor.getCount() == 0)
            return cursor;

        final int colUri = cursor.getColumnIndex(HistoryContract.COLUMN_URI);
        if (colUri < 0)
            return cursor;

        String[] columns = new String[cursor.getColumnCount()
                + ADDITIONAL_COLUMNS.length];
        System.arraycopy(cursor.getColumnNames(), 0, columns, 0,
                cursor.getColumnCount());
        System.arraycopy(ADDITIONAL_COLUMNS, 0, columns,
                cursor.getColumnCount(), ADDITIONAL_COLUMNS.length);

        MatrixCursor result = new MatrixCursor(columns);
        if (cursor.moveToFirst()) {
            do {
                RowBuilder builder = result.newRow();

                Cursor fileInfo = null;
                for (int i = 0; i < cursor.getColumnCount(); i++) {
                    String data = cursor.getString(i);
                    builder.add(data);

                    if (i == colUri)
                        fileInfo = getContext().getContentResolver().query(
                                Uri.parse(data), null, null, null, null);
                }

                if (fileInfo != null) {
                    if (fileInfo.moveToFirst()) {
                        builder.add(BaseFileProviderUtils.getFileName(fileInfo));
                        builder.add(BaseFileProviderUtils.getRealUri(fileInfo)
                                .toString());
                    }
                    fileInfo.close();
                }
            } while (cursor.moveToNext());
        }// if

        cursor.close();

        return result;
    }// appendNameAndRealUri()

}
