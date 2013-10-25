/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.history;

import java.util.ArrayList;
import java.util.List;

import android.os.Bundle;
import android.os.Parcel;
import android.os.Parcelable;
import android.util.Log;

/**
 * A history store of any object extending {@link Parcelable}.
 * <p/>
 * <b>Note:</b> This class does not support storing its {@link HistoryListener}
 * 's into {@link Parcelable}. You must re-build all listeners after getting
 * your {@link HistoryStore} from a {@link Bundle} for example.
 * 
 * @author Hai Bison
 * @since v2.0 alpha
 */
public class HistoryStore<A extends Parcelable> implements History<A> {

    /**
     * Uses for debugging...
     */
    private static final String CLASSNAME = HistoryStore.class.getName();

    /**
     * The default capacity of this store.
     */
    public static final int DEFAULT_CAPACITY = 99;

    private final ArrayList<A> mHistoryList = new ArrayList<A>();
    private final List<HistoryListener<A>> mListeners = new ArrayList<HistoryListener<A>>();
    private int mCapacity;

    /**
     * Creates new instance with {@link #DEFAULT_CAPACITY}.
     */
    public HistoryStore() {
        this(DEFAULT_CAPACITY);
    }// HistoryStore()

    /**
     * Creates new {@link HistoryStore}
     * 
     * @param capcacity
     *            the maximum size that allowed, if it is {@code <= 0},
     *            {@link #DEFAULT_CAPACITY} will be used
     */
    public HistoryStore(int capcacity) {
        mCapacity = capcacity > 0 ? capcacity : DEFAULT_CAPACITY;
    }// HistoryStore()

    /**
     * Gets the capacity.
     * 
     * @return the capacity.
     */
    public int getCapacity() {
        return mCapacity;
    }// getCapacity()

    @Override
    public void push(A newItem) {
        if (newItem == null)
            return;

        if (!mHistoryList.isEmpty()
                && indexOf(newItem) == mHistoryList.size() - 1)
            return;

        mHistoryList.add(newItem);
        if (mHistoryList.size() > mCapacity)
            mHistoryList.remove(0);

        notifyHistoryChanged();
    }// push()

    @Override
    public int truncateAfter(A item) {
        if (item == null)
            return 0;

        for (int i = mHistoryList.size() - 2; i >= 0; i--) {
            if (mHistoryList.get(i) == item) {
                List<A> subList = mHistoryList.subList(i + 1,
                        mHistoryList.size());
                int count = subList.size();

                subList.clear();
                notifyHistoryChanged();

                return count;
            }
        }

        return 0;
    }// truncateAfter()

    @Override
    public void remove(A item) {
        if (mHistoryList.remove(item))
            notifyHistoryChanged();
    }// remove()

    @Override
    public void removeAll(HistoryFilter<A> filter) {
        boolean changed = false;
        for (int i = mHistoryList.size() - 1; i >= 0; i--) {
            if (filter.accept(mHistoryList.get(i))) {
                mHistoryList.remove(i);
                if (!changed)
                    changed = true;
            }
        }// for

        if (changed)
            notifyHistoryChanged();
    }// removeAll()

    @Override
    public void notifyHistoryChanged() {
        for (HistoryListener<A> listener : mListeners)
            listener.onChanged(this);
    }// notifyHistoryChanged()

    @Override
    public int size() {
        return mHistoryList.size();
    }// size()

    @Override
    public int indexOf(A a) {
        for (int i = 0; i < mHistoryList.size(); i++)
            if (mHistoryList.get(i) == a)
                return i;
        return -1;
    }// indexOf()

    @Override
    public A prevOf(A a) {
        int idx = indexOf(a);
        if (idx > 0)
            return mHistoryList.get(idx - 1);
        return null;
    }// prevOf()

    @Override
    public A nextOf(A a) {
        int idx = indexOf(a);
        if (idx >= 0 && idx < mHistoryList.size() - 1)
            return mHistoryList.get(idx + 1);
        return null;
    }// nextOf()

    @SuppressWarnings("unchecked")
    @Override
    public ArrayList<A> items() {
        return (ArrayList<A>) mHistoryList.clone();
    }// items()

    @Override
    public boolean isEmpty() {
        return mHistoryList.isEmpty();
    }// isEmpty()

    @Override
    public void clear() {
        mHistoryList.clear();
        notifyHistoryChanged();
    }// clear()

    @Override
    public void addListener(HistoryListener<A> listener) {
        mListeners.add(listener);
    }// addListener()

    @Override
    public void removeListener(HistoryListener<A> listener) {
        mListeners.remove(listener);
    }// removeListener()

    @Override
    public boolean find(HistoryFilter<A> filter, boolean ascending) {
        for (int i = ascending ? 0 : mHistoryList.size() - 1; ascending ? i < mHistoryList
                .size() : i >= 0;) {
            if (filter.accept(mHistoryList.get(i)))
                return true;
            if (ascending)
                i++;
            else
                i--;
        }

        return false;
    }// find()

    /*-----------------------------------------------------
     * Parcelable
     */

    @Override
    public int describeContents() {
        return 0;
    }// describeContents()

    @Override
    public void writeToParcel(Parcel dest, int flags) {
        dest.writeInt(mCapacity);

        dest.writeInt(size());
        for (int i = 0; i < size(); i++)
            dest.writeParcelable(mHistoryList.get(i), flags);
    }// writeToParcel()

    /**
     * Reads data from {@code in}.
     * 
     * @param in
     *            {@link Parcel}.
     */
    @SuppressWarnings("unchecked")
    public void readFromParcel(Parcel in) {
        mCapacity = in.readInt();

        int count = in.readInt();
        for (int i = 0; i < count; i++) {
            try {
                mHistoryList.add((A) in.readParcelable(getClass()
                        .getClassLoader()));
            } catch (ClassCastException e) {
                Log.e(CLASSNAME, "readFromParcel() >> " + e);
                e.printStackTrace();
                break;
            }
        }
    }// readFromParcel()

    public static final Parcelable.Creator<HistoryStore<?>> CREATOR = new Parcelable.Creator<HistoryStore<?>>() {

        @SuppressWarnings("rawtypes")
        public HistoryStore<?> createFromParcel(Parcel in) {
            return new HistoryStore(in);
        }// createFromParcel()

        public HistoryStore<?>[] newArray(int size) {
            return new HistoryStore[size];
        }// newArray()
    };// CREATOR

    private HistoryStore(Parcel in) {
        readFromParcel(in);
    }// HistoryStore()

}
