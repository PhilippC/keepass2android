/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers.history;

import group.pals.android.lib.ui.filechooser.providers.BaseColumns;
import group.pals.android.lib.ui.filechooser.providers.ProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileContract.BaseFile;
import android.content.Context;
import android.net.Uri;

/**
 * History contract.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public final class HistoryContract implements BaseColumns {

    /**
     * The raw authority.
     */
    private static final String AUTHORITY = "android-filechooser.history";

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
    private HistoryContract() {
    }

    /**
     * The table name offered by this provider.
     */
    public static final String TABLE_NAME = "history";

    /*
     * URI definitions.
     */

    /**
     * Path parts for the URIs.
     */

    /**
     * Path part for the History URI.
     */
    public static final String PATH_HISTORY = "history";

    /**
     * The content:// style URL for this table.
     */
    public static final Uri genContentUri(Context context) {
        return Uri.parse(ProviderUtils.SCHEME + getAuthority(context) + "/"
                + PATH_HISTORY);
    }// genContentUri()

    /**
     * The content URI base for a single history item. Callers must append a
     * numeric history ID to this Uri to retrieve a history item.
     */
    public static final Uri genContentIdUriBase(Context context) {
        return Uri.parse(ProviderUtils.SCHEME + getAuthority(context) + "/"
                + PATH_HISTORY + "/");
    }

    /*
     * MIME type definitions.
     */

    /**
     * The MIME type of {@link #_ContentUri} providing a directory of history
     * items.
     */
    public static final String CONTENT_TYPE = "vnd.android.cursor.dir/vnd.android-filechooser.history";

    /**
     * The MIME type of a {@link #_ContentUri} sub-directory of a single history
     * item.
     */
    public static final String CONTENT_ITEM_TYPE = "vnd.android.cursor.item/vnd.android-filechooser.history";

    /**
     * The default sort order for this table.
     */
    public static final String DEFAULT_SORT_ORDER = COLUMN_MODIFICATION_TIME
            + " DESC";

    /*
     * Column definitions.
     */

    /**
     * Column name for the ID of the provider.
     * <p/>
     * Type: {@code String}
     */
    public static final String COLUMN_PROVIDER_ID = "provider_id";

    /**
     * Column name for the type of history. The value can be one of
     * {@link BaseFile#FILE_TYPE_DIRECTORY}, {@link BaseFile#FILE_TYPE_FILE}.
     * <p/>
     * Type: {@code Integer}
     */
    public static final String COLUMN_FILE_TYPE = "file_type";

    /**
     * Column name for the URI of history.
     * <p/>
     * Type: {@code URI}
     */
    public static final String COLUMN_URI = "uri";

}
