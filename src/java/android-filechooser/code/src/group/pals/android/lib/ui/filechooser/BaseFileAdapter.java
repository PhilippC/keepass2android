/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser;

import group.pals.android.lib.ui.filechooser.prefs.DisplayPrefs;
import group.pals.android.lib.ui.filechooser.prefs.DisplayPrefs.FileTimeDisplay;
import group.pals.android.lib.ui.filechooser.providers.BaseFileProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileContract.BaseFile;
import group.pals.android.lib.ui.filechooser.utils.Converter;
import group.pals.android.lib.ui.filechooser.utils.DateUtils;
import group.pals.android.lib.ui.filechooser.utils.Utils;
import group.pals.android.lib.ui.filechooser.utils.ui.ContextMenuUtils;
import group.pals.android.lib.ui.filechooser.utils.ui.LoadingDialog;
import group.pals.android.lib.ui.filechooser.utils.ui.Ui;

import java.util.ArrayList;

import android.content.Context;
import android.database.Cursor;
import android.net.Uri;
import android.support.v4.widget.ResourceCursorAdapter;
import android.util.Log;
import android.util.SparseArray;
import android.view.MotionEvent;
import android.view.View;
import android.widget.CheckBox;
import android.widget.CompoundButton;
import android.widget.GridView;
import android.widget.ImageView;
import android.widget.TextView;

/**
 * Adapter of base file.
 * 
 * @author Hai Bison
 * 
 */
public class BaseFileAdapter extends ResourceCursorAdapter {

    /**
     * Used for debugging...
     */
    private static final String CLASSNAME = BaseFileAdapter.class.getName();

    /**
     * Listener for building context menu editor.
     * 
     * @author Hai Bison
     * @since v5.1 beta
     */
    public static interface OnBuildOptionsMenuListener {

        /**
         * Will be called after the user touched on the icon of the item.
         * 
         * @param view
         *            the view displaying the item.
         * @param cursor
         *            the item which its icon has been touched.
         */
        void onBuildOptionsMenu(View view, Cursor cursor);

        /**
         * Will be called after the user touched and held ("long click") on the
         * icon of the item.
         * 
         * @param view
         *            the view displaying the item.
         * @param cursor
         *            the item which its icon has been touched.
         */
        void onBuildAdvancedOptionsMenu(View view, Cursor cursor);
    }// OnBuildOptionsMenuListener

    private final int mFilterMode;
    private final FileTimeDisplay mFileTimeDisplay;
    private final Integer[] mAdvancedSelectionOptions;
    private boolean mMultiSelection;
    private OnBuildOptionsMenuListener mOnBuildOptionsMenuListener;

    public BaseFileAdapter(Context context, int filterMode,
            boolean multiSelection) {
        super(context, R.layout.afc_file_item, null, 0);
        mFilterMode = filterMode;
        mMultiSelection = multiSelection;

        switch (mFilterMode) {
        case BaseFile.FILTER_FILES_AND_DIRECTORIES:
            mAdvancedSelectionOptions = new Integer[] {
                    R.string.afc_cmd_advanced_selection_all,
                    R.string.afc_cmd_advanced_selection_none,
                    R.string.afc_cmd_advanced_selection_invert,
                    R.string.afc_cmd_select_all_files,
                    R.string.afc_cmd_select_all_folders };
            break;// FILTER_FILES_AND_DIRECTORIES
        default:
            mAdvancedSelectionOptions = new Integer[] {
                    R.string.afc_cmd_advanced_selection_all,
                    R.string.afc_cmd_advanced_selection_none,
                    R.string.afc_cmd_advanced_selection_invert };
            break;// FILTER_DIRECTORIES_ONLY and FILTER_FILES_ONLY
        }

        mFileTimeDisplay = new FileTimeDisplay(
                DisplayPrefs.isShowTimeForOldDaysThisYear(context),
                DisplayPrefs.isShowTimeForOldDays(context));
    }// BaseFileAdapter()

    @Override
    public int getCount() {
        /*
         * The last item is used for information from the provider, we ignore
         * it.
         */
        int count = super.getCount();
        return count > 0 ? count - 1 : 0;
    }// getCount()

    /**
     * The "view holder"
     * 
     * @author Hai Bison
     */
    private static final class Bag {

        ImageView mImageIcon;
        ImageView mImageLockedSymbol;
        TextView mTxtFileName;
        TextView mTxtFileInfo;
        CheckBox mCheckboxSelection;
    }// Bag

