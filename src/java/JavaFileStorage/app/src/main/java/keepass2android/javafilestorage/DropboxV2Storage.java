package keepass2android.javafilestorage;

import com.dropbox.core.DbxAppInfo;
import com.dropbox.core.DbxException;
import com.dropbox.core.DbxOAuth1AccessToken;
import com.dropbox.core.DbxOAuth1Upgrader;
import com.dropbox.core.DbxRequestConfig;
import com.dropbox.core.InvalidAccessTokenException;
import com.dropbox.core.android.Auth;
import com.dropbox.core.json.JsonReadException;
import com.dropbox.core.oauth.DbxCredential;
import com.dropbox.core.v2.DbxClientV2;
import com.dropbox.core.http.OkHttp3Requestor;
import com.dropbox.core.v2.files.DeleteErrorException;
import com.dropbox.core.v2.files.DeletedMetadata;
import com.dropbox.core.v2.files.DownloadErrorException;
import com.dropbox.core.v2.files.FileMetadata;
import com.dropbox.core.v2.files.FolderMetadata;
import com.dropbox.core.v2.files.GetMetadataErrorException;
import com.dropbox.core.v2.files.ListFolderErrorException;
import com.dropbox.core.v2.files.ListFolderResult;
import com.dropbox.core.v2.files.Metadata;
import com.dropbox.core.v2.files.WriteMode;


import java.io.ByteArrayInputStream;
import java.io.FileNotFoundException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Date;
import java.util.List;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.SharedPreferences.Editor;
import android.os.AsyncTask;
import android.os.Bundle;
import android.util.Log;
import android.widget.Toast;


class DbxRequestConfigFactory {
    private static DbxRequestConfig sDbxRequestConfig;

    public static DbxRequestConfig getRequestConfig() {
        if (sDbxRequestConfig == null) {
            sDbxRequestConfig = DbxRequestConfig.newBuilder("Keepass2Android")
                    .withHttpRequestor(new OkHttp3Requestor(OkHttp3Requestor.defaultOkHttpClient()))
                    .build();
        }
        return sDbxRequestConfig;
    }
}
/**
 * Created by Philipp on 18.11.2016.
 */
public class DropboxV2Storage extends JavaFileStorageBase
{
    private List<String> scope = new ArrayList<String>(Arrays.asList("account_info.read", "files.metadata.write","files.content.write","files.content.read"));

    private DbxAppInfo appInfo;

    DbxRequestConfig requestConfig = DbxRequestConfig.newBuilder("kp2a")
            .withHttpRequestor(new OkHttp3Requestor(OkHttp3Requestor.defaultOkHttpClient()))
            .build();

    final static private String TAG = "KP2AJ";

    final static private String ACCOUNT_PREFS_NAME = "prefs";
    final static private String ACCESS_KEY_V1_NAME = "ACCESS_KEY";
    final static private String ACCESS_SECRET_V1_NAME = "ACCESS_SECRET";
    final static private String ACCESS_TOKEN_NAME = "ACCESS_TOKEN_V2";
    //key for serialized dropbox credentials (used to store access + refresh tokens as long-living access tokens (v2) have been deprecated
    final static private String SERIALIZED_CREDENTIALS = "SERIALIZED_CREDENTIALS";

    private boolean mLoggedIn = false;
    private Context mContext;

    public FileEntry getRootFileEntry() {

        FileEntry rootEntry = new FileEntry();

        rootEntry.displayName = "";
        rootEntry.isDirectory = true;
        rootEntry.lastModifiedTime = -1;
        rootEntry.canRead = rootEntry.canWrite = true;
        rootEntry.path = getProtocolId()+":///";
        rootEntry.sizeInBytes = -1;

        return rootEntry;
    }

    public enum AccessType { Full, AppFolder};

    protected AccessType mAccessType = AccessType.Full;

    DbxClientV2 dbxClient;


    public DropboxV2Storage(Context ctx, String _appKey, String _appSecret)
    {
        initialize(ctx, _appKey, _appSecret, false, mAccessType);
    }

    public DropboxV2Storage(Context ctx, String _appKey, String _appSecret, boolean clearKeysOnStart)
    {
        initialize(ctx, _appKey, _appSecret, clearKeysOnStart, mAccessType);
    }

