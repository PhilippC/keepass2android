/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.history;

/**
 * Filter of {@link History}
 * 
 * @author Hai Bison
 * @since v4.0 beta
 */
public interface HistoryFilter<A> {

    /**
     * Filters item.
     * 
     * @param item
     *            {@link A}
     * @return {@code true} if the {@code item} is accepted
     */
    boolean accept(A item);

}
