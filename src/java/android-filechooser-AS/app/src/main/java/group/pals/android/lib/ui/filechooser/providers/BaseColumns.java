/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers;

/**
 * The base columns.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public interface BaseColumns extends android.provider.BaseColumns {

    /**
     * Column name for the creation timestamp.
     * <p/>
     * Type: {@code String} representing {@code long} from
     * {@link java.util.Date#getTime()}. This is because SQLite doesn't handle
     * Java's {@code long} well.
     */
    public static final String COLUMN_CREATE_TIME = "create_time";

    /**
     * Column name for the modification timestamp.
     * <p/>
     * Type: {@code String} representing {@code long} from
     * {@link java.util.Date#getTime()}. This is because SQLite doesn't handle
     * Java's {@code long} well.
     */
    public static final String COLUMN_MODIFICATION_TIME = "modification_time";

}
