/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers.basefile;

import group.pals.android.lib.ui.filechooser.providers.BaseColumns;
import group.pals.android.lib.ui.filechooser.providers.ProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.localfile.FileObserverEx;
import group.pals.android.lib.ui.filechooser.providers.localfile.LocalFileProvider;

import java.io.File;

import android.content.ContentResolver;
import android.net.Uri;

/**
 * Base file contract.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class BaseFileContract {

    /**
     * This class cannot be instantiated.
     */
    private BaseFileContract() {
    }// BaseFileContract()

    /**
     * Base file.
     * 
     * @author Hai Bison
     * @since v5.1 beta
     */
    public static final class BaseFile implements BaseColumns {

        /**
         * This class cannot be instantiated.
         */
        private BaseFile() {
        }// BaseFile()

        /*
         * FILE TYPE.
         */

        /**
         * Directory.
         */
        public static final int FILE_TYPE_DIRECTORY = 0;
        /**
         * File.
         */
        public static final int FILE_TYPE_FILE = 1;
        /**
         * UNKNOWN file type.
         */
        public static final int FILE_TYPE_UNKNOWN = 2;
        /**
         * File is not existed.
         */
        public static final int FILE_TYPE_NOT_EXISTED = 3;

        /*
         * FILTER MODE.
         */

        /**
         * Only files.
         */
        public static final int FILTER_FILES_ONLY = 0;
        /**
         * Only directories.
         */
        public static final int FILTER_DIRECTORIES_ONLY = 1;
        /**
         * Files and directories.
         */
        public static final int FILTER_FILES_AND_DIRECTORIES = 2;

        /*
         * SORT MODE.
         */

        /**
         * Sort by name.
         */
        public static final int SORT_BY_NAME = 0;
        /**
         * Sort by size.
         */
        public static final int SORT_BY_SIZE = 1;
        /**
         * Sort by last modified.
         */
        public static final int SORT_BY_MODIFICATION_TIME = 2;

        /*
         * PATHS
         */

        /**
         * <i>This is internal field.</i>
         * <p/>
         * The path to a single directory's contents. You query this path to get
         * the contents of that directory.
         */
        public static final String PATH_DIR = "dir";
        /**
         * <i>This is internal field.</i>
         * <p/>
         * The path to a single file. This can be a file or a directory.
         */
        public static final String PATH_FILE = "file";
        /**
         * <i>This is internal field.</i>
         * <p/>
         * The path to query the provider's information such as name, ID...
         */
        public static final String PATH_API = "api";


        /*
         * COMMANDS.
         */

        /**
         * Use this command to cancel a previous task you executed. You set the
         * task ID with {@link #PARAM_TASK_ID}.
         * 
         * @see #PARAM_TASK_ID
         */
        public static final String CMD_CANCEL = "cancel";

        /**
         * Use this command along with two parameters: a source directory ID (
         * {@link #PARAM_SOURCE}) and a target file/ directory ID (
         * {@link #PARAM_TARGET}). It will return <i>a closed</i> cursor if the
         * given source file is a directory and it is ancestor of the target
         * file.
         * <p/>
         * If the given file is not a directory or is not ancestor of the file
         * provided by this parameter, the result will be {@code null}.
         * <p/>
         * For example, with local file, this query returns {@code true}:
         * <p/>
         * {@code content://local-file-authority/api/is_ancestor_of?source="/mnt/sdcard"&target="/mnt/sdcard/Android/data/cache"}
         * <p/>
         * Note that no matter how many levels between the ancestor and the
         * descendant are, it is still the ancestor. This is <b><i>not</i></b>
         * the same concept as "parent", which will return {@code false} in
         * above example.
         * 
         * @see #PARAM_SOURCE
         * @see #PARAM_TARGET
         */
        public static final String CMD_IS_ANCESTOR_OF = "is_ancestor_of";

        /**
         * Use this command to get default path of a provider.
         * <p/>
         * Type: {@code String}
         */
        public static final String CMD_GET_DEFAULT_PATH = "get_default_path";

        /**
         * Use this parameter to get parent file of a file. You provide the
         * source file ID with {@link #PARAM_SOURCE}.
         * 
         * @see #PARAM_SOURCE
         */
        public static final String CMD_GET_PARENT = "get_parent";

        /**
         * Use this command when you don't need to work with the content
         * provider anymore. Normally <i>Android handles ContentProvider startup
         * and shutdown automatically</i>. But in case of
         * {@link LocalFileProvider}, it uses {@link FileObserverEx} to watch
         * for changes of files. The SDK doesn't clarify the ending events of a
         * content provider. So the file-observer objects could continue to run
         * even if your activity has stopped. Hence this command is useful to
         * let the providers know when they can shutdown the background jobs.
         */
        public static final String CMD_SHUTDOWN = "shutdown";


        public static final String CMD_CHECK_CONNECTION = "check_connection";

        /*
         * PARAMETERS.
         */

        /**
         * Use this parameter to provide the source file ID.
         * <p/>
         * Type: URI
         */
        public static final String PARAM_SOURCE = "source";

        /**
         * Use this parameter to provide the target file ID.
         * <p/>
         * Type: URI
         */
        public static final String PARAM_TARGET = "target";

        /**
         * Use this parameter to provide the name of new file/ directory you
         * want to create.
         * <p/>
         * Type: {@code String}
         * 
         * @see #PARAM_FILE_TYPE
         */
        public static final String PARAM_NAME = "name";

        /**
         * Use this parameter to provide the type of new file that you want to
         * create. It can be {@link #FILE_TYPE_DIRECTORY} or
         * {@link #FILE_TYPE_FILE}. If not provided, default is
         * {@link #FILE_TYPE_DIRECTORY}.
         * 
         * @see #PARAM_NAME
         */
        public static final String PARAM_FILE_TYPE = "file_type";

        /**
         * Use this parameter to set an ID to any task.
         * <p/>
         * Default: {@code 0} with all methods.
         * <p/>
         * Type: {@code Integer}
         */
        public static final String PARAM_TASK_ID = "task_id";

        /**
         * Use this parameter for operators which can work recursively, such as
         * deleting a directory... The value can be {@code "true"} or
         * {@code "1"} for {@code true}, {@code "false"} or {@code "0"} for
         * {@code false}.
         * <p/>
         * Default:
         * <p/>
         * <ul>
         * <li>{@code "true"} with {@code delete()}.</li>
         * </ul>
         * <p/>
         * Type: {@code Boolean}
         */
        public static final String PARAM_RECURSIVE = "recursive";

        /**
         * Use this parameter to show hidden files. The value can be
         * {@code "true"} or {@code "1"} for {@code true}, {@code "false"} or
         * {@code "0"} for {@code false}.
         * <p/>
         * Default: {@code "false"} with {@code query()}.
         * <p/>
         * Type: {@code Boolean}
         */
        public static final String PARAM_SHOW_HIDDEN_FILES = "show_hidden_files";

        /**
         * Use this parameter to filter file type. Can be one of
         * {@link #FILTER_FILES_ONLY}, {@link #FILTER_DIRECTORIES_ONLY},
         * {@link #FILTER_FILES_AND_DIRECTORIES}.
         * <p/>
         * Default: {@link #FILTER_FILES_AND_DIRECTORIES} with {@code query()}.
         * <p/>
         * Type: {@code Integer}
         */
        public static final String PARAM_FILTER_MODE = "filter_mode";

        /**
         * Use this parameter to sort files. Can be one of
         * {@link #SORT_BY_MODIFICATION_TIME}, {@link #SORT_BY_NAME},
         * {@link #SORT_BY_SIZE}.
         * <p/>
         * Default: {@link #SORT_BY_NAME} with {@code query()}.
         * <p/>
         * Type: {@code Integer}
         */
        public static final String PARAM_SORT_BY = "sort_by";

        /**
         * Use this parameter for sort order. Can be {@code "true"} or
         * {@code "1"} for {@code true}, {@code "false"} or {@code "0"} for
         * {@code false}.
         * <p/>
         * Default: {@code "true"} with {@code query()}.
         * <p/>
         * Type: {@code Boolean}
         */
        public static final String PARAM_SORT_ASCENDING = "sort_ascending";

        /**
         * Use this parameter to limit results.
         * <p/>
         * Default: {@code 1000} with {@code query()}.
         * <p/>
         * Type: {@code Integer}
         */
        public static final String PARAM_LIMIT = "limit";

        /**
         * This parameter is returned from the provider. It's only used for
         * {@code query()} while querying directory contents. Can be
         * {@code "true"} or {@code "1"} for {@code true}, {@code "false"} or
         * {@code "0"} for {@code false}.
         * <p/>
         * Type: {@code Boolean}
         */
        public static final String PARAM_HAS_MORE_FILES = "has_more_files";

        /**
         * Use this parameter to append a file name to a full path of directory
         * to obtains its full pathname.
         * <p/>
         * This parameter can be use together with {@link #PARAM_APPEND_PATH},
         * the priority is lesser than that parameter.
         * <p/>
         * <ul>
         * <li>Scope:
         * {@link ContentResolver#query(Uri, String[], String, String[], String)}
         * and related.</li>
         * </ul>
         * <p/>
         * Type: {@code String}
         */
        public static final String PARAM_APPEND_NAME = "append_name";

        /**
         * Use this parameter to append a partial path to a full path of
         * directory to obtains its full pathname. The value is a URI, every
         * path segment of the URI is a partial name. You can build the URI with
         * scheme {@link ContentResolver#SCHEME_FILE}, appending your paths with
         * {@link Uri.Builder#appendPath(String)}.
         * <p/>
         * This parameter can be use together with {@link #PARAM_APPEND_NAME},
         * the priority is higher than that parameter.
         * <p/>
         * <ul>
         * <li>Scope:
         * {@link ContentResolver#query(Uri, String[], String, String[], String)}
         * and related.</li>
         * </ul>
         * <p/>
         * Type: {@code String}
         * 
         * @see #PARAM_APPEND_NAME
         */
        public static final String PARAM_APPEND_PATH = "append_path";

        /**
         * Use this parameter to set a positive regex to filter filename (with
         * {@code query()}). If the regex can't be compiled due to syntax error,
         * then it will be ignored.
         * <p/>
         * Type: {@code String}
         */
        public static final String PARAM_POSITIVE_REGEX_FILTER = "positive_regex_filter";

        /**
         * Use this parameter to set a negative regex to filter filename (with
         * {@code query()}). If the regex can't be compiled due to syntax error,
         * then it will be ignored.
         * <p/>
         * Type: {@code String}
         */
        public static final String PARAM_NEGATIVE_REGEX_FILTER = "negative_regex_filter";

        /**
         * Use this parameter to tell the provider to validate files or not.
         * <p/>
         * Type: {@code String} - can be {@code "true"} or {@code "1"} for
         * {@code true}, {@code "false"} or {@code "0"} for {@code false}.
         * <p/>
         * Scope:
         * {@link ContentResolver#query(Uri, String[], String, String[], String)}
         * and related.
         * <p/>
         * Default: {@code true}
         * 
         * @see #CMD_IS_ANCESTOR_OF
         */
        public static final String PARAM_VALIDATE = "validate";

        /*
         * URI builders.
         */

        /**
         * Generates content URI API for a provider.
         * 
         * @param authority
         *            the authority of file provider.
         * @return The API URI for a provider. Default will return provider name
         *         and ID.
         */
        public static Uri genContentUriApi(String authority) {
            return Uri.parse(ProviderUtils.SCHEME + authority + "/" + PATH_API);
        }// genContentUriBase()

        /**
         * Generates content URI base for a single directory's contents. That
         * means this URI is used to get the content of the given directory,
         * <b><i>not</b></i> the attributes of its. To get the attributes of a
         * directory (or a file), use {@link #genContentIdUriBase(String)}.
         * 
         * @param authority
         *            the authority of file provider.
         * @return The base URI for a single directory. You append it with the
         *         URI to full path of the directory.
         */
        public static Uri genContentUriBase(String authority) {
            return Uri.parse(ProviderUtils.SCHEME + authority + "/" + PATH_DIR
                    + "/");
        }// genContentUriBase()

        /**
         * Generates content URI base for a single file.
         * 
         * @param authority
         *            the authority of file provider.
         * @return The base URI for a single file. You append it with the URI to
         *         full path of a single file.
         */
        public static Uri genContentIdUriBase(String authority) {
            return Uri.parse(ProviderUtils.SCHEME + authority + "/" + PATH_FILE
                    + "/");
        }// genContentIdUriBase()

        /*
         * MIME type definitions.
         */

        /**
         * The MIME type providing a directory of files.
         */
        public static final String CONTENT_TYPE = "vnd.android.cursor.dir/vnd.android-filechooser.basefile";

        /**
         * The MIME type of a single file.
         */
        public static final String CONTENT_ITEM_TYPE = "vnd.android.cursor.item/vnd.android-filechooser.basefile";

        /*
         * Column definitions
         */

        /**
         * The URI of this file.
         * <p/>
         * Type: {@code String}
         */
        public static final String COLUMN_URI = "uri";

        /**
         * The real URI of this file. This URI is independent of the content
         * provider's URI. For example with {@link LocalFileProvider}, this
         * column contains the URI which you can create new {@link File} object
         * directly from it.
         * <p/>
         * Type: {@code String}
         */
        public static final String COLUMN_REAL_URI = "real_uri";

        /**
         * The name of this file.
         * <p/>
         * Type: {@code String}
         */
        public static final String COLUMN_NAME = "name";

        /**
         * Size of this file.
         * <p/>
         * Type: {@code Long}
         */
        public static final String COLUMN_SIZE = "size";

        /**
         * Holds the readable attribute of this file, {@code 0 == false} and
         * {@code 1 == true}.
         * <p/>
         * Type: {@code Integer}
         */
        public static final String COLUMN_CAN_READ = "can_read";

        /**
         * Holds the writable attribute of this file, {@code 0 == false} and
         * {@code 1 == true}.
         * <p/>
         * Type: {@code Integer}
         */
        public static final String COLUMN_CAN_WRITE = "can_write";

        /**
         * The type of this file. Can be one of {@link #FILE_TYPE_DIRECTORY},
         * {@link #FILE_TYPE_FILE}, {@link #FILE_TYPE_UNKNOWN},
         * {@link #FILE_TYPE_NOT_EXISTED}.
         * <p/>
         * Type: {@code Integer}
         */
        public static final String COLUMN_TYPE = "type";

        /**
         * The resource ID of the file icon.
         * <p/>
         * Type: {@code Integer}
         */
        public static final String COLUMN_ICON_ID = "icon_id";

        /**
         * The name of this provider.
         * <p/>
         * Type: {@code String}
         */
        public static final String COLUMN_PROVIDER_NAME = "provider_name";

        /**
         * The ID of this provider.
         * <p/>
         * Type: {@code String}
         */
        public static final String COLUMN_PROVIDER_ID = "provider_id";

        /**
         * The resource ID ({@code R.attr}) of the badge (icon) of the provider.
         * <p/>
         * Type: {@code Integer}
         */
        public static final String COLUMN_PROVIDER_ICON_ATTR = "provider_icon_attr";
    }// BaseFile

}
