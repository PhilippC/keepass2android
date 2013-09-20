/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers.bookmark;

import group.pals.android.lib.ui.filechooser.providers.DbUtils;

import java.util.Arrays;
import java.util.Date;
import java.util.HashMap;
import java.util.Map;

import android.content.ContentProvider;
import android.content.ContentUris;
import android.content.ContentValues;
import android.content.UriMatcher;
import android.database.Cursor;
import android.database.SQLException;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteQueryBuilder;
import android.net.Uri;
import android.text.TextUtils;

/**
 * Bookmark provider.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class BookmarkProvider extends ContentProvider {

    @SuppressWarnings("unused")
    private static final String CLASSNAME = BookmarkProvider.class.getName();

    /*
     * Constants used by the Uri matcher to choose an action based on the
     * pattern of the incoming URI.
     */

    /**
     * The incoming URI matches the Bookmark URI pattern.
     */
    private static final int URI_BOOKMARKS = 1;

    /**
     * The incoming URI matches the Bookmark ID URI pattern.
     */
    private static final int URI_BOOKMARK_ID = 2;

    /**
     * A {@link UriMatcher} instance.
     */
    private static final UriMatcher URI_MATCHER = new UriMatcher(
            UriMatcher.NO_MATCH);

    private static final Map<String, String> MAP_COLUMNS = new HashMap<String, String>();

    static {
        MAP_COLUMNS.put(DbUtils.SQLITE_FTS_COLUMN_ROW_ID,
                DbUtils.SQLITE_FTS_COLUMN_ROW_ID + " AS "
                        + BookmarkContract._ID);
        MAP_COLUMNS.put(BookmarkContract.COLUMN_NAME,
                BookmarkContract.COLUMN_NAME);
        MAP_COLUMNS.put(BookmarkContract.COLUMN_PROVIDER_ID,
                BookmarkContract.COLUMN_PROVIDER_ID);
        MAP_COLUMNS.put(BookmarkContract.COLUMN_URI,
                BookmarkContract.COLUMN_URI);
        MAP_COLUMNS.put(BookmarkContract.COLUMN_CREATE_TIME,
                BookmarkContract.COLUMN_CREATE_TIME);
        MAP_COLUMNS.put(BookmarkContract.COLUMN_MODIFICATION_TIME,
                BookmarkContract.COLUMN_MODIFICATION_TIME);
    }// static

    private BookmarkHelper mBookmarkHelper;

    @Override
    public boolean onCreate() {
        mBookmarkHelper = new BookmarkHelper(getContext());

        URI_MATCHER.addURI(BookmarkContract.getAuthority(getContext()),
                BookmarkContract.PATH_BOOKMARKS, URI_BOOKMARKS);
        URI_MATCHER.addURI(BookmarkContract.getAuthority(getContext()),
                BookmarkContract.PATH_BOOKMARKS + "/#", URI_BOOKMARK_ID);

        return true;
    }// onCreate()

    @Override
    public String getType(Uri uri) {
        /*
         * Chooses the MIME type based on the incoming URI pattern.
         */
        switch (URI_MATCHER.match(uri)) {
        case URI_BOOKMARKS:
            return BookmarkContract.CONTENT_TYPE;

        case URI_BOOKMARK_ID:
            return BookmarkContract.CONTENT_ITEM_TYPE;

        default:
            throw new IllegalArgumentException("UNKNOWN URI " + uri);
        }
    }// getType()

    @Override
    public synchronized int delete(Uri uri, String selection,
            String[] selectionArgs) {
        // Opens the database object in "write" mode.
        SQLiteDatabase db = mBookmarkHelper.getWritableDatabase();
        String finalWhere;

        int count;

        // Does the delete based on the incoming URI pattern.
        switch (URI_MATCHER.match(uri)) {
        /*
         * If the incoming pattern matches the general pattern for Bookmark
         * items, does a delete based on the incoming "where" columns and
         * arguments.
         */
        case URI_BOOKMARKS: {
            count = db.delete(BookmarkContract.TABLE_NAME, selection,
                    selectionArgs);
            break;
        }// URI_BOOKMARKS

        /*
         * If the incoming URI matches a single note ID, does the delete based
         * on the incoming data, but modifies the where clause to restrict it to
         * the particular Bookmark item ID.
         */
        case URI_BOOKMARK_ID: {
            /*
             * Starts a final WHERE clause by restricting it to the desired
             * Bookmark item ID.
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
            count = db.delete(BookmarkContract.TABLE_NAME, finalWhere,
                    selectionArgs);
            break;
        }// URI_BOOKMARK_ID

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
        if (URI_MATCHER.match(uri) != URI_BOOKMARKS)
            throw new IllegalArgumentException("UNKNOWN URI " + uri);

        // Gets the current time in milliseconds
        long now = new Date().getTime();

        /*
         * If the values map doesn't contain the creation date/ modification
         * date, sets the value to the current time.
         */
        for (String col : new String[] { BookmarkContract.COLUMN_CREATE_TIME,
                BookmarkContract.COLUMN_MODIFICATION_TIME })
            if (!values.containsKey(col))
                values.put(col, DbUtils.formatNumber(now));

        // Opens the database object in "write" mode.
        SQLiteDatabase db = mBookmarkHelper.getWritableDatabase();

        // Performs the insert and returns the ID of the new note.
        long rowId = db.insert(BookmarkContract.TABLE_NAME, null, values);

        // If the insert succeeded, the row ID exists.
        if (rowId > 0) {
            /*
             * Creates a URI with the note ID pattern and the new row ID
             * appended to it.
             */
            Uri noteUri = ContentUris.withAppendedId(
                    BookmarkContract.genContentIdUriBase(getContext()), rowId);

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
        SQLiteQueryBuilder qb = new SQLiteQueryBuilder();
        qb.setTables(BookmarkContract.TABLE_NAME);
        qb.setProjectionMap(MAP_COLUMNS);

        SQLiteDatabase db = null;
        Cursor cursor = null;

        /*
         * Choose the projection and adjust the "where" clause based on URI
         * pattern-matching.
         */
        switch (URI_MATCHER.match(uri)) {
        case URI_BOOKMARKS: {
            if (Arrays.equals(projection,
                    new String[] { BookmarkContract._COUNT })) {
                db = mBookmarkHelper.getReadableDatabase();
                cursor = db.rawQuery(
                        String.format(
                                "SELECT COUNT(*) AS %s FROM %s %s",
                                BookmarkContract._COUNT,
                                BookmarkContract.TABLE_NAME,
                                selection != null ? String.format("WHERE %s",
                                        selection) : "").trim(), null);
            }

            break;
        }// URI_BOOKMARKS

        /*
         * If the incoming URI is for a single Bookmark item identified by its
         * ID, chooses the Bookmark item ID projection, and appends
         * "_ID = <history-item-ID>" to the where clause, so that it selects
         * that single Bookmark item.
         */
        case URI_BOOKMARK_ID: {
            qb.appendWhere(DbUtils.SQLITE_FTS_COLUMN_ROW_ID + " = "
                    + uri.getLastPathSegment());
            break;
        }// URI_BOOKMARK_ID

        default:
            throw new IllegalArgumentException("UNKNOWN URI " + uri);
        }

        if (TextUtils.isEmpty(sortOrder))
            sortOrder = BookmarkContract.DEFAULT_SORT_ORDER;

        if (db == null) {
            /*
             * Opens the database object in "read" mode, since no writes need to
             * be done.
             */
            db = mBookmarkHelper.getReadableDatabase();
            /*
             * Performs the query. If no problems occur trying to read the
             * database, then a Cursor object is returned; otherwise, the cursor
             * variable contains null. If no records were selected, then the
             * Cursor object is empty, and Cursor.getCount() returns 0.
             */
            cursor = qb.query(db, projection, selection, selectionArgs, null,
                    null, sortOrder);
        }

        /*
         * Tells the Cursor what URI to watch, so it knows when its source data
         * changes.
         */
        cursor.setNotificationUri(getContext().getContentResolver(), uri);
        return cursor;
    }// query()

    @Override
    public synchronized int update(Uri uri, ContentValues values,
            String selection, String[] selectionArgs) {
        // Opens the database object in "write" mode.
        SQLiteDatabase db = mBookmarkHelper.getWritableDatabase();
        int count;
        String finalWhere;

        // Does the update based on the incoming URI pattern
        switch (URI_MATCHER.match(uri)) {
        /*
         * If the incoming URI matches the general Bookmark items pattern, does
         * the update based on the incoming data.
         */
        case URI_BOOKMARKS: {
            // Does the update and returns the number of rows updated.
            count = db.update(BookmarkContract.TABLE_NAME, values, selection,
                    selectionArgs);
            break;
        }// URI_BOOKMARKS

        /*
         * If the incoming URI matches a single Bookmark item ID, does the
         * update based on the incoming data, but modifies the where clause to
         * restrict it to the particular history item ID.
         */
        case URI_BOOKMARK_ID: {
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
            count = db.update(BookmarkContract.TABLE_NAME, values, finalWhere,
                    selectionArgs);
            break;
        }// URI_BOOKMARK_ID

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
}
