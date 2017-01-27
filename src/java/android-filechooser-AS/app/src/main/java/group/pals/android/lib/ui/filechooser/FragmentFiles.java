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
import group.pals.android.lib.ui.filechooser.utils.Texts;
import group.pals.android.lib.ui.filechooser.utils.Utils;
import group.pals.android.lib.ui.filechooser.utils.history.History;
import group.pals.android.lib.ui.filechooser.utils.history.HistoryListener;
import group.pals.android.lib.ui.filechooser.utils.history.HistoryStore;
import group.pals.android.lib.ui.filechooser.utils.ui.Dlg;
import group.pals.android.lib.ui.filechooser.utils.ui.LoadingDialog;
import group.pals.android.lib.ui.filechooser.utils.ui.Ui;

import java.util.ArrayList;
import java.util.Calendar;
import java.util.Date;
import java.util.List;
import java.util.regex.Pattern;

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
            FileChooserActivity.EXTRA_DEFAULT_FILE_EXT,
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
     * CONTROLS
     */

    private View mBtnGoHome;
    private HorizontalScrollView mViewLocationsContainer;
    private ViewGroup mViewAddressBar;
    private View mViewGroupFiles;
    private ViewGroup mViewFilesContainer;
    private TextView mTextFullDirName;
    private AbsListView mViewFiles;
    private TextView mFooterView;
    private View mViewLoading;
    private Button mBtnOk;
    private EditText mTextSaveas;
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
        mViewGoBack = (ImageView) rootView
                .findViewById(R.id.afc_button_go_back);
        mViewGoForward = (ImageView) rootView
                .findViewById(R.id.afc_button_go_forward);
        mViewAddressBar = (ViewGroup) rootView
                .findViewById(R.id.afc_view_locations);
        mViewLocationsContainer = (HorizontalScrollView) rootView
                .findViewById(R.id.afc_view_locations_container);
        mTextFullDirName = (TextView) rootView
                .findViewById(R.id.afc_textview_full_dir_name);
        mViewGroupFiles = rootView.findViewById(R.id.afc_viewgroup_files);
        mViewFilesContainer = (ViewGroup) rootView
                .findViewById(R.id.afc_view_files_container);
        mFooterView = (TextView) rootView
                .findViewById(R.id.afc_view_files_footer_view);
        mViewLoading = rootView.findViewById(R.id.afc_view_loading);
        mTextSaveas = (EditText) rootView
                .findViewById(R.id.afc_textview_saveas_filename);
        mBtnOk = (Button) rootView.findViewById(R.id.afc_button_ok);

        /*
         * INIT CONTROLS
         */

        
        return rootView;
    }// onCreateView()

    @Override
    public void onActivityCreated(final Bundle savedInstanceState) {
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
            resortViewFiles();
        else if (item.getItemId() == R.id.afc_menuitem_new_folder)
            checkConditionsThenConfirmUserToCreateNewDir();
        else if (item.getItemId() == R.id.afc_menuitem_switch_viewmode)
            switchViewType();
        else if (item.getItemId() == R.id.afc_menuitem_home)
            goHome();
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

        final Uri path = ((Uri) args.getParcelable(PATH));
        buildAddressBar(path);

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
        if (selectedFile != null)
            getArguments().remove(FileChooserActivity.EXTRA_SELECT_FILE);

        /*
         * Footer.
         */

        if (selectedFile != null && mIsSaveDialog) {
            new LoadingDialog<Void, Void, String>(getActivity(), false) {

                @Override
                protected String doInBackground(Void... params) {
                    if (BaseFileProviderUtils.isFile(getActivity(),
                            selectedFile))
                        return BaseFileProviderUtils.getFileName(getActivity(),
                                selectedFile);
                    return null;
                }// doInBackground()

                @Override
                protected void onPostExecute(String result) {
                    super.onPostExecute(result);

                    if (!TextUtils.isEmpty(result))
                        mTextSaveas.setText(result);
                }// onPostExecute()

            }.execute();
        }// if

        boolean hasMoreFiles = ProviderUtils.getBooleanQueryParam(uriInfo,
                BaseFile.PARAM_HAS_MORE_FILES);
        showFooterView(
                hasMoreFiles || mFileAdapter.isEmpty(),
                hasMoreFiles ? getString(
                        R.string.afc_pmsg_max_file_count_allowed, mMaxFileCount)
                        : getString(R.string.afc_msg_empty),
                mFileAdapter.isEmpty());

        if (mNewLoader || selectedFile != null)
            createFileSelector();

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
        /*
         * By default, view group footer and all its child views are hidden.
         */

        ViewGroup viewGroupFooterContainer = (ViewGroup) getView()
                .findViewById(R.id.afc_viewgroup_footer_container);
        ViewGroup viewGroupFooter = (ViewGroup) getView().findViewById(
                R.id.afc_viewgroup_footer);

        if (mIsSaveDialog) {
            viewGroupFooterContainer.setVisibility(View.VISIBLE);
            viewGroupFooter.setVisibility(View.VISIBLE);

            mTextSaveas.setVisibility(View.VISIBLE);
            mTextSaveas.setText(getArguments().getString(
                    FileChooserActivity.EXTRA_DEFAULT_FILENAME));
            mTextSaveas
                    .setOnEditorActionListener(new TextView.OnEditorActionListener() {

                        @Override
                        public boolean onEditorAction(TextView v, int actionId,
                                KeyEvent event) {
                            if (actionId == EditorInfo.IME_ACTION_DONE) {
                                Ui.showSoftKeyboard(v, false);
                                mBtnOk.performClick();
                                return true;
                            }
                            return false;
                        }// onEditorAction()
                    });
            mTextSaveas.addTextChangedListener(new TextWatcher() {

                @Override
                public void onTextChanged(CharSequence s, int start,
                        int before, int count) {
                    /*
                     * Do nothing.
                     */
                }// onTextChanged()

                @Override
                public void beforeTextChanged(CharSequence s, int start,
                        int count, int after) {
                    /*
                     * Do nothing.
                     */
                }// beforeTextChanged()

                @Override
                public void afterTextChanged(Editable s) {
                    /*
                     * If the user taps a file, the tag is set to that file's
                     * URI. But if the user types the file name, we remove the
                     * tag.
                     */
                    mTextSaveas.setTag(null);
                }// afterTextChanged()
            });

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

    /**
     * This should be called after the owner activity has been created
     * successfully.
     */
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
                        /*
                         * Do nothing.
                         */
                    }// onLongPress()

                    @Override
                    public boolean onSingleTapConfirmed(MotionEvent e) {
                        /*
                         * Do nothing.
                         */
                        return false;
                    }// onSingleTapConfirmed()

                    @Override
                    public boolean onDoubleTap(MotionEvent e) {
                        if (mDoubleTapToChooseFiles) {
                            if (mIsMultiSelection)
                                return false;

                            Cursor cursor = getData(e);
                            if (cursor == null)
                                return false;

                            if (BaseFileProviderUtils.isDirectory(cursor)
                                    && BaseFile.FILTER_FILES_ONLY == mFilterMode)
                                return false;

                            /*
                             * If mFilterMode == FILTER_DIRECTORIES_ONLY, files
                             * won't be shown.
                             */

                            if (mIsSaveDialog) {
                                if (BaseFileProviderUtils.isFile(cursor)) {
                                    mTextSaveas.setText(BaseFileProviderUtils
                                            .getFileName(cursor));
                                    /*
                                     * Always set tag after setting text, or tag
                                     * will be reset to null.
                                     */
                                    mTextSaveas.setTag(BaseFileProviderUtils
                                            .getUri(cursor));
                                    checkSaveasFilenameAndFinish();
                                } else
                                    return false;
                            } else
                                finish(BaseFileProviderUtils.getUri(cursor));
                        }// double tap to choose files
                        else {
                            /*
                             * Do nothing.
                             */
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

                                deleteFile(mViewFiles.getFirstVisiblePosition()
                                        + pos);
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

        new LoadingDialog<Void, Uri, Bundle>(getActivity(), false) {

            /**
             * In onPostExecute(), if result is null then check this value. If
             * this is not null, show a toast and finish. If this is null, call
             * showCannotConnectToServiceAndWaitForTheUserToFinish().
             */
            String errMsg = null;
            boolean errorMessageInDialog = false;

            String log = "";

            @Override
            protected Bundle doInBackground(Void... params) {
                /*
                 * Current location
                 */
                Uri path = (Uri) (savedInstanceState != null ? savedInstanceState
                        .getParcelable(CURRENT_LOCATION) : null);

                log += "savedInstanceState != null ? " + (savedInstanceState != null);
                log += "\npath != null ? " + (path != null);
                if (path != null) log += path;
                log += "\nmRoot != null ? " + (mRoot != null);
                if (mRoot != null) log += mRoot;

                if (mRoot != null) {
                    Uri queryUri = BaseFile.genContentUriApi(mRoot.getAuthority())
                            .buildUpon()
                            .appendPath(BaseFile.CMD_CHECK_CONNECTION)
                            .appendQueryParameter(
                                    BaseFile.PARAM_SOURCE,
                                    mRoot.getLastPathSegment()).build();
                    Cursor cursor = getActivity().getContentResolver().query(
                            queryUri,
                            null, null, null, null);
                    log += "\ncursor != null ? " + (cursor != null);
                    if (cursor != null) {
                        cursor.moveToFirst();

                        errMsg = getString(R.string.afc_msg_cannot_connect_to_file_provider_service) + " " + " " + cursor.getString(0);
                        errorMessageInDialog = true;
                        return null;
                    }

                }

                try
                {
                /*
                 * Selected file
                 */
                    log += "try selected file ";
                    if (path == null) {
                        path = (Uri) getArguments().getParcelable(
                                FileChooserActivity.EXTRA_SELECT_FILE);

                        if (path != null
                                && BaseFileProviderUtils.fileExists(getActivity(),
                                path))
                            path = BaseFileProviderUtils.getParentFile(
                                    getActivity(), path);
                    }
                    log += "success ? " + (path != null);

                /*
                 * Rootpath
                 */
                    log += "rootpath";
                    if ((path == null)
                            || (!BaseFileProviderUtils.isDirectory(getActivity(),
                            path))) {
                        log += " assign";
                        path = mRoot;
                    }
                    if (path != null) {
                        log += " path=" + path;
                        log += " isdir?" + BaseFileProviderUtils.isDirectory(getActivity(),
                                path);
                    }

                /*
                 * Last location
                 */
                    if (path == null
                            && DisplayPrefs.isRememberLastLocation(getActivity())) {
                        String lastLocation = DisplayPrefs
                                .getLastLocation(getActivity());
                        if (lastLocation != null) {
                            path = Uri.parse(lastLocation);
                            log += " from last loc";
                        }
                    }

                    if (path == null
                            || !BaseFileProviderUtils.isDirectory(getActivity(),
                            path)) {
                        path = BaseFileProviderUtils.getDefaultPath(
                                getActivity(),
                                path == null ? mFileProviderAuthority : path
                                        .getAuthority());
                        log += " getDefault. path==null?" + (path == null);
                    }
                }
                catch (Exception ex)
                {
                    errMsg = getString(R.string.afc_msg_cannot_connect_to_file_provider_service)  + " " + ex.toString() ;
                    errorMessageInDialog = true;
                    return null;
                }


                if (path == null) {
                    errMsg = "Did not find initial path.";
                    errorMessageInDialog = true;
                    return null;
                }
                if (BuildConfig.DEBUG)
                    Log.d(CLASSNAME, "loadInitialPath() >> " + path);

                publishProgress(path);

                if (BaseFileProviderUtils.fileCanRead(getActivity(), path)) {
                    Bundle result = new Bundle();
                    result.putParcelable(PATH, path);
                    return result;
                } else {
                    errorMessageInDialog = true;
                    errMsg = getString(R.string.afc_pmsg_cannot_access_dir,
                            BaseFileProviderUtils.getFileName(getActivity(),
                                    path));
                }

                return null;
            }// doInBackground()

            @Override
            protected void onProgressUpdate(Uri... progress) {
                setCurrentLocation(progress[0]);
            }// onProgressUpdate()

            @Override
            protected void onPostExecute(Bundle result) {
                super.onPostExecute(result);

                if (result != null) {
                    /*
                     * Prepare the loader. Either re-connect with an existing
                     * one, or start a new one.
                     */
                    getLoaderManager().initLoader(mIdLoaderData, result,
                            FragmentFiles.this);
                } else if (errMsg != null) {
                    if (errorMessageInDialog)
                    {
                        Dlg.showError(getActivity(),
                                errMsg,
                                new DialogInterface.OnCancelListener() {

                                    @Override
                                    public void onCancel(DialogInterface dialog) {
                                        getActivity().setResult(Activity.RESULT_FIRST_USER);
                                        getActivity().finish();
                                    }// onCancel()
                                });
                    }
                    else
                    {
                        Dlg.toast(getActivity(), errMsg, Dlg.LENGTH_SHORT);
                        getActivity().finish();
                    }

                } else
                    showCannotConnectToServiceAndWaitForTheUserToFinish("loadInitialPath");
            }// onPostExecute()

        }.execute();
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
    private void showCannotConnectToServiceAndWaitForTheUserToFinish(String info) {
        Dlg.showError(getActivity(),
                getActivity().getString(R.string.afc_msg_cannot_connect_to_file_provider_service) + " " + info,
                new DialogInterface.OnCancelListener() {

                    @Override
                    public void onCancel(DialogInterface dialog) {
                        getActivity().setResult(Activity.RESULT_FIRST_USER);
                        getActivity().finish();
                    }// onCancel()
                });
    }// showCannotConnectToServiceAndWaitForTheUserToFinish()

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

    private void goHome() {
        goTo(mRoot);
    }// goHome()


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
    private void resortViewFiles() {
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
    }// resortViewFiles()

    /**
     * Switch view type between {@link ViewType#LIST} and {@link ViewType#GRID}
     */
    private void switchViewType() {
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
    }// switchViewType()

    /**
     * Checks current conditions to see if we can create new directory. Then
     * confirms user to do so.
     */
    private void checkConditionsThenConfirmUserToCreateNewDir() {
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

        new LoadingDialog<Void, Void, Boolean>(getActivity(), false) {

            @Override
            protected Boolean doInBackground(Void... params) {
                return getCurrentLocation() != null
                        && BaseFileProviderUtils.fileCanWrite(getActivity(),
                                getCurrentLocation());
            }// doInBackground()

            @Override
            protected void onPostExecute(Boolean result) {
                super.onPostExecute(result);

                if (result)
                    showNewDirectoryCreationDialog();
                else
                    Dlg.toast(getActivity(),
                            R.string.afc_msg_cannot_create_new_folder_here,
                            Dlg.LENGTH_SHORT);
            }// onProgressUpdate()

        }.execute();
    }// checkConditionsThenConfirmUserToCreateNewDir()

    /**
     * Confirms user to create new directory.
     */
    private void showNewDirectoryCreationDialog() {
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
                        final String name = textFile.getText().toString()
                                .trim();
                        if (!FileUtils.isFilenameValid(name)) {
                            Dlg.toast(
                                    getActivity(),
                                    getString(
                                            R.string.afc_pmsg_filename_is_invalid,
                                            name), Dlg.LENGTH_SHORT);
                            return;
                        }

                        new LoadingDialog<Void, Void, Uri>(getActivity(), false) {

                            @Override
                            protected Uri doInBackground(Void... params) {
                                return getActivity()
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
                                                        BaseFile.PARAM_NAME,
                                                        name)
                                                .appendQueryParameter(
                                                        BaseFile.PARAM_FILE_TYPE,
                                                        Integer.toString(BaseFile.FILE_TYPE_DIRECTORY))
                                                .build(), null);
                            }// doInBackground()

                            @Override
                            protected void onPostExecute(Uri result) {
                                super.onPostExecute(result);

                                if (result != null) {
                                    Dlg.toast(getActivity(),
                                            getString(R.string.afc_msg_done),
                                            Dlg.LENGTH_SHORT);
                                } else
                                    Dlg.toast(
                                            getActivity(),
                                            getString(
                                                    R.string.afc_pmsg_cannot_create_folder,
                                                    name), Dlg.LENGTH_SHORT);
                            }// onPostExecute()

                        }.execute();
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
                /*
                 * Do nothing.
                 */
            }// onTextChanged()

            @Override
            public void beforeTextChanged(CharSequence s, int start, int count,
                    int after) {
                /*
                 * Do nothing.
                 */
            }// beforeTextChanged()

            @Override
            public void afterTextChanged(Editable s) {
                buttonOk.setEnabled(FileUtils.isFilenameValid(s.toString()
                        .trim()));
            }// afterTextChanged()
        });
    }// showNewDirectoryCreationDialog()

    /**
     * Deletes a file.
     * 
     * @param position
     *            the position of item to be delete.
     */
    private void deleteFile(final int position) {
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
                        new LoadingDialog<Void, Void, Boolean>(
                                getActivity(),
                                getString(
                                        R.string.afc_pmsg_deleting_file,
                                        isFile ? getString(R.string.afc_file)
                                                : getString(R.string.afc_folder),
                                        filename), true) {

                            final int taskId = EnvUtils.genId();

                            private void notifyFileDeleted() {
                                Dlg.toast(
                                        getActivity(),
                                        getString(
                                                R.string.afc_pmsg_file_has_been_deleted,
                                                isFile ? getString(R.string.afc_file)
                                                        : getString(R.string.afc_folder),
                                                filename), Dlg.LENGTH_SHORT);
                            }// notifyFileDeleted()

                            @Override
                            protected Boolean doInBackground(Void... params) {
                                getActivity()
                                        .getContentResolver()
                                        .delete(uri
                                                .buildUpon()
                                                .appendQueryParameter(
                                                        BaseFile.PARAM_TASK_ID,
                                                        Integer.toString(taskId))
                                                .build(), null, null);

                                return !BaseFileProviderUtils.fileExists(
                                        getActivity(), uri);
                            }// doInBackground()

                            @Override
                            protected void onCancelled() {
                                if (getCurrentLocation() != null)
                                    BaseFileProviderUtils.cancelTask(
                                            getActivity(), getCurrentLocation()
                                                    .getAuthority(), taskId);

                                new LoadingDialog<Void, Void, Boolean>(
                                        getActivity(), false) {

                                    @Override
                                    protected Boolean doInBackground(
                                            Void... params) {
                                        return BaseFileProviderUtils
                                                .fileExists(getActivity(), uri);
                                    }// doInBackground()

                                    @Override
                                    protected void onPostExecute(Boolean result) {
                                        super.onPostExecute(result);

                                        if (result) {
                                            mFileAdapter.markItemAsDeleted(id,
                                                    false);
                                            Dlg.toast(getActivity(),
                                                    R.string.afc_msg_cancelled,
                                                    Dlg.LENGTH_SHORT);
                                        } else
                                            notifyFileDeleted();
                                    }// onPostExecute()

                                }.execute();

                                super.onCancelled();
                            }// onCancelled()

                            @Override
                            protected void onPostExecute(Boolean result) {
                                super.onPostExecute(result);

                                if (result) {
                                    notifyFileDeleted();
                                } else {
                                    mFileAdapter.markItemAsDeleted(id, false);
                                    Dlg.toast(
                                            getActivity(),
                                            getString(
                                                    R.string.afc_pmsg_cannot_delete_file,
                                                    isFile ? getString(R.string.afc_file)
                                                            : getString(R.string.afc_folder),
                                                    filename), Dlg.LENGTH_SHORT);
                                }
                            }// onPostExecute()

                        }.execute();// LoadingDialog
                    }// onClick()
                }, new DialogInterface.OnCancelListener() {

                    @Override
                    public void onCancel(DialogInterface dialog) {
                        mFileAdapter.markItemAsDeleted(id, false);
                    }// onCancel()
                });
    }// deleteFile()

    /**
     * As the name means.
     */
    private void checkSaveasFilenameAndFinish() {
        new LoadingDialog<Void, String, Uri>(getActivity(), false) {

            String filename;
            Uri fileUri;
            int fileType;

            @Override
            protected void onPreExecute() {
                super.onPreExecute();

                /*
                 * If the user tapped a file, its URI was stored here. If not,
                 * this is null.
                 */
                fileUri = (Uri) mTextSaveas.getTag();

                /*
                 * File name and extension.
                 */
                filename = mTextSaveas.getText().toString().trim();
                if (fileUri == null
                        && getArguments().containsKey(
                                FileChooserActivity.EXTRA_DEFAULT_FILE_EXT)) {
                    if (!TextUtils.isEmpty(filename)) {
                        String ext = getArguments().getString(
                                FileChooserActivity.EXTRA_DEFAULT_FILE_EXT);
                        if (!filename.matches("(?si)^.+"
                                + Pattern.quote(Texts.C_PERIOD + ext) + "$")) {
                            filename += Texts.C_PERIOD + ext;
                            mTextSaveas.setText(filename);
                        }
                    }
                }
            }// onPreExecute()

            @Override
            protected Uri doInBackground(Void... params) {
                if (!BaseFileProviderUtils.fileCanWrite(getActivity(),
                        getCurrentLocation())) {
                    publishProgress(getString(R.string.afc_msg_cannot_save_a_file_here));
                    return null;
                }

                if (fileUri == null && !FileUtils.isFilenameValid(filename)) {
                    publishProgress(getString(
                            R.string.afc_pmsg_filename_is_invalid, filename));
                    return null;
                }

                if (fileUri == null)
                    fileUri = getCurrentLocation()
                            .buildUpon()
                            .appendQueryParameter(BaseFile.PARAM_APPEND_NAME,
                                    filename).build();
                final Cursor cursor = getActivity().getContentResolver().query(
                        fileUri, null, null, null, null);
                try {
                    if (cursor == null || !cursor.moveToFirst())
                        return null;

                    fileType = cursor.getInt(cursor
                            .getColumnIndex(BaseFile.COLUMN_TYPE));
                    return BaseFileProviderUtils.getUri(cursor);
                } finally {
                    if (cursor != null)
                        cursor.close();
                }
            }// doInBackground()

            @Override
            protected void onProgressUpdate(String... progress) {
                Dlg.toast(getActivity(), progress[0], Dlg.LENGTH_SHORT);
            }// onProgressUpdate()

            @Override
            protected void onPostExecute(final Uri result) {
                super.onPostExecute(result);

                if (result == null) {
                    /*
                     * TODO ?
                     */
                    return;
                }

                switch (fileType) {
                case BaseFile.FILE_TYPE_DIRECTORY: {
                    Dlg.toast(
                            getActivity(),
                            getString(R.string.afc_pmsg_filename_is_directory,
                                    filename), Dlg.LENGTH_SHORT);
                    break;
                }// FILE_TYPE_DIRECTORY

                case BaseFile.FILE_TYPE_FILE: {
                    Dlg.confirmYesno(
                            getActivity(),
                            getString(R.string.afc_pmsg_confirm_replace_file,
                                    filename),
                            new DialogInterface.OnClickListener() {

                                @Override
                                public void onClick(DialogInterface dialog,
                                        int which) {
                                    finish(result, true);
                                }// onClick()
                            });

                    break;
                }// FILE_TYPE_FILE

                case BaseFile.FILE_TYPE_NOT_EXISTED: {
                    finish(result, false);
                    break;
                }// FILE_TYPE_NOT_EXISTED
                }
            }// onPostExecute()

        }.execute();
    }// checkSaveasFilenameAndFinish()

    /**
     * Goes to a specified location.
     * 
     * @param dir
     *            a directory, of course.
     * @since v4.3 beta
     */
    private void goTo(final Uri dir) {
        new LoadingDialog<Uri, String, Bundle>(getActivity(), false) {

            /**
             * In onPostExecute(), if result is null then check this value. If
             * this is not null, show a toast. If this is null, call
             * showCannotConnectToServiceAndWaitForTheUserToFinish().
             */
            String errMsg = null;

            @Override
            protected Bundle doInBackground(Uri... params) {
                if (params[0] == null)
                    params[0] = BaseFileProviderUtils.getDefaultPath(
                            getActivity(), mFileProviderAuthority);
                if (params[0] == null)
                    return null;

                /*
                 * Check if the path of `params[0]` is same as current location,
                 * then set `params[0]` to current location. This avoids of
                 * pushing two same paths into history, because we compare the
                 * pointers (not the paths) when pushing it to history.
                 */
                if (params[0].equals(getCurrentLocation()))
                    params[0] = getCurrentLocation();

                if (BaseFileProviderUtils.fileCanRead(getActivity(), params[0])) {
                    /*
                     * Cancel previous loader if there is one.
                     */
                    cancelPreviousLoader();

                    Bundle bundle = new Bundle();
                    bundle.putParcelable(PATH, params[0]);
                    return bundle;
                }// if

                errMsg = getString(R.string.afc_pmsg_cannot_access_dir,
                        BaseFileProviderUtils.getFileName(getActivity(),
                                params[0]));

                return null;
            }// doInBackground()

            @Override
            protected void onPostExecute(Bundle result) {
                super.onPostExecute(result);

                if (result != null) {
                    setCurrentLocation((Uri) result.getParcelable(PATH));
                    getLoaderManager().restartLoader(mIdLoaderData, result,
                            FragmentFiles.this);
                } else if (errMsg != null) {

                    Dlg.toast(getActivity(), errMsg, Dlg.LENGTH_SHORT);
                }
                else
                    showCannotConnectToServiceAndWaitForTheUserToFinish("goTo: " + dir.toString());
            }// onPostExecute()

        }.execute(dir);
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
    private void buildAddressBar(final Uri path) {
        if (path == null)
            return;

        mViewAddressBar.removeAllViews();

        new LoadingDialog<Void, Cursor, Void>(getActivity(), false) {

            LinearLayout.LayoutParams lpBtnLoc;
            LinearLayout.LayoutParams lpDivider;
            LayoutInflater inflater = getLayoutInflater(null);
            final int dim = getResources().getDimensionPixelSize(
                    R.dimen.afc_5dp);
            int count = 0;

            @Override
            protected void onPreExecute() {
                super.onPreExecute();

                lpBtnLoc = new LinearLayout.LayoutParams(
                        LinearLayout.LayoutParams.WRAP_CONTENT,
                        LinearLayout.LayoutParams.WRAP_CONTENT);
                lpBtnLoc.gravity = Gravity.CENTER;
            }// onPreExecute()

            @Override
            protected Void doInBackground(Void... params) {
                Cursor cursor = getActivity().getContentResolver().query(path,
                        null, null, null, null);
                while (cursor != null) {
                    if (cursor.moveToFirst()) {
                        publishProgress(cursor);
                        cursor.close();
                    } else
                        break;

                    /*
                     * Process the parent directory.
                     */
                    Uri uri = Uri.parse(cursor.getString(cursor
                            .getColumnIndex(BaseFile.COLUMN_URI)));
                    cursor = getActivity().getContentResolver().query(
                            BaseFile.genContentUriApi(uri.getAuthority())
                                    .buildUpon()
                                    .appendPath(BaseFile.CMD_GET_PARENT)
                                    .appendQueryParameter(
                                            BaseFile.PARAM_SOURCE,
                                            uri.getLastPathSegment()).build(),
                            null, null, null, null);
                }// while

                return null;
            }// doInBackground()

            @Override
            protected void onProgressUpdate(Cursor... progress) {
                /*
                 * Add divider.
                 */
                if (mViewAddressBar.getChildCount() > 0) {
                    View divider = inflater.inflate(
                            R.layout.afc_view_locations_divider, null);

                    if (lpDivider == null) {
                        lpDivider = new LinearLayout.LayoutParams(dim, dim);
                        lpDivider.gravity = Gravity.CENTER;
                        lpDivider.setMargins(dim, dim, dim, dim);
                    }
                    mViewAddressBar.addView(divider, 0, lpDivider);
                }

                Uri lastUri = Uri.parse(progress[0].getString(progress[0]
                        .getColumnIndex(BaseFile.COLUMN_URI)));

                TextView btnLoc = (TextView) inflater.inflate(
                        R.layout.afc_button_location, null);
                String name = BaseFileProviderUtils.getFileName(progress[0]);
                btnLoc.setText(TextUtils.isEmpty(name) ? getString(R.string.afc_root)
                        : name);
                btnLoc.setTag(lastUri);
                btnLoc.setOnClickListener(mBtnLocationOnClickListener);
                btnLoc.setOnLongClickListener(mBtnLocationOnLongClickListener);
                mViewAddressBar.addView(btnLoc, 0, lpBtnLoc);

                if (count++ == 0) {
                    Rect r = new Rect();
                    btnLoc.getPaint().getTextBounds(name, 0, name.length(), r);
                    if (r.width() >= getResources().getDimensionPixelSize(
                            R.dimen.afc_button_location_max_width)
                            - btnLoc.getPaddingLeft()
                            - btnLoc.getPaddingRight()) {
                        mTextFullDirName.setText(progress[0]
                                .getString(progress[0]
                                        .getColumnIndex(BaseFile.COLUMN_NAME)));
                        mTextFullDirName.setVisibility(View.VISIBLE);
                    } else
                        mTextFullDirName.setVisibility(View.GONE);
                }// if
            }// onProgressUpdate()

            @Override
            protected void onPostExecute(Void result) {
                super.onPostExecute(result);

                /*
                 * Sometimes without delay time, it doesn't work...
                 */
                mViewLocationsContainer.postDelayed(new Runnable() {

                    public void run() {
                        mViewLocationsContainer
                                .fullScroll(HorizontalScrollView.FOCUS_RIGHT);
                    }// run()
                }, DisplayPrefs.DELAY_TIME_FOR_VERY_SHORT_ANIMATION);
            }// onPostExecute()

        }.execute();
    }// buildAddressBar()
    
    /**
     * Finishes this activity when save-as.
     * 
     * @param file
     *            @link Uri.
     */
    private void finish(Uri file, boolean fileExists) {
        ArrayList<Uri> list = new ArrayList<Uri>();
        list.add(file);
        Intent intent = new Intent();
        intent.setData(file);
        intent.putParcelableArrayListExtra(FileChooserActivity.EXTRA_RESULTS,
                list);
        intent.putExtra(FileChooserActivity.EXTRA_RESULT_FILE_EXISTS,
                fileExists);
        getActivity().setResult(FileChooserActivity.RESULT_OK, intent);

        getActivity().finish();
    }// finish()

    /**
     * Finishes this activity.
     * 
     * @param files
     *            list of {@link Uri}.
     */
    private void finish(Uri... files) {
        List<Uri> list = new ArrayList<Uri>();
        for (Uri uri : files)
            list.add(uri);
        finish((ArrayList<Uri>) list);
    }// finish()

    /**
     * Finishes this activity.
     * 
     * @param files
     *            list of {@link Uri}.
     */
    private void finish(ArrayList<Uri> files) {
        if (files == null || files.isEmpty()) {
            getActivity().setResult(Activity.RESULT_CANCELED);
            getActivity().finish();
            return;
        }

        Intent intent = new Intent();
        if (files.size() == 1)
        {
        	intent.setData(files.get(0));
        }
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
    }// finish()

    /*
     * =========================================================================
     * BUTTON LISTENERS
     * =========================================================================
     */

    private final View.OnClickListener mBtnGoHomeOnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            goHome();
        }// onClick()
    };// mBtnGoHomeOnClickListener



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

            finish((Uri) v.getTag());

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

    
    private final View.OnClickListener mBtnOk_SaveDialog_OnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            Ui.showSoftKeyboard(v, false);
            checkSaveasFilenameAndFinish();
        }// onClick()
    };// mBtnOk_SaveDialog_OnClickListener

    private final View.OnClickListener mBtnOk_OpenDialog_OnClickListener = new View.OnClickListener() {

        @Override
        public void onClick(View v) {
            finish(mFileAdapter.getSelectedItems());
        }// onClick()
    };// mBtnOk_OpenDialog_OnClickListener

    /*
     * FRAGMENT LISTENERS
     */

    
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

            if (mIsSaveDialog) {
                mTextSaveas.setText(BaseFileProviderUtils.getFileName(cursor));
                /*
                 * Always set tag after setting text, or tag will be reset to
                 * null.
                 */
                mTextSaveas.setTag(BaseFileProviderUtils.getUri(cursor));
            }

            if (mDoubleTapToChooseFiles) {
                /*
                 * Do nothing.
                 */
                return;
            }// double tap to choose files
            else {
                if (mIsMultiSelection)
                    return;

                if (mIsSaveDialog)
                    checkSaveasFilenameAndFinish();
                else
                    finish(BaseFileProviderUtils.getUri(cursor));
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
                    finish(BaseFileProviderUtils.getUri(cursor));
                }
            }// single tap to choose files

            /*
             * Notify that we already handled long click here.
             */
            return true;
        }// onItemLongClick()
    };// mViewFilesOnItemLongClickListener



    /**
     * We use a {@link LoadingDialog} to avoid of
     * {@code NetworkOnMainThreadException}.
     */
    private LoadingDialog<Void, Void, Integer> mFileSelector;

    /**
     * Creates new {@link #mFileSelector} to select appropriate file after
     * loading a folder's content. It's either the parent path of last path, or
     * the file provided by key {@link FileChooserActivity#EXTRA_SELECT_FILE}.
     * Note that this also cancels previous selector if there is such one.
     */
    private void createFileSelector() {
        if (mFileSelector != null)
            mFileSelector.cancel(true);

        mFileSelector = new LoadingDialog<Void, Void, Integer>(getActivity(),
                true) {

            @Override
            protected Integer doInBackground(Void... params) {
                final Cursor cursor = mFileAdapter.getCursor();
                if (cursor == null || cursor.isClosed())
                    return -1;

                final Uri selectedFile = (Uri) getArguments().getParcelable(
                        FileChooserActivity.EXTRA_SELECT_FILE);
                final int colUri = cursor.getColumnIndex(BaseFile.COLUMN_URI);
                if (selectedFile != null)
                    getArguments()
                            .remove(FileChooserActivity.EXTRA_SELECT_FILE);

                int shouldBeSelectedIdx = -1;
                final Uri uri = selectedFile != null ? selectedFile
                        : getLastLocation();
                if (uri == null
                        || !BaseFileProviderUtils
                                .fileExists(getActivity(), uri))
                    return -1;

                final String fileName = BaseFileProviderUtils.getFileName(
                        getActivity(), uri);
                if (fileName == null)
                    return -1;

                Uri parentUri = BaseFileProviderUtils.getParentFile(
                        getActivity(), uri);
                if ((uri == getLastLocation()
                        && !getCurrentLocation().equals(getLastLocation()) && BaseFileProviderUtils
                            .isAncestorOf(getActivity(), getCurrentLocation(),
                                    uri))
                        || getCurrentLocation().equals(parentUri)) {
                    if (cursor.moveToFirst()) {
                        while (!cursor.isLast()) {
                            if (isCancelled())
                                return -1;

                            Uri subUri = Uri.parse(cursor.getString(colUri));
                            if (uri == getLastLocation()) {
                                if (cursor.getInt(cursor
                                        .getColumnIndex(BaseFile.COLUMN_TYPE)) == BaseFile.FILE_TYPE_DIRECTORY) {
                                    if (subUri.equals(uri)
                                            || BaseFileProviderUtils
                                                    .isAncestorOf(
                                                            getActivity(),
                                                            subUri, uri)) {
                                        shouldBeSelectedIdx = Math.max(0,
                                                cursor.getPosition() - 2);
                                        break;
                                    }
                                }
                            } else {
                                if (uri.equals(subUri)) {
                                    shouldBeSelectedIdx = Math.max(0,
                                            cursor.getPosition() - 2);
                                    break;
                                }
                            }

                            cursor.moveToNext();
                        }// while
                    }// if
                }// if

                return shouldBeSelectedIdx;
            }// doInBackground()

            @Override
            protected void onPostExecute(final Integer result) {
                super.onPostExecute(result);

                if (isCancelled() || mFileAdapter.isEmpty())
                    return;

                /*
                 * Use a Runnable to make sure this works. Because if the list
                 * view is handling data, this might not work.
                 * 
                 * Also sometimes it doesn't work without a delay.
                 */
                mViewFiles.postDelayed(new Runnable() {

                    @Override
                    public void run() {
                        if (result >= 0 && result < mFileAdapter.getCount())
                            mViewFiles.setSelection(result);
                        else if (!mFileAdapter.isEmpty())
                            mViewFiles.setSelection(0);
                    }// run()
                }, DisplayPrefs.DELAY_TIME_FOR_VERY_SHORT_ANIMATION);
            }// onPostExecute()

        };

        mFileSelector.execute();
    }// createFileSelector()

}