    public DropboxV2Storage(Context ctx, String _appKey, String _appSecret, boolean clearKeysOnStart, AccessType accessType)
    {
        initialize(ctx, _appKey, _appSecret, clearKeysOnStart, accessType);
    }

    private void initialize(Context ctx, String _appKey, String _appSecret,
                            boolean clearKeysOnStart, AccessType accessType)
    {
        Log.d("KP2A","Initializing Dropbox storage. Update for use with short-lived access tokens.");
        appInfo = new DbxAppInfo(_appKey,_appSecret);
        mContext = ctx;

        if (clearKeysOnStart)
            clearKeys();

        this.mAccessType = accessType;

        buildSession();

    }

    public boolean tryConnect(Activity activity)
    {
        if (!mLoggedIn)
            Auth.startOAuth2PKCE(activity, appInfo.getKey(), DbxRequestConfigFactory.getRequestConfig(), scope);
        return mLoggedIn;
    }

    private void setLoggedIn(boolean b) {
        mLoggedIn = b;

    }


    public boolean isConnected()
    {
        return mLoggedIn;
    }


    public boolean checkForFileChangeFast(String path, String previousFileVersion) throws Exception
    {
        if ((previousFileVersion == null) || (previousFileVersion.equals("")))
            return false;
            path = removeProtocol(path);
        try {
            Metadata entry = dbxClient.files().getMetadata(path);
            return !String.valueOf(entry.hashCode()) .equals(previousFileVersion);

        } catch (DbxException e) {
            throw convertException(e);
        }

    }

    public String getCurrentFileVersionFast(String path)
    {
        try {
            path = removeProtocol(path);
            Metadata entry = dbxClient.files().getMetadata(path);
            return String.valueOf(entry.hashCode());
        } catch (DbxException e) {
            Log.d(TAG, e.toString());
            return "";
        }
    }

    public InputStream openFileForRead(String path) throws Exception
    {
        try {
            path = removeProtocol(path);
            return dbxClient.files().download(path).getInputStream();
        } catch (DbxException e) {
            //System.out.println("Something went wrong: " + e);
            throw convertException(e);
        }
    }

    public void uploadFile(String path, byte[] data, boolean writeTransactional) throws Exception
    {
        ByteArrayInputStream bis = new ByteArrayInputStream(data);
        try {

            //try to get the file id from the path and use that to create the uploadBuilder. This should preserve the case of the path.
            String id = null;

            try {
                id = getFileEntry(path).userData;
            }
            catch (Exception e)
            {
                //ignore. file might not exist yet.
            }

            if (id != null && id.length() > 0)
            {
                path = id;
            }
            else
            {
                path = removeProtocol(path);
            }





            dbxClient.files().uploadBuilder(path).withMode(WriteMode.OVERWRITE).uploadAndFinish(bis);

        } catch (DbxException e) {
            throw convertException(e);
        }
    }