    private static class BagInfo {

        boolean mChecked = false;
        boolean mMarkedAsDeleted = false;
        Uri mUri;
    }// BagChildInfo

    /**
     * Map of child IDs to {@link BagChildInfo}.
     */
    private final SparseArray<BagInfo> mSelectedChildrenMap = new SparseArray<BagInfo>();

    @Override
    public void bindView(View view, Context context, Cursor cursor) {
        Bag bag = (Bag) view.getTag();

        if (bag == null) {
            bag = new Bag();
            bag.mImageIcon = (ImageView) view
                    .findViewById(R.id.afc_imageview_icon);
            bag.mImageLockedSymbol = (ImageView) view
                    .findViewById(R.id.afc_imageview_locked_symbol);
            bag.mTxtFileName = (TextView) view
                    .findViewById(R.id.afc_textview_filename);
            bag.mTxtFileInfo = (TextView) view
                    .findViewById(R.id.afc_textview_file_info);
            bag.mCheckboxSelection = (CheckBox) view
                    .findViewById(R.id.afc_checkbox_selection);

            view.setTag(bag);
        }

        final int id = cursor.getInt(cursor.getColumnIndex(BaseFile._ID));
        final Uri uri = BaseFileProviderUtils.getUri(cursor);

        final BagInfo bagInfo;
        if (mSelectedChildrenMap.get(id) == null) {
            bagInfo = new BagInfo();
            bagInfo.mUri = uri;
            mSelectedChildrenMap.put(id, bagInfo);
        } else
            bagInfo = mSelectedChildrenMap.get(id);

        /*
         * Update views.
         */

        /*
         * Use single line for grid view, multiline for list view
         */
        bag.mTxtFileName.setSingleLine(view.getParent() instanceof GridView);

        /*
         * File icon.
         */
        bag.mImageLockedSymbol.setVisibility(cursor.getInt(cursor
                .getColumnIndex(BaseFile.COLUMN_CAN_READ)) > 0 ? View.GONE
                : View.VISIBLE);
        bag.mImageIcon.setImageResource(cursor.getInt(cursor
                .getColumnIndex(BaseFile.COLUMN_ICON_ID)));
        bag.mImageIcon.setOnTouchListener(mImageIconOnTouchListener);
        bag.mImageIcon.setOnClickListener(BaseFileProviderUtils
                .isDirectory(cursor) ? newImageIconOnClickListener(cursor
                .getPosition()) : null);

        /*
         * Filename.
         */
        bag.mTxtFileName.setText(BaseFileProviderUtils.getFileName(cursor));
        Ui.strikeOutText(bag.mTxtFileName, bagInfo.mMarkedAsDeleted);

        /*
         * File info.
         */
        String time = DateUtils.formatDate(context, cursor.getLong(cursor
                .getColumnIndex(BaseFile.COLUMN_MODIFICATION_TIME)),
                mFileTimeDisplay);
        if (BaseFileProviderUtils.isFile(cursor))
            bag.mTxtFileInfo.setText(String.format("%s, %s", Converter
                    .sizeToStr(cursor.getLong(cursor
                            .getColumnIndex(BaseFile.COLUMN_SIZE))), time));
        else
            bag.mTxtFileInfo.setText(time);

        /*
         * Check box.
         */
        if (mMultiSelection) {
            if (mFilterMode == BaseFile.FILTER_FILES_ONLY
                    && BaseFileProviderUtils.isDirectory(cursor)) {
                bag.mCheckboxSelection.setVisibility(View.GONE);
            } else {
                bag.mCheckboxSelection.setVisibility(View.VISIBLE);

                bag.mCheckboxSelection.setOnCheckedChangeListener(null);
                bag.mCheckboxSelection.setChecked(bagInfo.mChecked);
                bag.mCheckboxSelection
                        .setOnCheckedChangeListener(new CompoundButton.OnCheckedChangeListener() {

                            @Override
                            public void onCheckedChanged(
                                    CompoundButton buttonView, boolean isChecked) {
                                bagInfo.mChecked = isChecked;
                            }// onCheckedChanged()
                        });

                bag.mCheckboxSelection
                        .setOnLongClickListener(mCheckboxSelectionOnLongClickListener);
            }
        } else
            bag.mCheckboxSelection.setVisibility(View.GONE);
    }// bindView()

    @Override
    public void changeCursor(Cursor cursor) {
        super.changeCursor(cursor);
        synchronized (mSelectedChildrenMap) {
            mSelectedChildrenMap.clear();
        }
    }// changeCursor()

