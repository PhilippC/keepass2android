/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser;

import group.pals.android.lib.ui.filechooser.FileChooserActivity.ViewType;
import group.pals.android.lib.ui.filechooser.prefs.DisplayPrefs;
import group.pals.android.lib.ui.filechooser.providers.BaseFileProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.DbUtils;
import group.pals.android.lib.ui.filechooser.providers.ProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileContract.BaseFile;
import group.pals.android.lib.ui.filechooser.providers.history.HistoryContract;
import group.pals.android.lib.ui.filechooser.providers.history.HistoryProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.localfile.LocalFileContract;
import group.pals.android.lib.ui.filechooser.utils.E;
import group.pals.android.lib.ui.filechooser.utils.EnvUtils;
import group.pals.android.lib.ui.filechooser.utils.FileUtils;
import group.pals.android.lib.ui.filechooser.utils.Utils;
import group.pals.android.lib.ui.filechooser.utils.history.History;
import group.pals.android.lib.ui.filechooser.utils.history.HistoryFilter;
import group.pals.android.lib.ui.filechooser.utils.history.HistoryListener;
import group.pals.android.lib.ui.filechooser.utils.history.HistoryStore;
import group.pals.android.lib.ui.filechooser.utils.ui.ContextMenuUtils;
import group.pals.android.lib.ui.filechooser.utils.ui.Dlg;
import group.pals.android.lib.ui.filechooser.utils.ui.LoadingDialog;
import group.pals.android.lib.ui.filechooser.utils.ui.Ui;
import group.pals.android.lib.ui.filechooser.utils.ui.bookmark.BookmarkFragment;
import group.pals.android.lib.ui.filechooser.utils.ui.history.HistoryFragment;

import java.util.ArrayList;
import java.util.Calendar;
import java.util.Date;
import java.util.List;

import android.Manifest;
import android.annotation.SuppressLint;
import android.app.Activity;
import android.app.AlertDialog;
import android.app.Dialog;
import android.content.ContentValues;
import android.content.DialogInterface;
import android.content.Intent;
import android.database.Cursor;
import android.database.DatabaseUtils;
import android.graphics.Rect;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.support.v4.app.Fragment;
import android.support.v4.app.LoaderManager;
import android.support.v4.content.CursorLoader;
import android.support.v4.content.Loader;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.text.format.DateUtils;
import android.util.Log;
import android.view.GestureDetector;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.LayoutInflater;
import android.view.Menu;
import android.view.MenuInflater;
import android.view.MenuItem;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.inputmethod.EditorInfo;
import android.widget.AbsListView;
import android.widget.AdapterView;
import android.widget.Button;
import android.widget.EditText;
import android.widget.GridView;
import android.widget.HorizontalScrollView;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.RelativeLayout;
import android.widget.TextView;

/**
 * Fragment of files.
 * 
 * @author Hai Bison
 * @since v5.4 beta
 */
