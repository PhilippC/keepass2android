/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers;

import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileContract.BaseFile;
import group.pals.android.lib.ui.filechooser.providers.localfile.LocalFileProvider;
import group.pals.android.lib.ui.filechooser.utils.ui.Ui;

import java.io.File;
import java.util.HashMap;
import java.util.Map;
import java.util.Map.Entry;

import android.content.Context;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.Bundle;

/**
 * Utilities for base file provider.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class BaseFileProviderUtils {

    @SuppressWarnings("unused")
    private static final String CLASSNAME = BaseFileProviderUtils.class
            .getName();

    /**
     * Map of provider ID to its authority.
     * <p/>
     * <b>Note for developers:</b> If you provide your own provider, use
     * {@link #registerProviderInfo(String, String)} to register it..
     */
    private static final Map<String, Bundle> MAP_PROVIDER_INFO = new HashMap<String, Bundle>();

    private static final String COLUMN_AUTHORITY = "authority";

    /**
     * Registers a file provider.
     * 
     * @param id
     *            the provider ID. It should be a UUID.
     * @param authority
     *            the autority.
     */
    public static void registerProviderInfo(String id, String authority) {
        Bundle bundle = new Bundle();
        bundle.putString(COLUMN_AUTHORITY, authority);
        MAP_PROVIDER_INFO.put(id, bundle);
    }// registerProviderInfo()

    /**
     * Gets provider authority from its ID.
     * 
     * @param providerId
     *            the provider ID.
     * @return the provider authority, or {@code null} if not available.
     */
    public static String getProviderAuthority(String providerId) {
        return MAP_PROVIDER_INFO.get(providerId).getString(COLUMN_AUTHORITY);
    }// getProviderAuthority()

    /**
     * Gets provider ID from its authority.
     * 
     * @param authority
     *            the provider authority.
     * @return the provider ID, or {@code null} if not available.
     */
    public static String getProviderId(String authority) {
        for (Entry<String, Bundle> entry : MAP_PROVIDER_INFO.entrySet())
            if (entry.getValue().getString(COLUMN_AUTHORITY).equals(authority))
                return entry.getKey();
        return null;
    }// getProviderId()

    /**
     * Gets provider name from its ID.
     * <p/>
     * <b>Note:</b> You should always use the method
     * {@link #getProviderName(Context, String)} rather than this one whenever
     * possible. Because this method does not guarantee the result.
     * 
     * @param providerId
     *            the provider ID.
     * @return the provider name, or {@code null} if not available.
     */
    private static String getProviderName(String providerId) {
        return MAP_PROVIDER_INFO.get(providerId).getString(
                BaseFile.COLUMN_PROVIDER_NAME);
    }// getProviderName()

    /**
     * Gets provider name from its ID.
     * 
     * @param context
     *            {@link Context}.
     * @param providerId
     *            the provider ID.
     * @return the provider name, can be {@code null} if not provided.
     */
    public static String getProviderName(Context context, String providerId) {
        if (getProviderAuthority(providerId) == null)
            return null;

        String result = getProviderName(providerId);

        if (result == null) {
            Cursor cursor = context
                    .getContentResolver()
                    .query(BaseFile
                            .genContentUriApi(getProviderAuthority(providerId)),
                            null, null, null, null);
            if (cursor == null)
                return null;

            try {
                if (cursor.moveToFirst()) {
                    result = cursor.getString(cursor
                            .getColumnIndex(BaseFile.COLUMN_PROVIDER_NAME));
                    setProviderName(providerId, result);
                } else
                    return null;
            } finally {
                cursor.close();
            }
        }

        return result;
    }// getProviderName()

    /**
     * Sets provider name.
     * 
     * @param providerId
     *            the provider ID.
     * @param providerName
     *            the provider name.
     */
    private static void setProviderName(String providerId, String providerName) {
        MAP_PROVIDER_INFO.get(providerId).putString(
                BaseFile.COLUMN_PROVIDER_NAME, providerName);
    }// setProviderName()

    /**
     * Gets the provider icon (badge) resource ID.
     * 
     * @param context
     *            the context. The resource ID will be retrieved based on this
     *            context's theme (for example light or dark).
     * @param providerId
     *            the provider ID.
     * @return the resource ID of the icon (badge).
     */
    public static int getProviderIconId(Context context, String providerId) {
        int attr = MAP_PROVIDER_INFO.get(providerId).getInt(
                BaseFile.COLUMN_PROVIDER_ICON_ATTR);
        if (attr == 0) {
            Cursor cursor = context
                    .getContentResolver()
                    .query(BaseFile
                            .genContentUriApi(getProviderAuthority(providerId)),
                            null, null, null, null);
            if (cursor != null) {
                try {
                    if (cursor.moveToFirst()) {
                        attr = cursor
                                .getInt(cursor
                                        .getColumnIndex(BaseFile.COLUMN_PROVIDER_ICON_ATTR));
                        MAP_PROVIDER_INFO.get(providerId).putInt(
                                BaseFile.COLUMN_PROVIDER_ICON_ATTR, attr);
                    }
                } finally {
                    cursor.close();
                }
            }
        }

        int res = Ui.resolveAttribute(context, attr);
        if (res == 0)
            res = attr;
        return res;
    }// getProviderIconId()

    /**
     * Default columns of a base file cursor.
     * <p/>
     * The column orders are:
     * <p/>
     * <ol>
     * <li>{@link BaseFile#_ID}</li>
     * <li>{@link BaseFile#COLUMN_URI}</li>
     * <li>{@link BaseFile#COLUMN_REAL_URI}</li>
     * <li>{@link BaseFile#COLUMN_NAME}</li>
     * <li>{@link BaseFile#COLUMN_CAN_READ}</li>
     * <li>{@link BaseFile#COLUMN_CAN_WRITE}</li>
     * <li>{@link BaseFile#COLUMN_SIZE}</li>
     * <li>{@link BaseFile#COLUMN_TYPE}</li>
     * <li>{@link BaseFile#COLUMN_MODIFICATION_TIME}</li>
     * <li>{@link BaseFile#COLUMN_ICON_ID}</li>
     * </ol>
     */
    public static final String[] BASE_FILE_CURSOR_COLUMNS = { BaseFile._ID,
            BaseFile.COLUMN_URI, BaseFile.COLUMN_REAL_URI,
            BaseFile.COLUMN_NAME, BaseFile.COLUMN_CAN_READ,
            BaseFile.COLUMN_CAN_WRITE, BaseFile.COLUMN_SIZE,
            BaseFile.COLUMN_TYPE, BaseFile.COLUMN_MODIFICATION_TIME,
            BaseFile.COLUMN_ICON_ID };

    public static final String[] CONNECTION_CHECK_CURSOR_COLUMNS = {"error"};


    /**
     * Creates new cursor which holds default properties of a base file for
     * client to access.
     * 
     * @return the new empty cursor. The columns are
     *         {@link #BASE_FILE_CURSOR_COLUMNS}.
     */
    public static MatrixCursor newBaseFileCursor() {
        return new MatrixCursor(BASE_FILE_CURSOR_COLUMNS);
    }// newBaseFileCursor()

    /**
     * Creates new cursor, closes it and returns it ^^
     * 
     * @return the newly closed cursor.
     */
    public static MatrixCursor newClosedCursor() {
        MatrixCursor cursor = new MatrixCursor(new String[0]);
        cursor.close();
        return cursor;
    }// newClosedCursor()

    /**
     * Checks if {@code uri} is a directory.
     * 
     * @param context
     *            {@link Context}.
     * @param uri
     *            the URI you want to check.
     * @return {@code true} if {@code uri} is a directory, {@code false}
     *         otherwise.
     */
    public static boolean isDirectory(Context context, Uri uri) {
        Cursor cursor = context.getContentResolver().query(uri, null, null,
                null, null);
        if (cursor == null)
            return false;

        try {
            if (cursor.moveToFirst())
                return isDirectory(cursor);
            return false;
        } finally {
            cursor.close();
        }
    }// isDirectory()

    /**
     * Checks if {@code cursor} is a directory.
     * 
     * @param cursor
     *            the cursor points to a file.
     * @return {@code true} if {@code cursor} is a directory, {@code false}
     *         otherwise.
     */
    public static boolean isDirectory(Cursor cursor) {
        return cursor.getInt(cursor.getColumnIndex(BaseFile.COLUMN_TYPE)) == BaseFile.FILE_TYPE_DIRECTORY;
    }// isDirectory()

    /**
     * Checks if {@code uri} is a file.
     * 
     * @param context
     *            {@link Context}.
     * @param uri
     *            the URI you want to check.
     * @return {@code true} if {@code uri} is a file, {@code false} otherwise.
     */
    public static boolean isFile(Context context, Uri uri) {
        Cursor cursor = context.getContentResolver().query(uri, null, null,
                null, null);
        if (cursor == null)
            return false;

        try {
            if (cursor.moveToFirst())
                return isFile(cursor);
            return false;
        } finally {
            cursor.close();
        }
    }// isFile()

    /**
     * Checks if {@code cursor} is a file.
     * 
     * @param cursor
     *            the cursor points to a file.
     * @return {@code true} if {@code uri} is a file, {@code false} otherwise.
     */
    public static boolean isFile(Cursor cursor) {
        return cursor.getInt(cursor.getColumnIndex(BaseFile.COLUMN_TYPE)) == BaseFile.FILE_TYPE_FILE;
    }// isFile()

    /**
     * Gets file name of {@code uri}.
     * 
     * @param context
     *            {@link Context}.
     * @param uri
     *            the URI you want to get.
     * @return the file name if {@code uri} is a file, {@code null} otherwise.
     */
    public static String getFileName(Context context, Uri uri) {
        Cursor cursor = context.getContentResolver().query(uri, null, null,
                null, null);
        if (cursor == null)
            return null;

        try {
            if (cursor.moveToFirst())
                return getFileName(cursor);
            return null;
        } finally {
            cursor.close();
        }
    }// getFileName()

    /**
     * Gets filename of {@code cursor}.
     * 
     * @param cursor
     *            the cursor points to a file.
     * @return the filename.
     */
    public static String getFileName(Cursor cursor) {
        return cursor.getString(cursor.getColumnIndex(BaseFile.COLUMN_NAME));
    }// getFileName()

    /**
     * Gets the real URI of {@code uri}. This is independent of the content
     * provider's URI ({@code uri}). For example with {@link LocalFileProvider},
     * this method gets the URI which you can create new {@link File} object
     * directly from it.
     * 
     * @param context
     *            {@link Context}.
     * @param uri
     *            the content provider URI which you want to get real URI from.
     * @return the real URI of {@code uri}.
     */
    public static Uri getRealUri(Context context, Uri uri) {
        Cursor cursor = context.getContentResolver().query(uri, null, null,
                null, null);
        if (cursor == null)
            return null;

        try {
            if (cursor.moveToFirst())
                return getRealUri(cursor);
            return null;
        } finally {
            cursor.close();
        }
    }// getRealUri()

    /**
     * Gets the real URI. This is independent of the content provider's URI
     * which {@code cursor} points to. For example with
     * {@link LocalFileProvider}, this method gets the URI which you can create
     * new {@link File} object directly from it.
     * 
     * @param cursor
     *            the cursor points to a file.
     * @return the real URI.
     */
    public static Uri getRealUri(Cursor cursor) {
        return Uri.parse(cursor.getString(cursor
                .getColumnIndex(BaseFile.COLUMN_REAL_URI)));
    }// getRealUri()

    /**
     * Gets file type of the file pointed by {@code uri}.
     * 
     * @param context
     *            {@link Context}.
     * @param uri
     *            the URI you want to get.
     * @return the file type of {@code uri}, can be one of
     *         {@link #FILE_TYPE_DIRECTORY}, {@link #FILE_TYPE_FILE},
     *         {@link #FILE_TYPE_UNKNOWN}, {@link #FILE_TYPE_NOT_EXISTED}.
     */
    public static int getFileType(Context context, Uri uri) {
        Cursor cursor = context.getContentResolver().query(uri, null, null,
                null, null);
        if (cursor == null)
            return BaseFile.FILE_TYPE_NOT_EXISTED;

        try {
            if (cursor.moveToFirst())
                return getFileType(cursor);
            return BaseFile.FILE_TYPE_NOT_EXISTED;
        } finally {
            cursor.close();
        }
    }// getFileType()

    /**
     * Gets file type of the file pointed by {@code cursor}.
     * 
     * @param cursor
     *            the cursor points to a file.
     * @return the file type, can be one of {@link #FILE_TYPE_DIRECTORY},
     *         {@link #FILE_TYPE_FILE}, {@link #FILE_TYPE_UNKNOWN},
     *         {@link #FILE_TYPE_NOT_EXISTED}.
     */
    public static int getFileType(Cursor cursor) {
        return cursor.getInt(cursor.getColumnIndex(BaseFile.COLUMN_TYPE));
    }// getFileType()

    /**
     * Gets URI of {@code cursor}.
     * 
     * @param cursor
     *            the cursor points to a file.
     * @return the URI.
     */
    public static Uri getUri(Cursor cursor) {
        return Uri.parse(cursor.getString(cursor
                .getColumnIndex(BaseFile.COLUMN_URI)));
    }// getFileName()

    /**
     * Checks if the file pointed by {@code uri} is existed or not.
     * 
     * @param context
     *            {@link Context}.
     * @param uri
     *            the URI you want to check.
     * @return {@code true} or {@code false}.
     */
    public static boolean fileExists(Context context, Uri uri) {
        Cursor cursor = context.getContentResolver().query(uri, null, null,
                null, null);
        if (cursor == null)
            return false;

        try {
            if (cursor.moveToFirst())
                return cursor.getInt(cursor
                        .getColumnIndex(BaseFile.COLUMN_TYPE)) != BaseFile.FILE_TYPE_NOT_EXISTED;
            return false;
        } finally {
            cursor.close();
        }
    }// fileExists()

    /**
     * Checks if the file pointed by {@code uri} is readable or not.
     * 
     * @param context
     *            {@link Context}.
     * @param uri
     *            the URI you want to check.
     * @return {@code true} or {@code false}.
     */
    public static boolean fileCanRead(Context context, Uri uri) {
        Cursor cursor = context.getContentResolver().query(uri, null, null,
                null, null);
        if (cursor == null)
            return false;

        try {
            if (cursor.moveToFirst())
                return fileCanRead(cursor);
            return false;
        } finally {
            cursor.close();
        }
    }// fileCanRead()

    /**
     * Checks if the file pointed be {@code cursor} is readable or not.
     * 
     * @param cursor
     *            the cursor points to a file.
     * @return {@code true} or {@code false}.
     */
    public static boolean fileCanRead(Cursor cursor) {
        if (cursor.getInt(cursor.getColumnIndex(BaseFile.COLUMN_CAN_READ)) != 0) {
            switch (cursor.getInt(cursor.getColumnIndex(BaseFile.COLUMN_TYPE))) {
            case BaseFile.FILE_TYPE_DIRECTORY:
            case BaseFile.FILE_TYPE_FILE:
                return true;
            }
        }

        return false;
    }// fileCanRead()

    /**
     * Checks if the file pointed by {@code uri} is writable or not.
     * 
     * @param context
     *            {@link Context}.
     * @param uri
     *            the URI you want to check.
     * @return {@code true} or {@code false}.
     */
    public static boolean fileCanWrite(Context context, Uri uri) {
        Cursor cursor = context.getContentResolver().query(uri, null, null,
                null, null);
        if (cursor == null)
            return false;

        try {
            if (cursor.moveToFirst())
                return fileCanWrite(cursor);
            return false;
        } finally {
            cursor.close();
        }
    }// fileCanWrite()

    /**
     * Checks if the file pointed by {@code cursor} is writable or not.
     * 
     * @param cursor
     *            the cursor points to a file.
     * @return {@code true} or {@code false}.
     */
    public static boolean fileCanWrite(Cursor cursor) {
        if (cursor.getInt(cursor.getColumnIndex(BaseFile.COLUMN_CAN_WRITE)) != 0) {
            switch (cursor.getInt(cursor.getColumnIndex(BaseFile.COLUMN_TYPE))) {
            case BaseFile.FILE_TYPE_DIRECTORY:
            case BaseFile.FILE_TYPE_FILE:
                return true;
            }
        }

        return false;
    }// fileCanWrite()

    /**
     * Gets default path of a provider.
     * 
     * @param context
     *            {@link Context}.
     * @param authority
     *            the provider's authority.
     * @return the default path, can be {@code null}.
     */
    public static Uri getDefaultPath(Context context, String authority) {
        Cursor cursor = context.getContentResolver().query(
                BaseFile.genContentUriApi(authority).buildUpon()
                        .appendPath(BaseFile.CMD_GET_DEFAULT_PATH).build(),
                null, null, null, null);
        if (cursor == null)
            return null;

        try {
            if (cursor.moveToFirst())
                return Uri.parse(cursor.getString(cursor
                        .getColumnIndex(BaseFile.COLUMN_URI)));
            return null;
        } finally {
            cursor.close();
        }
    }// getDefaultPath()

    /**
     * Gets parent directory of {@code uri}.
     * 
     * @param context
     *            {@link Context}.
     * @param uri
     *            the URI of an existing file.
     * @return the parent file if it exists, {@code null} otherwise.
     */
    public static Uri getParentFile(Context context, Uri uri) {
        Cursor cursor = context.getContentResolver().query(
                BaseFile.genContentUriApi(uri.getAuthority())
                        .buildUpon()
                        .appendPath(BaseFile.CMD_GET_PARENT)
                        .appendQueryParameter(BaseFile.PARAM_SOURCE,
                                uri.getLastPathSegment()).build(), null, null,
                null, null);
        if (cursor == null)
            return null;

        try {
            if (cursor.moveToFirst())
                return Uri.parse(cursor.getString(cursor
                        .getColumnIndex(BaseFile.COLUMN_URI)));
            return null;
        } finally {
            cursor.close();
        }
    }// getParentFile()

    /**
     * Checks if {@code uri1} is ancestor of {@code uri2}.
     * 
     * @param context
     *            {@link Context}.
     * @param uri1
     *            the first URI.
     * @param uri2
     *            the second URI.
     * @return {@code true} if {@code uri1} is ancestor of {@code uri2},
     *         {@code false} otherwise.
     */
    public static boolean isAncestorOf(Context context, Uri uri1, Uri uri2) {
        return context.getContentResolver().query(
                BaseFile.genContentUriApi(uri1.getAuthority())
                        .buildUpon()
                        .appendPath(BaseFile.CMD_IS_ANCESTOR_OF)
                        .appendQueryParameter(BaseFile.PARAM_SOURCE,
                                uri1.getLastPathSegment())
                        .appendQueryParameter(BaseFile.PARAM_TARGET,
                                uri2.getLastPathSegment()).build(), null, null,
                null, null) != null;
    }// isAncestorOf()

    /**
     * Cancels a task with its ID.
     * 
     * @param context
     *            the context.
     * @param authority
     *            the file provider authority.
     * @param taskId
     *            the task ID.
     */
    public static void cancelTask(Context context, String authority, int taskId) {
        context.getContentResolver().query(
                BaseFile.genContentUriApi(authority)
                        .buildUpon()
                        .appendPath(BaseFile.CMD_CANCEL)
                        .appendQueryParameter(BaseFile.PARAM_TASK_ID,
                                Integer.toString(taskId)).build(), null, null,
                null, null);
    }// cancelTask()

}
