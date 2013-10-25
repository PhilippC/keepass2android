/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.ui.bookmark;

import group.pals.android.lib.ui.filechooser.BuildConfig;
import group.pals.android.lib.ui.filechooser.R;
import group.pals.android.lib.ui.filechooser.prefs.DisplayPrefs;
import group.pals.android.lib.ui.filechooser.providers.DbUtils;
import group.pals.android.lib.ui.filechooser.providers.bookmark.BookmarkContract;
import group.pals.android.lib.ui.filechooser.utils.EnvUtils;
import group.pals.android.lib.ui.filechooser.utils.TextUtils;
import group.pals.android.lib.ui.filechooser.utils.ui.ContextMenuUtils;
import group.pals.android.lib.ui.filechooser.utils.ui.Dlg;
import group.pals.android.lib.ui.filechooser.utils.ui.GestureUtils;
import group.pals.android.lib.ui.filechooser.utils.ui.GestureUtils.FlingDirection;
import group.pals.android.lib.ui.filechooser.utils.ui.Ui;
import group.pals.android.lib.ui.filechooser.utils.ui.history.HistoryFragment;

import java.text.Collator;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.Date;
import java.util.List;

import android.app.AlertDialog;
import android.app.Dialog;
import android.content.ContentResolver;
import android.content.ContentUris;
import android.content.ContentValues;
import android.content.Context;
import android.content.DialogInterface;
import android.database.Cursor;
import android.database.DatabaseUtils;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.support.v4.app.DialogFragment;
import android.support.v4.app.LoaderManager;
import android.support.v4.content.CursorLoader;
import android.support.v4.content.Loader;
import android.text.Editable;
import android.text.TextWatcher;
import android.util.Log;
import android.util.SparseArray;
import android.view.KeyEvent;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;
import android.view.inputmethod.EditorInfo;
import android.widget.AdapterView;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ExpandableListView;
import android.widget.TextView;

