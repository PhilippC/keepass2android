/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser;

import group.pals.android.lib.ui.filechooser.prefs.DisplayPrefs;
import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileContract.BaseFile;
import group.pals.android.lib.ui.filechooser.providers.localfile.LocalFileContract;
import group.pals.android.lib.ui.filechooser.utils.Utils;
import group.pals.android.lib.ui.filechooser.utils.ui.Dlg;
import group.pals.android.lib.ui.filechooser.utils.ui.Ui;

import java.util.ArrayList;

import android.content.Context;
import android.content.res.Configuration;
import android.net.Uri;
import android.os.Bundle;
import android.support.v4.app.FragmentActivity;
import android.util.Log;
import android.view.KeyEvent;
import android.widget.GridView;
import android.widget.ListView;

/**
 * Main activity for this library.
 * <p/>
 * <h1>Notes:</h1>
 * <p/>
 * <ol>
 * <li>About keys {@link FileChooserActivity#EXTRA_ROOTPATH},
 * {@link FileChooserActivity#EXTRA_SELECT_FILE} and preference
 * {@link DisplayPrefs#isRememberLastLocation(Context)}, the priorities of them
 * are:
 * <ol>
 * <li>{@link FileChooserActivity#EXTRA_SELECT_FILE}</li>
 * <li>{@link FileChooserActivity#EXTRA_ROOTPATH}</li>
 * <li>{@link DisplayPrefs#isRememberLastLocation(Context)}</li>
 * </ol>
 * </li>
 * </ol>
 * 
 * @author Hai Bison
 */
public class FileChooserActivity extends FragmentActivity {

    /**
     * The full name of this class. Generally used for debugging.
     */
    private static final String CLASSNAME = FileChooserActivity.class.getName();

    /**
     * Types of view.
     * 
     * @author Hai Bison
     * @since v4.0 beta
     */
    public static enum ViewType {
        /**
         * Use {@link ListView} to display file list.
         */
        LIST,
        /**
         * Use {@link GridView} to display file list.
         */
        GRID
    }// ViewType

    /*---------------------------------------------
     * KEYS
     */

    /**
     * Sets value of this key to a theme which is one of {@code Afc_Theme_*}.
     * 
     * @since v4.3 beta
     */
    public static final String EXTRA_THEME = CLASSNAME + ".theme";

    /**
     * Key to hold the root path.
     * <p/>
     * If {@link LocalFileProvider} is used, then default is SD card, if SD card
     * is not available, {@code "/"} will be used.
     * <p/>
     * <b>Note</b>: The value of this key is a file provider's {@link Uri}. For
     * example with {@link LocalFileProvider}, you can use this command:
     * 
     * <pre>
     * <code>...
     *  intent.putExtra(FileChooserActivity.EXTRA_ROOTPATH,
     *          BaseFile.genContentIdUriBase(LocalFileContract.getAuthority())
     *          .buildUpon().appendPath("/sdcard").build())
     * </code>
     * </pre>
     */
    public static final String EXTRA_ROOTPATH = CLASSNAME + ".rootpath";

    /**
     * Key to hold the authority of file provider.
     * <p/>
     * Default is {@link LocalFileContract#getAuthority(Context)}.
     */
    public static final String EXTRA_FILE_PROVIDER_AUTHORITY = CLASSNAME
            + ".file_provider_authority";

    // ---------------------------------------------------------

    /**
     * Key to hold filter mode, can be one of
     * {@link BaseFile#FILTER_DIRECTORIES_ONLY},
     * {@link BaseFile#FILTER_FILES_AND_DIRECTORIES},
     * {@link BaseFile#FILTER_FILES_ONLY}.
     * <p/>
     * Default is {@link BaseFile#FILTER_FILES_ONLY}.
     */
    public static final String EXTRA_FILTER_MODE = CLASSNAME + ".filter_mode";

    // flags

    // ---------------------------------------------------------

