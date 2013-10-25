/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.ui.bookmark;

import group.pals.android.lib.ui.filechooser.BuildConfig;
import group.pals.android.lib.ui.filechooser.R;
import group.pals.android.lib.ui.filechooser.providers.BaseFileProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.bookmark.BookmarkContract;
import group.pals.android.lib.ui.filechooser.utils.ui.ContextMenuUtils;
import group.pals.android.lib.ui.filechooser.utils.ui.Ui;

import java.util.ArrayList;
import java.util.List;

import android.content.Context;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.util.Log;
import android.util.SparseArray;
import android.view.View;
import android.widget.CheckBox;
import android.widget.CompoundButton;
import android.widget.ResourceCursorTreeAdapter;
import android.widget.TextView;

/**
 * Bookmark cursor adapter.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class BookmarkCursorAdapter extends ResourceCursorTreeAdapter {

    private static final String CLASSNAME = BookmarkCursorAdapter.class
            .getName();

    /**
     * Advanced selection options: All, None, Invert.
     */
    public static final Integer[] ADVANCED_SELECTION_OPTIONS = new Integer[] {
            R.string.afc_cmd_advanced_selection_all,
            R.string.afc_cmd_advanced_selection_none,
            R.string.afc_cmd_advanced_selection_invert };

    /**
     * The "view holder".
     * 
     * @author Hai Bison
     */
    private static class BagGroup {

        TextView mTextHeader;
    }// BagGroup

    /**
     * The "view holder".
     * 
     * @author Hai Bison
     */
    private static class BagChild {

        TextView mTextName;
        TextView mTextPath;
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

    private static final String[] GROUP_CURSOR_COLUMNS = {
            BookmarkContract._ID, BookmarkContract.COLUMN_PROVIDER_ID,
            COLUMN_ORG_GROUP_POSITION };

    private static final String[] CHILD_CURSOR_COLUMNS = {
            BookmarkContract._ID, BookmarkContract.COLUMN_NAME,
            BookmarkContract.COLUMN_URI, BookmarkContract.COLUMN_PROVIDER_ID,
            BookmarkContract.COLUMN_MODIFICATION_TIME };

    /**
     * Map of child IDs to {@link BagChildInfo}.
     */
    private final SparseArray<BagChildInfo> mSelectedChildrenMap = new SparseArray<BagChildInfo>();
    private boolean mEditor;

    private Cursor mOrgCursor;
    private MatrixCursor mGroupCursor;
    private SparseArray<MatrixCursor> mChildrenCursor;

    /**
     * Creates new instance.
     * 
     * @param context
     *            {@link Context}.
     */
    public BookmarkCursorAdapter(Context context) {
        super(context, null, R.layout.afc_view_bookmark_item,
                R.layout.afc_view_bookmark_sub_item);
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
            String lastProviderId = null;
            do {
                String providerId = cursor.getString(cursor
                        .getColumnIndex(BookmarkContract.COLUMN_PROVIDER_ID));

                if (!providerId.equals(lastProviderId)) {
                    newGroupCursor
                            .addRow(new Object[] {
                                    cursor.getInt(cursor
                                            .getColumnIndex(BookmarkContract._ID)),
                                    cursor.getString(cursor
                                            .getColumnIndex(BookmarkContract.COLUMN_PROVIDER_ID)),
                                    cursor.getPosition() });
                }
                lastProviderId = providerId;
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
        String providerId = groupCursor.getString(groupCursor
                .getColumnIndex(BookmarkContract.COLUMN_PROVIDER_ID));
        do {
            childrenCursor
                    .addRow(new Object[] {
                            mOrgCursor.getInt(mOrgCursor
                                    .getColumnIndex(BookmarkContract._ID)),
                            mOrgCursor.getString(mOrgCursor
                                    .getColumnIndex(BookmarkContract.COLUMN_NAME)),
                            mOrgCursor.getString(mOrgCursor
                                    .getColumnIndex(BookmarkContract.COLUMN_URI)),
                            mOrgCursor.getString(mOrgCursor
                                    .getColumnIndex(BookmarkContract.COLUMN_PROVIDER_ID)),
                            mOrgCursor.getString(mOrgCursor
                                    .getColumnIndex(BookmarkContract.COLUMN_MODIFICATION_TIME)) });
        } while (mOrgCursor.moveToNext()
                && mOrgCursor
                        .getString(
                                mOrgCursor
                                        .getColumnIndex(BookmarkContract.COLUMN_PROVIDER_ID))
                        .equals(providerId));

        /*
         * Put it to the map.
         */
        mChildrenCursor.put(orgGroupPosition, childrenCursor);
        return childrenCursor;
    }// getChildrenCursor()

    @Override
    protected void bindChildView(View view, Context context, Cursor cursor,
            boolean isLastChild) {
        final int id = cursor.getInt(cursor
                .getColumnIndex(BookmarkContract._ID));
        Uri uri = Uri.parse(cursor.getString(cursor
                .getColumnIndex(BookmarkContract.COLUMN_URI)));

        /*
         * Child Info
         */
        final BagChildInfo childInfo;
        if (mSelectedChildrenMap.get(id) == null) {
            childInfo = new BagChildInfo();
            mSelectedChildrenMap.put(id, childInfo);
        } else
            childInfo = mSelectedChildrenMap.get(id);

        /*
         * Child
         */
        BagChild bag = (BagChild) view.getTag();

        if (bag == null) {
            bag = new BagChild();
            bag.mTextName = (TextView) view.findViewById(R.id.afc_text_name);
            bag.mTextPath = (TextView) view.findViewById(R.id.afc_text_path);
            bag.mCheckBox = (CheckBox) view.findViewById(R.id.afc_checkbox);

            view.setTag(bag);
        }

        /*
         * Name.
         */

        bag.mTextName.setText(cursor.getString(cursor
                .getColumnIndex(BookmarkContract.COLUMN_NAME)));
        Ui.strikeOutText(bag.mTextName, childInfo.mMarkedAsDeleted);

        /*
         * Path.
         */

        if (isEditor()) {
            bag.mTextPath.setVisibility(View.VISIBLE);
            bag.mTextPath.setText(BaseFileProviderUtils
                    .getRealUri(context, uri).toString());
        } else
            bag.mTextPath.setVisibility(View.GONE);

        /*
         * Checkbox.
         */

        bag.mCheckBox.setVisibility(isEditor() ? View.VISIBLE : View.GONE);
        bag.mCheckBox.setOnCheckedChangeListener(null);
        bag.mCheckBox.setChecked(childInfo.mChecked);
        bag.mCheckBox
                .setOnCheckedChangeListener(new CompoundButton.OnCheckedChangeListener() {

                    @Override
                    public void onCheckedChanged(CompoundButton buttonView,
                            boolean isChecked) {
                        childInfo.mChecked = isChecked;
                    }// onCheckedChanged()
                });

        bag.mCheckBox.setOnLongClickListener(new View.OnLongClickListener() {

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
            b.mTextHeader = (TextView) view
                    .findViewById(R.id.afc_textview_header);

            view.setTag(b);
        } else
            b = (BagGroup) view.getTag();

        /*
         * Provider name.
         */
        String providerId = cursor.getString(cursor
                .getColumnIndex(BookmarkContract.COLUMN_PROVIDER_ID));
        b.mTextHeader.setText(BaseFileProviderUtils.getProviderName(context,
                providerId));
        /*
         * Provider badge icon.
         */
        b.mTextHeader.setCompoundDrawablesWithIntrinsicBounds(
                BaseFileProviderUtils.getProviderIconId(context, providerId),
                0, 0, 0);
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
     * Checks if this is in editor mode.
     * 
     * @return {@code true} or {@code false}.
     */
    public boolean isEditor() {
        return mEditor;
    }// isEditor()

    /**
     * Sets editor mode.
     * <p/>
     * <b>Note:</b> This calls {@link #notifyDataSetChanged(boolean)} (with
     * {@code false}) after done.
     * 
     * @param v
     *            {@code true} or {@code false}.
     */
    public void setEditor(boolean v) {
        if (mEditor != v) {
            mEditor = v;
            notifyDataSetChanged(false);
        }
    }// setEditor()

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
                    .getColumnIndex(BookmarkContract._ID));
            BagChildInfo b = mSelectedChildrenMap.get(id);
            if (b == null) {
                b = new BagChildInfo();
                mSelectedChildrenMap.put(id, b);
            }
            b.mChecked = selected;
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
                    .getColumnIndex(BookmarkContract._ID));
            BagChildInfo b = mSelectedChildrenMap.get(id);
            if (b == null) {
                b = new BagChildInfo();
                mSelectedChildrenMap.put(id, b);
            }
            b.mChecked = !b.mChecked;
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

}
