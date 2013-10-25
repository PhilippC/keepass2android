/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.history;

import java.util.ArrayList;

import android.os.Parcelable;

/**
 * A history store of any object.
 * 
 * @param <A>
 *            any type
 * @author Hai Bison
 * @since v2.0 alpha
 */
public interface History<A> extends Parcelable {

    /**
     * Pushes {@code newItem} to the history. If the top item is same as this
     * one, then does nothing.
     * 
     * @param newItem
     *            the new item
     */
    void push(A newItem);

    /**
     * Finds {@code item} and if it exists, removes all items after it.
     * 
     * @param item
     *            {@link A}
     * @return the total items truncated.
     * @since v4.3 beta
     */
    int truncateAfter(A item);

    /**
     * Removes an item.
     * 
     * @param item
     *            {@link A}
     * @since v4.0 beta
     */
    void remove(A item);

    /**
     * Removes all items by a filter.
     * 
     * @param filter
     *            {@link HistoryFilter}
     * @since v4.0 beta
     */
    void removeAll(HistoryFilter<A> filter);

    /**
     * Gets size of the history
     * 
     * @return the size of the history
     */
    int size();

    /**
     * Gets index of item {@code a}
     * 
     * @param a
     *            an item
     * @return index of the {@code a}, or -1 if there is no one
     */
    int indexOf(A a);

    /**
     * Gets previous item of {@code a}
     * 
     * @param a
     *            current item
     * @return the previous item, can be {@code null}
     */
    A prevOf(A a);

    /**
     * Gets next item of {@code a}
     * 
     * @param a
     *            current item
     * @return the next item, can be {@code null}
     */
    A nextOf(A a);

    /**
     * Retrieves all items in this history, in an <i>independent</i> list.
     * 
     * @return list of {@link A}.
     * @since v4.3 beta
     */
    ArrayList<A> items();

    /**
     * Checks if the history is empty or not.
     * 
     * @return {@code true} if this history is empty, {@code false} otherwise.
     * @since v4.3 beta
     */
    boolean isEmpty();

    /**
     * Clears this history.
     * 
     * @since v4.3 beta.
     */
    void clear();

    /**
     * Adds a {@link HistoryListener}
     * 
     * @param listener
     *            {@link HistoryListener}
     * @since v4.0 beta
     */
    void addListener(HistoryListener<A> listener);

    /**
     * Removes a {@link HistoryListener}
     * 
     * @param listener
     *            {@link HistoryListener}
     * @return the removed listener
     * @since v4.0 beta
     */
    void removeListener(HistoryListener<A> listener);

    /**
     * Notifies to all {@link HistoryListener}'s that the history changed.
     */
    void notifyHistoryChanged();

    /**
     * Finds items with a filter.
     * 
     * @param filter
     *            {@link HistoryFilter}
     * @param ascending
     *            {@code true} if you want to process the history list ascending
     *            (oldest to newest), {@code false} for descending.
     * @return {@code true} if the desired items have been found, {@code false}
     *         otherwise.
     * @since v5.1 beta
     */
    boolean find(HistoryFilter<A> filter, boolean ascending);

}