    /**
     * Key to hold max file count that's allowed to be listed, default =
     * {@code 1000}.
     */
    public static final String EXTRA_MAX_FILE_COUNT = CLASSNAME
            + ".max_file_count";
    /**
     * Key to hold multi-selection mode, default = {@code false}.
     */
    public static final String EXTRA_MULTI_SELECTION = CLASSNAME
            + ".multi_selection";
    /**
     * Key to hold the positive regex to filter files (<b><i>not</i></b>
     * directories), default is {@code null}.
     * 
     * @since v5.1 beta
     */
    public static final String EXTRA_POSITIVE_REGEX_FILTER = CLASSNAME
            + ".positive_regex_filter";
    /**
     * Key to hold the negative regex to filter files (<b><i>not</i></b>
     * directories), default is {@code null}.
     * 
     * @since v5.1 beta
     */
    public static final String EXTRA_NEGATIVE_REGEX_FILTER = CLASSNAME
            + ".negative_regex_filter";
    /**
     * Key to hold display-hidden-files, default = {@code false}.
     */
    public static final String EXTRA_DISPLAY_HIDDEN_FILES = CLASSNAME
            + ".display_hidden_files";
    /**
     * Sets this to {@code true} to enable double tapping to choose files/
     * directories. In older versions, double tapping is default. However, since
     * v4.7 beta, single tapping is default. So if you want to keep the old way,
     * please set this key to {@code true}.
     * 
     * @since v4.7 beta
     */
    public static final String EXTRA_DOUBLE_TAP_TO_CHOOSE_FILES = CLASSNAME
            + ".double_tap_to_choose_files";
    /**
     * Sets the file you want to select when starting this activity. This is a
     * file provider's {@link Uri}. For example with {@link LocalFileProvider},
     * you can use this command:
     * <p/>
     * 
     * <pre>
     * <code>...
     *   intent.putExtra(FileChooserActivity.EXTRA_SELECT_FILE,
     *           BaseFile.genContentIdUriBase(LocalFileContract.getAuthority())
     *           .buildUpon().appendPath("/sdcard").build())
     * </code>
     * </pre>
     * <p/>
     * <b>Notes:</b>
     * <ul>
     * <li>Currently this key is only used for single selection mode.</li>
     * <li>If you use save dialog mode, this key will override key
     * {@link #EXTRA_DEFAULT_FILENAME}.</li>
     * </ul>
     * 
     * @since v4.7 beta
     */
    public static final String EXTRA_SELECT_FILE = CLASSNAME + ".select_file";

    // ---------------------------------------------------------

    /**
     * Key to hold property save-dialog, default = {@code false}.
     */
    public static final String EXTRA_SAVE_DIALOG = CLASSNAME + ".save_dialog";
    /**
     * Key to hold default filename, default = {@code null}.
     */
    public static final String EXTRA_DEFAULT_FILENAME = CLASSNAME
            + ".default_filename";
    /**
     * Key to hold default file extension (<b>without</b> the period prefix),
     * default = {@code null}.
     * <p/>
     * Note that this will be compared to the user's input value as
     * case-insensitive. For example if you provide "csv" and the user types
     * "CSV" then it is OK to use "CSV".
     */
    public static final String EXTRA_DEFAULT_FILE_EXT = CLASSNAME
            + ".default_file_ext";

    /**
     * Key to hold results, which is an {@link ArrayList} of {@link Uri}. It can
     * be one or multiple files.
     */
    public static final String EXTRA_RESULTS = CLASSNAME + ".results";
    
    public static final String EXTRA_RESULT_FILE_EXISTS = CLASSNAME + ".result_file_exists";
    
    

    /*
     * CONTROLS
     */

    FragmentFiles mFragmentFiles;

    /**
     * Called when the activity is first created.
     */
    @Override
    public void onCreate(Bundle savedInstanceState) {
        /*
         * EXTRA_THEME
         */

        if (getIntent().hasExtra(EXTRA_THEME))
            setTheme(getIntent().getIntExtra(EXTRA_THEME,
                    R.style.Afc_Theme_Dark));

        super.onCreate(savedInstanceState);
        setContentView(R.layout.afc_activity_filechooser);
        Ui.adjustDialogSizeForLargeScreen(getWindow());

        /*
         * Make sure RESULT_CANCELED is default.
         */
        setResult(RESULT_CANCELED);

        mFragmentFiles = FragmentFiles.newInstance(getIntent());
        getSupportFragmentManager().beginTransaction()
                .add(R.id.afc_fragment_files, mFragmentFiles).commit();
    }// onCreate()

    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        super.onConfigurationChanged(newConfig);
        Ui.adjustDialogSizeForLargeScreen(getWindow());
    }// onConfigurationChanged()

    @Override
    public boolean onKeyDown(int keyCode, KeyEvent event) {
        if (Utils.doLog())
            Log.d(CLASSNAME, String.format("onKeyDown() >> %,d", keyCode));

        if (keyCode == KeyEvent.KEYCODE_BACK) {
            /*
             * Use this hook instead of onBackPressed(), because onBackPressed()
             * is not available in API 4.
             */
            if (mFragmentFiles.isLoading()) {
                if (Utils.doLog())
                    Log.d(CLASSNAME,
                            "onKeyDown() >> KEYCODE_BACK >> cancelling previous query...");
                mFragmentFiles.cancelPreviousLoader();
                Dlg.toast(this, R.string.afc_msg_cancelled, Dlg.LENGTH_SHORT);
                return true;
            }
        }

        return super.onKeyDown(keyCode, event);
    }// onKeyDown()

}
