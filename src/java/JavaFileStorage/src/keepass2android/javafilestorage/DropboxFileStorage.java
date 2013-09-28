package keepass2android.javafilestorage;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.FileNotFoundException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.List;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.SharedPreferences.Editor;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.DropBoxManager.Entry;
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



public class DropboxFileStorage implements JavaFileStorage {
	
	//NOTE: also adjust secret!
	final static private String APP_KEY = "i8shu7v1hgh7ynt"; //KP2A
	//final static private String APP_KEY = "4ybka4p4a1027n6"; //FileStorageTest
	
    // If you'd like to change the access type to the full Dropbox instead of
    // an app folder, change this value.
    final static private AccessType ACCESS_TYPE = AccessType.DROPBOX;
    
    final static private String TAG = "KP2AJ";
    
    final static private String ACCOUNT_PREFS_NAME = "prefs";
    final static private String ACCESS_KEY_NAME = "ACCESS_KEY";
    final static private String ACCESS_SECRET_NAME = "ACCESS_SECRET";
    
    DropboxAPI<AndroidAuthSession> mApi;
	private boolean mLoggedIn = false;
	private Context mContext;
	
	public DropboxFileStorage(Context ctx)
	{
		mContext = ctx;
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
	
	public void onResume()
	{
		AndroidAuthSession session = mApi.getSession();

        // The next part must be inserted in the onResume() method of the
        // activity from which session.startAuthentication() was called, so
        // that Dropbox authentication completes properly.
        if (session.authenticationSuccessful()) {
            try {
                // Mandatory call to complete the auth
                session.finishAuthentication();

                // Store it locally in our app for later use
                TokenPair tokens = session.getAccessTokenPair();
                storeKeys(tokens.key, tokens.secret);
                setLoggedIn(true);
            } catch (IllegalStateException e) {
                Log.i(TAG, "Error authenticating", e);
                throw e;
            }
        }
	}
	
    private void setLoggedIn(boolean b) {
		mLoggedIn = b;
		
	}

	private boolean checkAppKeySetup() {

        // Check if the app has set up its manifest properly.
        Intent testIntent = new Intent(Intent.ACTION_VIEW);
        String scheme = "db-" + APP_KEY;
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
		if ((previousFileVersion == null) || (previousFileVersion == ""))
			return false;
		try {
			com.dropbox.client2.DropboxAPI.Entry entry = mApi.metadata(path, 1, null, false, null);
			return entry.hash != previousFileVersion;
		} catch (DropboxException e) {
			throw convertException(e);
		}
	}
	
	public String getCurrentFileVersionFast(String path)
	{
		try {
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
    		return new Exception("Unlinked from Dropbox!", e);
    		
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

    //TODO: call when Unlinked Exception	
    private void clearKeys() {
        SharedPreferences prefs = mContext.getSharedPreferences(ACCOUNT_PREFS_NAME, 0);
        Editor edit = prefs.edit();
        edit.clear();
        edit.commit();
    }

    private AndroidAuthSession buildSession() {
    	//note: the SecretKeys class is not public because the App-Secret must be secret!
        AppKeyPair appKeyPair = new AppKeyPair(APP_KEY, SecretKeys.DROPBOX_APP_SECRET);
        AndroidAuthSession session;

        String[] stored = getKeys();
        if (stored != null) {
            AccessTokenPair accessToken = new AccessTokenPair(stored[0], stored[1]);
            session = new AndroidAuthSession(appKeyPair, ACCESS_TYPE, accessToken);
            setLoggedIn(true);
            Log.d(TAG, "Creating Dropbox Session with accessToken");
        } else {
            session = new AndroidAuthSession(appKeyPair, ACCESS_TYPE);
            setLoggedIn(false);
            Log.d(TAG, "Creating Dropbox Session without accessToken");
        }

        return session;
    }

	@Override
	public void createFolder(String path) throws Exception {
		try
		{
			mApi.createFolder(path);		
		} 
		catch (DropboxException e) {
		    throw convertException(e);
		}
	}

	@Override
	public List<FileEntry> listFiles(String dirName) throws Exception {
		try
		{
			com.dropbox.client2.DropboxAPI.Entry dirEntry = mApi.metadata(dirName, 0, null, true, null);
			
			if (dirEntry.isDeleted)
				throw new FileNotFoundException("Directory "+dirName+" is deleted!");
			
			List<FileEntry> result = new ArrayList<FileEntry>();
			
			for (com.dropbox.client2.DropboxAPI.Entry e: dirEntry.contents)
			{
				if (e.isDeleted) 
					continue;
				FileEntry fileEntry = new FileEntry();
				fileEntry.canRead = true;
				fileEntry.canWrite = true;
				fileEntry.isDirectory = e.isDir;
				fileEntry.sizeInBytes = e.bytes;
				fileEntry.path = e.path;
				fileEntry.lastModifiedTime = com.dropbox.client2.RESTUtility.parseDate(e.modified).getTime();
				result.add(fileEntry);
			}
			return result;

			
		} catch (DropboxException e) {
			
		     throw convertException(e);
		}
		
	}

	@Override
	public void delete(String path) throws Exception {
		try
		{
		mApi.delete(path);
		} catch (DropboxException e) {
		     throw convertException(e);
		}
		
		
	}

}
