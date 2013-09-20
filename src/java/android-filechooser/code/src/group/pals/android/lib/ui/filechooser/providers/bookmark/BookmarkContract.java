/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers.bookmark;

import android.content.Context;
import android.net.Uri;
import group.pals.android.lib.ui.filechooser.providers.BaseColumns;
import group.pals.android.lib.ui.filechooser.providers.ProviderUtils;

/**
 * Bookmark contract.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public final class BookmarkContract implements BaseColumns {

    /**
     * The raw authority.
     */
    private static final String AUTHORITY = "android-filechooser.bookmark";

    /**
     * Gets the authority of this provider.
     * 
     * @param context
     *            the context.
     * @return the authority.
     */
    public static final String getAuthority(Context context) {
        return context.getPackageName() + "." + AUTHORITY;
    }// getAuthority()

    // This class cannot be instantiated
    private BookmarkContract() {
    }

    /**
     * The table name offered by this provider.
     */
    public static final String TABLE_NAME = "bookmarks";

    /*
     * URI definitions.
     */

    /**
     * Path parts for the URIs.
     */

    /**
     * Path part for the Bookmark URI.
     */
    public static final String PATH_BOOKMARKS = "bookmarks";

    /**
     * The content:// style URL for this table.
     */
    public static final Uri genContentUri(Context context) {
        return Uri.parse(ProviderUtils.SCHEME + getAuthority(context) + "/"
                + PATH_BOOKMARKS);
    }// genContentUri()

    /**
     * The content URI base for a single Bookmark item. Callers must append a
     * numeric Bookmark id to this Uri to retrieve a Bookmark item.
     */
    public static final Uri genContentIdUriBase(Context context) {
        return Uri.parse(ProviderUtils.SCHEME + getAuthority(context) + "/"
                + PATH_BOOKMARKS + "/");
    }// genContentIdUriBase()

    /*
     * MIME type definitions
     */

    /**
     * The MIME type of {@link #_ContentUri} providing a directory of Bookmark
     * items.
     */
    public static final String CONTENT_TYPE = "vnd.android.cursor.dir/vnd.android-filechooser.bookmarks";

    /**
     * The MIME type of a {@link #_ContentUri} sub-directory of a single
     * Bookmark item.
     */
    public static final String CONTENT_ITEM_TYPE = "vnd.android.cursor.item/vnd.android-filechooser.bookmarks";

    /**
     * The default sort order for this table.
     */
    public static final String DEFAULT_SORT_ORDER = COLUMN_MODIFICATION_TIME
            + " DESC";

    /*
     * Column definitions
     */

    /**
     * Column name for the URI of bookmark.
     * <p/>
     * Type: {@code String}
     */
    public static final String COLUMN_URI = "uri";

    /**
     * Column name for the name of bookmark.
     * <p/>
     * Type: {@code String}
     */
    public static final String COLUMN_NAME = "name";

    /**
     * Column name for the ID of bookmark's provider ID.
     * <p/>
     * Type: {@code String}
     */
    public static final String COLUMN_PROVIDER_ID = "provider_id";
}
