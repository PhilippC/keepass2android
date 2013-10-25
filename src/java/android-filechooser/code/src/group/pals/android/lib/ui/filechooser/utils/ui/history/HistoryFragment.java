/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils.ui.history;

import group.pals.android.lib.ui.filechooser.BuildConfig;
import group.pals.android.lib.ui.filechooser.R;
import group.pals.android.lib.ui.filechooser.prefs.DisplayPrefs;
import group.pals.android.lib.ui.filechooser.providers.DbUtils;
import group.pals.android.lib.ui.filechooser.providers.history.HistoryContract;
import group.pals.android.lib.ui.filechooser.ui.widget.AfcSearchView;
import group.pals.android.lib.ui.filechooser.utils.EnvUtils;
import group.pals.android.lib.ui.filechooser.utils.ui.ContextMenuUtils;
import group.pals.android.lib.ui.filechooser.utils.ui.Dlg;
import group.pals.android.lib.ui.filechooser.utils.ui.GestureUtils;
import group.pals.android.lib.ui.filechooser.utils.ui.GestureUtils.FlingDirection;
import group.pals.android.lib.ui.filechooser.utils.ui.Ui;

import java.util.ArrayList;
import java.util.List;

import android.app.Dialog;
import android.content.DialogInterface;
import android.content.res.Configuration;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.support.v4.app.DialogFragment;
import android.support.v4.app.LoaderManager;
import android.support.v4.content.CursorLoader;
import android.support.v4.content.Loader;
import android.text.TextUtils;
import android.util.Log;
import android.view.KeyEvent;
import android.view.LayoutInflater;
import android.view.Menu;
import android.view.MenuItem;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;
import android.widget.AdapterView;
import android.widget.ExpandableListView;

