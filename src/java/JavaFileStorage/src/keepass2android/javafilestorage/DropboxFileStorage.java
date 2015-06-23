package keepass2android.javafilestorage;

import java.io.ByteArrayInputStream;
import java.io.FileNotFoundException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.SharedPreferences.Editor;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Bundle;
import android.util.Log;
import android.widget.Toast;

import com.dropbox.client2.DropboxAPI;
import com.dropbox.client2.android.AndroidAuthSession;
import com.dropbox.client2.android.AuthActivity;
import com.dropbox.client2.exception.DropboxException;
import com.dropbox.client2.exception.DropboxServerException;
import com.dropbox.client2.exception.DropboxUnlinkedException;
import com.dropbox.client2.session.AccessTokenPair;
import com.dropbox.client2.session.AppKeyPair;
import com.dropbox.client2.session.TokenPair;
import com.dropbox.client2.session.Session.AccessType;



public class DropboxFileStorage extends JavaFileStorageBase {
	
	final static private String TAG = "KP2AJ";
    
    final static private String ACCOUNT_PREFS_NAME = "prefs";
    final static private String ACCESS_KEY_NAME = "ACCESS_KEY";
    final static private String ACCESS_SECRET_NAME = "ACCESS_SECRET";
    
    DropboxAPI<AndroidAuthSession> mApi;
	private boolean mLoggedIn = false;
	private Context mContext;
	
    protected AccessType mAccessType = AccessType.DROPBOX;


	private String appKey;
	private String appSecret;
	
	public DropboxFileStorage(Context ctx, String _appKey, String _appSecret)
	{
		initialize(ctx, _appKey, _appSecret, false, mAccessType);
	}
	
	public DropboxFileStorage(Context ctx, String _appKey, String _appSecret, boolean clearKeysOnStart)
	{
		initialize(ctx, _appKey, _appSecret, clearKeysOnStart, mAccessType);
	}
	
	public DropboxFileStorage(Context ctx, String _appKey, String _appSecret, boolean clearKeysOnStart, AccessType accessType)
	{
		initialize(ctx, _appKey, _appSecret, clearKeysOnStart, accessType);
	}

	private void initialize(Context ctx, String _appKey, String _appSecret,
			boolean clearKeysOnStart, AccessType accessType) {
		appKey = _appKey;
		appSecret = _appSecret;
		mContext = ctx;
		
		if (clearKeysOnStart)
			clearKeys();
		
		this.mAccessType = accessType;
		
		// We create a new AuthSession so that we can use the Dropbox API.
        AndroidAuthSession session = buildSession();
        mApi = new DropboxAPI<AndroidAuthSession>(session);
        
        checkAppKeySetup();
	}
    
	public boolean tryConnect(Activity activity)
	{
		if (!mLoggedIn)
			mApi.getSession().startAuthentication(activity);
		return mLoggedIn;
	}
	
    private void setLoggedIn(boolean b) {
		mLoggedIn = b;
		
	}

	private boolean checkAppKeySetup() {

        // Check if the app has set up its manifest properly.
        Intent testIntent = new Intent(Intent.ACTION_VIEW);
        String scheme = "db-" + appKey;
        String uri = scheme + "://" + AuthActivity.AUTH_VERSION + "/test";
        testIntent.setData(Uri.parse(uri));
        PackageManager pm = mContext.getPackageManager();
        if (0 == pm.queryIntentActivities(testIntent, 0).size()) {
            showToast("URL scheme in your app's " +
                    "manifest is not set up correctly. You should have a " +
                    "com.dropbox.client2.android.AuthActivity with the " +
                    "scheme: " + scheme);
            return false;
        }
        return true;
    }
	
	public boolean isConnected()
	{
		return mLoggedIn;
	}
	
	
	public boolean checkForFileChangeFast(String path, String previousFileVersion) throws Exception
	{
		if ((previousFileVersion == null) || (previousFileVersion.equals("")))
			return false;
		try {
			path = removeProtocol(path);
			com.dropbox.client2.DropboxAPI.Entry entry = mApi.metadata(path, 1, null, false, null);
			return entry.hash != previousFileVersion;
		} catch (DropboxException e) {
			throw convertException(e);
		}
	}
	
