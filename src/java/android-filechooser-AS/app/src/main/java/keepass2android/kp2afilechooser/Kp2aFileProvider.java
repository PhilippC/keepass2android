package keepass2android.kp2afilechooser;
/* Author: Philipp Crocoll
 * 
 *    Based on a file provider by Hai Bison
 *
 */



import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.database.MatrixCursor.RowBuilder;
import android.net.Uri;
import android.util.Log;

import java.io.FileNotFoundException;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.CancellationException;

import group.pals.android.lib.ui.filechooser.R;
import group.pals.android.lib.ui.filechooser.providers.BaseFileProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.ProviderUtils;
import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileContract.BaseFile;
import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileProvider;
import group.pals.android.lib.ui.filechooser.utils.FileUtils;
import group.pals.android.lib.ui.filechooser.utils.Utils;

public abstract class Kp2aFileProvider extends BaseFileProvider {


    /**
     * Gets the authority of this provider.
     * 
     * abstract because the concrete authority can be decided by the overriding class.
     *
     * @return the authority.
     */
    public abstract String getAuthority();
    
    /**
     * The unique ID of this provider.
     */
    public static final String _ID = "9dab9818-0a8b-47ef-88cc-10fe538bf8f7";
    
    /**
     * Used for debugging or something...
     */
    private static final String CLASSNAME = Kp2aFileProvider.class.getName();
    
    //cache for FileEntry objects to reduce network traffic
    private HashMap<String, FileEntry> fileEntryMap = new HashMap<String, FileEntry>();
    //during write operations it is not desired to put entries to the cache. This set indicates which 
    //files cannot be cached currently:
    private Set<String> cacheBlockedFiles = new HashSet<String>();


    @Override
    public boolean onCreate() {

        Log.d("KP2A_FC_P", "onCreate");

        BaseFileProviderUtils.registerProviderInfo(_ID,
                getAuthority());

        URI_MATCHER.addURI(getAuthority(),
                BaseFile.PATH_DIR + "/*", URI_DIRECTORY);
        URI_MATCHER.addURI(getAuthority(),
                BaseFile.PATH_FILE + "/*", URI_FILE);
        URI_MATCHER.addURI(getAuthority(),
                BaseFile.PATH_API, URI_API);
        URI_MATCHER.addURI(getAuthority(),
                BaseFile.PATH_API + "/*", URI_API_COMMAND);

        return true;
    }// onCreate()

    @Override
    public int delete(Uri uri, String selection, String[] selectionArgs) {
        if (Utils.doLog())
            Log.d("KP2A_FC_P", "delete() >> " + uri);

        int count = 0;
        
        

        switch (URI_MATCHER.match(uri)) {
        case URI_FILE: {
            boolean isRecursive = ProviderUtils.getBooleanQueryParam(uri,
                    BaseFile.PARAM_RECURSIVE, true);
            String filename = extractFile(uri);
            removeFromCache(filename, isRecursive);
            blockFromCache(filename);
            if (deletePath(filename, isRecursive))
            {
	            getContext()
	                            .getContentResolver()
	                            .notifyChange(
	                                    BaseFile.genContentUriBase(
	                                            
	                                                    getAuthority())
	                                            .buildUpon()
	                                            .appendPath(
	                                                    getParentPath(filename)
	                                                    )
	                                            .build(), null);
	            count = 1; //success
            }
            blockFromCache(filename);
            break;// URI_FILE
        }

        default:
            throw new IllegalArgumentException("UNKNOWN URI " + uri);
        }


        if (count > 0)
            getContext().getContentResolver().notifyChange(uri, null);

        return count;
    }// delete()

    



	