/**
 * Fragment used to manage history.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class HistoryFragment extends DialogFragment implements
        LoaderManager.LoaderCallbacks<Cursor> {

    /**
     * Used for debugging or something...
     */
    private static final String CLASSNAME = HistoryFragment.class.getName();

    /**
     * As the name means.
     * 
     * @author Hai Bison
     * @since v5.1 beta
     */
    public static interface OnHistoryItemClickListener {

        /**
         * Will be called after history item was clicked.
         * 
         * @param providerId
         *            the original provider ID.
         * @param uri
         *            the URI to a directory or a file.
         */
        void onItemClick(String providerId, Uri uri);
    }// OnHistoryItemClickListener

    /**
     * Creates a new instance of {@link HistoryFragment}.
     */
    public static HistoryFragment newInstance() {
        return new HistoryFragment();
    }// newInstance()

    private final int mLoaderIdHistoryData = EnvUtils.genId();
    private final int mLoaderIdHistoryCounter = EnvUtils.genId();

    /*
     * Fields.
     */

    private OnHistoryItemClickListener mOnHistoryItemClickListener;

    private final Handler mHandler = new Handler();
    private int mLastScreenOrientation;
    private int mMaxItemsPerPage;
    private int mItemCount = 0;
    private int mPageCount = 1;
    private int mCurrentPage = 0;

    private Cursor mCursorCounter;

    /*
     * Controls.
     */

    private View mBtnSearch;
    private View mViewGroupListView;
    private ExpandableListView mListView;
    private HistoryCursorAdapter mHistoryCursorAdapter;
    private AfcSearchView mSearchView;
    private View mBtnNext;
    private View mBtnPrev;
    private View mViewLoading;

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        mMaxItemsPerPage = getResources().getInteger(
                R.integer.afc_pkey_history_manager_display_items_per_page);
    }// onCreate()

    @Override
    public Dialog onCreateDialog(Bundle savedInstanceState) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onCreateDialog()");
        Dialog dialog = new Dialog(getActivity(), Ui.resolveAttribute(
                getActivity(), R.attr.afc_theme_dialog)) {

            @Override
            public boolean onCreateOptionsMenu(Menu menu) {
                getActivity().getMenuInflater().inflate(
                        R.menu.afc_viewgroup_history, menu);
                return super.onCreateOptionsMenu(menu);
            }// onCreateOptionsMenu()

            @Override
            public boolean onPrepareOptionsMenu(Menu menu) {
                menu.findItem(R.id.afc_menuitem_clear).setEnabled(
                        mHistoryCursorAdapter != null
                                && mHistoryCursorAdapter.getGroupCount() > 0);
                return true;
            }// onPrepareOptionsMenu()

            @Override
            public boolean onMenuItemSelected(int featureId, MenuItem item) {
                if (BuildConfig.DEBUG)
                    Log.d(CLASSNAME, "onMenuItemSelected() in Dialog");

                Ui.showSoftKeyboard(mSearchView, false);

                if (item.getItemId() == R.id.afc_menuitem_clear)
                    doConfirmClearHistory();

                return true;
            }// onMenuItemSelected()
        };

        dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);
        dialog.setCanceledOnTouchOutside(true);
        dialog.setContentView(initContentView(dialog.getLayoutInflater(), null));
        dialog.setOnKeyListener(mDialogOnKeyListener);

        Ui.adjustDialogSizeForLargeScreen(dialog);

        return dialog;
    }// onCreateDialog()

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container,
            Bundle savedInstanceState) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onCreateView() -- getDialog() = " + getDialog());
        return getDialog() != null ? null
                : initContentView(inflater, container);
    }// onCreateView()

    @Override
    public void onActivityCreated(Bundle savedInstanceState) {
        super.onActivityCreated(savedInstanceState);

        /*
         * Prepare the loaders. Either re-connect with the existing ones, or
         * start the new ones.
         */
        getLoaderManager().initLoader(mLoaderIdHistoryCounter, null, this);
        getLoaderManager().initLoader(mLoaderIdHistoryData, null, this);
    }// onActivityCreated()

    @Override
    public void onResume() {
        super.onResume();
        if (mLastScreenOrientation != getResources().getConfiguration().orientation) {
            mLastScreenOrientation = getResources().getConfiguration().orientation;
            Ui.adjustDialogSizeForLargeScreen(getDialog());
        }
    }// onResume()

    @Override
    public void onPause() {
        super.onPause();
        mLastScreenOrientation = getResources().getConfiguration().orientation;
    }// onPause()

    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        super.onConfigurationChanged(newConfig);
        if (getDialog() != null)
            Ui.adjustDialogSizeForLargeScreen(getDialog());
    }// onConfigurationChanged()

    /*
     * LOADERMANAGER.LOADERCALLBACKS
     */

    @Override
    public Loader<Cursor> onCreateLoader(int id, Bundle args) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onCreateLoader()");

        enableControls(false);

        String selection = null;
        if (!TextUtils.isEmpty(mHistoryCursorAdapter.getSearchText())) {
            selection = DbUtils.rawSqlEscapeString(Uri
                    .encode(mHistoryCursorAdapter.getSearchText().toString()));
            selection = String.format("%s LIKE '%%://%%%s%%'",
                    HistoryContract.COLUMN_URI, selection);
        }

        if (id == mLoaderIdHistoryData) {
            mHandler.removeCallbacksAndMessages(null);
            mHandler.postDelayed(mViewLoadingShower,
                    DisplayPrefs.DELAY_TIME_FOR_SIMPLE_ANIMATION);

            mHistoryCursorAdapter.changeCursor(null);

            /*
             * Offset.
             */
            if (mCurrentPage >= mPageCount)
                mCurrentPage = mPageCount - 1;
            if (mCurrentPage < 0)
                mCurrentPage = 0;
            int offset = mCurrentPage * mMaxItemsPerPage;

            return new CursorLoader(getActivity(),
                    HistoryContract.genContentUri(getActivity()), null,
                    selection, null, String.format(
                            "%s DESC LIMIT %s OFFSET %s",
                            HistoryContract.COLUMN_MODIFICATION_TIME,
                            mMaxItemsPerPage, offset));
        } // mLoaderIdHistoryData
        else if (id == mLoaderIdHistoryCounter) {
            mPageCount = 1;
            mCurrentPage = 0;

            if (mCursorCounter != null) {
                mCursorCounter.close();
                mCursorCounter = null;
            }

            return new CursorLoader(getActivity(),
                    HistoryContract.genContentUri(getActivity()),
                    new String[] { HistoryContract._COUNT }, selection, null,
                    null);
        }// mLoaderIdHistoryCounter

        return null;
    }// onCreateLoader()

    @Override
    public void onLoadFinished(Loader<Cursor> loader, Cursor data) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onLoadFinished() -- data = " + data);

        if (loader.getId() == mLoaderIdHistoryData) {
            mSearchView.setEnabled(true);

            mHistoryCursorAdapter.changeCursor(data);

            for (int i = 0; i < mHistoryCursorAdapter.getGroupCount(); i++)
                mListView.expandGroup(i);

            /*
             * Views visibilities. Always call these to make sure all views are
             * in right visibilities.
             */
            mHandler.removeCallbacksAndMessages(null);
            mViewLoading.setVisibility(View.GONE);
            mViewGroupListView.setVisibility(View.VISIBLE);

            mListView.post(new Runnable() {

                @Override
                public void run() {
                    mListView.setSelection(-1);
                }
            });
        }// mLoaderIdHistoryData
        else if (loader.getId() == mLoaderIdHistoryCounter) {
            if (mCursorCounter != null)
                mCursorCounter.close();

            mCursorCounter = data;
            if (mCursorCounter.moveToFirst()) {
                if (mItemCount < 0)
                    getLoaderManager().restartLoader(mLoaderIdHistoryData,
                            null, this);
                mItemCount = mCursorCounter.getInt(mCursorCounter
                        .getColumnIndex(HistoryContract._COUNT));
                mPageCount = (int) Math.ceil((float) mItemCount
                        / mMaxItemsPerPage);
                mBtnNext.setEnabled(mCurrentPage < mPageCount - 1);
                mBtnPrev.setEnabled(mCurrentPage > 0);
            } else {
                mBtnNext.setEnabled(false);
                mBtnPrev.setEnabled(false);
            }
        }// mLoaderIdHistoryCounter

        enableControls(true);
    }// onLoadFinished()

    @Override
    public void onLoaderReset(Loader<Cursor> loader) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onLoaderReset()");

        if (loader.getId() == mLoaderIdHistoryData) {
            mHistoryCursorAdapter.changeCursor(null);
            mViewLoading.setVisibility(View.VISIBLE);
        }// mLoaderIdHistoryData
        else if (loader.getId() == mLoaderIdHistoryCounter) {
            /*
             * NOTE: if using an adapter, set its cursor to null to release
             * memory.
             */
            if (mCursorCounter != null) {
                mCursorCounter.close();
                mCursorCounter = null;
            }
        }// mLoaderIdHistoryCounter
    }// onLoaderReset()

    /**
     * Loads content view from XML and init controls.
     * 
     * @param inflater
     *            {@link LayoutInflater}.
     * @param container
     *            {@link ViewGroup}.
     */
    private View initContentView(LayoutInflater inflater, ViewGroup container) {
        /*
         * LOADS CONTROLS
         */
        View mainView = inflater.inflate(R.layout.afc_viewgroup_history,
                container, false);

        mBtnSearch = mainView.findViewById(R.id.afc_button_search);
        mViewGroupListView = mainView.findViewById(R.id.afc_viewgroup_listview);
        mListView = (ExpandableListView) mainView
                .findViewById(R.id.afc_listview);
        mSearchView = (AfcSearchView) mainView
                .findViewById(R.id.afc_afc_search_view);
        mBtnNext = mainView.findViewById(R.id.afc_button_go_forward);
        mBtnPrev = mainView.findViewById(R.id.afc_button_go_back);
        mViewLoading = mainView.findViewById(R.id.afc_view_loading);

        /*
         * INITIALIZES CONTROLS
         */

        if (mBtnSearch != null) {
            mBtnSearch.setOnClickListener(mBtnSearchOnClickListener);
            mBtnSearch.setVisibility(View.VISIBLE);
        }
        mSearchView.setOnQueryTextListener(mSearchViewOnQueryTextListener);
        mSearchView.setOnStateChangeListener(mSearchViewOnStateChangeListener);

        mListView.setEmptyView(mainView.findViewById(R.id.afc_empty_view));
        mListView.setOnChildClickListener(mListViewOnChildClickListener);
        mListView.setOnItemLongClickListener(mListViewOnItemLongClickListener);
        initListViewGestureListener();

        mHistoryCursorAdapter = new HistoryCursorAdapter(getActivity());
        mListView.setAdapter(mHistoryCursorAdapter);

        /*
         * Default states of button navigators in XML are disabled, so we just
         * set their listeners here.
         */
        for (View v : new View[] { mBtnNext, mBtnPrev })
            v.setOnTouchListener(mBtnNextPrevOnTouchListener);

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
                        if (!(data instanceof Cursor))
                            return false;

                        List<Integer> ids = new ArrayList<Integer>();

                        final int historyId = ((Cursor) data)
                                .getInt(((Cursor) data)
                                        .getColumnIndex(HistoryContract._ID));
                        if (mHistoryCursorAdapter.isSelected(historyId))
                            ids.addAll(mHistoryCursorAdapter
                                    .getSelectedItemIds());
                        else
                            ids.add(historyId);

                        if (ids.size() <= 1)
                            mHistoryCursorAdapter.markItemAsDeleted(historyId,
                                    true);
                        else
                            mHistoryCursorAdapter
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
                                        HistoryContract
                                                .genContentUri(getActivity()),
                                        sb.toString(), null);
                            }
                        }, DisplayPrefs.DELAY_TIME_FOR_VERY_SHORT_ANIMATION);

                        return true;
                    }// onFling()
                });
    }// initListViewGestureListener()

    /**
     * Enables/ disables search view, button navigators' listeners (the
     * listeners are DB querying related)...
     * 
     * @param enabled
     *            {@code true} or {@code false}.
     */
    private void enableControls(boolean enabled) {
        /*
         * If the user is typing, disabling search view will turn off the
         * keyboard. So don't do that.
         */
        // mSearchView.setEnabled(enabled);

        for (View v : new View[] { mBtnNext, mBtnPrev }) {
            v.setOnClickListener(enabled ? mBtnNextPrevOnClickListener : null);
            v.setOnLongClickListener(enabled ? mBtnNextPrevOnLongClickListener
                    : null);
        }
    }// enableButtonNavigatorsListeners()

    /**
     * Asks user to confirm to clear history.
     */
    private void doConfirmClearHistory() {
        Dlg.confirmYesno(getActivity(),
                getString(R.string.afc_msg_confirm_clear_history),
                new DialogInterface.OnClickListener() {

                    @Override
                    public void onClick(DialogInterface dialog, int which) {
                        getActivity().getContentResolver().delete(
                                HistoryContract.genContentUri(getActivity()),
                                null, null);
                        if (getDialog() != null)
                            getDialog().dismiss();
                    }// onClick()
                });
    }// doConfirmClearHistory()

    /**
     * Checks if the search view is on or off.
     * 
     * @return {@code true} or {@code false}.
     */
    private boolean isSearchViewOn() {
        return mSearchView.getVisibility() == View.VISIBLE
                && !mSearchView.isIconified();
    }// isSearchViewShowing()

    /**
     * Shows search view and opens up search components.
     */
    private void switchOnSearchView() {
        if (mBtnSearch != null)
            mBtnSearch.setVisibility(View.GONE);

        mSearchView.setVisibility(View.VISIBLE);
        if (mSearchView.isIconified())
            mSearchView.open();
    }// switchOnSearchView()

    /**
     * Hides the search view and closes its components.
     */
    private void switchOffSearchView() {
        if (mBtnSearch != null)
            mBtnSearch.setVisibility(View.VISIBLE);

        mSearchView.setVisibility(View.GONE);
        if (!mSearchView.isIconified())
            mSearchView.close();
    }// switchOffSearchView()

    /*
     * LISTENERS
     */

    private final Runnable mViewLoadingShower = new Runnable() {

        @Override
        public void run() {
            if (isAdded()) {
                mViewGroupListView.setVisibility(View.GONE);
                mViewLoading.setVisibility(View.VISIBLE);
            }
        }// run()
    };// mViewLoadingShower

    /**
     * Sets {@code listener}.
     * 
     * @param listener
     *            {@link OnHistoryItemClickListener}.
     */
    public void setOnHistoryItemClickListener(
            OnHistoryItemClickListener listener) {
        mOnHistoryItemClickListener = listener;
    }// setOnHistoryItemClickListener()

    /**
     * Gets an {@link OnHistoryItemClickListener}.
     * 
     * @return the listener.
     */
    public OnHistoryItemClickListener getOnHistoryItemClickListener() {
        return mOnHistoryItemClickListener;
    }// getOnHistoryItemClickListener()

    /*
     * CONTROLS' LISTENERS.
     */

    /**
     * Handles when user presses the search key, then opens up search view.
     */
    private final DialogInterface.OnKeyListener mDialogOnKeyListener = new DialogInterface.OnKeyListener() {

        @Override
        public boolean onKey(DialogInterface dialog, int keyCode, KeyEvent event) {
            if (keyCode == KeyEvent.KEYCODE_SEARCH) {
                if (event.getAction() == KeyEvent.ACTION_UP) {
                    if (isSearchViewOn())
                        switchOffSearchView();
                    else
                        switchOnSearchView();
                }
                return true;
            }

            return false;
        }// onKey()
    };// mDialogOnKeyListener

    private final View.OnClickListener mBtnSearchOnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            switchOnSearchView();
        }// onClick()
    };// mBtnSearchOnClickListener

    private final AfcSearchView.OnQueryTextListener mSearchViewOnQueryTextListener = new AfcSearchView.OnQueryTextListener() {

        @Override
        public void onQueryTextSubmit(String query) {
            if (!isAdded())
                return;

            try {
                mHistoryCursorAdapter.setSearchText(query != null ? query
                        .trim() : null);
                /*
                 * Sets total item count to -1, then restarts the counter, it
                 * will restart the data loader.
                 */
                mItemCount = -1;
                getLoaderManager().restartLoader(mLoaderIdHistoryCounter, null,
                        HistoryFragment.this);
            } catch (Throwable t) {
                Log.e(CLASSNAME, "onQueryTextSubmit() >> " + t);
                t.printStackTrace();
            }
        }// onQueryTextSubmit()
    };// mSearchViewOnQueryTextListener

    private final AfcSearchView.OnStateChangeListener mSearchViewOnStateChangeListener = new AfcSearchView.OnStateChangeListener() {

        @Override
        public void onOpen() {
            // do nothing
        }// onOpen()

        @Override
        public void onClose() {
            switchOffSearchView();

            if (!TextUtils.isEmpty(mHistoryCursorAdapter.getSearchText())
                    || !TextUtils.isEmpty(mSearchView.getSearchText())) {
                mHistoryCursorAdapter.setSearchText(null);
                /*
                 * Sets total item count to -1, then restarts the counter, it
                 * will restart the data loader.
                 */
                mItemCount = -1;
                getLoaderManager().restartLoader(mLoaderIdHistoryCounter, null,
                        HistoryFragment.this);
            }
        }// onClose()
    };// mSearchViewOnStateChangeListener

    private final View.OnClickListener mBtnNextPrevOnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            Ui.showSoftKeyboard(mSearchView, false);

            if (v.getId() == R.id.afc_button_go_forward)
                mCurrentPage++;
            else if (v.getId() == R.id.afc_button_go_back)
                mCurrentPage--;
            getLoaderManager().restartLoader(mLoaderIdHistoryData, null,
                    HistoryFragment.this);
        }// onClick()
    };// mBtnNextPrevOnClickListener

    private final View.OnLongClickListener mBtnNextPrevOnLongClickListener = new View.OnLongClickListener() {

        @Override
        public boolean onLongClick(View v) {
            Ui.showSoftKeyboard(mSearchView, false);

            if (v.getId() == R.id.afc_button_go_forward)
                mCurrentPage = Integer.MAX_VALUE;
            else if (v.getId() == R.id.afc_button_go_back)
                mCurrentPage = 0;

            getLoaderManager().restartLoader(mLoaderIdHistoryData, null,
                    HistoryFragment.this);

            return true;
        }// onLongClick()
    };// mBtnNextPrevOnLongClickListener

    private final View.OnTouchListener mBtnNextPrevOnTouchListener = new View.OnTouchListener() {

        @Override
        public boolean onTouch(View v, MotionEvent event) {
            if (event.getAction() == MotionEvent.ACTION_UP) {
                v.postDelayed(new Runnable() {

                    @Override
                    public void run() {
                        if (BuildConfig.DEBUG)
                            Log.d(CLASSNAME, "BtnNextPrev -- TOUCH ACTION_UP");
                        if (isAdded()) {
                            mBtnNext.setEnabled(mCurrentPage < mPageCount - 1);
                            mBtnPrev.setEnabled(mCurrentPage > 0);
                        }
                    }// run()
                }, DisplayPrefs.DELAY_TIME_FOR_VERY_SHORT_ANIMATION);
            }

            return false;
        }// onTouch()
    };// mBtnNextPrevOnTouchListener

    private final ExpandableListView.OnChildClickListener mListViewOnChildClickListener = new ExpandableListView.OnChildClickListener() {

        @Override
        public boolean onChildClick(ExpandableListView parent, View v,
                int groupPosition, int childPosition, long id) {
            if (getOnHistoryItemClickListener() != null) {
                Cursor cursor = mHistoryCursorAdapter.getChild(groupPosition,
                        childPosition);
                getOnHistoryItemClickListener()
                        .onItemClick(
                                cursor.getString(cursor
                                        .getColumnIndex(HistoryContract.COLUMN_PROVIDER_ID)),
                                Uri.parse(cursor.getString(cursor
                                        .getColumnIndex(HistoryContract.COLUMN_URI))));
            }

            if (getDialog() != null)
                getDialog().dismiss();

            return true;
        }// onChildClick()
    };// mListViewOnChildClickListener

    private final AdapterView.OnItemLongClickListener mListViewOnItemLongClickListener = new AdapterView.OnItemLongClickListener() {

        @Override
        public boolean onItemLongClick(AdapterView<?> parent, View view,
                int position, long id) {
            switch (ExpandableListView.getPackedPositionType(id)) {
            case ExpandableListView.PACKED_POSITION_TYPE_GROUP:
                final int iGroup = ExpandableListView
                        .getPackedPositionGroup(mListView
                                .getExpandableListPosition(position));
                if (!mListView.isGroupExpanded(iGroup))
                    return false;

                if (BuildConfig.DEBUG)
                    Log.d(CLASSNAME, String.format(
                            "onItemLongClick() -- group = %,d", iGroup));
                ContextMenuUtils.showContextMenu(getActivity(), 0,
                        R.string.afc_title_advanced_selection,
                        HistoryCursorAdapter.ADVANCED_SELECTION_OPTIONS,
                        new ContextMenuUtils.OnMenuItemClickListener() {

                            @Override
                            public void onClick(final int resId) {
                                if (resId == R.string.afc_cmd_advanced_selection_all)
                                    mHistoryCursorAdapter.selectAll(iGroup,
                                            true);
                                else if (resId == R.string.afc_cmd_advanced_selection_none)
                                    mHistoryCursorAdapter.selectAll(iGroup,
                                            false);
                                else if (resId == R.string.afc_cmd_advanced_selection_invert)
                                    mHistoryCursorAdapter
                                            .invertSelection(iGroup);
                            }// onClick()
                        });

                return true;// PACKED_POSITION_TYPE_GROUP

            case ExpandableListView.PACKED_POSITION_TYPE_CHILD:
                return false;// PACKED_POSITION_TYPE_CHILD
            }

            return false;
        }// onItemLongClick()
    };// mListViewOnItemLongClickListener

}