    /*
     * UTILITIES.
     */

    /**
     * Sets the listener {@link OnBuildOptionsMenuListener}.
     * 
     * @param listener
     *            the listener.
     */
    public void setBuildOptionsMenuListener(OnBuildOptionsMenuListener listener) {
        mOnBuildOptionsMenuListener = listener;
    }// setBuildOptionsMenuListener()

    /**
     * Gets the listener {@link OnBuildOptionsMenuListener}.
     * 
     * @return the listener.
     */
    public OnBuildOptionsMenuListener getOnBuildOptionsMenuListener() {
        return mOnBuildOptionsMenuListener;
    }// getOnBuildOptionsMenuListener()

    /**
     * Gets the short name of this path.
     * 
     * @return the path name, can be {@code null} if there is no data.
     */
    public String getPathName() {
        Cursor cursor = getCursor();
        if (cursor == null || !cursor.moveToLast())
            return null;
        return BaseFileProviderUtils.getFileName(cursor);
    }// getPathName()

    /**
     * Selects all items.
     * <p/>
     * <b>Note:</b> This will <i>not</i> notify data set for changes after done.
     * 
     * @param fileType
     *            can be {@code -1} for all file types; or one of
     *            {@link BaseFile#FILE_TYPE_DIRECTORY},
     *            {@link BaseFile#FILE_TYPE_FILE}.
     * @param selected
     *            {@code true} or {@code false}.
     */
    private void asyncSelectAll(int fileType, boolean selected) {
        int count = getCount();
        for (int i = 0; i < count; i++) {
            Cursor cursor = (Cursor) getItem(i);

            int itemFileType = cursor.getInt(cursor
                    .getColumnIndex(BaseFile.COLUMN_TYPE));
            if ((mFilterMode == BaseFile.FILTER_DIRECTORIES_ONLY && itemFileType == BaseFile.FILE_TYPE_FILE)
                    || (mFilterMode == BaseFile.FILTER_FILES_ONLY && itemFileType == BaseFile.FILE_TYPE_DIRECTORY))
                continue;

            final int id = cursor.getInt(cursor.getColumnIndex(BaseFile._ID));
            BagInfo b = mSelectedChildrenMap.get(id);
            if (b == null) {
                b = new BagInfo();
                b.mUri = BaseFileProviderUtils.getUri(cursor);
                mSelectedChildrenMap.put(id, b);
            }

            if (fileType >= 0 && itemFileType != fileType)
                b.mChecked = false;
            else if (b.mChecked != selected)
                b.mChecked = selected;
        }// for i
    }// asyncSelectAll()

    /**
     * Selects all items.
     * <p/>
     * <b>Note:</b> This calls {@link #notifyDataSetChanged()} when done.
     * 
     * @param selected
     *            {@code true} or {@code false}.
     */
    public synchronized void selectAll(boolean selected) {
        asyncSelectAll(-1, selected);
        notifyDataSetChanged();
    }// selectAll()

    /**
     * Inverts selection of all items.
     * <p/>
     * <b>Note:</b> This will <i>not</i> notify data set for changes after done.
     */
    private void asyncInvertSelection() {
        int count = getCount();
        for (int i = 0; i < count; i++) {
            Cursor cursor = (Cursor) getItem(i);

            int fileType = cursor.getInt(cursor
                    .getColumnIndex(BaseFile.COLUMN_TYPE));
            if ((mFilterMode == BaseFile.FILTER_DIRECTORIES_ONLY && fileType == BaseFile.FILE_TYPE_FILE)
                    || (mFilterMode == BaseFile.FILTER_FILES_ONLY && fileType == BaseFile.FILE_TYPE_DIRECTORY))
                continue;

            final int id = cursor.getInt(cursor.getColumnIndex(BaseFile._ID));
            BagInfo b = mSelectedChildrenMap.get(id);
            if (b == null) {
                b = new BagInfo();
                b.mUri = BaseFileProviderUtils.getUri(cursor);
                mSelectedChildrenMap.put(id, b);
            }
            b.mChecked = !b.mChecked;
        }// for i
    }// asyncInvertSelection()

    /**
     * Inverts selection of all items.
     * <p/>
     * <b>Note:</b> This calls {@link #notifyDataSetChanged()} after done.
     */
    public synchronized void invertSelection() {
        asyncInvertSelection();
        notifyDataSetChanged();
    }// invertSelection()