	public String getCurrentFileVersionFast(String path)
	{
		try {
			path = removeProtocol(path);
			com.dropbox.client2.DropboxAPI.Entry entry = mApi.metadata(path, 1, null, false, null);
			return entry.rev;
		} catch (DropboxException e) {
			Log.d(TAG, e.toString());
			return "";
		}
	}
	
	public InputStream openFileForRead(String path) throws Exception
	{
		try {
			path = removeProtocol(path);
			return mApi.getFileStream(path, null);
		} catch (DropboxException e) {
			//System.out.println("Something went wrong: " + e);
			throw convertException(e);
		}
	}
	
	public void uploadFile(String path, byte[] data, boolean writeTransactional) throws Exception
	{
		ByteArrayInputStream bis = new ByteArrayInputStream(data);
		try {
			path = removeProtocol(path);
			//TODO: it would be nice to be able to use the parent version with putFile()
			mApi.putFileOverwrite(path, bis, data.length, null);
		} catch (DropboxException e) {
			throw convertException(e);
		}
	}

    private Exception convertException(DropboxException e) {

    	Log.d(TAG, "Exception of type " +e.getClass().getName()+":" + e.getMessage());
    	
    	if (DropboxUnlinkedException.class.isAssignableFrom(e.getClass()) )
    	{
    		Log.d(TAG, "LoggedIn=false (due to unlink exception)");
    		setLoggedIn(false);
    		clearKeys();
    		return new UserInteractionRequiredException("Unlinked from Dropbox! User must re-link.", e);
    		
    	}
    	
    	//test for special error FileNotFound which must be reported with FileNotFoundException
    	if (DropboxServerException.class.isAssignableFrom(e.getClass()) )
    	{
    		
    		DropboxServerException serverEx = (DropboxServerException)e;
    		if (serverEx.error == 404)
    			return new FileNotFoundException(e.toString());
    	}
    	
    	return e;
	}

	private void showToast(String msg) {
        Toast error = Toast.makeText(mContext, msg, Toast.LENGTH_LONG);
        error.show();
    }

    /**
     * Keep the access keys returned from Trusted Authenticator in a local
     * store, rather than storing user name & password, and re-authenticating each
     * time (which is not to be done, ever).
     *
     * @return Array of [access_key, access_secret], or null if none stored
     */
    private String[] getKeys() {
        SharedPreferences prefs = mContext.getSharedPreferences(ACCOUNT_PREFS_NAME, 0);
        String key = prefs.getString(ACCESS_KEY_NAME, null);
        String secret = prefs.getString(ACCESS_SECRET_NAME, null);
        if (key != null && secret != null) {
        	String[] ret = new String[2];
        	ret[0] = key;
        	ret[1] = secret;
        	return ret;
        } else {
        	return null;
        }
    }

    /**
     * Keeping the access keys returned from Trusted Authenticator in a local
     * store, rather than storing user name & password, and re-authenticating each
     * time (which is not to be done, ever).
     */
    private void storeKeys(String key, String secret) {
    	Log.d(TAG, "Storing Dropbox accessToken");
        // Save the access key for later
        SharedPreferences prefs = mContext.getSharedPreferences(ACCOUNT_PREFS_NAME, 0);
        Editor edit = prefs.edit();
        edit.putString(ACCESS_KEY_NAME, key);
        edit.putString(ACCESS_SECRET_NAME, secret);
        edit.commit();
    }

    private void clearKeys() {
        SharedPreferences prefs = mContext.getSharedPreferences(ACCOUNT_PREFS_NAME, 0);
        Editor edit = prefs.edit();
        edit.clear();
        edit.commit();
    }

    private AndroidAuthSession buildSession() {
        AppKeyPair appKeyPair = new AppKeyPair(appKey, appSecret);
        AndroidAuthSession session;

        String[] stored = getKeys();
        if (stored != null) {
            AccessTokenPair accessToken = new AccessTokenPair(stored[0], stored[1]);
            session = new AndroidAuthSession(appKeyPair, mAccessType, accessToken);
            setLoggedIn(true);
            Log.d(TAG, "Creating Dropbox Session with accessToken");
        } else {
            session = new AndroidAuthSession(appKeyPair, mAccessType);
            setLoggedIn(false);
            Log.d(TAG, "Creating Dropbox Session without accessToken");
        }

        return session;
    }