public class FragmentFiles extends Fragment implements
        LoaderManager.LoaderCallbacks<Cursor> {

    /**
     * The full name of this class. Generally used for debugging.
     */
    private static final String CLASSNAME = FragmentFiles.class.getName();

    /**
     * This key holds current location (a {@link Uri}), to restore it after
     * screen orientation changed.
     */
    private static final String CURRENT_LOCATION = CLASSNAME
            + ".current_location";
    /**
     * This key holds current history (a {@link History}{@code <}{@link Uri}
     * {@code >}), to restore it after screen orientation changed
     */
    private static final String HISTORY = CLASSNAME + ".history";

    private static final String PATH = CLASSNAME + ".path";

    /**
     * All string extras.
     */
    private static final String[] EXTRAS_STRING = {
            FileChooserActivity.EXTRA_DEFAULT_FILENAME,
            FileChooserActivity.EXTRA_FILE_PROVIDER_AUTHORITY,
            FileChooserActivity.EXTRA_NEGATIVE_REGEX_FILTER,
            FileChooserActivity.EXTRA_POSITIVE_REGEX_FILTER };

    /**
     * All boolean extras.
     */
    private static final String[] EXTRAS_BOOLEAN = {
            FileChooserActivity.EXTRA_DISPLAY_HIDDEN_FILES,
            FileChooserActivity.EXTRA_DOUBLE_TAP_TO_CHOOSE_FILES,
            FileChooserActivity.EXTRA_MULTI_SELECTION,
            FileChooserActivity.EXTRA_SAVE_DIALOG };

    /**
     * All integer extras.
     */
    private static final String[] EXTRAS_INTEGER = {
            FileChooserActivity.EXTRA_FILTER_MODE,
            FileChooserActivity.EXTRA_MAX_FILE_COUNT,
            FileChooserActivity.EXTRA_THEME };

    /**
     * All parcelable extras.
     */
    private static final String[] EXTRAS_PARCELABLE = {
            FileChooserActivity.EXTRA_ROOTPATH,
            FileChooserActivity.EXTRA_SELECT_FILE };

    /**
     * Creates new instance.
     * 
     * @param intent
     *            the intent you got from {@link FileChooserActivity}.
     * @return the new instance of this fragment.
     */
    public static FragmentFiles newInstance(Intent intent) {
        /*
         * Load the extras.
         */
        final Bundle args = new Bundle();

        for (String ex : EXTRAS_BOOLEAN)
            if (intent.hasExtra(ex))
                args.putBoolean(ex, intent.getBooleanExtra(ex, false));
        for (String ex : EXTRAS_INTEGER)
            if (intent.hasExtra(ex))
                args.putInt(ex, intent.getIntExtra(ex, 0));
        for (String ex : EXTRAS_PARCELABLE)
            if (intent.hasExtra(ex))
                args.putParcelable(ex, intent.getParcelableExtra(ex));
        for (String ex : EXTRAS_STRING)
            if (intent.hasExtra(ex))
                args.putString(ex, intent.getStringExtra(ex));

        return newInstance(args);
    }// newInstance()

    /**
     * Creates new instance.
     * 
     * @param args
     *            the arguments.
     * @return the new instance of this fragment.
     */
    public static FragmentFiles newInstance(Bundle args) {
        FragmentFiles fragment = new FragmentFiles();
        fragment.setArguments(args);
        return fragment;
    }// newInstance()

    // ====================
    // "CONSTANT" VARIABLES

    /**
     * Task ID for loading directory content.
     */
    private final int mIdLoaderData = EnvUtils.genId();

    private String mFileProviderAuthority;
    private Uri mRoot;
    private int mFilterMode;
    private int mMaxFileCount;
    private boolean mIsMultiSelection;
    private boolean mIsSaveDialog;
    private boolean mDoubleTapToChooseFiles;

    private History<Uri> mHistory;
    private Uri mLastLocation;
    private Uri mCurrentLocation;
    private Handler mViewLoadingHandler = new Handler();

    /**
     * The adapter of list view.
     */
    private BaseFileAdapter mFileAdapter;

    private boolean mLoading = false;
    private boolean mNewLoader = true;

    /*
     * Controls.
     */

    private View mBtnGoHome;
    private View mBtnBookmarkManager;
    private BookmarkFragment mBookmarkFragment;
    private HorizontalScrollView mViewLocationsContainer;
    private ViewGroup mViewLocations;
    private View mViewGroupFiles;
    private ViewGroup mViewFilesContainer;
    private TextView mTxtFullDirName;
    private AbsListView mViewFiles;
    private TextView mFooterView;
    private View mViewLoading;
    private Button mBtnOk;
    private EditText mTxtSaveas;
    private ImageView mViewGoBack;
    private ImageView mViewGoForward;
    private GestureDetector mListviewFilesGestureDetector;

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        setHasOptionsMenu(true);

        /*
         * Load configurations.
         */

        mFileProviderAuthority = getArguments().getString(
                FileChooserActivity.EXTRA_FILE_PROVIDER_AUTHORITY);
        if (mFileProviderAuthority == null)
            mFileProviderAuthority = LocalFileContract
                    .getAuthority(getActivity());

        mIsMultiSelection = getArguments().getBoolean(
                FileChooserActivity.EXTRA_MULTI_SELECTION);

        mIsSaveDialog = getArguments().getBoolean(
                FileChooserActivity.EXTRA_SAVE_DIALOG);
        if (mIsSaveDialog)
            mIsMultiSelection = false;

        mDoubleTapToChooseFiles = getArguments().getBoolean(
                FileChooserActivity.EXTRA_DOUBLE_TAP_TO_CHOOSE_FILES);

        mRoot = getArguments()
                .getParcelable(FileChooserActivity.EXTRA_ROOTPATH);
        mFilterMode = getArguments().getInt(
                FileChooserActivity.EXTRA_FILTER_MODE,
                BaseFile.FILTER_FILES_ONLY);
        mMaxFileCount = getArguments().getInt(
                FileChooserActivity.EXTRA_MAX_FILE_COUNT, 1000);
        mFileAdapter = new BaseFileAdapter(getActivity(), mFilterMode,
                mIsMultiSelection);
        mFileAdapter.setBuildOptionsMenuListener(mOnBuildOptionsMenuListener);

        /*
         * History.
         */
        if (savedInstanceState != null
                && savedInstanceState.get(HISTORY) instanceof HistoryStore<?>)
            mHistory = savedInstanceState.getParcelable(HISTORY);
        else
            mHistory = new HistoryStore<Uri>();
        mHistory.addListener(new HistoryListener<Uri>() {

            @Override
            public void onChanged(History<Uri> history) {
                int idx = history.indexOf(getCurrentLocation());
                mViewGoBack.setEnabled(idx > 0);
                mViewGoForward.setEnabled(idx >= 0 && idx < history.size() - 1);
            }// onChanged()
        });
    }// onCreate()

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container,
            Bundle savedInstanceState) {
        final View rootView = inflater.inflate(R.layout.afc_fragment_files,
                container, false);

        /*
         * MAP CONTROLS
         */

        mBtnGoHome = rootView.findViewById(R.id.afc_textview_home);
        mBtnBookmarkManager = rootView
                .findViewById(R.id.afc_textview_bookmarks);
        mViewGoBack = (ImageView) rootView
                .findViewById(R.id.afc_button_go_back);
        mViewGoForward = (ImageView) rootView
                .findViewById(R.id.afc_button_go_forward);
        mViewLocations = (ViewGroup) rootView
                .findViewById(R.id.afc_view_locations);
        mViewLocationsContainer = (HorizontalScrollView) rootView
                .findViewById(R.id.afc_view_locations_container);
        mTxtFullDirName = (TextView) rootView
                .findViewById(R.id.afc_textview_full_dir_name);
        mViewGroupFiles = rootView.findViewById(R.id.afc_viewgroup_files);
        mViewFilesContainer = (ViewGroup) rootView
                .findViewById(R.id.afc_view_files_container);
        mFooterView = (TextView) rootView
                .findViewById(R.id.afc_view_files_footer_view);
        mViewLoading = rootView.findViewById(R.id.afc_view_loading);
        mTxtSaveas = (EditText) rootView
                .findViewById(R.id.afc_textview_saveas_filename);
        mBtnOk = (Button) rootView.findViewById(R.id.afc_button_ok);

        /*
         * INIT CONTROLS
         */

        /*
         * Load BookmarkFragment.
         */

        View viewBookmarks = rootView.findViewById(R.id.afc_fragment_bookmarks);
        if (viewBookmarks != null) {
            mBookmarkFragment = (BookmarkFragment) getChildFragmentManager()
                    .findFragmentById(R.id.afc_fragment_bookmarks);
            if (mBookmarkFragment == null) {
                mBookmarkFragment = BookmarkFragment.newInstance(false);
                mBookmarkFragment
                        .setOnBookmarkItemClickListener(mBookmarkFragmentOnBookmarkItemClickListener);

                getChildFragmentManager().beginTransaction()
                        .add(R.id.afc_fragment_bookmarks, mBookmarkFragment)
                        .commit();
            }
        }// if

        return rootView;
    }// onCreateView()

    @Override
    public void onActivityCreated(Bundle savedInstanceState) {
        super.onActivityCreated(savedInstanceState);

        setupHeader();
        setupViewFiles();
        setupFooter();

        initGestureDetector();
        loadInitialPath(savedInstanceState);
    }// onActivityCreated()

    @Override
    public void onCreateOptionsMenu(Menu menu, MenuInflater inflater) {
        inflater.inflate(R.menu.afc_fragment_files, menu);
    }// onCreateOptionsMenu()

    @Override
    public void onPrepareOptionsMenu(Menu menu) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onPrepareOptionsMenu()");

        /*
         * Some bugs? This method seems to be called even after `onDestroy()`...
         */
        if (getActivity() == null)
            return;

        /*
         * Sorting.
         */

        final boolean sortAscending = DisplayPrefs
                .isSortAscending(getActivity());
        MenuItem miSort = menu.findItem(R.id.afc_menuitem_sort);

        switch (DisplayPrefs.getSortType(getActivity())) {
        case BaseFile.SORT_BY_NAME:
            miSort.setIcon(Ui.resolveAttribute(getActivity(),
                    sortAscending ? R.attr.afc_ic_menu_sort_by_name_asc
                            : R.attr.afc_ic_menu_sort_by_name_desc));
            break;
        case BaseFile.SORT_BY_SIZE:
            miSort.setIcon(Ui.resolveAttribute(getActivity(),
                    sortAscending ? R.attr.afc_ic_menu_sort_by_size_asc
                            : R.attr.afc_ic_menu_sort_by_size_desc));
            break;
        case BaseFile.SORT_BY_MODIFICATION_TIME:
            miSort.setIcon(Ui.resolveAttribute(getActivity(),
                    sortAscending ? R.attr.afc_ic_menu_sort_by_date_asc
                            : R.attr.afc_ic_menu_sort_by_date_desc));
            break;
        }

        /*
         * View type.
         */

        MenuItem menuItem = menu.findItem(R.id.afc_menuitem_switch_viewmode);
        switch (DisplayPrefs.getViewType(getActivity())) {
        case GRID:
            menuItem.setIcon(Ui.resolveAttribute(getActivity(),
                    R.attr.afc_ic_menu_listview));
            menuItem.setTitle(R.string.afc_cmd_list_view);
            break;
        case LIST:
            menuItem.setIcon(Ui.resolveAttribute(getActivity(),
                    R.attr.afc_ic_menu_gridview));
            menuItem.setTitle(R.string.afc_cmd_grid_view);
            break;
        }

        /*
         * New folder.
         */

        menu.findItem(R.id.afc_menuitem_new_folder).setEnabled(!mLoading);
    }// onPrepareOptionsMenu()

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        if (item.getItemId() == R.id.afc_menuitem_sort)
            doResortViewFiles();
        else if (item.getItemId() == R.id.afc_menuitem_new_folder)
            doCreateNewDir();
        else if (item.getItemId() == R.id.afc_menuitem_switch_viewmode)
            doSwitchViewType();
        else if (item.getItemId() == R.id.afc_menuitem_home)
            doGoHome();
        else if (item.getItemId() == R.id.afc_menuitem_history)
            doShowHistoryManager();
        else if (item.getItemId() == R.id.afc_menuitem_bookmarks)
            doShowBookmarkManager();
        else
            return false;

        return true;
    }// onOptionsItemSelected()

    @Override
    public void onSaveInstanceState(Bundle outState) {
        outState.putParcelable(CURRENT_LOCATION, getCurrentLocation());
        outState.putParcelable(HISTORY, mHistory);
    }// onSaveInstanceState()

    @Override
    public void onStop() {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onStop()");

        super.onStop();
        HistoryProviderUtils.doCleanupOutdatedHistoryItems(getActivity());
    }// onStop()

    @Override
    public void onDestroy() {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onDestroy()");

        super.onDestroy();
    }// onDestroy()

    /*
     * LOADERMANAGER.LOADERCALLBACKS
     */

    @Override
    public Loader<Cursor> onCreateLoader(int id, Bundle args) {
        mLoading = true;
        mNewLoader = true;

        mViewGroupFiles.setVisibility(View.GONE);
        mViewLoadingHandler.postDelayed(new Runnable() {

            @Override
            public void run() {
                mViewLoading.setVisibility(View.VISIBLE);
            }// run()
        }, DisplayPrefs.DELAY_TIME_FOR_SHORT_ANIMATION);

        getActivity().supportInvalidateOptionsMenu();

        Uri path = ((Uri) args.getParcelable(PATH));
        createLocationButtons(path);

        String positiveRegex = getArguments().getString(
                FileChooserActivity.EXTRA_POSITIVE_REGEX_FILTER);
        String negativeRegex = getArguments().getString(
                FileChooserActivity.EXTRA_NEGATIVE_REGEX_FILTER);

        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "onCreateLoader() >> path = " + path);

        return new CursorLoader(
                getActivity(),
                BaseFile.genContentUriBase(path.getAuthority())
                        .buildUpon()
                        .appendPath(path.getLastPathSegment())
                        .appendQueryParameter(BaseFile.PARAM_TASK_ID,
                                Integer.toString(mIdLoaderData))
                        .appendQueryParameter(
                                BaseFile.PARAM_SHOW_HIDDEN_FILES,
                                Boolean.toString(getArguments()
                                        .getBoolean(
                                                FileChooserActivity.EXTRA_DISPLAY_HIDDEN_FILES)))
                        .appendQueryParameter(BaseFile.PARAM_FILTER_MODE,
                                Integer.toString(mFilterMode))
                        .appendQueryParameter(
                                BaseFile.PARAM_SORT_BY,
                                Integer.toString(DisplayPrefs
                                        .getSortType(getActivity())))
                        .appendQueryParameter(
                                BaseFile.PARAM_SORT_ASCENDING,
                                Boolean.toString(DisplayPrefs
                                        .isSortAscending(getActivity())))
                        .appendQueryParameter(BaseFile.PARAM_LIMIT,
                                Integer.toString(mMaxFileCount))
                        .appendQueryParameter(
                                BaseFile.PARAM_POSITIVE_REGEX_FILTER,
                                TextUtils.isEmpty(positiveRegex) ? ""
                                        : positiveRegex)
                        .appendQueryParameter(
                                BaseFile.PARAM_NEGATIVE_REGEX_FILTER,
                                TextUtils.isEmpty(negativeRegex) ? ""
                                        : negativeRegex).build(), null, null,
                null, null);
    }// onCreateLoader()

    @Override
    public void onLoadFinished(Loader<Cursor> loader, final Cursor data) {
        mLoading = false;

        /*
         * Update list view.
         */
        mFileAdapter.changeCursor(data);

        mViewGroupFiles.setVisibility(View.VISIBLE);
        mViewLoadingHandler.removeCallbacksAndMessages(null);
        mViewLoading.setVisibility(View.GONE);
        getActivity().supportInvalidateOptionsMenu();

        if (data == null) {
            showFooterView(true,
                    getString(R.string.afc_msg_failed_please_try_again), true);
            return;
        }

        data.moveToLast();
        final Uri uriInfo = BaseFileProviderUtils.getUri(data);
        final Uri selectedFile = (Uri) getArguments().getParcelable(
                FileChooserActivity.EXTRA_SELECT_FILE);
        final int colUri = data.getColumnIndex(BaseFile.COLUMN_URI);
        if (selectedFile != null)
            getArguments().remove(FileChooserActivity.EXTRA_SELECT_FILE);

        /*
         * Footer.
         */

        if (selectedFile != null && mIsSaveDialog
                && BaseFileProviderUtils.isFile(getActivity(), selectedFile))
            mTxtSaveas.setText(BaseFileProviderUtils.getFileName(getActivity(),
                    selectedFile));

        boolean hasMoreFiles = ProviderUtils.getBooleanQueryParam(uriInfo,
                BaseFile.PARAM_HAS_MORE_FILES);
        showFooterView(
                hasMoreFiles || mFileAdapter.isEmpty(),
                hasMoreFiles ? getString(
                        R.string.afc_pmsg_max_file_count_allowed, mMaxFileCount)
                        : getString(R.string.afc_msg_empty),
                mFileAdapter.isEmpty());

        if (mNewLoader || selectedFile != null) {
            /*
             * Select either the parent path of last path, or the file provided
             * by key EXTRA_SELECT_FILE. Use a Runnable to make sure this work.
             * Because if the list view is handling data, this might not work.
             */
            mViewFiles.post(new Runnable() {

                @Override
                public void run() {
                    int shouldBeSelectedIdx = -1;
                    final Uri uri = selectedFile != null ? selectedFile
                            : getLastLocation();
                    if (uri != null
                            && BaseFileProviderUtils.fileExists(getActivity(),
                                    uri)) {
                        final String fileName = BaseFileProviderUtils
                                .getFileName(getActivity(), uri);
                        if (fileName != null) {
                            Uri parentUri = BaseFileProviderUtils
                                    .getParentFile(getActivity(), uri);
                            if ((uri == getLastLocation()
                                    && !getCurrentLocation().equals(
                                            getLastLocation()) && BaseFileProviderUtils
                                        .isAncestorOf(getActivity(),
                                                getCurrentLocation(), uri))
                                    || getCurrentLocation().equals(parentUri)) {
                                if (data.moveToFirst()) {
                                    while (!data.isLast()) {
                                        Uri subUri = Uri.parse(data
                                                .getString(colUri));
                                        if (uri == getLastLocation()) {
                                            if (data.getInt(data
                                                    .getColumnIndex(BaseFile.COLUMN_TYPE)) == BaseFile.FILE_TYPE_DIRECTORY) {
                                                if (subUri.equals(uri)
                                                        || BaseFileProviderUtils
                                                                .isAncestorOf(
                                                                        getActivity(),
                                                                        subUri,
                                                                        uri)) {
                                                    shouldBeSelectedIdx = Math.max(
                                                            0,
                                                            data.getPosition() - 2);
                                                    break;
                                                }
                                            }
                                        } else {
                                            if (uri.equals(subUri)) {
                                                shouldBeSelectedIdx = Math.max(
                                                        0,
                                                        data.getPosition() - 2);
                                                break;
                                            }
                                        }

                                        data.moveToNext();
                                    }
                                }
                            }
                        }
                    }

                    if (shouldBeSelectedIdx >= 0
                            && shouldBeSelectedIdx < mFileAdapter.getCount())
                        mViewFiles.setSelection(shouldBeSelectedIdx);
                    else if (!mFileAdapter.isEmpty())
                        mViewFiles.setSelection(0);
                }// run()
            });
        }

        mNewLoader = false;
    }// onLoadFinished()

    @Override
    public void onLoaderReset(Loader<Cursor> loader) {
        /*
         * Cancel previous loader if there is one.
         */
        cancelPreviousLoader();

        mFileAdapter.changeCursor(null);
        mViewGroupFiles.setVisibility(View.GONE);
        mViewLoadingHandler.postDelayed(new Runnable() {

            @Override
            public void run() {
                mViewLoading.setVisibility(View.VISIBLE);
            }// run()
        }, DisplayPrefs.DELAY_TIME_FOR_SHORT_ANIMATION);

        getActivity().supportInvalidateOptionsMenu();
    }// onLoaderReset()

    /**
     * Setup:
     * <p/>
     * <ul>
     * <li>title of activity;</li>
     * <li>button go back;</li>
     * <li>button location;</li>
     * <li>button go forward;</li>
     * </ul>
     */
    private void setupHeader() {
        if (mBtnGoHome != null)
            mBtnGoHome.setOnClickListener(mBtnGoHomeOnClickListener);
        if (mBtnBookmarkManager != null)
            mBtnBookmarkManager
                    .setOnClickListener(mBtnBookmarkManagerOnClickListener);

        if (mIsSaveDialog) {
            getActivity().setTitle(R.string.afc_title_save_as);
        } else {
            switch (mFilterMode) {
            case BaseFile.FILTER_FILES_ONLY:
                getActivity().setTitle(
                        getResources().getQuantityText(
                                R.plurals.afc_title_choose_files,
                                mIsMultiSelection ? 2 : 1));
                break;
            case BaseFile.FILTER_FILES_AND_DIRECTORIES:
                getActivity().setTitle(
                        getResources().getQuantityText(
                                R.plurals.afc_title_choose_files_directories,
                                mIsMultiSelection ? 2 : 1));
                break;
            case BaseFile.FILTER_DIRECTORIES_ONLY:
                getActivity().setTitle(
                        getResources().getQuantityText(
                                R.plurals.afc_title_choose_directories,
                                mIsMultiSelection ? 2 : 1));
                break;
            }
        }// title of activity

        mViewGoBack.setEnabled(false);
        mViewGoBack.setOnClickListener(mBtnGoBackOnClickListener);

        mViewGoForward.setEnabled(false);
        mViewGoForward.setOnClickListener(mBtnGoForwardOnClickListener);

        for (ImageView v : new ImageView[] { mViewGoBack, mViewGoForward })
            v.setOnLongClickListener(mBtnGoBackForwardOnLongClickListener);
    }// setupHeader()

    /**
     * Setup:
     * <p/>
     * <ul>
     * <li>{@link #mViewFiles}</li>
     * <li>{@link #mViewFilesContainer}</li>
     * <li>{@link #mFileAdapter}</li>
     * </ul>
     */
    private void setupViewFiles() {
        switch (DisplayPrefs.getViewType(getActivity())) {
        case GRID:
            mViewFiles = (AbsListView) getLayoutInflater(null).inflate(
                    R.layout.afc_gridview_files, null);
            break;
        case LIST:
            mViewFiles = (AbsListView) getLayoutInflater(null).inflate(
                    R.layout.afc_listview_files, null);
            break;
        }

        mViewFilesContainer.removeAllViews();
        mViewFilesContainer.addView(mViewFiles, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT, 1));

        mViewFiles.setOnItemClickListener(mViewFilesOnItemClickListener);
        mViewFiles
                .setOnItemLongClickListener(mViewFilesOnItemLongClickListener);
        mViewFiles.setOnTouchListener(new View.OnTouchListener() {

            @Override
            public boolean onTouch(View v, MotionEvent event) {
                return mListviewFilesGestureDetector.onTouchEvent(event);
            }
        });

        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.HONEYCOMB
                && !getActivity().getWindow().isFloating()) {
            mViewFiles.setCacheColorHint(getResources().getColor(
                    Ui.resolveAttribute(getActivity(),
                            R.attr.afc_color_listview_cache_hint)));
        }

        /*
         * API 13+ does not recognize AbsListView.setAdapter(), so we cast it to
         * explicit class
         */
        if (mViewFiles instanceof ListView)
            ((ListView) mViewFiles).setAdapter(mFileAdapter);
        else
            ((GridView) mViewFiles).setAdapter(mFileAdapter);

        // no comments :-D
        mFooterView.setOnLongClickListener(new View.OnLongClickListener() {

            @Override
            public boolean onLongClick(View v) {
                E.show(getActivity());
                return false;
            }
        });
    }// setupViewFiles()

    /**
     * Setup:
     * <p/>
     * <ul>
     * <li>button Cancel;</li>
     * <li>text field "save as" filename;</li>
     * <li>button OK;</li>
     * </ul>
     */
    private void setupFooter() {
        // by default, view group footer and all its child views are hidden

        ViewGroup viewGroupFooterContainer = (ViewGroup) getView()
                .findViewById(R.id.afc_viewgroup_footer_container);
        ViewGroup viewGroupFooter = (ViewGroup) getView().findViewById(
                R.id.afc_viewgroup_footer);

        if (mIsSaveDialog) {
            viewGroupFooterContainer.setVisibility(View.VISIBLE);
            viewGroupFooter.setVisibility(View.VISIBLE);

            mTxtSaveas.setVisibility(View.VISIBLE);
            mTxtSaveas.setText(getArguments().getString(
                    FileChooserActivity.EXTRA_DEFAULT_FILENAME));
            mTxtSaveas
                    .setOnEditorActionListener(mTxtFilenameOnEditorActionListener);

            mBtnOk.setVisibility(View.VISIBLE);
            mBtnOk.setOnClickListener(mBtnOk_SaveDialog_OnClickListener);
            mBtnOk.setBackgroundResource(Ui.resolveAttribute(getActivity(),
                    R.attr.afc_selector_button_ok_saveas));

            int size = getResources().getDimensionPixelSize(
                    R.dimen.afc_button_ok_saveas_size);
            LinearLayout.LayoutParams lp = (LinearLayout.LayoutParams) mBtnOk
                    .getLayoutParams();
            lp.width = size;
            lp.height = size;
            mBtnOk.setLayoutParams(lp);
        }// this is in save mode
        else {
            if (mIsMultiSelection) {
                viewGroupFooterContainer.setVisibility(View.VISIBLE);
                viewGroupFooter.setVisibility(View.VISIBLE);

                ViewGroup.LayoutParams lp = viewGroupFooter.getLayoutParams();
                lp.width = ViewGroup.LayoutParams.WRAP_CONTENT;
                viewGroupFooter.setLayoutParams(lp);

                mBtnOk.setMinWidth(getResources().getDimensionPixelSize(
                        R.dimen.afc_single_button_min_width));
                mBtnOk.setText(android.R.string.ok);
                mBtnOk.setVisibility(View.VISIBLE);
                mBtnOk.setOnClickListener(mBtnOk_OpenDialog_OnClickListener);
            }
        }// this is in open mode
    }// setupFooter()

    /**
     * Shows footer view.
     * 
     * @param show
     *            {@code true} or {@code false}.
     * @param text
     *            the message you want to set.
     * @param center
     *            {@code true} or {@code false}.
     */
    @SuppressLint("InlinedApi")
    private void showFooterView(boolean show, String text, boolean center) {
        if (show) {
            mFooterView.setText(text);

            RelativeLayout.LayoutParams lp = new RelativeLayout.LayoutParams(
                    RelativeLayout.LayoutParams.MATCH_PARENT,
                    RelativeLayout.LayoutParams.MATCH_PARENT);
            if (!center)
                lp.addRule(RelativeLayout.ABOVE,
                        R.id.afc_view_files_footer_view);
            mViewFilesContainer.setLayoutParams(lp);

            lp = (RelativeLayout.LayoutParams) mFooterView.getLayoutParams();
            lp.addRule(RelativeLayout.CENTER_IN_PARENT, center ? 1 : 0);
            lp.addRule(RelativeLayout.ALIGN_PARENT_BOTTOM, center ? 0 : 1);
            mFooterView.setLayoutParams(lp);

            mFooterView.setVisibility(View.VISIBLE);
        } else
            mFooterView.setVisibility(View.GONE);
    }// showFooterView()

    private void initGestureDetector() {
        mListviewFilesGestureDetector = new GestureDetector(getActivity(),
                new GestureDetector.SimpleOnGestureListener() {

                    private Object getData(float x, float y) {
                        int i = getSubViewId(x, y);
                        if (i >= 0)
                            return mViewFiles.getItemAtPosition(mViewFiles
                                    .getFirstVisiblePosition() + i);
                        return null;
                    }// getSubView()

                    private int getSubViewId(float x, float y) {
                        Rect r = new Rect();
                        for (int i = 0; i < mViewFiles.getChildCount(); i++) {
                            mViewFiles.getChildAt(i).getHitRect(r);
                            if (r.contains((int) x, (int) y))
                                return i;
                        }

                        return -1;
                    }// getSubViewId()

                    /**
                     * Gets {@link Cursor} from {@code e}.
                     * 
                     * @param e
                     *            {@link MotionEvent}.
                     * @return the cursor, or {@code null} if not available.
                     */
                    private Cursor getData(MotionEvent e) {
                        Object o = getData(e.getX(), e.getY());
                        return o instanceof Cursor ? (Cursor) o : null;
                    }// getDataModel()

                    @Override
                    public void onLongPress(MotionEvent e) {
                        // do nothing
                    }// onLongPress()

                    @Override
                    public boolean onSingleTapConfirmed(MotionEvent e) {
                        // do nothing
                        return false;
                    }// onSingleTapConfirmed()

                    @Override
                    public boolean onDoubleTap(MotionEvent e) {
                        if (mDoubleTapToChooseFiles) {
                            if (mIsMultiSelection)
                                return false;

                            Cursor data = getData(e);
                            if (data == null)
                                return false;

                            if (BaseFileProviderUtils.isDirectory(data)
                                    && BaseFile.FILTER_FILES_ONLY == mFilterMode)
                                return false;

                            /*
                             * If mFilterMode == FILTER_DIRECTORIES_ONLY, files
                             * won't be shown.
                             */

                            if (mIsSaveDialog) {
                                if (BaseFileProviderUtils.isFile(data)) {
                                    mTxtSaveas.setText(BaseFileProviderUtils
                                            .getFileName(data));
                                    doCheckSaveasFilenameAndFinish(BaseFileProviderUtils
                                            .getFileName(data));
                                } else
                                    return false;
                            } else
                                doFinish(BaseFileProviderUtils.getUri(data));
                        }// double tap to choose files
                        else {
                            // do nothing
                            return false;
                        }// single tap to choose files

                        return true;
                    }// onDoubleTap()

                    @Override
                    public boolean onFling(MotionEvent e1, MotionEvent e2,
                            float velocityX, float velocityY) {
                        /*
                         * Sometimes e1 or e2 can be null. This came from users'
                         * experiences.
                         */
                        if (e1 == null || e2 == null)
                            return false;

                        final int max_y_distance = 19;// 10 is too short :-D
                        final int min_x_distance = 80;
                        final int min_x_velocity = 200;
                        if (Math.abs(e1.getY() - e2.getY()) < max_y_distance
                                && Math.abs(e1.getX() - e2.getX()) > min_x_distance
                                && Math.abs(velocityX) > min_x_velocity) {
                            int pos = getSubViewId(e1.getX(), e1.getY());
                            if (pos >= 0) {
                                /*
                                 * Don't let this event to be recognized as a
                                 * single tap.
                                 */
                                MotionEvent cancelEvent = MotionEvent
                                        .obtain(e1);
                                cancelEvent
                                        .setAction(MotionEvent.ACTION_CANCEL);
                                mViewFiles.onTouchEvent(cancelEvent);

                                doDeleteFile(mViewFiles
                                        .getFirstVisiblePosition() + pos);
                            }
                        }

                        /*
                         * Always return false to let the default handler draw
                         * the item properly.
                         */
                        return false;
                    }// onFling()
                });// mListviewFilesGestureDetector
    }// initGestureDetector()

    /**
     * Connects to file provider service, then loads root directory. If can not,
     * then finishes this activity with result code =
     * {@link Activity#RESULT_CANCELED}
     * 
     * @param savedInstanceState
     */
    private void loadInitialPath(final Bundle savedInstanceState) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, String.format(
                    "loadInitialPath() >> authority=[%s] | mRoot=[%s]",
                    mFileProviderAuthority, mRoot));

        /*
         * Priorities for starting path:
         * 
         * 1. Current location (in case the activity has been killed after
         * configurations changed).
         * 
         * 2. Selected file from key EXTRA_SELECT_FILE.
         * 
         * 3. Root path from key EXTRA_ROOTPATH.
         * 
         * 4. Last location.
         */

        /*
         * Current location
         */
        Uri path = (Uri) (savedInstanceState != null ? savedInstanceState
                .getParcelable(CURRENT_LOCATION) : null);

        /*
         * Selected file
         */
        if (path == null) {
            path = (Uri) getArguments().getParcelable(
                    FileChooserActivity.EXTRA_SELECT_FILE);
            if (path != null
                    && BaseFileProviderUtils.fileExists(getActivity(), path))
                path = BaseFileProviderUtils.getParentFile(getActivity(), path);
        }

        /*
         * Rootpath
         */
        if (path == null
                || !BaseFileProviderUtils.isDirectory(getActivity(), path)) {
            path = mRoot;
        }

        /*
         * Last location
         */
        if (path == null && DisplayPrefs.isRememberLastLocation(getActivity())) {
            String lastLocation = DisplayPrefs.getLastLocation(getActivity());
            if (lastLocation != null)
                path = Uri.parse(lastLocation);
        }

        if (path == null
                || !BaseFileProviderUtils.isDirectory(getActivity(), path))
            path = BaseFileProviderUtils
                    .getDefaultPath(
                            getActivity(),
                            path == null ? mFileProviderAuthority : path
                                    .getAuthority());

        if (path == null) {
            doShowCannotConnectToServiceAndFinish();
            return;
        }

        if (!BaseFileProviderUtils.fileCanRead(getActivity(), path)) {
            Dlg.toast(
                    getActivity(),
                    getString(R.string.afc_pmsg_cannot_access_dir,
                            BaseFileProviderUtils.getFileName(getActivity(),
                                    path)), Dlg.LENGTH_SHORT);
            getActivity().finish();
        }

        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "loadInitialPath() >> " + path);

        setCurrentLocation(path);

        /*
         * Prepare the loader. Either re-connect with an existing one, or start
         * a new one.
         */
        Bundle b = new Bundle();
        b.putParcelable(PATH, path);
        getLoaderManager().initLoader(mIdLoaderData, b, this);
    }// loadInitialPath()

    /**
     * Checks if the fragment is loading files...
     * 
     * @return {@code true} or {@code false}.
     */
    public boolean isLoading() {
        return mLoading;
    }// isLoading()

    /**
     * Cancels the loader in progress.
     */
    public void cancelPreviousLoader() {
        /*
         * Adds a fake path...
         */
        if (getCurrentLocation() != null
                && getLoaderManager().getLoader(mIdLoaderData) != null)
            BaseFileProviderUtils.cancelTask(getActivity(),
                    getCurrentLocation().getAuthority(), mIdLoaderData);

        mLoading = false;
    }// cancelPreviousLoader()

    /**
     * As the name means...
     */
    private void doShowCannotConnectToServiceAndFinish() {
        Dlg.showError(getActivity(),
                R.string.afc_msg_cannot_connect_to_file_provider_service,
                new DialogInterface.OnCancelListener() {

                    @Override
                    public void onCancel(DialogInterface dialog) {
                        getActivity().setResult(Activity.RESULT_CANCELED);
                        getActivity().finish();
                    }// onCancel()
                });
    }// doShowCannotConnectToServiceAndFinish()

    /**
     * Gets last location.
     * 
     * @return the last location.
     */
    private Uri getLastLocation() {
        return mLastLocation;
    }// getLastLocation()

    /**
     * Gets current location.
     * 
     * @return the current location.
     */
    private Uri getCurrentLocation() {
        return mCurrentLocation;
    }// getCurrentLocation()

    /**
     * Sets current location.
     * 
     * @param location
     *            the location to set.
     */
    private void setCurrentLocation(Uri location) {
        /*
         * Do this so history's listener will retrieve the right current
         * location.
         */
        mLastLocation = mCurrentLocation;
        mCurrentLocation = location;

        if (mHistory.indexOf(location) < 0) {
            mHistory.truncateAfter(mLastLocation);
            mHistory.push(location);
        } else
            mHistory.notifyHistoryChanged();

        updateDbHistory(location);
    }// setCurrentLocation()

    private void doGoHome() {
        goTo(mRoot);
    }// doGoHome()

    /**
     * Shows bookmark manager.
     */
    private void doShowBookmarkManager() {
        BookmarkFragment bf = BookmarkFragment.newInstance(true);
        bf.setOnBookmarkItemClickListener(mBookmarkFragmentOnBookmarkItemClickListener);
        bf.show(getChildFragmentManager().beginTransaction(),
                BookmarkFragment.class.getName());
    }// doShowBookmarkManager()

    /**
     * Shows history manager.
     */
    private void doShowHistoryManager() {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "doShowHistoryManager()");

        // Create and show the dialog.
        final HistoryFragment fragment = HistoryFragment.newInstance();
        fragment.setOnHistoryItemClickListener(new HistoryFragment.OnHistoryItemClickListener() {

            @Override
            public void onItemClick(String providerId, final Uri uri) {
                /*
                 * TODO what to do with `providerId`?
                 */

                /*
                 * Check if `uri` is in internal list, then use it instead of
                 * that.
                 */
                if (!mHistory.find(new HistoryFilter<Uri>() {

                    @Override
                    public boolean accept(Uri item) {
                        if (uri.equals(item)) {
                            goTo(item);
                            return true;
                        }

                        return false;
                    }// accept()
                }, false))
                    goTo(uri);
            }// onItemClick()
        });

        /*
         * DialogFragment.show() will take care of adding the fragment in a
         * transaction. We also want to remove any currently showing dialog, so
         * make our own transaction and take care of that here.
         */
        fragment.show(getChildFragmentManager().beginTransaction(),
                HistoryFragment.class.getName());
    }// doShowHistoryManager()

    private static final int[] BUTTON_SORT_IDS = {
            R.id.afc_button_sort_by_name_asc,
            R.id.afc_button_sort_by_name_desc,
            R.id.afc_button_sort_by_size_asc,
            R.id.afc_button_sort_by_size_desc,
            R.id.afc_button_sort_by_date_asc, R.id.afc_button_sort_by_date_desc };

    /**
     * Show a dialog for sorting options and resort file list after user
     * selected an option.
     */
    private void doResortViewFiles() {
        final Dialog dialog = new Dialog(getActivity(), Ui.resolveAttribute(
                getActivity(), R.attr.afc_theme_dialog));
        dialog.setCanceledOnTouchOutside(true);

        // get the index of button of current sort type
        int btnCurrentSortTypeIdx = 0;
        switch (DisplayPrefs.getSortType(getActivity())) {
        case BaseFile.SORT_BY_NAME:
            btnCurrentSortTypeIdx = 0;
            break;
        case BaseFile.SORT_BY_SIZE:
            btnCurrentSortTypeIdx = 2;
            break;
        case BaseFile.SORT_BY_MODIFICATION_TIME:
            btnCurrentSortTypeIdx = 4;
            break;
        }
        if (!DisplayPrefs.isSortAscending(getActivity()))
            btnCurrentSortTypeIdx++;

        View.OnClickListener listener = new View.OnClickListener() {

            @Override
            public void onClick(View v) {
                dialog.dismiss();

                if (v.getId() == R.id.afc_button_sort_by_name_asc) {
                    DisplayPrefs.setSortType(getActivity(),
                            BaseFile.SORT_BY_NAME);
                    DisplayPrefs.setSortAscending(getActivity(), true);
                } else if (v.getId() == R.id.afc_button_sort_by_name_desc) {
                    DisplayPrefs.setSortType(getActivity(),
                            BaseFile.SORT_BY_NAME);
                    DisplayPrefs.setSortAscending(getActivity(), false);
                } else if (v.getId() == R.id.afc_button_sort_by_size_asc) {
                    DisplayPrefs.setSortType(getActivity(),
                            BaseFile.SORT_BY_SIZE);
                    DisplayPrefs.setSortAscending(getActivity(), true);
                } else if (v.getId() == R.id.afc_button_sort_by_size_desc) {
                    DisplayPrefs.setSortType(getActivity(),
                            BaseFile.SORT_BY_SIZE);
                    DisplayPrefs.setSortAscending(getActivity(), false);
                } else if (v.getId() == R.id.afc_button_sort_by_date_asc) {
                    DisplayPrefs.setSortType(getActivity(),
                            BaseFile.SORT_BY_MODIFICATION_TIME);
                    DisplayPrefs.setSortAscending(getActivity(), true);
                } else if (v.getId() == R.id.afc_button_sort_by_date_desc) {
                    DisplayPrefs.setSortType(getActivity(),
                            BaseFile.SORT_BY_MODIFICATION_TIME);
                    DisplayPrefs.setSortAscending(getActivity(), false);
                }

                /*
                 * Reload current location.
                 */
                goTo(getCurrentLocation());
                getActivity().supportInvalidateOptionsMenu();
            }// onClick()
        };// listener

        View view = getLayoutInflater(null).inflate(
                R.layout.afc_settings_sort_view, null);
        for (int i = 0; i < BUTTON_SORT_IDS.length; i++) {
            View v = view.findViewById(BUTTON_SORT_IDS[i]);
            v.setOnClickListener(listener);
            if (i == btnCurrentSortTypeIdx) {
                v.setEnabled(false);
                if (v instanceof Button)
                    ((Button) v).setText(R.string.afc_bullet);
            }
        }

        dialog.setTitle(R.string.afc_title_sort_by);
        dialog.setContentView(view);
        dialog.show();
    }// doResortViewFiles()

    /**
     * Switch view type between {@link ViewType#LIST} and {@link ViewType#GRID}
     */
    private void doSwitchViewType() {
        switch (DisplayPrefs.getViewType(getActivity())) {
        case GRID:
            DisplayPrefs.setViewType(getActivity(), ViewType.LIST);
            break;
        case LIST:
            DisplayPrefs.setViewType(getActivity(), ViewType.GRID);
            break;
        }

        setupViewFiles();
        getActivity().supportInvalidateOptionsMenu();
        goTo(getCurrentLocation());
    }// doSwitchViewType()

    /**
     * Confirms user to create new directory.
     */
    private void doCreateNewDir() {
        if (LocalFileContract.getAuthority(getActivity()).equals(
                mFileProviderAuthority)
                && !Utils.hasPermissions(getActivity(),
                        Manifest.permission.WRITE_EXTERNAL_STORAGE)) {
            Dlg.toast(
                    getActivity(),
                    R.string.afc_msg_app_doesnot_have_permission_to_create_files,
                    Dlg.LENGTH_SHORT);
            return;
        }

        if (getCurrentLocation() == null
                || !BaseFileProviderUtils.fileCanWrite(getActivity(),
                        getCurrentLocation())) {
            Dlg.toast(getActivity(),
                    R.string.afc_msg_cannot_create_new_folder_here,
                    Dlg.LENGTH_SHORT);
            return;
        }

        final AlertDialog dialog = Dlg.newAlertDlg(getActivity());

        View view = getLayoutInflater(null).inflate(
                R.layout.afc_simple_text_input_view, null);
        final EditText textFile = (EditText) view.findViewById(R.id.afc_text1);
        textFile.setHint(R.string.afc_hint_folder_name);
        textFile.setOnEditorActionListener(new TextView.OnEditorActionListener() {

            @Override
            public boolean onEditorAction(TextView v, int actionId,
                    KeyEvent event) {
                if (actionId == EditorInfo.IME_ACTION_DONE) {
                    Ui.showSoftKeyboard(v, false);
                    dialog.getButton(DialogInterface.BUTTON_POSITIVE)
                            .performClick();
                    return true;
                }
                return false;
            }
        });

        dialog.setView(view);
        dialog.setTitle(R.string.afc_cmd_new_folder);
        dialog.setIcon(android.R.drawable.ic_menu_add);
        dialog.setButton(DialogInterface.BUTTON_POSITIVE,
                getString(android.R.string.ok),
                new DialogInterface.OnClickListener() {

                    @Override
                    public void onClick(DialogInterface dialog, int which) {
                        String name = textFile.getText().toString().trim();
                        if (!FileUtils.isFilenameValid(name)) {
                            Dlg.toast(
                                    getActivity(),
                                    getString(
                                            R.string.afc_pmsg_filename_is_invalid,
                                            name), Dlg.LENGTH_SHORT);
                            return;
                        }

                        if (getActivity()
                                .getContentResolver()
                                .insert(BaseFile
                                        .genContentUriBase(
                                                getCurrentLocation()
                                                        .getAuthority())
                                        .buildUpon()
                                        .appendPath(
                                                getCurrentLocation()
                                                        .getLastPathSegment())
                                        .appendQueryParameter(
                                                BaseFile.PARAM_NAME, name)
                                        .appendQueryParameter(
                                                BaseFile.PARAM_FILE_TYPE,
                                                Integer.toString(BaseFile.FILE_TYPE_DIRECTORY))
                                        .build(), null) != null) {
                            Dlg.toast(getActivity(),
                                    getString(R.string.afc_msg_done),
                                    Dlg.LENGTH_SHORT);
                        } else
                            Dlg.toast(
                                    getActivity(),
                                    getString(
                                            R.string.afc_pmsg_cannot_create_folder,
                                            name), Dlg.LENGTH_SHORT);
                    }// onClick()
                });
        dialog.show();
        Ui.showSoftKeyboard(textFile, true);

        final Button buttonOk = dialog
                .getButton(DialogInterface.BUTTON_POSITIVE);
        buttonOk.setEnabled(false);

        textFile.addTextChangedListener(new TextWatcher() {

            @Override
            public void onTextChanged(CharSequence s, int start, int before,
                    int count) {
                // do nothing
            }

            @Override
            public void beforeTextChanged(CharSequence s, int start, int count,
                    int after) {
                // do nothing
            }

            @Override
            public void afterTextChanged(Editable s) {
                buttonOk.setEnabled(FileUtils.isFilenameValid(s.toString()
                        .trim()));
            }
        });
    }// doCreateNewDir()

    /**
     * Deletes a file.
     * 
     * @param position
     *            the position of item to be delete.
     */
    private void doDeleteFile(final int position) {
        Cursor cursor = (Cursor) mFileAdapter.getItem(position);

        /*
         * The cursor can be changed if the list view is updated, so we take its
         * properties here.
         */
        final boolean isFile = BaseFileProviderUtils.isFile(cursor);
        final String filename = BaseFileProviderUtils.getFileName(cursor);

        if (!BaseFileProviderUtils.fileCanWrite(cursor)) {
            Dlg.toast(
                    getActivity(),
                    getString(R.string.afc_pmsg_cannot_delete_file,
                            isFile ? getString(R.string.afc_file)
                                    : getString(R.string.afc_folder), filename),
                    Dlg.LENGTH_SHORT);
            return;
        }

        if (LocalFileContract.getAuthority(getActivity()).equals(
                mFileProviderAuthority)
                && !Utils.hasPermissions(getActivity(),
                        Manifest.permission.WRITE_EXTERNAL_STORAGE)) {
            Dlg.toast(
                    getActivity(),
                    R.string.afc_msg_app_doesnot_have_permission_to_delete_files,
                    Dlg.LENGTH_SHORT);
            return;
        }

        /*
         * The cursor can be changed if the list view is updated, so we take its
         * properties here.
         */
        final int id = cursor.getInt(cursor.getColumnIndex(BaseFile._ID));
        final Uri uri = BaseFileProviderUtils.getUri(cursor);

        mFileAdapter.markItemAsDeleted(id, true);

        Dlg.confirmYesno(
                getActivity(),
                getString(R.string.afc_pmsg_confirm_delete_file,
                        isFile ? getString(R.string.afc_file)
                                : getString(R.string.afc_folder), filename),
                new DialogInterface.OnClickListener() {

                    @Override
                    public void onClick(DialogInterface dialog, int which) {
                        new LoadingDialog(getActivity(), getString(
                                R.string.afc_pmsg_deleting_file,
                                isFile ? getString(R.string.afc_file)
                                        : getString(R.string.afc_folder),
                                filename), true) {

                            final int mTaskId = EnvUtils.genId();

                            private void notifyFileDeleted() {
                                mHistory.removeAll(new HistoryFilter<Uri>() {

                                    @Override
                                    public boolean accept(Uri item) {
                                        return !BaseFileProviderUtils
                                                .isDirectory(getActivity(),
                                                        item);
                                    }// accept()
                                });
                                /*
                                 * TODO remove all duplicate items?
                                 */

                                Dlg.toast(
                                        getActivity(),
                                        getString(
                                                R.string.afc_pmsg_file_has_been_deleted,
                                                isFile ? getString(R.string.afc_file)
                                                        : getString(R.string.afc_folder),
                                                filename), Dlg.LENGTH_SHORT);
                            }// notifyFileDeleted()

                            @Override
                            protected Object doInBackground(Void... arg0) {
                                getActivity()
                                        .getContentResolver()
                                        .delete(uri
                                                .buildUpon()
                                                .appendQueryParameter(
                                                        BaseFile.PARAM_TASK_ID,
                                                        Integer.toString(mTaskId))
                                                .build(), null, null);

                                return null;
                            }// doInBackground()

                            @Override
                            protected void onCancelled() {
                                if (getCurrentLocation() != null)
                                    BaseFileProviderUtils.cancelTask(
                                            getActivity(), getCurrentLocation()
                                                    .getAuthority(), mTaskId);

                                if (BaseFileProviderUtils.fileExists(
                                        getActivity(), uri)) {
                                    mFileAdapter.markItemAsDeleted(id, false);
                                    Dlg.toast(getActivity(),
                                            R.string.afc_msg_cancelled,
                                            Dlg.LENGTH_SHORT);
                                } else
                                    notifyFileDeleted();

                                super.onCancelled();
                            }// onCancelled()

                            @Override
                            protected void onPostExecute(Object result) {
                                super.onPostExecute(result);

                                if (BaseFileProviderUtils.fileExists(
                                        getActivity(), uri)) {
                                    mFileAdapter.markItemAsDeleted(id, false);
                                    Dlg.toast(
                                            getActivity(),
                                            getString(
                                                    R.string.afc_pmsg_cannot_delete_file,
                                                    isFile ? getString(R.string.afc_file)
                                                            : getString(R.string.afc_folder),
                                                    filename), Dlg.LENGTH_SHORT);
                                } else
                                    notifyFileDeleted();
                            }// onPostExecute()
                        }.execute();// LoadingDialog
                    }// onClick()
                }, new DialogInterface.OnCancelListener() {

                    @Override
                    public void onCancel(DialogInterface dialog) {
                        mFileAdapter.markItemAsDeleted(id, false);
                    }// onCancel()
                });
    }// doDeleteFile()

    /**
     * As the name means.
     * 
     * @param filename
     * @since v1.91
     */
    private void doCheckSaveasFilenameAndFinish(String filename) {
        if (!BaseFileProviderUtils.fileCanWrite(getActivity(),
                getCurrentLocation())) {
            Dlg.toast(getActivity(),
                    getString(R.string.afc_msg_cannot_save_a_file_here),
                    Dlg.LENGTH_SHORT);
            return;
        }
        if (TextUtils.isEmpty(filename) || !FileUtils.isFilenameValid(filename)) {
            Dlg.toast(getActivity(),
                    getString(R.string.afc_pmsg_filename_is_invalid, filename),
                    Dlg.LENGTH_SHORT);
            return;
        }

        final Cursor cursor = getActivity().getContentResolver().query(
                getCurrentLocation()
                        .buildUpon()
                        .appendQueryParameter(BaseFile.PARAM_APPEND_NAME,
                                filename).build(), null, null, null, null);
        if (cursor != null) {
            try {
                if (cursor.moveToFirst()) {
                    final Uri uri = BaseFileProviderUtils.getUri(cursor);
                    switch (cursor.getInt(cursor
                            .getColumnIndex(BaseFile.COLUMN_TYPE))) {
                    case BaseFile.FILE_TYPE_DIRECTORY:
                        Dlg.toast(
                                getActivity(),
                                getString(
                                        R.string.afc_pmsg_filename_is_directory,
                                        filename), Dlg.LENGTH_SHORT);
                        break;// FILE_TYPE_DIRECTORY

                    case BaseFile.FILE_TYPE_FILE:
                        Dlg.confirmYesno(
                                getActivity(),
                                getString(
                                        R.string.afc_pmsg_confirm_replace_file,
                                        filename),
                                new DialogInterface.OnClickListener() {

                                    @Override
                                    public void onClick(DialogInterface dialog,
                                            int which) {
                                        doFinish(uri);
                                    }// onClick()
                                });

                        break;// FILE_TYPE_FILE

                    case BaseFile.FILE_TYPE_NOT_EXISTED:
                        /*
                         * TODO file type unknown?
                         */
                        doFinish(uri);
                        break;// FILE_TYPE_NOT_EXISTED
                    }
                }
            } finally {
                cursor.close();
            }
        }
    }// doCheckSaveasFilenameAndFinish()

    /**
     * Goes to a specified location.
     * 
     * @param dir
     *            a directory, of course.
     * @return {@code true} if {@code dir} <b><i>can</i></b> be browsed to.
     * @since v4.3 beta
     */
    private boolean goTo(Uri dir) {
        if (dir == null)
            dir = BaseFileProviderUtils.getDefaultPath(getActivity(),
                    mFileProviderAuthority);
        if (dir == null) {
            doShowCannotConnectToServiceAndFinish();
            return false;
        }

        /*
         * Check if the path of `dir` is same as current location, then set
         * `dir` to current location. This avoids of pushing two same paths into
         * history, because we compare the pointers (not the paths) when pushing
         * it to history.
         */
        if (dir.equals(getCurrentLocation()))
            dir = getCurrentLocation();

        if (BaseFileProviderUtils.fileCanRead(getActivity(), dir)) {
            /*
             * Cancel previous loader if there is one.
             */
            cancelPreviousLoader();

            setCurrentLocation(dir);

            Bundle b = new Bundle();
            b.putParcelable(PATH, dir);
            getLoaderManager().restartLoader(mIdLoaderData, b, this);
            return true;
        }

        Dlg.toast(
                getActivity(),
                getString(R.string.afc_pmsg_cannot_access_dir,
                        BaseFileProviderUtils.getFileName(getActivity(), dir)),
                Dlg.LENGTH_SHORT);
        return false;
    }// goTo()

    /**
     * Updates or inserts {@code path} into history database.
     */
    private void updateDbHistory(Uri path) {
        if (BuildConfig.DEBUG)
            Log.d(CLASSNAME, "updateDbHistory() >> path = " + path);

        Calendar cal = Calendar.getInstance();
        final long beginTodayMillis = cal.getTimeInMillis()
                - (cal.get(Calendar.HOUR_OF_DAY) * 60 * 60 * 1000
                        + cal.get(Calendar.MINUTE) * 60 * 1000 + cal
                        .get(Calendar.SECOND) * 1000);
        if (BuildConfig.DEBUG) {
            Log.d(CLASSNAME,
                    String.format("beginToday = %s (%s)", DbUtils
                            .formatNumber(beginTodayMillis), new Date(
                            beginTodayMillis)));
            Log.d(CLASSNAME, String.format("endToday = %s (%s)", DbUtils
                    .formatNumber(beginTodayMillis + DateUtils.DAY_IN_MILLIS),
                    new Date(beginTodayMillis + DateUtils.DAY_IN_MILLIS)));
        }

        /*
         * Does the update and returns the number of rows updated.
         */
        long time = new Date().getTime();
        ContentValues values = new ContentValues();
        values.put(HistoryContract.COLUMN_PROVIDER_ID,
                BaseFileProviderUtils.getProviderId(path.getAuthority()));
        values.put(HistoryContract.COLUMN_FILE_TYPE,
                BaseFile.FILE_TYPE_DIRECTORY);
        values.put(HistoryContract.COLUMN_URI, path.toString());
        values.put(HistoryContract.COLUMN_MODIFICATION_TIME,
                DbUtils.formatNumber(time));

        int count = getActivity()
                .getContentResolver()
                .update(HistoryContract.genContentUri(getActivity()),
                        values,
                        String.format(
                                "%s >= '%s' and %s < '%s' and %s = %s and %s like %s",
                                HistoryContract.COLUMN_MODIFICATION_TIME,
                                DbUtils.formatNumber(beginTodayMillis),
                                HistoryContract.COLUMN_MODIFICATION_TIME,
                                DbUtils.formatNumber(beginTodayMillis
                                        + DateUtils.DAY_IN_MILLIS),
                                HistoryContract.COLUMN_PROVIDER_ID,
                                DatabaseUtils.sqlEscapeString(values
                                        .getAsString(HistoryContract.COLUMN_PROVIDER_ID)),
                                HistoryContract.COLUMN_URI,
                                DatabaseUtils.sqlEscapeString(values
                                        .getAsString(HistoryContract.COLUMN_URI))),
                        null);
        if (count <= 0) {
            values.put(HistoryContract.COLUMN_CREATE_TIME,
                    DbUtils.formatNumber(time));
            getActivity().getContentResolver().insert(
                    HistoryContract.genContentUri(getActivity()), values);
        }
    }// updateDbHistory()

    /**
     * As the name means.
     */
    private void createLocationButtons(Uri path) {
        if (path == null)
            return;

        mViewLocations.removeAllViews();

        LinearLayout.LayoutParams lpBtnLoc = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT);
        lpBtnLoc.gravity = Gravity.CENTER;
        LinearLayout.LayoutParams lpDivider = null;
        LayoutInflater inflater = getLayoutInflater(null);
        final int dim = getResources().getDimensionPixelSize(R.dimen.afc_5dp);
        int count = 0;

        Cursor cursor = getActivity().getContentResolver().query(path, null,
                null, null, null);
        while (cursor != null) {
            Uri lastUri = null;
            if (cursor.moveToFirst()) {
                lastUri = Uri.parse(cursor.getString(cursor
                        .getColumnIndex(BaseFile.COLUMN_URI)));

                TextView btnLoc = (TextView) inflater.inflate(
                        R.layout.afc_button_location, null);
                String name = BaseFileProviderUtils.getFileName(cursor);
                btnLoc.setText(TextUtils.isEmpty(name) ? getString(R.string.afc_root)
                        : name);
                btnLoc.setTag(lastUri);
                btnLoc.setOnClickListener(mBtnLocationOnClickListener);
                btnLoc.setOnLongClickListener(mBtnLocationOnLongClickListener);
                mViewLocations.addView(btnLoc, 0, lpBtnLoc);

                if (count++ == 0) {
                    Rect r = new Rect();
                    btnLoc.getPaint().getTextBounds(name, 0, name.length(), r);
                    if (r.width() >= getResources().getDimensionPixelSize(
                            R.dimen.afc_button_location_max_width)
                            - btnLoc.getPaddingLeft()
                            - btnLoc.getPaddingRight()) {
                        mTxtFullDirName.setText(cursor.getString(cursor
                                .getColumnIndex(BaseFile.COLUMN_NAME)));
                        mTxtFullDirName.setVisibility(View.VISIBLE);
                    } else
                        mTxtFullDirName.setVisibility(View.GONE);
                }
            }

            cursor.close();

            if (lastUri == null)
                break;

            /*
             * Process the parent directory.
             */
            cursor = getActivity().getContentResolver().query(
                    BaseFile.genContentUriApi(lastUri.getAuthority())
                            .buildUpon()
                            .appendPath(BaseFile.CMD_GET_PARENT)
                            .appendQueryParameter(BaseFile.PARAM_SOURCE,
                                    lastUri.getLastPathSegment()).build(),
                    null, null, null, null);
            if (cursor != null) {
                View divider = inflater.inflate(
                        R.layout.afc_view_locations_divider, null);

                if (lpDivider == null) {
                    lpDivider = new LinearLayout.LayoutParams(dim, dim);
                    lpDivider.gravity = Gravity.CENTER;
                    lpDivider.setMargins(dim, dim, dim, dim);
                }
                mViewLocations.addView(divider, 0, lpDivider);
            }
        }

        /*
         * Sometimes without delay time, it doesn't work...
         */
        mViewLocationsContainer.postDelayed(new Runnable() {

            public void run() {
                mViewLocationsContainer
                        .fullScroll(HorizontalScrollView.FOCUS_RIGHT);
            }
        }, DisplayPrefs.DELAY_TIME_FOR_VERY_SHORT_ANIMATION);
    }// createLocationButtons()

    /**
     * Finishes this activity.
     * 
     * @param files
     *            list of {@link Uri}.
     */
    private void doFinish(Uri... files) {
        List<Uri> list = new ArrayList<Uri>();
        for (Uri uri : files)
            list.add(uri);
        doFinish((ArrayList<Uri>) list);
    }// doFinish()

    /**
     * Finishes this activity.
     * 
     * @param files
     *            list of {@link Uri}.
     */
    private void doFinish(ArrayList<Uri> files) {
        if (files == null || files.isEmpty()) {
            getActivity().setResult(Activity.RESULT_CANCELED);
            getActivity().finish();
            return;
        }

        Intent intent = new Intent();
        intent.putParcelableArrayListExtra(FileChooserActivity.EXTRA_RESULTS,
                files);
        getActivity().setResult(FileChooserActivity.RESULT_OK, intent);

        if (DisplayPrefs.isRememberLastLocation(getActivity())
                && getCurrentLocation() != null)
            DisplayPrefs.setLastLocation(getActivity(), getCurrentLocation()
                    .toString());
        else
            DisplayPrefs.setLastLocation(getActivity(), null);

        getActivity().finish();
    }// doFinish()

    /**
     * ******************************************************* BUTTON LISTENERS
     */

    private final View.OnClickListener mBtnGoHomeOnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            doGoHome();
        }// onClick()
    };// mBtnGoHomeOnClickListener

    private final View.OnClickListener mBtnBookmarkManagerOnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            if (mBookmarkFragment != null)
                mBookmarkFragment.setEditor(!mBookmarkFragment.isEditor());
        }// onClick()
    };// mBtnBookmarkManagerOnClickListener

    private final View.OnClickListener mBtnGoBackOnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            /*
             * If user deleted a dir which was one in history, then maybe there
             * are duplicates, so we check and remove them here.
             */
            Uri currentLoc = getCurrentLocation();
            Uri preLoc = null;

            while (currentLoc.equals(preLoc = mHistory.prevOf(currentLoc)))
                mHistory.remove(preLoc);

            if (preLoc != null)
                goTo(preLoc);
            else
                mViewGoBack.setEnabled(false);
        }
    };// mBtnGoBackOnClickListener

    private final View.OnClickListener mBtnLocationOnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            if (v.getTag() instanceof Uri) {
                goTo((Uri) v.getTag());
            }
        }// onClick()
    };// mBtnLocationOnClickListener

    private final View.OnLongClickListener mBtnLocationOnLongClickListener = new View.OnLongClickListener() {

        @Override
        public boolean onLongClick(View v) {
            if (BaseFile.FILTER_FILES_ONLY == mFilterMode || mIsSaveDialog)
                return false;

            doFinish((Uri) v.getTag());

            return false;
        }// onLongClick()

    };// mBtnLocationOnLongClickListener

    private final View.OnClickListener mBtnGoForwardOnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            /*
             * If user deleted a dir which was one in history, then maybe there
             * are duplicates, so we check and remove them here.
             */
            Uri currentLoc = getCurrentLocation();
            Uri nextLoc = null;

            while (currentLoc.equals(nextLoc = mHistory.nextOf(currentLoc)))
                mHistory.remove(nextLoc);

            if (nextLoc != null)
                goTo(nextLoc);
            else
                mViewGoForward.setEnabled(false);
        }// onClick()
    };// mBtnGoForwardOnClickListener

    private final View.OnLongClickListener mBtnGoBackForwardOnLongClickListener = new View.OnLongClickListener() {

        @Override
        public boolean onLongClick(View v) {
            doShowHistoryManager();
            return true;
        }// onLongClick()
    };// mBtnGoBackForwardOnLongClickListener

    private final TextView.OnEditorActionListener mTxtFilenameOnEditorActionListener = new TextView.OnEditorActionListener() {

        @Override
        public boolean onEditorAction(TextView v, int actionId, KeyEvent event) {
            if (actionId == EditorInfo.IME_ACTION_DONE) {
                Ui.showSoftKeyboard(v, false);
                mBtnOk.performClick();
                return true;
            }
            return false;
        }// onEditorAction()
    };// mTxtFilenameOnEditorActionListener

    private final View.OnClickListener mBtnOk_SaveDialog_OnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            Ui.showSoftKeyboard(v, false);
            String filename = mTxtSaveas.getText().toString().trim();
            doCheckSaveasFilenameAndFinish(filename);
        }// onClick()
    };// mBtnOk_SaveDialog_OnClickListener

    private final View.OnClickListener mBtnOk_OpenDialog_OnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            doFinish(mFileAdapter.getSelectedItems());
        }// onClick()
    };// mBtnOk_OpenDialog_OnClickListener

    /*
     * FRAGMENT LISTENERS
     */

    private final BookmarkFragment.OnBookmarkItemClickListener mBookmarkFragmentOnBookmarkItemClickListener = new BookmarkFragment.OnBookmarkItemClickListener() {

        @Override
        public void onItemClick(String providerId, final Uri uri) {
            /*
             * TODO what to do with `providerId`?
             */

            /*
             * Check if `uri` is in internal list, then use it instead of that.
             */
            if (!mHistory.find(new HistoryFilter<Uri>() {

                @Override
                public boolean accept(Uri item) {
                    if (uri.equals(item)) {
                        goTo(item);
                        return true;
                    }

                    return false;
                }// accept()
            }, false))
                goTo(uri);
        }// onItemClick()
    };// mBookmarkFragmentOnBookmarkItemClickListener

    /*
     * LISTVIEW HELPER
     */

    private final AdapterView.OnItemClickListener mViewFilesOnItemClickListener = new AdapterView.OnItemClickListener() {

        @Override
        public void onItemClick(AdapterView<?> parent, View view, int position,
                long id) {
            Cursor cursor = (Cursor) mFileAdapter.getItem(position);

            if (BaseFileProviderUtils.isDirectory(cursor)) {
                goTo(BaseFileProviderUtils.getUri(cursor));
                return;
            }

            if (mIsSaveDialog)
                mTxtSaveas.setText(BaseFileProviderUtils.getFileName(cursor));

            if (mDoubleTapToChooseFiles) {
                // do nothing
                return;
            }// double tap to choose files
            else {
                if (mIsMultiSelection)
                    return;

                if (mIsSaveDialog)
                    doCheckSaveasFilenameAndFinish(BaseFileProviderUtils
                            .getFileName(cursor));
                else
                    doFinish(BaseFileProviderUtils.getUri(cursor));
            }// single tap to choose files
        }// onItemClick()
    };// mViewFilesOnItemClickListener

    private final AdapterView.OnItemLongClickListener mViewFilesOnItemLongClickListener = new AdapterView.OnItemLongClickListener() {

        @Override
        public boolean onItemLongClick(AdapterView<?> parent, View view,
                int position, long id) {
            Cursor cursor = (Cursor) mFileAdapter.getItem(position);

            if (mDoubleTapToChooseFiles) {
                // do nothing
            }// double tap to choose files
            else {
                if (!mIsSaveDialog
                        && !mIsMultiSelection
                        && BaseFileProviderUtils.isDirectory(cursor)
                        && (BaseFile.FILTER_DIRECTORIES_ONLY == mFilterMode || BaseFile.FILTER_FILES_AND_DIRECTORIES == mFilterMode)) {
                    doFinish(BaseFileProviderUtils.getUri(cursor));
                }
            }// single tap to choose files

            /*
             * Notify that we already handled long click here.
             */
            return true;
        }// onItemLongClick()
    };// mViewFilesOnItemLongClickListener

    private final BaseFileAdapter.OnBuildOptionsMenuListener mOnBuildOptionsMenuListener = new BaseFileAdapter.OnBuildOptionsMenuListener() {

        @Override
        public void onBuildOptionsMenu(View view, Cursor cursor) {
            if (!BaseFileProviderUtils.fileCanRead(cursor)
                    || !BaseFileProviderUtils.isDirectory(cursor))
                return;

            final Uri uri = BaseFileProviderUtils.getUri(cursor);
            final String name = BaseFileProviderUtils.getFileName(cursor);

            ContextMenuUtils.showContextMenu(getActivity(), 0, 0,
                    new Integer[] { R.string.afc_cmd_add_to_bookmarks },
                    new ContextMenuUtils.OnMenuItemClickListener() {

                        @Override
                        public void onClick(final int resId) {
                            if (resId == R.string.afc_cmd_add_to_bookmarks) {
                                BookmarkFragment
                                        .doEnterNewNameOrRenameBookmark(
                                                getActivity(),
                                                BaseFileProviderUtils.getProviderId(uri
                                                        .getAuthority()), -1,
                                                uri, name);
                            }
                        }// onClick()
                    });
        }// onBuildOptionsMenu()

        @Override
        public void onBuildAdvancedOptionsMenu(View view, Cursor cursor) {
            // TODO Auto-generated method stub
        }// onBuildAdvancedOptionsMenu()
    };// mOnBuildOptionsMenuListener
}