    /**
     * Checks if item with {@code id} is selected or not.
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
     * Gets selected items.
     * 
     * @return list of URIs, can be empty.
     */
    public ArrayList<Uri> getSelectedItems() {
        ArrayList<Uri> res = new ArrayList<Uri>();

        synchronized (mSelectedChildrenMap) {
            for (int i = 0; i < mSelectedChildrenMap.size(); i++)
                if (mSelectedChildrenMap.get(mSelectedChildrenMap.keyAt(i)).mChecked)
                    res.add(mSelectedChildrenMap.get(mSelectedChildrenMap
                            .keyAt(i)).mUri);
        }

        return res;
    }// getSelectedItems()

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

        notifyDataSetChanged();
    }// markSelectedItemsAsDeleted()

    /**
     * Marks specified item as deleted.
     * <p/>
     * <b>Note:</b> This calls {@link #notifyDataSetChanged()} after done.
     * 
     * @param id
     *            the ID of the item.
     * @param deleted
     *            {@code true} or {@code false}.
     */
    public void markItemAsDeleted(int id, boolean deleted) {
        synchronized (mSelectedChildrenMap) {
            if (mSelectedChildrenMap.get(id) != null) {
                mSelectedChildrenMap.get(id).mMarkedAsDeleted = deleted;
                notifyDataSetChanged();
            }
        }
    }// markItemAsDeleted()

    /*
     * LISTENERS
     */

    /**
     * If the user touches the list item, and the image icon <i>declared</i> a
     * selector in XML, then that selector works. But we just want the selector
     * to work only when the user touches the image, hence this listener.
     */
    private final View.OnTouchListener mImageIconOnTouchListener = new View.OnTouchListener() {

        @Override
        public boolean onTouch(View v, MotionEvent event) {
            if (Utils.doLog())
                Log.d(CLASSNAME,
                        "mImageIconOnTouchListener.onTouch() >> ACTION = "
                                + event.getAction());

            switch (event.getAction()) {
            case MotionEvent.ACTION_DOWN:
                v.setBackgroundResource(R.drawable.afc_image_button_dark_pressed);
                break;
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL:
                v.setBackgroundResource(0);
                break;
            }
            return false;
        }// onTouch()
    };// mImageIconOnTouchListener

    /**
     * Creates new listener to handle click event of image icon.
     * 
     * @param cursorPosition
     *            the cursor position.
     * @return the listener.
     */
    private View.OnClickListener newImageIconOnClickListener(
            final int cursorPosition) {
        return new View.OnClickListener() {

            @Override
            public void onClick(View v) {
                if (getOnBuildOptionsMenuListener() != null)
                    getOnBuildOptionsMenuListener().onBuildOptionsMenu(v,
                            (Cursor) getItem(cursorPosition));
            }// onClick()
        };
    }// newImageIconOnClickListener()

    private final View.OnLongClickListener mCheckboxSelectionOnLongClickListener = new View.OnLongClickListener() {

        @Override
        public boolean onLongClick(final View v) {
            ContextMenuUtils.showContextMenu(v.getContext(), 0,
                    R.string.afc_title_advanced_selection,
                    mAdvancedSelectionOptions,
                    new ContextMenuUtils.OnMenuItemClickListener() {

                        @Override
                        public void onClick(final int resId) {
                            new LoadingDialog<Void, Void, Void>(v.getContext(),
                                    R.string.afc_msg_loading, false) {

                                @Override
                                protected Void doInBackground(Void... params) {
                                    if (resId == R.string.afc_cmd_advanced_selection_all)
                                        asyncSelectAll(-1, true);
                                    else if (resId == R.string.afc_cmd_advanced_selection_none)
                                        asyncSelectAll(-1, false);
                                    else if (resId == R.string.afc_cmd_advanced_selection_invert)
                                        asyncInvertSelection();
                                    else if (resId == R.string.afc_cmd_select_all_files)
                                        asyncSelectAll(BaseFile.FILE_TYPE_FILE,
                                                true);
                                    else if (resId == R.string.afc_cmd_select_all_folders)
                                        asyncSelectAll(
                                                BaseFile.FILE_TYPE_DIRECTORY,
                                                true);

                                    return null;
                                }// doInBackground()

                                @Override
                                protected void onPostExecute(Void result) {
                                    super.onPostExecute(result);
                                    notifyDataSetChanged();
                                }// onPostExecute()
                            }.execute();
                        }// onClick()
                    });

            return true;
        }// onLongClick()
    };// mCheckboxSelectionOnLongClickListener

}