	@Override
	public String createFolder(String parentPath, String newDirName) throws Exception {
		try
		{
			String path = parentPath;
			if (!path.endsWith("/"))
				path = path + "/";
			path = path + newDirName;
			
			String pathWithoutProtocol = removeProtocol(path);
			mApi.createFolder(pathWithoutProtocol);
			return path;
		} 
		catch (DropboxException e) {
		    throw convertException(e);
		}
	}
	
	@Override
	public String createFilePath(String parentPath, String newFileName) throws Exception {
		String path = parentPath;
		if (!path.endsWith("/"))
			path = path + "/";
		path = path + newFileName;
		
		return path;
	}


	@Override
	public List<FileEntry> listFiles(String parentPath) throws Exception {
		try
		{
			parentPath = removeProtocol(parentPath);
			com.dropbox.client2.DropboxAPI.Entry dirEntry = mApi.metadata(parentPath, 0, null, true, null);
			
			if (dirEntry.isDeleted)
				throw new FileNotFoundException("Directory "+parentPath+" is deleted!");
			
			List<FileEntry> result = new ArrayList<FileEntry>();
			
			for (com.dropbox.client2.DropboxAPI.Entry e: dirEntry.contents)
			{
				if (e.isDeleted) 
					continue;
				FileEntry fileEntry = convertToFileEntry(e);
				result.add(fileEntry);
			}
			return result;

			
		} catch (DropboxException e) {
			
		     throw convertException(e);
		}
		
	}

	private FileEntry convertToFileEntry(com.dropbox.client2.DropboxAPI.Entry e) {
		//Log.d("JFS","e="+e);
		FileEntry fileEntry = new FileEntry();
		fileEntry.canRead = true;
		fileEntry.canWrite = true;
		fileEntry.isDirectory = e.isDir;
		fileEntry.sizeInBytes = e.bytes;
		fileEntry.path = getProtocolId()+"://"+ e.path;
		fileEntry.displayName = e.path.substring(e.path.lastIndexOf("/")+1);
		//Log.d("JFS","fileEntry="+fileEntry);
		Date lastModifiedDate = null;
		if (e.modified != null)
			lastModifiedDate = com.dropbox.client2.RESTUtility.parseDate(e.modified);
		if (lastModifiedDate != null)
			fileEntry.lastModifiedTime = lastModifiedDate.getTime();
		else 
			fileEntry.lastModifiedTime = -1;
		//Log.d("JFS","Ok. Dir="+fileEntry.isDirectory);
		return fileEntry;
	}

	@Override
	public void delete(String path) throws Exception {
		try
		{
			path = removeProtocol(path);
		mApi.delete(path);
		} catch (DropboxException e) {
		     throw convertException(e);
		}
		
		
	}

	@Override
	public FileEntry getFileEntry(String filename) throws Exception {
		try
		{
			filename = removeProtocol(filename);
			Log.d("KP2AJ", "getFileEntry(), mApi = "+mApi+" filename="+filename);
			com.dropbox.client2.DropboxAPI.Entry dbEntry = mApi.metadata(filename, 0, null, false, null);
			Log.d("JFS", "dbEntry = "+dbEntry);
			
			if (dbEntry.isDeleted)
				throw new FileNotFoundException(filename+" is deleted!");
			
			return convertToFileEntry(dbEntry);
			
		} catch (DropboxException e) {
			
		     throw convertException(e);
		}
	}

	@Override
	public void startSelectFile(FileStorageSetupInitiatorActivity activity, boolean isForSave,
			int requestCode) 
	{
		
		String path = getProtocolId()+":///";
		Log.d("KP2AJ", "startSelectFile "+path+", connected: "+path);
		/*if (isConnected())
		{
			Intent intent = new Intent();
			intent.putExtra(EXTRA_IS_FOR_SAVE, isForSave);
			intent.putExtra(EXTRA_PATH, path);
			activity.onImmediateResult(requestCode, RESULT_FILECHOOSER_PREPARED, intent);
		}
		else*/
		{
			activity.startSelectFileProcess(path, isForSave, requestCode);	
		}

		
		
	}

