/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.history;

/**
 * Listener of {@link History}
 *
 * @author Hai Bison
 * @since v4.0 beta
 */
public interface HistoryListener<A> {

    /**
     * Will be called after the history changed.
     *
     * @param history {@link History}
     */
    void onChanged(History<A> history);
}