/**
 * Fragment to manage bookmarks.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class BookmarkFragment extends DialogFragment implements
        LoaderManager.LoaderCallbacks<Cursor> {

    /**
     * As the name means.
     * 
     * @author Hai Bison
     * @since v5.1 beta
     */
    public static interface OnBookmarkItemClickListener {

        /**
         * Will be called after the bookmark was clicked.
         * 
         * @param providerId
         *            the original provider ID.
         * @param uri
         *            the URI to a directory.
         */
        void onItemClick(String providerId, Uri uri);
    }// OnBookmarkItemClickListener

    /**
     * Used for debugging or something...
     */
    private static final String CLASSNAME = BookmarkFragment.class.getName();

    private static final String MODE_EDITOR = CLASSNAME + ".mode_editor";

    private final int mLoaderBookmarkData = EnvUtils.genId();

    /**
     * Creates a new instance of {@link HistoryFragment}.
     * 
     * @param editor
     *            {@code true} if you want to use this as an editor, and
     *            {@code false} as a viewer.
     * @return {@link BookmarkFragment}.
     */
    public static BookmarkFragment newInstance(boolean editor) {
        Bundle args = new Bundle();
        args.putBoolean(MODE_EDITOR, editor);

        BookmarkFragment res = new BookmarkFragment();
        res.setArguments(args);

        return res;
    }// newInstance()

    /*
     * Controls.
     */

    private View mViewGroupControls;
    private ExpandableListView mListView;
    private ViewGroup mViewFooter;
    private Button mBtnClear;
    private Button mBtnOk;
    private View mViewLoading;

    /*
     * Fields.
     */

    private final Handler mHandler = new Handler();
    private boolean mEditor = false;
    private BookmarkCursorAdapter mBookmarkCursorAdapter;
    private OnBookmarkItemClickListener mOnBookmarkItemClickListener;

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        mEditor = getArguments().getBoolean(MODE_EDITOR);
    }// onCreate()

    @Override
    public Dialog onCreateDialog(Bundle savedInstanceState) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onCreateDialog()");

        Dialog dialog = new Dialog(getActivity(), Ui.resolveAttribute(
                getActivity(), R.attr.afc_theme_dialog));
        dialog.setCanceledOnTouchOutside(true);
        dialog.requestWindowFeature(Window.FEATURE_LEFT_ICON);
        dialog.setTitle(R.string.afc_title_bookmark_manager);
        dialog.setContentView(initContentView(dialog.getLayoutInflater(), null));
        dialog.setFeatureDrawableResource(Window.FEATURE_LEFT_ICON,
                R.drawable.afc_bookmarks_dark);

        return dialog;
    }// onCreateDialog()

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container,
            Bundle savedInstanceState) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onCreateView()");
        if (getDialog() != null) {
            getDialog().setOnKeyListener(new DialogInterface.OnKeyListener() {

                @Override
                public boolean onKey(DialogInterface dialog, int keyCode,
                        KeyEvent event) {
                    /*
                     * Don't let the Search key dismiss this dialog.
                     */
                    return keyCode == KeyEvent.KEYCODE_SEARCH;
                }// onKey()
            });

            return null;
        }

        return initContentView(inflater, container);
    }// onCreateView()

    @Override
    public void onActivityCreated(Bundle savedInstanceState) {
        super.onActivityCreated(savedInstanceState);

        /*
         * Prepare the loader. Either re-connect with an existing one, or start
         * a new one.
         */
        getLoaderManager().initLoader(mLoaderBookmarkData, null, this);
    }// onActivityCreated()

    /*
     * LOADERMANAGER.LOADERCALLBACKS
     */

    @Override
    public Loader<Cursor> onCreateLoader(int id, Bundle args) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onCreateLoader()");
        if (id == mLoaderBookmarkData) {
            mHandler.removeCallbacksAndMessages(null);
            mHandler.postDelayed(mViewLoadingShower,
                    DisplayPrefs.DELAY_TIME_FOR_SIMPLE_ANIMATION);

            mBookmarkCursorAdapter.changeCursor(null);

            return new CursorLoader(getActivity(),
                    BookmarkContract.genContentUri(getActivity()), null, null,
                    null, String.format("%s, %s DESC",
                            BookmarkContract.COLUMN_PROVIDER_ID,
                            BookmarkContract.COLUMN_MODIFICATION_TIME));
        }// mLoaderBookmarkData

        return null;
    }// onCreateLoader()

    @Override
    public void onLoadFinished(Loader<Cursor> loader, Cursor data) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onLoadFinished() -- data = " + data);

        if (loader.getId() == mLoaderBookmarkData) {
            mBookmarkCursorAdapter.changeCursor(data);

            for (int i = 0; i < mBookmarkCursorAdapter.getGroupCount(); i++)
                mListView.expandGroup(i);
            updateUI();

            /*
             * Views visibilities. Always call these to make sure all views are
             * in right visibilities.
             */
            mHandler.removeCallbacksAndMessages(null);
            mViewLoading.setVisibility(View.GONE);
            mViewGroupControls.setVisibility(View.VISIBLE);

            mListView.post(new Runnable() {

                @Override
                public void run() {
                    mListView.setSelection(-1);
                }
            });
        }// mLoaderBookmarkData
    }// onLoadFinished()

    @Override
    public void onLoaderReset(Loader<Cursor> loader) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onLoaderReset()");

        if (loader.getId() == mLoaderBookmarkData) {
            mBookmarkCursorAdapter.changeCursor(null);
            mViewLoading.setVisibility(View.VISIBLE);
        }// mLoaderBookmarkData
    }// onLoaderReset()

    /**
     * Loads content view from XML and init controls.
     */
    private View initContentView(LayoutInflater inflater, ViewGroup container) {
        View mainView = inflater.inflate(R.layout.afc_viewgroup_bookmarks,
                container, false);

        /*
         * Maps controls.
         */

        mViewGroupControls = mainView.findViewById(R.id.afc_viewgroup_controls);
        mListView = (ExpandableListView) mainView
                .findViewById(R.id.afc_listview_bookmarks);
        mViewFooter = (ViewGroup) mainView
                .findViewById(R.id.afc_viewgroup_footer);
        mBtnClear = (Button) mainView.findViewById(R.id.afc_button_clear);
        mBtnOk = (Button) mainView.findViewById(R.id.afc_button_ok);
        mViewLoading = mainView.findViewById(R.id.afc_view_loading);

        if (mEditor) {
            mViewFooter.setVisibility(View.VISIBLE);
        }

        /*
         * Listview.
         */

        mListView.setEmptyView(mainView.findViewById(R.id.afc_empty_view));
        mListView.setOnChildClickListener(mListViewOnChildClickListener);
        mListView.setOnItemLongClickListener(mListViewOnItemLongClickListener);
        initListViewGestureListener();

        /*
         * Adapter.
         */

        mBookmarkCursorAdapter = new BookmarkCursorAdapter(getActivity());
        mBookmarkCursorAdapter.setEditor(mEditor);
        mListView.setAdapter(mBookmarkCursorAdapter);

        /*
         * Events.
         */

        mBtnClear.setOnClickListener(mBtnClearOnClickListener);
        mBtnOk.setOnClickListener(mBtnOkOnClickListener);

        return mainView;
    }// initContentView()

    /**
     * As the name means.
     */
    private void initListViewGestureListener() {
        GestureUtils.setupGestureDetector(mListView,
                new GestureUtils.SimpleOnGestureListener() {

                    @Override
                    public boolean onFling(View view, Object data,
                            FlingDirection flingDirection) {
                        if (!isEditor() || !(data instanceof Cursor))
                            return false;

                        List<Integer> ids = new ArrayList<Integer>();

                        final int bookmarkId = ((Cursor) data)
                                .getInt(((Cursor) data)
                                        .getColumnIndex(BookmarkContract._ID));
                        if (mBookmarkCursorAdapter.isSelected(bookmarkId))
                            ids.addAll(mBookmarkCursorAdapter
                                    .getSelectedItemIds());
                        else
                            ids.add(bookmarkId);

                        if (ids.size() <= 1)
                            mBookmarkCursorAdapter.markItemAsDeleted(
                                    bookmarkId, true);
                        else
                            mBookmarkCursorAdapter
                                    .markSelectedItemsAsDeleted(true);

                        final StringBuilder sb = new StringBuilder(String
                                .format("%s in (",
                                        DbUtils.SQLITE_FTS_COLUMN_ROW_ID));
                        for (int id : ids)
                            sb.append(Integer.toString(id)).append(',');
                        sb.setCharAt(sb.length() - 1, ')');

                        new Handler().postDelayed(new Runnable() {

                            @Override
                            public void run() {
                                getActivity().getContentResolver().delete(
                                        BookmarkContract
                                                .genContentUri(getActivity()),
                                        sb.toString(), null);
                            }// run()
                        }, DisplayPrefs.DELAY_TIME_FOR_VERY_SHORT_ANIMATION);

                        return true;
                    }// onFling()
                });
    }// initListViewGestureListener()

    /**
     * Updates UI.
     */
    private void updateUI() {
        mViewFooter.setVisibility(isEditor() ? View.VISIBLE : View.GONE);
        mBtnClear.setEnabled(mBookmarkCursorAdapter.getGroupCount() > 0);
    }// updateUI()

    /*
     * UTILITIES
     */

    /**
     * Enables or disables editor mode.
     * 
     * @param editor
     *            {@code true} to enable, {@code false} to disable.
     */
    public void setEditor(boolean editor) {
        if (mEditor != editor) {
            mEditor = editor;
            if (mBookmarkCursorAdapter != null)
                mBookmarkCursorAdapter.setEditor(mEditor);

            updateUI();
        }
    }// setEditor()

    /**
     * Checks if current mode is editor or not.
     * 
     * @return {@code true} if current mode is editor.
     */
    public boolean isEditor() {
        return mEditor;
    }// isEditor()

    /**
     * Sets a listener to {@link OnBookmarkItemClickListener}.
     * 
     * @param listener
     *            the listener.
     */
    public void setOnBookmarkItemClickListener(
            OnBookmarkItemClickListener listener) {
        mOnBookmarkItemClickListener = listener;
    }// setOnBookmarkItemClickListener()

    /**
     * Gets the listener of {@link OnBookmarkItemClickListener}.
     * 
     * @return the listener.
     */
    public OnBookmarkItemClickListener getOnBookmarkItemClickListener() {
        return mOnBookmarkItemClickListener;
    }// getOnBookmarkItemClickListener()

    /*
     * LISTENERS
     */

    private final Runnable mViewLoadingShower = new Runnable() {

        @Override
        public void run() {
            if (isAdded()) {
                mViewGroupControls.setVisibility(View.GONE);
                mViewLoading.setVisibility(View.VISIBLE);
            }
        }// run()
    };// mViewLoadingShower

    private final ExpandableListView.OnChildClickListener mListViewOnChildClickListener = new ExpandableListView.OnChildClickListener() {

        @Override
        public boolean onChildClick(ExpandableListView parent, View v,
                int groupPosition, int childPosition, long id) {
            if (getOnBookmarkItemClickListener() != null) {
                Cursor cursor = mBookmarkCursorAdapter.getChild(groupPosition,
                        childPosition);
                getOnBookmarkItemClickListener()
                        .onItemClick(
                                cursor.getString(cursor
                                        .getColumnIndex(BookmarkContract.COLUMN_PROVIDER_ID)),
                                Uri.parse(cursor.getString(cursor
                                        .getColumnIndex(BookmarkContract.COLUMN_URI))));
            }

            if (getDialog() != null)
                dismiss();

            return false;
        }// onChildClick()
    };// mListViewOnChildClickListener

    private final AdapterView.OnItemLongClickListener mListViewOnItemLongClickListener = new AdapterView.OnItemLongClickListener() {

        @Override
        public boolean onItemLongClick(AdapterView<?> parent, View view,
                int position, long id) {
            final int iGroup = ExpandableListView
                    .getPackedPositionGroup(mListView
                            .getExpandableListPosition(position));
            final int iChild = ExpandableListView
                    .getPackedPositionChild(mListView
                            .getExpandableListPosition(position));

            switch (ExpandableListView.getPackedPositionType(id)) {
            case ExpandableListView.PACKED_POSITION_TYPE_GROUP:
                if (!isEditor())
                    return false;

                if (!mListView.isGroupExpanded(iGroup))
                    return false;

                if (BuildConfig.DEBUG)
                    Log.d(CLASSNAME, String.format(
                            "onItemLongClick() -- group = %,d", iGroup));
                ContextMenuUtils.showContextMenu(getActivity(), 0,
                        R.string.afc_title_advanced_selection,
                        BookmarkCursorAdapter.ADVANCED_SELECTION_OPTIONS,
                        new ContextMenuUtils.OnMenuItemClickListener() {

                            @Override
                            public void onClick(final int resId) {
                                if (resId == R.string.afc_cmd_advanced_selection_all)
                                    mBookmarkCursorAdapter.selectAll(iGroup,
                                            true);
                                else if (resId == R.string.afc_cmd_advanced_selection_none)
                                    mBookmarkCursorAdapter.selectAll(iGroup,
                                            false);
                                else if (resId == R.string.afc_cmd_advanced_selection_invert)
                                    mBookmarkCursorAdapter
                                            .invertSelection(iGroup);
                            }// onClick()
                        });

                return true;// PACKED_POSITION_TYPE_GROUP

            case ExpandableListView.PACKED_POSITION_TYPE_CHILD:
                Cursor cursor = mBookmarkCursorAdapter.getChild(iGroup, iChild);
                final String providerId = cursor.getString(cursor
                        .getColumnIndex(BookmarkContract.COLUMN_PROVIDER_ID));
                final int bookmarkId = cursor.getInt(cursor
                        .getColumnIndex(BookmarkContract._ID));
                final Uri uri = Uri.parse(cursor.getString(cursor
                        .getColumnIndex(BookmarkContract.COLUMN_URI)));
                final String name = cursor.getString(cursor
                        .getColumnIndex(BookmarkContract.COLUMN_NAME));

                ContextMenuUtils.showContextMenu(getActivity(),
                        R.drawable.afc_bookmarks_dark, TextUtils.quote(name),
                        new Integer[] { R.string.afc_cmd_rename,
                                R.string.afc_cmd_sort_by_name },
                        new ContextMenuUtils.OnMenuItemClickListener() {

                            @Override
                            public void onClick(int resId) {
                                if (resId == R.string.afc_cmd_rename) {
                                    doEnterNewNameOrRenameBookmark(
                                            getActivity(), providerId,
                                            bookmarkId, uri, name);
                                } else if (resId == R.string.afc_cmd_sort_by_name) {
                                    sortBookmarks(iGroup);
                                }
                            }// onClick()
                        });
                return true;// PACKED_POSITION_TYPE_CHILD
            }

            return false;
        }// onItemLongClick()

        /**
         * Sorts bookmarks.
         * 
         * @param groupPosition
         *            the group position.
         */
        private void sortBookmarks(int groupPosition) {
            SparseArray<String> bookmarks = new SparseArray<String>();
            List<String> names = new ArrayList<String>();
            for (int i = 0; i < mBookmarkCursorAdapter
                    .getChildrenCount(groupPosition); i++) {
                Cursor cursor = mBookmarkCursorAdapter.getChild(groupPosition,
                        i);
                names.add(cursor.getString(cursor
                        .getColumnIndex(BookmarkContract.COLUMN_NAME)));
                bookmarks.put(cursor.getInt(cursor
                        .getColumnIndex(BookmarkContract._ID)), names.get(i));
            }

            Collections.sort(names, new Comparator<String>() {

                final Collator mCollator = Collator.getInstance();

                @Override
                public int compare(String lhs, String rhs) {
                    return mCollator.compare(lhs, rhs);
                }// compare()
            });

            ContentResolver contentResolver = getActivity()
                    .getContentResolver();
            /*
             * The list was sorted ascending by name (A-Z), now we add "i" to
             * timestamp (last modified), so the list will be obtained ascending
             * by name (A-Z) as it will be obtained from DB descending by last
             * modified.
             */
            ContentValues values = new ContentValues();
            while (names.size() > 0) {
                values.put(
                        BookmarkContract.COLUMN_MODIFICATION_TIME,
                        DbUtils.formatNumber(new Date().getTime()
                                + bookmarks.size() - names.size()));
                contentResolver.update(BookmarkContract
                        .genContentUri(getActivity()), values, String.format(
                        "%s = %d", DbUtils.SQLITE_FTS_COLUMN_ROW_ID, bookmarks
                                .keyAt(bookmarks.indexOfValue(names
                                        .remove(names.size() - 1)))), null);
            }
        }// sortBookmarks()
    };// mListViewOnItemLongClickListener

    /**
     * Shows a dialog to let the user enter new name or change current name of a
     * bookmark.
     * 
     * @param context
     *            {@link Context}
     * @param providerId
     *            the provider ID.
     * @param id
     *            the bookmark ID.
     * @param uri
     *            the URI to the bookmark.
     * @param name
     *            the name. To enter new name, this is the suggested name you
     *            provide. To rename, this is the old name.
     */
    public static void doEnterNewNameOrRenameBookmark(final Context context,
            final String providerId, final int id, final Uri uri,
            final String name) {
        final AlertDialog dialog = Dlg.newAlertDlg(context);

        View view = LayoutInflater.from(context).inflate(
                R.layout.afc_simple_text_input_view, null);
        final EditText textName = (EditText) view.findViewById(R.id.afc_text1);
        textName.setText(name);
        textName.selectAll();
        textName.setHint(R.string.afc_hint_new_name);
        textName.setOnEditorActionListener(new TextView.OnEditorActionListener() {

            @Override
            public boolean onEditorAction(TextView v, int actionId,
                    KeyEvent event) {
                if (actionId == EditorInfo.IME_ACTION_DONE) {
                    Ui.showSoftKeyboard(textName, false);
                    Button btn = dialog
                            .getButton(DialogInterface.BUTTON_POSITIVE);
                    if (btn.isEnabled())
                        btn.performClick();
                    return true;
                }
                return false;
            }// onEditorAction()
        });

        dialog.setView(view);
        dialog.setIcon(R.drawable.afc_bookmarks_dark);
        dialog.setTitle(id < 0 ? R.string.afc_title_new_bookmark
                : R.string.afc_title_rename);
        dialog.setButton(DialogInterface.BUTTON_POSITIVE,
                context.getString(android.R.string.ok),
                new DialogInterface.OnClickListener() {

                    @Override
                    public void onClick(DialogInterface dialog, int which) {
                        String newName = textName.getText().toString().trim();
                        if (android.text.TextUtils.isEmpty(newName)) {
                            Dlg.toast(context,
                                    R.string.afc_msg_bookmark_name_is_invalid,
                                    Dlg.LENGTH_SHORT);
                            return;
                        }

                        Ui.showSoftKeyboard(textName, false);

                        ContentValues values = new ContentValues();
                        values.put(BookmarkContract.COLUMN_NAME, newName);

                        if (id >= 0) {
                            values.put(
                                    BookmarkContract.COLUMN_MODIFICATION_TIME,
                                    DbUtils.formatNumber(new Date().getTime()));
                            context.getContentResolver().update(
                                    ContentUris.withAppendedId(BookmarkContract
                                            .genContentIdUriBase(context), id),
                                    values, null, null);
                        } else {
                            /*
                             * Check if the URI exists or doesn't. If it exists,
                             * update it instead of inserting the new one.
                             */
                            Cursor cursor = context
                                    .getContentResolver()
                                    .query(BookmarkContract
                                            .genContentUri(context),
                                            null,
                                            String.format(
                                                    "%s = %s AND %s LIKE %s",
                                                    BookmarkContract.COLUMN_PROVIDER_ID,
                                                    DatabaseUtils
                                                            .sqlEscapeString(providerId),
                                                    BookmarkContract.COLUMN_URI,
                                                    DatabaseUtils
                                                            .sqlEscapeString(uri
                                                                    .toString())),
                                            null, null);
                            try {
                                if (cursor != null && cursor.moveToFirst()) {
                                    values.put(
                                            BookmarkContract.COLUMN_MODIFICATION_TIME,
                                            DbUtils.formatNumber(new Date()
                                                    .getTime()));
                                    context.getContentResolver()
                                            .update(Uri
                                                    .withAppendedPath(
                                                            BookmarkContract
                                                                    .genContentIdUriBase(context),
                                                            Uri.encode(cursor
                                                                    .getString(cursor
                                                                            .getColumnIndex(BookmarkContract._ID)))),
                                                    values, null, null);
                                } else {
                                    values.put(
                                            BookmarkContract.COLUMN_PROVIDER_ID,
                                            providerId);
                                    values.put(BookmarkContract.COLUMN_URI,
                                            uri.toString());

                                    context.getContentResolver().insert(
                                            BookmarkContract
                                                    .genContentUri(context),
                                            values);
                                }
                            } finally {
                                if (cursor != null)
                                    cursor.close();
                            }
                        }

                        Dlg.toast(context,
                                context.getString(R.string.afc_msg_done),
                                Dlg.LENGTH_SHORT);
                    }// onClick()
                });

        dialog.show();
        Ui.showSoftKeyboard(textName, true);

        final Button buttonOk = dialog
                .getButton(DialogInterface.BUTTON_POSITIVE);
        buttonOk.setEnabled(id < 0);

        textName.addTextChangedListener(new TextWatcher() {

            @Override
            public void onTextChanged(CharSequence s, int start, int before,
                    int count) {
                // TODO Auto-generated method stub
            }

            @Override
            public void beforeTextChanged(CharSequence s, int start, int count,
                    int after) {
                // TODO Auto-generated method stub
            }

            @Override
            public void afterTextChanged(Editable s) {
                String newName = s.toString().trim();
                boolean enabled = !android.text.TextUtils.isEmpty(newName);
                buttonOk.setEnabled(enabled);

                /*
                 * If renaming, only enable button OK if new name is not equal
                 * to the old one.
                 */
                if (enabled && id >= 0)
                    buttonOk.setEnabled(!newName.equals(name));
            }
        });
    }// doEnterNewNameOrRenameBookmark()

    private final View.OnClickListener mBtnClearOnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            if (mBookmarkCursorAdapter.getGroupCount() == 1
                    && mBookmarkCursorAdapter.getChildrenCount(0) == 1) {
                clearBookmarksAndDismiss();
            } else {
                Dlg.confirmYesno(
                        getActivity(),
                        getString(R.string.afc_msg_confirm_clear_all_bookmarks),
                        new DialogInterface.OnClickListener() {

                            @Override
                            public void onClick(DialogInterface dialog,
                                    int which) {
                                clearBookmarksAndDismiss();
                            }// onClick()
                        });
            }
        }// onClick()

        private void clearBookmarksAndDismiss() {
            getActivity().getContentResolver().delete(
                    BookmarkContract.genContentUri(getActivity()), null, null);
            updateUI();
            if (getDialog() != null)
                dismiss();
        }// clearBookmarks()
    };// mBtnClearOnClickListener

    private final View.OnClickListener mBtnOkOnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            if (getDialog() != null)
                dismiss();
            else
                setEditor(false);
        }// onClick()
    };// mBtnOkOnClickListener

}
