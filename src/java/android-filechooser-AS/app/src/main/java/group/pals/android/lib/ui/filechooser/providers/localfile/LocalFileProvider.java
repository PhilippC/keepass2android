/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers.localfile;

import group.pals.android.lib.ui.filechooser.BuildConfig;
import group.pals.android.lib.ui.filechooser.R;
import group.pals.android.lib.ui.filechooser.providers.BaseFileProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.ProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileContract.BaseFile;
import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileProvider;
import group.pals.android.lib.ui.filechooser.utils.FileUtils;
import group.pals.android.lib.ui.filechooser.utils.TextUtils;
import group.pals.android.lib.ui.filechooser.utils.Texts;
import group.pals.android.lib.ui.filechooser.utils.Utils;
import java.io.File;
import java.io.FileFilter;
import java.io.IOException;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.concurrent.CancellationException;
import java.util.regex.Pattern;

import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.database.MatrixCursor.RowBuilder;
import android.net.Uri;
import android.os.Environment;
import android.util.Log;

/**
 * Local file provider.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class LocalFileProvider extends BaseFileProvider {

    /**
     * Used for debugging or something...
     */
    private static final String CLASSNAME = LocalFileProvider.class.getName();

    private FileObserverEx mFileObserverEx;

    @Override
    public boolean onCreate() {
        BaseFileProviderUtils.registerProviderInfo(LocalFileContract._ID,
                LocalFileContract.getAuthority(getContext()));

        URI_MATCHER.addURI(LocalFileContract.getAuthority(getContext()),
                BaseFile.PATH_DIR + "/*", URI_DIRECTORY);
        URI_MATCHER.addURI(LocalFileContract.getAuthority(getContext()),
                BaseFile.PATH_FILE + "/*", URI_FILE);
        URI_MATCHER.addURI(LocalFileContract.getAuthority(getContext()),
                BaseFile.PATH_API, URI_API);
        URI_MATCHER.addURI(LocalFileContract.getAuthority(getContext()),
                BaseFile.PATH_API + "/*", URI_API_COMMAND);

        return true;
    }// onCreate()

    @Override
    public int delete(Uri uri, String selection, String[] selectionArgs) {
        if (Utils.doLog())
            Log.d(CLASSNAME, "delete() >> " + uri);

        int count = 0;

        switch (URI_MATCHER.match(uri)) {
        case URI_FILE: {
            int taskId = ProviderUtils.getIntQueryParam(uri,
                    BaseFile.PARAM_TASK_ID, 0);

            boolean isRecursive = ProviderUtils.getBooleanQueryParam(uri,
                    BaseFile.PARAM_RECURSIVE, true);
            File file = extractFile(uri);
            if (file.canWrite()) {
                File parentFile = file.getParentFile();

                if (file.isFile() || !isRecursive) {
                    if (file.delete())
                        count = 1;
                } else {
                    mMapInterruption.put(taskId, false);
                    count = deleteFile(taskId, file, isRecursive);
                    if (mMapInterruption.get(taskId))
                        if (Utils.doLog())
                            Log.d(CLASSNAME, "delete() >> cancelled...");
                    mMapInterruption.delete(taskId);
                }

                if (count > 0) {
                    getContext()
                            .getContentResolver()
                            .notifyChange(
                                    BaseFile.genContentUriBase(
                                            LocalFileContract
                                                    .getAuthority(getContext()))
                                            .buildUpon()
                                            .appendPath(
                                                    Uri.fromFile(parentFile)
                                                            .toString())
                                            .build(), null);
                }
            }

            break;// URI_FILE
        }

        default:
            throw new IllegalArgumentException("UNKNOWN URI " + uri);
        }

        if (Utils.doLog())
            Log.d(CLASSNAME, "delete() >> count = " + count);

        if (count > 0)
            getContext().getContentResolver().notifyChange(uri, null);

        return count;
    }// delete()

    @Override
    public Uri insert(Uri uri, ContentValues values) {
        if (Utils.doLog())
            Log.d(CLASSNAME, "insert() >> " + uri);

        switch (URI_MATCHER.match(uri)) {
        case URI_DIRECTORY:
            File file = extractFile(uri);
            if (!file.isDirectory() || !file.canWrite())
                return null;

            File newFile = new File(String.format("%s/%s",
                    file.getAbsolutePath(),
                    uri.getQueryParameter(BaseFile.PARAM_NAME)));

            switch (ProviderUtils.getIntQueryParam(uri,
                    BaseFile.PARAM_FILE_TYPE, BaseFile.FILE_TYPE_DIRECTORY)) {
            case BaseFile.FILE_TYPE_DIRECTORY:
                newFile.mkdir();
                break;// FILE_TYPE_DIRECTORY

            case BaseFile.FILE_TYPE_FILE:
                try {
                    newFile.createNewFile();
                } catch (IOException e) {
                    e.printStackTrace();
                }
                break;// FILE_TYPE_FILE

            default:
                return null;
            }

            if (newFile.exists()) {
                Uri newUri = BaseFile
                        .genContentIdUriBase(
                                LocalFileContract.getAuthority(getContext()))
                        .buildUpon()
                        .appendPath(Uri.fromFile(newFile).toString()).build();
                getContext().getContentResolver().notifyChange(uri, null);
                return newUri;
            }
            return null;// URI_FILE

        default:
            throw new IllegalArgumentException("UNKNOWN URI " + uri);
        }
    }// insert()

    @Override
    public Cursor query(Uri uri, String[] projection, String selection,
            String[] selectionArgs, String sortOrder) {
        if (Utils.doLog())
            Log.d(CLASSNAME, String.format(
                    "query() >> uri = %s (%s) >> match = %s", uri,
                    uri.getLastPathSegment(), URI_MATCHER.match(uri)));

        switch (URI_MATCHER.match(uri)) {
        case URI_API: {
            /*
             * If there is no command given, return provider ID and name.
             */
            MatrixCursor matrixCursor = new MatrixCursor(new String[] {
                    BaseFile.COLUMN_PROVIDER_ID, BaseFile.COLUMN_PROVIDER_NAME,
                    BaseFile.COLUMN_PROVIDER_ICON_ATTR });
            matrixCursor.newRow().add(LocalFileContract._ID)
                    .add(getContext().getString(R.string.afc_phone))
                    .add(R.attr.afc_badge_file_provider_localfile);
            return matrixCursor;
        }
        case URI_API_COMMAND: {
            return doAnswerApiCommand(uri);
        }// URI_API

        case URI_DIRECTORY: {
            return doListFiles(uri);
        }// URI_DIRECTORY

        case URI_FILE: {
            return doRetrieveFileInfo(uri);
        }// URI_FILE

        default:
            throw new IllegalArgumentException("UNKNOWN URI " + uri);
        }
    }// query()

    /*
     * UTILITIES
     */

    /**
     * Answers the incoming URI.
     * 
     * @param uri
     *            the request URI.
     * @return the response.
     */
    private MatrixCursor doAnswerApiCommand(Uri uri) {
        MatrixCursor matrixCursor = null;

        if (BaseFile.CMD_CANCEL.equals(uri.getLastPathSegment())) {
            int taskId = ProviderUtils.getIntQueryParam(uri,
                    BaseFile.PARAM_TASK_ID, 0);
            synchronized (mMapInterruption) {
                if (taskId == 0) {
                    for (int i = 0; i < mMapInterruption.size(); i++)
                        mMapInterruption.put(mMapInterruption.keyAt(i), true);
                } else if (mMapInterruption.indexOfKey(taskId) >= 0)
                    mMapInterruption.put(taskId, true);
            }
            return null;
        } else if (BaseFile.CMD_GET_DEFAULT_PATH.equals(uri
                .getLastPathSegment())) {
            matrixCursor = BaseFileProviderUtils.newBaseFileCursor();

            File file = Environment.getExternalStorageDirectory();
            if (file == null || !file.isDirectory())
                file = new File("/");
            int type = file.isFile() ? BaseFile.FILE_TYPE_FILE : (file
                    .isDirectory() ? BaseFile.FILE_TYPE_DIRECTORY
                    : BaseFile.FILE_TYPE_UNKNOWN);
            RowBuilder newRow = matrixCursor.newRow();
            newRow.add(0);// _ID
            newRow.add(BaseFile
                    .genContentIdUriBase(
                            LocalFileContract.getAuthority(getContext()))
                    .buildUpon().appendPath(Uri.fromFile(file).toString())
                    .build().toString());
            newRow.add(Uri.fromFile(file).toString());
            newRow.add(file.getName());
            newRow.add(file.canRead() ? 1 : 0);
            newRow.add(file.canWrite() ? 1 : 0);
            newRow.add(file.length());
            newRow.add(type);
            newRow.add(file.lastModified());
            newRow.add(FileUtils.getResIcon(type, file.getName()));
        }// get default path
        else if (BaseFile.CMD_IS_ANCESTOR_OF.equals(uri.getLastPathSegment())) {
            return doCheckAncestor(uri);
        } else if (BaseFile.CMD_GET_PARENT.equals(uri.getLastPathSegment())) {
            File file = new File(Uri.parse(
                    uri.getQueryParameter(BaseFile.PARAM_SOURCE)).getPath());
            file = file.getParentFile();
            if (file == null)
                return null;

            matrixCursor = BaseFileProviderUtils.newBaseFileCursor();

            int type = file.isFile() ? BaseFile.FILE_TYPE_FILE : (file
                    .isDirectory() ? BaseFile.FILE_TYPE_DIRECTORY : (file
                    .exists() ? BaseFile.FILE_TYPE_UNKNOWN
                    : BaseFile.FILE_TYPE_NOT_EXISTED));

            RowBuilder newRow = matrixCursor.newRow();
            newRow.add(0);// _ID
            newRow.add(BaseFile
                    .genContentIdUriBase(
                            LocalFileContract.getAuthority(getContext()))
                    .buildUpon().appendPath(Uri.fromFile(file).toString())
                    .build().toString());
            newRow.add(Uri.fromFile(file).toString());
            newRow.add(file.getName());
            newRow.add(file.canRead() ? 1 : 0);
            newRow.add(file.canWrite() ? 1 : 0);
            newRow.add(file.length());
            newRow.add(type);
            newRow.add(file.lastModified());
            newRow.add(FileUtils.getResIcon(type, file.getName()));
        } else if (BaseFile.CMD_SHUTDOWN.equals(uri.getLastPathSegment())) {
            /*
             * TODO Stop all tasks. If the activity call this command in
             * onDestroy(), it seems that this code block will be suspended and
             * started next time the activity starts. So we comment out this.
             * Let the Android system do what it wants to do!!!! I hate this.
             */
            // synchronized (mMapInterruption) {
            // for (int i = 0; i < mMapInterruption.size(); i++)
            // mMapInterruption.put(mMapInterruption.keyAt(i), true);
            // }

            if (mFileObserverEx != null) {
                mFileObserverEx.stopWatching();
                mFileObserverEx = null;
            }
        }

        return matrixCursor;
    }// doAnswerApiCommand()

    /**
     * Lists the content of a directory, if available.
     * 
     * @param uri
     *            the URI pointing to a directory.
     * @return the content of a directory, or {@code null} if not available.
     */
    private MatrixCursor doListFiles(Uri uri) {
        MatrixCursor matrixCursor = BaseFileProviderUtils.newBaseFileCursor();

        File dir = extractFile(uri);

        if (Utils.doLog())
            Log.d(CLASSNAME, "srcFile = " + dir);

        if (!dir.isDirectory() || !dir.canRead())
            return null;

        /*
         * Prepare params...
         */
        int taskId = ProviderUtils.getIntQueryParam(uri,
                BaseFile.PARAM_TASK_ID, 0);
        boolean showHiddenFiles = ProviderUtils.getBooleanQueryParam(uri,
                BaseFile.PARAM_SHOW_HIDDEN_FILES);
        boolean sortAscending = ProviderUtils.getBooleanQueryParam(uri,
                BaseFile.PARAM_SORT_ASCENDING, true);
        int sortBy = ProviderUtils.getIntQueryParam(uri,
                BaseFile.PARAM_SORT_BY, BaseFile.SORT_BY_NAME);
        int filterMode = ProviderUtils.getIntQueryParam(uri,
                BaseFile.PARAM_FILTER_MODE,
                BaseFile.FILTER_FILES_AND_DIRECTORIES);
        int limit = ProviderUtils.getIntQueryParam(uri, BaseFile.PARAM_LIMIT,
                1000);
        String positiveRegex = uri
                .getQueryParameter(BaseFile.PARAM_POSITIVE_REGEX_FILTER);
        String negativeRegex = uri
                .getQueryParameter(BaseFile.PARAM_NEGATIVE_REGEX_FILTER);

        mMapInterruption.put(taskId, false);

        boolean[] hasMoreFiles = { false };
        List<File> files = new ArrayList<File>();
        listFiles(taskId, dir, showHiddenFiles, filterMode, limit,
                positiveRegex, negativeRegex, files, hasMoreFiles);
        if (!mMapInterruption.get(taskId)) {
            sortFiles(taskId, files, sortAscending, sortBy);
            if (!mMapInterruption.get(taskId)) {
                for (int i = 0; i < files.size(); i++) {
                    if (mMapInterruption.get(taskId))
                        break;

                    File f = files.get(i);
                    int type = f.isFile() ? BaseFile.FILE_TYPE_FILE : (f
                            .isDirectory() ? BaseFile.FILE_TYPE_DIRECTORY
                            : BaseFile.FILE_TYPE_UNKNOWN);
                    RowBuilder newRow = matrixCursor.newRow();
                    newRow.add(i);// _ID
                    newRow.add(BaseFile
                            .genContentIdUriBase(
                                    LocalFileContract
                                            .getAuthority(getContext()))
                            .buildUpon().appendPath(Uri.fromFile(f).toString())
                            .build().toString());
                    newRow.add(Uri.fromFile(f).toString());
                    newRow.add(f.getName());
                    newRow.add(f.canRead() ? 1 : 0);
                    newRow.add(f.canWrite() ? 1 : 0);
                    newRow.add(f.length());
                    newRow.add(type);
                    newRow.add(f.lastModified());
                    newRow.add(FileUtils.getResIcon(type, f.getName()));
                }// for files

                /*
                 * The last row contains:
                 * 
                 * - The ID;
                 * 
                 * - The base file URI to original directory, which has
                 * parameter BaseFile.PARAM_HAS_MORE_FILES to indicate the
                 * directory has more files or not.
                 * 
                 * - The system absolute path to original directory.
                 * 
                 * - The name of original directory.
                 */
                RowBuilder newRow = matrixCursor.newRow();
                newRow.add(files.size());// _ID
                newRow.add(BaseFile
                        .genContentIdUriBase(
                                LocalFileContract.getAuthority(getContext()))
                        .buildUpon()
                        .appendPath(Uri.fromFile(dir).toString())
                        .appendQueryParameter(BaseFile.PARAM_HAS_MORE_FILES,
                                Boolean.toString(hasMoreFiles[0])).build()
                        .toString());
                newRow.add(Uri.fromFile(dir).toString());
                newRow.add(dir.getName());
            }
        }

        try {
            if (mMapInterruption.get(taskId)) {
                if (Utils.doLog())
                    Log.d(CLASSNAME, "query() >> cancelled...");
                return null;
            }
        } finally {
            mMapInterruption.delete(taskId);
        }

        if (mFileObserverEx != null)
            mFileObserverEx.stopWatching();
        mFileObserverEx = new FileObserverEx(getContext(),
                dir.getAbsolutePath(), uri);
        mFileObserverEx.startWatching();

        /*
         * Tells the Cursor what URI to watch, so it knows when its source data
         * changes.
         */
        matrixCursor.setNotificationUri(getContext().getContentResolver(), uri);
        return matrixCursor;
    }// doListFiles()

    /**
     * Retrieves file information of a single file.
     * 
     * @param uri
     *            the URI pointing to a file.
     * @return the file information. Can be {@code null}, based on the input
     *         parameters.
     */
    private MatrixCursor doRetrieveFileInfo(Uri uri) {
        MatrixCursor matrixCursor = BaseFileProviderUtils.newBaseFileCursor();

        File file = extractFile(uri);
        int type = file.isFile() ? BaseFile.FILE_TYPE_FILE : (file
                .isDirectory() ? BaseFile.FILE_TYPE_DIRECTORY
                : (file.exists() ? BaseFile.FILE_TYPE_UNKNOWN
                        : BaseFile.FILE_TYPE_NOT_EXISTED));
        RowBuilder newRow = matrixCursor.newRow();
        newRow.add(0);// _ID
        newRow.add(BaseFile
                .genContentIdUriBase(
                        LocalFileContract.getAuthority(getContext()))
                .buildUpon().appendPath(Uri.fromFile(file).toString()).build()
                .toString());
        newRow.add(Uri.fromFile(file).toString());
        newRow.add(file.getName());
        newRow.add(file.canRead() ? 1 : 0);
        newRow.add(file.canWrite() ? 1 : 0);
        newRow.add(file.length());
        newRow.add(type);
        newRow.add(file.lastModified());
        newRow.add(FileUtils.getResIcon(type, file.getName()));

        return matrixCursor;
    }// doRetrieveFileInfo()

    /**
     * Lists all file inside {@code dir}.
     * 
     * @param taskId
     *            the task ID.
     * @param dir
     *            the source directory.
     * @param showHiddenFiles
     *            {@code true} or {@code false}.
     * @param filterMode
     *            can be one of {@link BaseFile#FILTER_DIRECTORIES_ONLY},
     *            {@link BaseFile#FILTER_FILES_ONLY},
     *            {@link BaseFile#FILTER_FILES_AND_DIRECTORIES}.
     * @param limit
     *            the limit.
     * @param positiveRegex
     *            the positive regex filter.
     * @param negativeRegex
     *            the negative regex filter.
     * @param results
     *            the results.
     * @param hasMoreFiles
     *            the first item will contain a value representing that there is
     *            more files (exceeding {@code limit}) or not.
     */
    private void listFiles(final int taskId, final File dir,
            final boolean showHiddenFiles, final int filterMode,
            final int limit, String positiveRegex, String negativeRegex,
            final List<File> results, final boolean hasMoreFiles[]) {
        final Pattern positivePattern = Texts.compileRegex(positiveRegex);
        final Pattern negativePattern = Texts.compileRegex(negativeRegex);

        hasMoreFiles[0] = false;
        try {
            dir.listFiles(new FileFilter() {

                @Override
                public boolean accept(File pathname) {
                    if (mMapInterruption.get(taskId))
                        throw new CancellationException();

                    final boolean isFile = pathname.isFile();
                    final String name = pathname.getName();

                    /*
                     * Filters...
                     */
                    if (filterMode == BaseFile.FILTER_DIRECTORIES_ONLY
                            && isFile)
                        return false;
                    if (!showHiddenFiles && name.startsWith("."))
                        return false;
                    if (isFile && positivePattern != null
                            && !positivePattern.matcher(name).find())
                        return false;
                    if (isFile && negativePattern != null
                            && negativePattern.matcher(name).find())
                        return false;

                    /*
                     * Limit...
                     */
                    if (results.size() >= limit) {
                        hasMoreFiles[0] = true;
                        throw new CancellationException("Exceeding limit...");
                    }
                    results.add(pathname);

                    return false;
                }// accept()
            });
        } catch (CancellationException e) {
            if (Utils.doLog())
                Log.d(CLASSNAME, "listFiles() >> cancelled... >> " + e);
        }
    }// listFiles()

    /**
     * Sorts {@code files}.
     * 
     * @param taskId
     *            the task ID.
     * @param files
     *            list of files.
     * @param ascending
     *            {@code true} or {@code false}.
     * @param sortBy
     *            can be one of {@link BaseFile.#_SortByModificationTime},
     *            {@link BaseFile.#_SortByName}, {@link BaseFile.#_SortBySize}.
     */
    private void sortFiles(final int taskId, final List<File> files,
            final boolean ascending, final int sortBy) {
        try {
            Collections.sort(files, new Comparator<File>() {

                @Override
                public int compare(File lhs, File rhs) {
                    if (mMapInterruption.get(taskId))
                        throw new CancellationException();

                    if (lhs.isDirectory() && !rhs.isDirectory())
                        return -1;
                    if (!lhs.isDirectory() && rhs.isDirectory())
                        return 1;

                    /*
                     * Default is to compare by name (case insensitive).
                     */
                    int res = mCollator.compare(lhs.getName(), rhs.getName());

                    switch (sortBy) {
                    case BaseFile.SORT_BY_NAME:
                        break;// SortByName

                    case BaseFile.SORT_BY_SIZE:
                        if (lhs.length() > rhs.length())
                            res = 1;
                        else if (lhs.length() < rhs.length())
                            res = -1;
                        break;// SortBySize

                    case BaseFile.SORT_BY_MODIFICATION_TIME:
                        if (lhs.lastModified() > rhs.lastModified())
                            res = 1;
                        else if (lhs.lastModified() < rhs.lastModified())
                            res = -1;
                        break;// SortByDate
                    }

                    return ascending ? res : -res;
                }// compare()
            });
        } catch (CancellationException e) {
            if (Utils.doLog())
                Log.d(CLASSNAME, "sortFiles() >> cancelled...");
        }
    }// sortFiles()

    /**
     * Deletes {@code file}.
     * 
     * @param taskId
     *            the task ID.
     * @param file
     *            {@link File}.
     * @param recursive
     *            if {@code true} and {@code file} is a directory, this thread
     *            will delete all sub files/ folders of it recursively.
     * @return the total files deleted.
     */
    private int deleteFile(final int taskId, final File file,
            final boolean recursive) {
        final int[] count = { 0 };
        if (mMapInterruption.get(taskId))
            return count[0];

        if (file.isFile()) {
            if (file.delete())
                count[0]++;
            return count[0];
        }

        /*
         * If the directory is empty, try to delete it and return here.
         */
        if (file.delete()) {
            count[0]++;
            return count[0];
        }

        if (!recursive)
            return count[0];

        try {
            try {
                file.listFiles(new FileFilter() {

                    @Override
                    public boolean accept(File pathname) {
                        if (mMapInterruption.get(taskId))
                            throw new CancellationException();

                        if (pathname.isFile()) {
                            if (pathname.delete())
                                count[0]++;
                        } else if (pathname.isDirectory()) {
                            if (recursive)
                                count[0] += deleteFile(taskId, pathname,
                                        recursive);
                            else if (pathname.delete())
                                count[0]++;
                        }

                        return false;
                    }// accept()
                });
            } catch (CancellationException e) {
                return count[0];
            }

            if (file.delete())
                count[0]++;
        } catch (Throwable t) {
            // TODO
        }

        return count[0];
    }// deleteFile()

    /**
     * Checks ancestor with {@link BaseFile#CMD_IS_ANCESTOR_OF},
     * {@link BaseFile#PARAM_SOURCE} and {@link BaseFile#PARAM_TARGET}.
     * 
     * @param uri
     *            the original URI from client.
     * @return {@code null} if source is not ancestor of target; or a
     *         <i>non-null but empty</i> cursor if the source is.
     */
    private MatrixCursor doCheckAncestor(Uri uri) {
        File source = new File(Uri.parse(
                uri.getQueryParameter(BaseFile.PARAM_SOURCE)).getPath());
        File target = new File(Uri.parse(
                uri.getQueryParameter(BaseFile.PARAM_TARGET)).getPath());
        if (source == null || target == null)
            return null;

        boolean validate = ProviderUtils.getBooleanQueryParam(uri,
                BaseFile.PARAM_VALIDATE, true);
        if (validate) {
            if (!source.isDirectory() || !target.exists())
                return null;
        }

        if (source.equals(target.getParentFile())
                || (target.getParent() != null && target.getParent()
                        .startsWith(source.getAbsolutePath())))
            return BaseFileProviderUtils.newClosedCursor();

        return null;
    }// doCheckAncestor()

    /**
     * Extracts source file from request URI.
     * 
     * @param uri
     *            the original URI.
     * @return the file.
     */
    private static File extractFile(Uri uri) {
        String fileName = Uri.parse(uri.getLastPathSegment()).getPath();
        if (uri.getQueryParameter(BaseFile.PARAM_APPEND_PATH) != null)
            fileName += Uri.parse(
                    uri.getQueryParameter(BaseFile.PARAM_APPEND_PATH))
                    .getPath();
        if (uri.getQueryParameter(BaseFile.PARAM_APPEND_NAME) != null)
            fileName += "/" + uri.getQueryParameter(BaseFile.PARAM_APPEND_NAME);

        if (Utils.doLog())
            Log.d(CLASSNAME, "extractFile() >> " + fileName);

        return new File(fileName);
    }// extractFile()

}