	@Override
    public Uri insert(Uri uri, ContentValues values) {
        if (Utils.doLog())
            Log.d("KP2A_FC_P", "insert() >> " + uri);

        switch (URI_MATCHER.match(uri)) {
        case URI_DIRECTORY:
            String dirname = extractFile(uri);
            String newDirName = uri.getQueryParameter(BaseFile.PARAM_NAME);
            String newFullName = removeTrailingSlash(dirname)+"/"+newDirName;

            boolean success = false;

            switch (ProviderUtils.getIntQueryParam(uri,
                    BaseFile.PARAM_FILE_TYPE, BaseFile.FILE_TYPE_DIRECTORY)) {
            case BaseFile.FILE_TYPE_DIRECTORY:
            	success = createDirectory(dirname, newDirName);
                break;// FILE_TYPE_DIRECTORY

            case BaseFile.FILE_TYPE_FILE:
                //not supported at the moment
                break;// FILE_TYPE_FILE

            default:
                return null;
            }

            if (success) 
            {
                Uri newUri = BaseFile
                        .genContentIdUriBase(
                                getAuthority())
                        .buildUpon()
                        .appendPath( newFullName).build();
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
            Log.d("KP2A_FC_P", String.format(
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
            matrixCursor.newRow().add(_ID)
                    .add("KP2A")
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

    private MatrixCursor getCheckConnectionCursor(Uri uri) {
        try
        {
            checkConnection(uri);
            Log.d("KP2A_FC_P", "checking connection for " + uri + " ok.");
            return null;
        }
        catch (Exception e)
        {
            Log.d("KP2A_FC_P","Check connection failed with: " + e.toString());

            MatrixCursor matrixCursor = new MatrixCursor(BaseFileProviderUtils.CONNECTION_CHECK_CURSOR_COLUMNS);
            RowBuilder newRow = matrixCursor.newRow();
            String message = e.getLocalizedMessage();
            if (message == null)
                message = e.getMessage();
            if (message == null)
                message = e.toString();
            newRow.add(message);
            return matrixCursor;
        }
    }

    private void checkConnection(Uri uri) throws Exception {
       try
       {
           String path = Uri.parse(
                   uri.getQueryParameter(BaseFile.PARAM_SOURCE)).toString();
           StringBuilder sb = new StringBuilder();
           FileEntry result = getFileEntry(path, sb);
           if (result == null)
               throw new Exception(sb.toString());


        }
        catch (FileNotFoundException ex)
        {
            Log.d("KP2A_FC_P","File not found. Ignore.");

            return;
        }
    }

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

        String lastPathSegment = uri.getLastPathSegment();
        
        Log.d("KP2A_FC_P", "lastPathSegment:" + lastPathSegment);
        
        if (BaseFile.CMD_CANCEL.equals(lastPathSegment)) {
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
        } else if (BaseFile.CMD_GET_DEFAULT_PATH.equals(lastPathSegment)) {
        	
        	return null;
        	
        }// get default path
        else if (BaseFile.CMD_IS_ANCESTOR_OF.equals(lastPathSegment)) {
            return doCheckAncestor(uri);
        } else if (BaseFile.CMD_GET_PARENT.equals(lastPathSegment)) {
        	
        	{
        		String path = Uri.parse(
                    uri.getQueryParameter(BaseFile.PARAM_SOURCE)).toString();
	
	            String parentPath = getParentPath(path);
	            
        		
        		if (parentPath == null)
	            {
        			if (Utils.doLog())
        				Log.d(CLASSNAME, "parent file is null");
	                return null;
	            }
                FileEntry e;
                try {
                    e = this.getFileEntryCached(parentPath);
                }
                catch (Exception ex)
                {
                    ex.printStackTrace();
                    return null;
                }
                if (e == null)
                    return null;

	            matrixCursor = BaseFileProviderUtils.newBaseFileCursor();
	
	            int type = parentPath != null ? BaseFile.FILE_TYPE_DIRECTORY 
	            		: BaseFile.FILE_TYPE_NOT_EXISTED;
	            
	           
	            RowBuilder newRow = matrixCursor.newRow();
	            newRow.add(0);// _ID
	            newRow.add(BaseFile
	                    .genContentIdUriBase(
	                            getAuthority())
	                    .buildUpon().appendPath(parentPath)
	                    .build().toString());
	            newRow.add(e.path);
	            newRow.add(e.displayName);
	            newRow.add(e.canRead); //can read
	            newRow.add(e.canWrite); //can write
	            newRow.add(0);
	            newRow.add(type);
	            newRow.add(0);
	            newRow.add(FileUtils.getResIcon(type, e.displayName));
	            return matrixCursor;
	        }
          	
        } else if (BaseFile.CMD_SHUTDOWN.equals(lastPathSegment)) {
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

        } else if (BaseFile.CMD_CHECK_CONNECTION.equals(lastPathSegment))
        {
            Log.d("KP2A_FC_P","Check connection...");
            return getCheckConnectionCursor(uri);
        }

        return matrixCursor;
    }// doAnswerApiCommand()

    
/*
	private String addProtocol(String path) {
		if (path == null)
			return null;
		if (path.startsWith(getProtocolId()+"://"))
			return path;
		return getProtocolId()+"://"+path;
	}*/

	/**
     * Lists the content of a directory, if available.
     * 
     * @param uri
     *            the URI pointing to a directory.
     * @return the content of a directory, or {@code null} if not available.
     */
    private MatrixCursor doListFiles(Uri uri) {
        MatrixCursor matrixCursor = BaseFileProviderUtils.newBaseFileCursor();

        String dirName = extractFile(uri);

        if (Utils.doLog())
            Log.d(CLASSNAME, "doListFiles. srcFile = " + dirName);

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
        List<FileEntry> files = new ArrayList<FileEntry>();
        listFiles(taskId, dirName, showHiddenFiles, filterMode, limit,
                positiveRegex, negativeRegex, files, hasMoreFiles);
        if (!mMapInterruption.get(taskId)) {
        	
            try {
				sortFiles(taskId, files, sortAscending, sortBy);
			} catch (Exception e) {
				// TODO Auto-generated catch block
				e.printStackTrace();
			}
            if (!mMapInterruption.get(taskId)) {
                
            	for (int i = 0; i < files.size(); i++) {
                    if (mMapInterruption.get(taskId))
                        break;

                    FileEntry f = files.get(i);
                    updateFileEntryCache(f);
                    
                    if (Utils.doLog())
                    	Log.d(CLASSNAME, "listing " + f.path +" for "+dirName);
                    
                    addFileInfo(matrixCursor, i, f);
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
                                getAuthority())
                        .buildUpon()
                        .appendPath(dirName)
                        .appendQueryParameter(BaseFile.PARAM_HAS_MORE_FILES,
                                Boolean.toString(hasMoreFiles[0])).build()
                        .toString());
                newRow.add(dirName);
                String displayName = getFileEntryCached(dirName).displayName;
                newRow.add(displayName);
                
                Log.d(CLASSNAME, "Returning name " + displayName+" for " +dirName);
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

        /*
         * Tells the Cursor what URI to watch, so it knows when its source data
         * changes.
         */
        matrixCursor.setNotificationUri(getContext().getContentResolver(), uri);
        return matrixCursor;
    }// doListFiles()



	private RowBuilder addFileInfo(MatrixCursor matrixCursor, int id,
			FileEntry f) {
		int type = !f.isDirectory ? BaseFile.FILE_TYPE_FILE : BaseFile.FILE_TYPE_DIRECTORY;
		RowBuilder newRow = matrixCursor.newRow();
		newRow.add(id);// _ID
		newRow.add(BaseFile
		        .genContentIdUriBase(
		                getAuthority())
		        .buildUpon().appendPath(f.path)
		        .build().toString());
		newRow.add(f.path);
		if (f.displayName == null)
			Log.w("KP2AJ", "displayName is null for " + f.path);
		newRow.add(f.displayName);
		newRow.add(f.canRead ? 1 : 0);
		newRow.add(f.canWrite ? 1 : 0);
		newRow.add(f.sizeInBytes);
		newRow.add(type);
		if (f.lastModifiedTime > 0)
			newRow.add(f.lastModifiedTime);
		else 
			newRow.add(null);
		newRow.add(FileUtils.getResIcon(type, f.displayName));
		return newRow;
	}

    /**
     * Retrieves file information of a single file.
     * 
     * @param uri
     *            the URI pointing to a file.
     * @return the file information. Can be {@code null}, based on the input
     *         parameters.
     */
    private MatrixCursor doRetrieveFileInfo(Uri uri) {
    	Log.d(CLASSNAME, "retrieve file info "+uri.toString());
        MatrixCursor matrixCursor = BaseFileProviderUtils.newBaseFileCursor();

        String filename = extractFile(uri);
        
        FileEntry f = getFileEntryCached(filename);
        if (f == null)
        	addDeletedFileInfo(matrixCursor, filename);
        else	
        	addFileInfo(matrixCursor, 0, f);
        
        return matrixCursor;
    }// doRetrieveFileInfo()

   

    //puts the file entry in the cache for later reuse with retrieveFileInfo
	private void updateFileEntryCache(FileEntry f) {
		if (f != null)
			fileEntryMap.put(f.path, f);
	}
	//removes the file entry from the cache (if cached). Should be called whenever the file changes
	private void removeFromCache(String filename, boolean recursive) {
		fileEntryMap.remove(filename);
		
		if (recursive)
		{
			Set<String> keys = fileEntryMap.keySet();
			Set<String> keysToRemove = new HashSet<String>();
			for (String key: keys)
			{
				if (key.startsWith(key))
					keysToRemove.add(key);
			}
			for (String key: keysToRemove)
			{
				fileEntryMap.remove(key);	
			}
			
		}
		
		
	}
	
	private void blockFromCache(String filename) {
		cacheBlockedFiles.add(filename);
	}
	
	private void unblockFromCache(String filename) {
		cacheBlockedFiles.remove(filename);
	}

	//returns the file entry from the cache if present or queries the concrete provider method to return the file info
    private FileEntry getFileEntryCached(String filename) {
    	//check if enry is cached:
    	FileEntry cachedEntry = fileEntryMap.get(filename);
    	if (cachedEntry != null)
    	{
    		if (Utils.doLog())
    			Log.d(CLASSNAME, "getFileEntryCached: from cache. " + filename);
    		return cachedEntry;
    	}
    	
		if (Utils.doLog())
			Log.d(CLASSNAME, "getFileEntryCached: not in cache :-( " + filename);


        FileEntry newEntry ;
        try {
            //it's not -> query the information.
           newEntry = getFileEntry(filename, null);
        } catch (Exception e) {
            e.printStackTrace();
            return null;
        }
		
		if (!cacheBlockedFiles.contains(filename))
			updateFileEntryCache(newEntry);
		
		return newEntry;
	}

	private void addDeletedFileInfo(MatrixCursor matrixCursor, String filename) {
    	int type = BaseFile.FILE_TYPE_NOT_EXISTED;
    	RowBuilder newRow = matrixCursor.newRow();
		newRow.add(0);// _ID
		newRow.add(BaseFile
		        .genContentIdUriBase(
		                getAuthority())
		        .buildUpon().appendPath(filename)
		        .build().toString());
		newRow.add(filename);
		newRow.add(filename);
		newRow.add(0);
		newRow.add(0);
		newRow.add(0);
		newRow.add(type);
		newRow.add(null);
		newRow.add(FileUtils.getResIcon(type, filename));
	}

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
     * @throws Exception
     */
    private void sortFiles(final int taskId, final List<FileEntry> files,
            final boolean ascending, final int sortBy) throws Exception {
        try {
            Collections.sort(files, new Comparator<FileEntry>() {

                @Override
                public int compare(FileEntry lhs, FileEntry rhs) {
                    if (mMapInterruption.get(taskId))
                        throw new CancellationException();

                    if (lhs.isDirectory && !rhs.isDirectory)
                        return -1;
                    if (!lhs.isDirectory && rhs.isDirectory)
                        return 1;

                    /*
                     * Default is to compare by name (case insensitive).
                     */
                    int res = mCollator.compare(lhs.path, rhs.path);

                    switch (sortBy) {
                    case BaseFile.SORT_BY_NAME:
                        break;// SortByName

                    case BaseFile.SORT_BY_SIZE:
                        if (lhs.sizeInBytes > rhs.sizeInBytes)
                            res = 1;
                        else if (lhs.sizeInBytes < rhs.sizeInBytes)
                            res = -1;
                        break;// SortBySize

                    case BaseFile.SORT_BY_MODIFICATION_TIME:
                        if (lhs.lastModifiedTime > rhs.lastModifiedTime)
                            res = 1;
                        else if (lhs.lastModifiedTime < rhs.lastModifiedTime)
                            res = -1;
                        break;// SortByDate
                    }

                    return ascending ? res : -res;
                }// compare()
            });
        } catch (CancellationException e) {
            if (Utils.doLog())
                Log.d("KP2A_FC_P", "sortFiles() >> cancelled...");
        }
        catch (Exception e)
        {
            Log.d("KP2A_FC_P", "sortFiles() >> "+e);
            throw e;
        }
    }// sortFiles()

    
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
        String source = Uri.parse(
                uri.getQueryParameter(BaseFile.PARAM_SOURCE)).toString();
        String target = Uri.parse(
                uri.getQueryParameter(BaseFile.PARAM_TARGET)).toString();
        if (source == null || target == null)
            return null;

        boolean validate = ProviderUtils.getBooleanQueryParam(uri,
                BaseFile.PARAM_VALIDATE, true);
        if (validate) {
         //not supported
        }
        
        if (!source.endsWith("/"))
        	source += "/";
        
        
        String targetParent = getParentPath(target);
        if (targetParent != null && targetParent.startsWith(source))
        {
        	if (Utils.doLog())
        		Log.d("KP2A_FC_P", source+" is parent of "+target);
            return BaseFileProviderUtils.newClosedCursor();
        }
        if (Utils.doLog())
    		Log.d("KP2A_FC_P", source+" is no parent of "+target);

        return null;
    }// doCheckAncestor()

    /**
     * Extracts source file from request URI.
     * 
     * @param uri
     *            the original URI.
     * @return the filename.
     */
    private static String extractFile(Uri uri) {
        String fileName = Uri.parse(uri.getLastPathSegment()).toString();
        if (uri.getQueryParameter(BaseFile.PARAM_APPEND_PATH) != null)
            fileName += Uri.parse(
                    uri.getQueryParameter(BaseFile.PARAM_APPEND_PATH)).toString();
        if (uri.getQueryParameter(BaseFile.PARAM_APPEND_NAME) != null)
            fileName += "/" + uri.getQueryParameter(BaseFile.PARAM_APPEND_NAME);

        if (Utils.doLog())
            Log.d(CLASSNAME, "extractFile() >> " + fileName);

        return fileName;
    }// extractFile()
    
    private static String removeTrailingSlash(String path)
    {
    	if (path.endsWith("/")) {
    		return path.substring(0, path.length() - 1);
    	}
    	return path;
    }

    private String getParentPath(String path)
    {
    	path = removeTrailingSlash(path);
    	if (path.indexOf("://") == -1)
    	{
    		Log.d("KP2A_FC_P", "invalid path: " + path);
    		return null; 
    	}
    	String pathWithoutProtocol = path.substring(path.indexOf("://")+3);
    	int lastSlashPos = path.lastIndexOf("/");
    	if (pathWithoutProtocol.indexOf("/") == -1)
    	{
    		Log.d("KP2A_FC_P", "parent of " + path +" is null");
    		return null;
    	}
    	else
    	{
    		String parent = path.substring(0, lastSlashPos)+"/";
    		Log.d("KP2A_FC_P", "parent of " + path +" is "+parent);
    		return parent;
    	}
    }
    
    

	protected abstract FileEntry getFileEntry(String path, StringBuilder errorMessageBuilder) throws Exception;
    
	/**
     * Lists all file inside {@code dirName}.
     * 
     * @param taskId
     *            the task ID.
     * @param dirName
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
    protected abstract void listFiles(final int taskId, final String dirName,
            final boolean showHiddenFiles, final int filterMode,
            final int limit, String positiveRegex, String negativeRegex,
            final List<FileEntry> results, final boolean hasMoreFiles[]);

    
    protected abstract boolean deletePath(String filename, boolean isRecursive);
    protected abstract boolean createDirectory(String dirname, String newDirName);
    


}
