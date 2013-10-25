/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.ui.history;

import group.pals.android.lib.ui.filechooser.BuildConfig;
import group.pals.android.lib.ui.filechooser.R;
import group.pals.android.lib.ui.filechooser.providers.BaseFileProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileContract.BaseFile;
import group.pals.android.lib.ui.filechooser.providers.history.HistoryContract;
import group.pals.android.lib.ui.filechooser.utils.DateUtils;
import group.pals.android.lib.ui.filechooser.utils.ui.ContextMenuUtils;
import group.pals.android.lib.ui.filechooser.utils.ui.Ui;

import java.util.ArrayList;
import java.util.Calendar;
import java.util.List;
import java.util.TimeZone;

import android.content.Context;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.util.Log;
import android.util.SparseArray;
import android.view.View;
import android.widget.CheckBox;
import android.widget.CompoundButton;
import android.widget.ResourceCursorTreeAdapter;
import android.widget.TextView;

/**
 * History cursor adapter.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class HistoryCursorAdapter extends ResourceCursorTreeAdapter {

    private static final String CLASSNAME = HistoryCursorAdapter.class
            .getName();

    /**
     * @see android.text.format.DateUtils#DAY_IN_MILLIS
     */
    private static final long DAY_IN_MILLIS = android.text.format.DateUtils.DAY_IN_MILLIS;

    /**
     * Advanced selection options: All, None, Invert.
     */
    public static final Integer[] ADVANCED_SELECTION_OPTIONS = new Integer[] {
            R.string.afc_cmd_advanced_selection_all,
            R.string.afc_cmd_advanced_selection_none,
            R.string.afc_cmd_advanced_selection_invert };

    private static class BagGroup {

        TextView mTextViewHeader;
    }// BagGroup

    private static class BagChild {

        TextView mTextViewTime;
        TextView mTextViewName;
        TextView mTextViewPath;
        CheckBox mCheckBox;
    }// BagChild

    private static class BagChildInfo {

        boolean mChecked = false;
        boolean mMarkedAsDeleted = false;
    }// BagChildInfo

    /**
     * This column holds the original position of group cursor in original
     * cursor.
     * <p/>
     * Type: {@code Integer}
     */
    private static final String COLUMN_ORG_GROUP_POSITION = "org_group_position";

    private static final String[] GROUP_CURSOR_COLUMNS = { HistoryContract._ID,
            HistoryContract.COLUMN_MODIFICATION_TIME, COLUMN_ORG_GROUP_POSITION };

    private static final String[] CHILD_CURSOR_COLUMNS = { HistoryContract._ID,
            HistoryContract.COLUMN_URI, HistoryContract.COLUMN_PROVIDER_ID,
            HistoryContract.COLUMN_MODIFICATION_TIME, BaseFile.COLUMN_NAME,
            BaseFile.COLUMN_REAL_URI };

    /**
     * Map of child IDs to {@link BagChildInfo}.
     */
    private final SparseArray<BagChildInfo> mSelectedChildrenMap = new SparseArray<BagChildInfo>();

    private Cursor mOrgCursor;
    private MatrixCursor mGroupCursor;
    private SparseArray<MatrixCursor> mChildrenCursor;
    private CharSequence mSearchText;

    /**
     * Creates new instance.
     * 
     * @param context
     *            {@link Context}.
     */
    public HistoryCursorAdapter(Context context) {
        super(context, null, R.layout.afc_view_history_item,
                R.layout.afc_view_history_sub_item);
    }// BookmarkCursorAdapter()

    /**
     * Changes new cursor.
     * <p/>
     * You have to query the items in descending order of modification time.
     * 
     * @param cursor
     *            the cursor.
     * @param notificationUri
     *            the notification URI.
     */
    @Override
    public synchronized void changeCursor(Cursor cursor) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "changeCursor()");

        if (mOrgCursor != null)
            mOrgCursor.close();
        mOrgCursor = cursor;

        MatrixCursor newGroupCursor = cursor != null ? new MatrixCursor(
                GROUP_CURSOR_COLUMNS) : null;
        SparseArray<MatrixCursor> newChildrenCursor = cursor != null ? new SparseArray<MatrixCursor>()
                : null;

        /*
         * Build new group cursor.
         */
        if (cursor != null && cursor.moveToFirst()) {
            long lastDayCount = 0;
            do {
                long dayCount = (long) Math
                        .floor((Long.parseLong(cursor.getString(cursor
                                .getColumnIndex(HistoryContract.COLUMN_MODIFICATION_TIME))) + TimeZone
                                .getDefault().getRawOffset())
                                / DAY_IN_MILLIS);

                if (dayCount != lastDayCount || newGroupCursor.getCount() == 0) {
                    newGroupCursor
                            .addRow(new Object[] {
                                    cursor.getInt(cursor
                                            .getColumnIndex(HistoryContract._ID)),
                                    cursor.getString(cursor
                                            .getColumnIndex(HistoryContract.COLUMN_MODIFICATION_TIME)),
                                    cursor.getPosition() });
                }
                lastDayCount = dayCount;
            } while (cursor.moveToNext());
        }

        /*
         * Clean up children cursor.
         */
        if (mChildrenCursor != null) {
            for (int i = 0; i < mChildrenCursor.size(); i++)
                mChildrenCursor.valueAt(i).close();
            mChildrenCursor.clear();
        }

        /*
         * Apply new changes... Note that we don't need to close the old group
         * cursor. The call to `super.changeCursor()` will do that.
         */
        mGroupCursor = newGroupCursor;
        mChildrenCursor = newChildrenCursor;
        super.changeCursor(mGroupCursor);
    }// changeCursor()

    @Override
    protected Cursor getChildrenCursor(Cursor groupCursor) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "getChildrenCursor()");

        /*
         * Try to find the child cursor in the map. If found then it'd be great
         * :-)
         */
        int orgGroupPosition = groupCursor.getInt(groupCursor
                .getColumnIndex(COLUMN_ORG_GROUP_POSITION));
        int idx = mChildrenCursor.indexOfKey(orgGroupPosition);
        if (idx >= 0)
            return mChildrenCursor.valueAt(idx);

        /*
         * If not found, create new cursor.
         */
        MatrixCursor childrenCursor = new MatrixCursor(CHILD_CURSOR_COLUMNS);

        mOrgCursor.moveToPosition(orgGroupPosition);
        long startOfDay = Long.parseLong(groupCursor.getString(groupCursor
                .getColumnIndex(HistoryContract.COLUMN_MODIFICATION_TIME)))
                + TimeZone.getDefault().getRawOffset();
        startOfDay -= startOfDay % DAY_IN_MILLIS;
        do {
            childrenCursor
                    .addRow(new Object[] {
                            mOrgCursor.getInt(mOrgCursor
                                    .getColumnIndex(HistoryContract._ID)),
                            mOrgCursor.getString(mOrgCursor
                                    .getColumnIndex(HistoryContract.COLUMN_URI)),
                            mOrgCursor.getString(mOrgCursor
                                    .getColumnIndex(HistoryContract.COLUMN_PROVIDER_ID)),
                            mOrgCursor.getString(mOrgCursor
                                    .getColumnIndex(HistoryContract.COLUMN_MODIFICATION_TIME)),
                            mOrgCursor.getString(mOrgCursor
                                    .getColumnIndex(BaseFile.COLUMN_NAME)),
                            mOrgCursor.getString(mOrgCursor
                                    .getColumnIndex(BaseFile.COLUMN_REAL_URI)) });
        } while (mOrgCursor.moveToNext()
                && Long.parseLong(mOrgCursor.getString(mOrgCursor
                        .getColumnIndex(HistoryContract.COLUMN_MODIFICATION_TIME)))
                        + TimeZone.getDefault().getRawOffset() >= startOfDay);

        /*
         * Put it to the map.
         */
        mChildrenCursor.put(orgGroupPosition, childrenCursor);
        return childrenCursor;
    }// getChildrenCursor()

    @Override
    protected void bindChildView(View view, Context context, Cursor cursor,
            boolean isLastChild) {
        final int id = cursor
                .getInt(cursor.getColumnIndex(HistoryContract._ID));
        final BagChild child;

        if (view.getTag() == null) {
            child = new BagChild();
            child.mTextViewTime = (TextView) view
                    .findViewById(R.id.afc_textview_time);
            child.mTextViewName = (TextView) view
                    .findViewById(R.id.afc_textview_name);
            child.mTextViewPath = (TextView) view
                    .findViewById(R.id.afc_textview_path);
            child.mCheckBox = (CheckBox) view.findViewById(R.id.afc_checkbox);

            view.setTag(child);
        } else
            child = (BagChild) view.getTag();

        final BagChildInfo childInfo;
        if (mSelectedChildrenMap.get(id) == null) {
            childInfo = new BagChildInfo();
            mSelectedChildrenMap.put(id, childInfo);
        } else
            childInfo = mSelectedChildrenMap.get(id);

        String fileName = null;
        String fileUri = null;
        int col = -1;
        if ((col = cursor.getColumnIndex(BaseFile.COLUMN_NAME)) >= 0)
            fileName = cursor.getString(col);
        if ((col = cursor.getColumnIndex(BaseFile.COLUMN_REAL_URI)) >= 0)
            fileUri = cursor.getString(col);

        child.mTextViewTime
                .setText(formatTime(
                        view.getContext(),
                        Long.parseLong(cursor.getString(cursor
                                .getColumnIndex(HistoryContract.COLUMN_MODIFICATION_TIME)))));
        child.mTextViewName.setText(fileName);
        Ui.strikeOutText(child.mTextViewName, childInfo.mMarkedAsDeleted);
        child.mTextViewPath.setText(fileUri);

        /*
         * Provider ID.
         */
        String providerId = cursor.getString(cursor
                .getColumnIndex(HistoryContract.COLUMN_PROVIDER_ID));
        /*
         * Provider badge icon.
         */
        child.mTextViewTime.setCompoundDrawablesWithIntrinsicBounds(0, 0, 0,
                BaseFileProviderUtils.getProviderIconId(context, providerId));

        /*
         * Check box.
         */
        child.mCheckBox.setOnCheckedChangeListener(null);
        child.mCheckBox.setChecked(childInfo.mChecked);
        child.mCheckBox
                .setOnCheckedChangeListener(new CompoundButton.OnCheckedChangeListener() {

                    @Override
                    public void onCheckedChanged(CompoundButton buttonView,
                            boolean isChecked) {
                        if (BuildConfig.DEBUG)
                            Log.d(CLASSNAME, "onCheckedChanged() >> id = " + id);
                        childInfo.mChecked = isChecked;
                    }// onCheckedChanged()
                });

        child.mCheckBox.setOnLongClickListener(new View.OnLongClickListener() {

            @Override
            public boolean onLongClick(View v) {
                ContextMenuUtils.showContextMenu(v.getContext(), 0,
                        R.string.afc_title_advanced_selection,
                        ADVANCED_SELECTION_OPTIONS,
                        new ContextMenuUtils.OnMenuItemClickListener() {

                            @Override
                            public void onClick(final int resId) {
                                if (resId == R.string.afc_cmd_advanced_selection_all)
                                    selectAll(true);
                                else if (resId == R.string.afc_cmd_advanced_selection_none)
                                    selectAll(false);
                                else if (resId == R.string.afc_cmd_advanced_selection_invert)
                                    invertSelection();
                            }// onClick()
                        });
                return true;
            }// onLongClick()
        });
    }// bindChildView()

    @Override
    protected void bindGroupView(View view, Context context,
            final Cursor cursor, boolean isExpanded) {
        BagGroup b;
        if (view.getTag() == null) {
            b = new BagGroup();
            b.mTextViewHeader = (TextView) view
                    .findViewById(R.id.afc_textview_header);

            view.setTag(b);
        } else
            b = (BagGroup) view.getTag();

        b.mTextViewHeader
                .setText(formatDate(
                        view.getContext(),
                        Long.parseLong(cursor.getString(cursor
                                .getColumnIndex(HistoryContract.COLUMN_MODIFICATION_TIME)))));
    }// bindGroupView()

    @Override
    public void notifyDataSetChanged(boolean releaseCursors) {
        super.notifyDataSetChanged(releaseCursors);
        if (releaseCursors)
            synchronized (mSelectedChildrenMap) {
                mSelectedChildrenMap.clear();
            }
    }// notifyDataSetChanged()

    @Override
    public void notifyDataSetInvalidated() {
        super.notifyDataSetInvalidated();
        synchronized (mSelectedChildrenMap) {
            mSelectedChildrenMap.clear();
        }
    }// notifyDataSetInvalidated()

    /*
     * UTILITIES
     */

    /**
     * Gets the search text.
     * 
     * @return the search text, can be {@code null}.
     */
    public CharSequence getSearchText() {
        return mSearchText;
    }// getSearchText()

    /**
     * Sets search text.
     * 
     * @param searchText
     *            the search text.
     */
    public void setSearchText(CharSequence searchText) {
        mSearchText = searchText;
    }// setSearchText()

    /**
     * Selects all items in a specified group.
     * <p/>
     * <b>Note:</b> This will <i>not</i> notify data set for changes after done.
     * 
     * @param groupPosition
     *            the group position.
     * @param selected
     *            {@code true} or {@code false}.
     */
    private void asyncSelectAll(int groupPosition, boolean selected) {
        int chidrenCount = getChildrenCount(groupPosition);
        for (int iChild = 0; iChild < chidrenCount; iChild++) {
            Cursor cursor = getChild(groupPosition, iChild);
            final int id = cursor.getInt(cursor
                    .getColumnIndex(HistoryContract._ID));
            BagChildInfo childInfo = mSelectedChildrenMap.get(id);
            if (childInfo == null) {
                childInfo = new BagChildInfo();
                mSelectedChildrenMap.put(id, childInfo);
            }
            childInfo.mChecked = selected;
        }// for children
    }// asyncSelectAll()

    /**
     * Selects all items of a specified group.
     * <p/>
     * <b>Note:</b> This calls {@link #notifyDataSetChanged(boolean)} (with
     * {@code false}) after done.
     * 
     * @param groupPosition
     *            the group position.
     * @param selected
     *            {@code true} or {@code false}.
     */
    public synchronized void selectAll(int groupPosition, boolean selected) {
        asyncSelectAll(groupPosition, selected);
        notifyDataSetChanged(false);
    }// selectAll()

    /**
     * Selects all items.
     * <p/>
     * <b>Note:</b> This calls {@link #notifyDataSetChanged(boolean)} (with
     * {@code false}) after done.
     * 
     * @param selected
     *            {@code true} or {@code false}.
     */
    public synchronized void selectAll(boolean selected) {
        for (int iGroup = 0; iGroup < getGroupCount(); iGroup++)
            asyncSelectAll(iGroup, selected);
        notifyDataSetChanged(false);
    }// selectAll()

    /**
     * Inverts selection.
     * <p/>
     * <b>Note:</b> This will <i>not</i> notify data set for changes after done.
     * 
     * @param groupPosition
     *            the group position.
     */
    private void asyncInvertSelection(int groupPosition) {
        int chidrenCount = getChildrenCount(groupPosition);
        for (int iChild = 0; iChild < chidrenCount; iChild++) {
            Cursor cursor = getChild(groupPosition, iChild);
            final int id = cursor.getInt(cursor
                    .getColumnIndex(HistoryContract._ID));
            BagChildInfo childInfo = mSelectedChildrenMap.get(id);
            if (childInfo == null) {
                childInfo = new BagChildInfo();
                mSelectedChildrenMap.put(id, childInfo);
            }
            childInfo.mChecked = !childInfo.mChecked;
        }// for children
    }// asyncInvertSelection()

    /**
     * Inverts selection of all items of a specified group.
     * <p/>
     * <b>Note:</b> This calls {@link #notifyDataSetChanged(boolean)} (with
     * {@code false}) after done.
     * 
     * @param groupPosition
     *            the group position.
     */
    public synchronized void invertSelection(int groupPosition) {
        asyncInvertSelection(groupPosition);
        notifyDataSetChanged(false);
    }// invertSelection()

    /**
     * Inverts selection of all items.
     * <p/>
     * <b>Note:</b> This calls {@link #notifyDataSetChanged(boolean)} (with
     * {@code false}) after done.
     */
    public synchronized void invertSelection() {
        for (int iGroup = 0; iGroup < getGroupCount(); iGroup++)
            asyncInvertSelection(iGroup);
        notifyDataSetChanged(false);
    }// invertSelection()

    /**
     * Checks if item with {@code id} (the database ID) is selected or not.
     * 
     * @param id
     *            the database ID.
     * @return {@code true} or {@code false}.
     */
    public boolean isSelected(int id) {
        synchronized (mSelectedChildrenMap) {
            return mSelectedChildrenMap.get(id) != null ? mSelectedChildrenMap
                    .get(id).mChecked : false;
        }
    }// isSelected()

    /**
     * Gets IDs of selected items.
     * 
     * @return list of IDs, can be empty.
     */
    public List<Integer> getSelectedItemIds() {
        List<Integer> res = new ArrayList<Integer>();

        synchronized (mSelectedChildrenMap) {
            for (int i = 0; i < mSelectedChildrenMap.size(); i++)
                if (mSelectedChildrenMap.get(mSelectedChildrenMap.keyAt(i)).mChecked)
                    res.add(mSelectedChildrenMap.keyAt(i));
        }

        return res;
    }// getSelectedItemIds()

    /**
     * Marks all selected items as deleted.
     * <p/>
     * <b>Note:</b> This calls {@link #notifyDataSetChanged()} after done.
     * 
     * @param deleted
     *            {@code true} or {@code false}.
     */
    public void markSelectedItemsAsDeleted(boolean deleted) {
        synchronized (mSelectedChildrenMap) {
            for (int i = 0; i < mSelectedChildrenMap.size(); i++)
                if (mSelectedChildrenMap.get(mSelectedChildrenMap.keyAt(i)).mChecked)
                    mSelectedChildrenMap.get(mSelectedChildrenMap.keyAt(i)).mMarkedAsDeleted = deleted;
        }

        notifyDataSetChanged(false);
    }// markSelectedItemsAsDeleted()

    /**
     * Marks specified item as deleted.
     * <p/>
     * <b>Note:</b> This calls {@link #notifyDataSetChanged()} after done.
     * 
     * @param id
     *            the database ID of the item.
     * @param deleted
     *            {@code true} or {@code false}.
     */
    public void markItemAsDeleted(int id, boolean deleted) {
        synchronized (mSelectedChildrenMap) {
            if (mSelectedChildrenMap.get(id) != null) {
                mSelectedChildrenMap.get(id).mMarkedAsDeleted = deleted;
                notifyDataSetChanged(false);
            }
        }
    }// markItemAsDeleted()

    /*
     * STATIC UTILITIES
     */

    /**
     * Formats {@code millis} to time.
     * 
     * @param c
     *            {@link Context}.
     * @param millis
     *            the time in milliseconds.
     * @return the formatted time.
     */
    private static String formatDate(Context c, long millis) {
        if (android.text.format.DateUtils.isToday(millis))
            return c.getString(R.string.afc_today);

        Calendar cal = Calendar.getInstance();
        cal.setTimeInMillis(millis);

        final Calendar yesterday = Calendar.getInstance();
        yesterday.add(Calendar.DAY_OF_YEAR, -1);

        if (cal.get(Calendar.YEAR) == yesterday.get(Calendar.YEAR)) {
            if (cal.get(Calendar.DAY_OF_YEAR) == yesterday
                    .get(Calendar.DAY_OF_YEAR))
                return c.getString(R.string.afc_yesterday);
            else
                return android.text.format.DateUtils.formatDateTime(c, millis,
                        DateUtils.FORMAT_MONTH_AND_DAY);
        }

        return android.text.format.DateUtils.formatDateTime(c, millis,
                DateUtils.FORMAT_MONTH_AND_DAY | DateUtils.FORMAT_YEAR);
    }// formatDate()

    /**
     * Formats {@code millis} to short time. E.g: "10:01am".
     * 
     * @param c
     *            {@link Context}.
     * @param millis
     *            time in milliseconds.
     * @return the formatted time.
     */
    private static String formatTime(Context c, long millis) {
        return android.text.format.DateUtils.formatDateTime(c, millis,
                DateUtils.FORMAT_SHORT_TIME);
    }// formatTime()

}