	@Override
	public String getProtocolId() {
		return "dropbox";
	}
	
	public boolean requiresSetup(String path)
	{
		return !isConnected();
	}

	@Override
	public void prepareFileUsage(FileStorageSetupInitiatorActivity activity, String path, int requestCode, boolean alwaysReturnSuccess) {
		if (isConnected())
		{
			Intent intent = new Intent();
			intent.putExtra(EXTRA_PATH, path);
			activity.onImmediateResult(requestCode, RESULT_FILEUSAGE_PREPARED, intent);
		}
		else
		{
			activity.startFileUsageProcess(path, requestCode, alwaysReturnSuccess);	
		}
		
	}
	
	@Override
	public void prepareFileUsage(Context appContext, String path) throws UserInteractionRequiredException {
		if (!isConnected())
		{
			throw new UserInteractionRequiredException();
		}
		
	}

	@Override
	public void onCreate(FileStorageSetupActivity activity, Bundle savedInstanceState) {

		Log.d("KP2AJ", "OnCreate");
		
	}

	@Override
	public void onResume(FileStorageSetupActivity activity) {
		
		if (activity.getProcessName().equals(PROCESS_NAME_SELECTFILE))
			activity.getState().putString(EXTRA_PATH, activity.getPath());
		
		Log.d("KP2AJ", "OnResume (3). LoggedIn="+mLoggedIn);
		/*if (mLoggedIn)
		{
			finishActivityWithSuccess(activity);
			return;
		}*/
		
		AndroidAuthSession session = mApi.getSession();


        JavaFileStorage.FileStorageSetupActivity storageSetupAct = (JavaFileStorage.FileStorageSetupActivity)activity;
        
        if (storageSetupAct.getState().containsKey("hasStartedAuth"))
        {
        	Log.d("KP2AJ", "auth started");
            // The next part must be inserted in the onResume() method of the
            // activity from which session.startAuthentication() was called, so
            // that Dropbox authentication completes properly.
            if (session.authenticationSuccessful()) {
            	Log.d("KP2AJ", "auth successful");
                try {
                    // Mandatory call to complete the auth
                    session.finishAuthentication();
                    Log.d("KP2AJ", "finished auth ");
                    // Store it locally in our app for later use
                    TokenPair tokens = session.getAccessTokenPair();
                    storeKeys(tokens.key, tokens.secret);
                    setLoggedIn(true);
                    Log.d("KP2AJ", "success");
                    finishActivityWithSuccess(activity);
                    return;
                    
                } catch (Exception e) {
                	Log.d("KP2AJ", "finish with error: " + e.toString());
                    finishWithError(activity, e);
                	return;
                }
            }

            
        	Log.i(TAG, "authenticating not succesful");
        	Intent data = new Intent();
        	data.putExtra(EXTRA_ERROR_MESSAGE, "authenticating not succesful");
        	((Activity)activity).setResult(Activity.RESULT_CANCELED, data);
        	((Activity)activity).finish();
		}
        else
        {
        	Log.d("KP2AJ", "Starting auth");
        	mApi.getSession().startAuthentication(((Activity)activity));
        	storageSetupAct.getState().putBoolean("hasStartedAuth", true);

        }
        
		
	}

	@Override
	public void onStart(FileStorageSetupActivity activity) {
		
		
	}

	@Override
	public void onActivityResult(FileStorageSetupActivity activity, int requestCode, int resultCode, Intent data) {
		//nothing to do here
		
	}
	
	String removeProtocol(String path)
	{
		if (path == null)
			return null;
		return path.substring(getProtocolId().length()+3);
	}

	@Override
	public String getDisplayName(String path) {
		return path;
	}

	@Override
	public String getFilename(String path) throws Exception {
		return path.substring(path.lastIndexOf("/")+1);
	}

}
