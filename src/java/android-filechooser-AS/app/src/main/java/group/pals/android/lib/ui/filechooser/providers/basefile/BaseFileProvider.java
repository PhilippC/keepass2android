/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers.basefile;

import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileContract.BaseFile;

import java.text.Collator;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.UriMatcher;
import android.database.Cursor;
import android.net.Uri;
import android.util.SparseBooleanArray;

/**
 * Base provider for files.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public abstract class BaseFileProvider extends ContentProvider {

    /*
     * Constants used by the Uri matcher to choose an action based on the
     * pattern of the incoming URI.
     */

    /**
     * The incoming URI matches the directory's contents URI pattern.
     */
    protected static final int URI_DIRECTORY = 1;

    /**
     * The incoming URI matches the single file URI pattern.
     */
    protected static final int URI_FILE = 2;

    /**
     * The incoming URI matches the identification URI pattern.
     */
    protected static final int URI_API = 3;

    /**
     * The incoming URI matches the API command URI pattern.
     */
    protected static final int URI_API_COMMAND = 4;

    /**
     * Check if connection to the file service is ok.
     */
    protected static final int URI_CHECK_CONNECTION = 5;


    /**
     * A {@link UriMatcher} instance.
     */
    protected static final UriMatcher URI_MATCHER = new UriMatcher(
            UriMatcher.NO_MATCH);

    /**
     * Map of task IDs to their interruption signals.
     */
    protected final SparseBooleanArray mMapInterruption = new SparseBooleanArray();
    /**
     * This collator is used to compare file names.
     */
    protected final Collator mCollator = Collator.getInstance();

    @Override
    public boolean onCreate() {
        return true;
    }// onCreate()

    @Override
    public String getType(Uri uri) {
        /*
         * Chooses the MIME type based on the incoming URI pattern.
         */
        switch (URI_MATCHER.match(uri)) {
        case URI_API:
        case URI_API_COMMAND:
            case URI_DIRECTORY :
            case URI_CHECK_CONNECTION:
                return BaseFile.CONTENT_TYPE;

        case URI_FILE:
            return BaseFile.CONTENT_ITEM_TYPE;

        default:
            throw new IllegalArgumentException("UNKNOWN URI " + uri);
        }
    }// getType()

    @Override
    public int delete(Uri uri, String selection, String[] selectionArgs) {
        /*
         * Do nothing.
         */
        return 0;
    }// delete()

    @Override
    public Uri insert(Uri uri, ContentValues values) {
        /*
         * Do nothing.
         */
        return null;
    }// insert()

    @Override
    public Cursor query(Uri uri, String[] projection, String selection,
            String[] selectionArgs, String sortOrder) {
        /*
         * Do nothing.
         */
        return null;
    }// query()

    @Override
    public int update(Uri uri, ContentValues values, String selection,
            String[] selectionArgs) {
        /*
         * Do nothing.
         */
        return 0;
    }// update()

}