    private Exception convertException(DbxException e) {

        Log.d(TAG, "Exception of type " +e.getClass().getName()+":" + e.getMessage());

        if (InvalidAccessTokenException.class.isAssignableFrom(e.getClass()) ) {
            Log.d(TAG, "LoggedIn=false (due to InvalidAccessTokenException)");
            setLoggedIn(false);
            clearKeys();
            return new UserInteractionRequiredException("Unlinked from Dropbox! User must re-link.", e);
        }

        if (ListFolderErrorException.class.isAssignableFrom(e.getClass()) ) {
            ListFolderErrorException listFolderErrorException = (ListFolderErrorException)e;
            if (listFolderErrorException.errorValue.getPathValue().isNotFound())
                return new FileNotFoundException(e.toString());
        }
        if (DownloadErrorException.class.isAssignableFrom(e.getClass()) ) {
            DownloadErrorException downloadErrorException = (DownloadErrorException)e;
            if (downloadErrorException.errorValue.getPathValue().isNotFound())
                return new FileNotFoundException(e.toString());
        }
        if (GetMetadataErrorException.class.isAssignableFrom(e.getClass()) ) {
            GetMetadataErrorException getMetadataErrorException = (GetMetadataErrorException)e;
            if (getMetadataErrorException.errorValue.getPathValue().isNotFound())
                return new FileNotFoundException(e.toString());
        }
        if (DeleteErrorException.class.isAssignableFrom(e.getClass()) ) {
            DeleteErrorException deleteErrorException = (DeleteErrorException)e;
            if (deleteErrorException.errorValue.getPathLookupValue().isNotFound())
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
    private String[] getKeysV1() {
        SharedPreferences prefs = mContext.getSharedPreferences(ACCOUNT_PREFS_NAME, 0);
        String key = prefs.getString(ACCESS_KEY_V1_NAME, null);
        String secret = prefs.getString(ACCESS_SECRET_V1_NAME, null);
        if (key != null && secret != null) {
            String[] ret = new String[2];
            ret[0] = key;
            ret[1] = secret;
            return ret;
        } else {
            return null;
        }
    }

    private String getKeyV2() {
        SharedPreferences prefs = mContext.getSharedPreferences(ACCOUNT_PREFS_NAME, 0);
        return prefs.getString(ACCESS_TOKEN_NAME, null);
    }


    private DbxCredential getStoredCredential(){
        SharedPreferences prefs = mContext.getSharedPreferences(ACCOUNT_PREFS_NAME, 0);
        String serialized = prefs.getString(SERIALIZED_CREDENTIALS, null);
        if (serialized == null)
            return null;
        try {
            return DbxCredential.Reader.readFully(serialized);
        } catch (JsonReadException e) {
            return null;
        }
    }



    //stores a long-living access token from API v2
    //New tokens of this kind will no longer be issued, but we have a v1-updater which converts v1 to v2 tokens so we should still be able to store them.
    private void storeKey(String v2token) {
        Log.d(TAG, "Storing Dropbox accessToken");
        // Save the access key for later
        SharedPreferences prefs = mContext.getSharedPreferences(ACCOUNT_PREFS_NAME, 0);
        Editor edit = prefs.edit();
        edit.putString(ACCESS_TOKEN_NAME, v2token);
        edit.commit();
    }

    private void clearKeys() {
        SharedPreferences prefs = mContext.getSharedPreferences(ACCOUNT_PREFS_NAME, 0);
        Editor edit = prefs.edit();
        edit.clear();
        edit.commit();
    }

    private void buildSession() {

        DbxCredential credential = getStoredCredential();
        if (credential != null)
        {
            credential = new DbxCredential(credential.getAccessToken(), -1L, credential.getRefreshToken(), credential.getAppKey());
            dbxClient = new DbxClientV2(DbxRequestConfigFactory.getRequestConfig(), credential);

            setLoggedIn(true);

            return;
        }


        String v2Token = getKeyV2();

        if (v2Token != null)
        {
            dbxClient = new DbxClientV2(requestConfig, v2Token);

            setLoggedIn(true);
            Log.d(TAG, "Creating Dropbox Session with accessToken");
        } else {
            setLoggedIn(false);
            Log.d(TAG, "Creating Dropbox Session without accessToken");
        }

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
            dbxClient.files().createFolder(pathWithoutProtocol);

            return path;
        }
        catch (DbxException e) {
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
            if (parentPath.equals("/"))
                parentPath = ""; //Dropbox is a bit picky here
            ListFolderResult dirEntry = dbxClient.files().listFolder(parentPath);

            List<FileEntry> result = new ArrayList<FileEntry>();

            while (true)
            {
                for (Metadata e: dirEntry.getEntries())
                {
                    FileEntry fileEntry = convertToFileEntry(e);
                    result.add(fileEntry);
                }

                if (!dirEntry.getHasMore()) {
                    break;
                }

                dirEntry = dbxClient.files().listFolderContinue(dirEntry.getCursor());
            }


            return result;


        } catch (DbxException e) {

            throw convertException(e);
        }

    }

    private FileEntry convertToFileEntry(Metadata e) throws Exception {
        //Log.d("JFS","e="+e);

        FileEntry fileEntry = new FileEntry();
        fileEntry.canRead = true;
        fileEntry.canWrite = true;
        if (e instanceof FolderMetadata)
        {
            FolderMetadata fm = (FolderMetadata)e;
            fileEntry.isDirectory = true;
            fileEntry.sizeInBytes = 0;
            fileEntry.lastModifiedTime = 0;
        }
        else if (e instanceof FileMetadata)
        {
            FileMetadata fm = (FileMetadata)e;
            fileEntry.sizeInBytes = fm.getSize();
            fileEntry.isDirectory = false;
            fileEntry.lastModifiedTime = fm.getServerModified().getTime();
            fileEntry.userData = fm.getId();
        }
        else if (e instanceof DeletedMetadata)
        {
            throw new FileNotFoundException();
        }
        else
        {
            throw new Exception("unexpected metadata " + e.getClass().getName() );
        }

        fileEntry.path = getProtocolId()+"://"+ e.getPathLower();
        fileEntry.displayName = e.getName();

        //Log.d("JFS","fileEntry="+fileEntry);
        //Log.d("JFS","Ok. Dir="+fileEntry.isDirectory);
        return fileEntry;
    }

    @Override
    public void delete(String path) throws Exception {
        try
        {
            path = removeProtocol(path);
            dbxClient.files().delete(path);
        } catch (DbxException e) {
            throw convertException(e);
        }


    }

    @Override
    public FileEntry getFileEntry(String filename) throws Exception {
        try
        {
            filename = removeProtocol(filename);
            Log.d("KP2AJ", "getFileEntry(), " +filename);

            //querying root is not supported
            if ((filename.equals("")) || (filename.equals("/")))
                return getRootFileEntry();
            if (filename.endsWith("/"))
                filename = filename.substring(0,filename.length()-1);

            Metadata dbEntry = dbxClient.files().getMetadata(filename);
            return convertToFileEntry(dbEntry);

        } catch (DbxException e) {

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
    public void onResume(final FileStorageSetupActivity activity) {

        if (activity.getProcessName().equals(PROCESS_NAME_SELECTFILE))
            activity.getState().putString(EXTRA_PATH, activity.getPath());

        Log.d("KP2AJ", "OnResume (3). LoggedIn="+mLoggedIn);
		/*if (mLoggedIn)
		{
			finishActivityWithSuccess(activity);
			return;
		}*/


        final String[] storedV1Keys = getKeysV1();
        if (storedV1Keys != null) {
           new AsyncTask<Object, Object, Object>()
            {
                @Override
                protected Object doInBackground(Object... objects) {
                    DbxOAuth1AccessToken v1Token = new DbxOAuth1AccessToken(storedV1Keys[0], storedV1Keys[1]);
                    DbxOAuth1Upgrader upgrader = new DbxOAuth1Upgrader(requestConfig, appInfo);
                    try {
                        String v2Token = upgrader.createOAuth2AccessToken(v1Token);
                        upgrader.disableOAuth1AccessToken(v1Token);
                        storeKey(v2Token);
                        return v2Token;
                    } catch (Exception e) {
                        e.printStackTrace();
                        clearKeys();
                        return null;
                    }

                }

                @Override
                protected void onPostExecute(Object o) {
                    if (o != null) {
                        buildSession();
                        finishActivityWithSuccess(activity);
                    }
                    else
                        resumeGetAuthToken(activity);
                }
            }.execute();
        }

        else {

            resumeGetAuthToken(activity);
        }


    }

    private void resumeGetAuthToken(FileStorageSetupActivity activity) {
        FileStorageSetupActivity storageSetupAct = activity;

        if (storageSetupAct.getState().containsKey("hasStartedAuth")) {
            Log.d("KP2AJ", "auth started");

            DbxCredential dbxCredential = Auth.getDbxCredential();


            if (dbxCredential != null) {
                Log.d("KP2AJ", "auth successful");
                try {
                    storeCredentials(dbxCredential);
                    buildSession();
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
            ((Activity) activity).setResult(Activity.RESULT_CANCELED, data);
            ((Activity) activity).finish();
        } else {
            Log.d("KP2AJ", "Starting auth");
            Auth.startOAuth2PKCE((Activity) activity, appInfo.getKey(), DbxRequestConfigFactory.getRequestConfig(), scope);
            Log.d("KP2AJ", "Started auth");
            storageSetupAct.getState().putBoolean("hasStartedAuth", true);
            Log.d("KP2AJ", "add state flag");

        }
    }

    private void storeCredentials(DbxCredential dbxCredential)
    {
        Log.d(TAG, "Storing Dropbox credentials");
        // Save the access key for later
        SharedPreferences prefs = mContext.getSharedPreferences(ACCOUNT_PREFS_NAME, 0);
        Editor edit = prefs.edit();
        edit.putString(SERIALIZED_CREDENTIALS, dbxCredential.toString());
        edit.commit();

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
