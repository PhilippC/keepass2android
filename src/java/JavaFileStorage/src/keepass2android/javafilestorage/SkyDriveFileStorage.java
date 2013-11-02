package keepass2android.javafilestorage;

import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.InputStream;
import java.io.UnsupportedEncodingException;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;

import keepass2android.javafilestorage.JavaFileStorageBase.InvalidPathException;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import com.google.api.services.drive.model.File;
import com.microsoft.live.LiveAuthClient;
import com.microsoft.live.LiveAuthException;
import com.microsoft.live.LiveAuthListener;
import com.microsoft.live.LiveConnectClient;
import com.microsoft.live.LiveConnectSession;
import com.microsoft.live.LiveOperation;
import com.microsoft.live.LiveOperationException;
import com.microsoft.live.LiveStatus;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;


public class SkyDriveFileStorage extends JavaFileStorageBase {
	
	private LiveAuthClient mAuthClient;

	private LiveConnectSession mSession;

	private LiveConnectClient mConnectClient;

	private String mRootFolderId;
	
	private HashMap<String /*id*/, SkyDriveObject> mFolderCache = new HashMap<String, SkyDriveObject>();
	
	public static final String[] SCOPES = {
        "wl.signin",
        "wl.skydrive_update",
    };
	
	public final class JsonKeys {
	    public static final String CODE = "code";
	    public static final String DATA = "data";
	    public static final String DESCRIPTION = "description";
	    public static final String ERROR = "error";
	    public static final String EMAIL_HASHES = "email_hashes";
	    public static final String FIRST_NAME = "first_name";
	    public static final String GENDER = "gender";
	    public static final String ID = "id";
	    public static final String IS_FAVORITE = "is_favorite";
	    public static final String IS_FRIEND = "is_friend";
	    public static final String LAST_NAME = "last_name";
	    public static final String LOCALE = "locale";
	    public static final String LINK = "link";
	    public static final String MESSAGE = "message";
	    public static final String NAME = "name";
	    public static final String UPDATED_TIME = "updated_time";
	    public static final String USER_ID = "user_id";
	    public static final String PERMISSIONS = "permissions";
	    public static final String IS_DEFAULT = "is_default";
	    public static final String FROM = "from";
	    public static final String SUBSCRIPTION_LOCATION = "subscription_location";
	    public static final String CREATED_TIME = "created_time";
	    public static final String LOCATION = "location";
	    public static final String TYPE = "type";
	    public static final String PARENT_ID = "parent_id";
	    public static final String SOURCE = "source";

	    private JsonKeys() {
	        throw new AssertionError();
	    }
	}
	
	

	
	class SkyDrivePath
	{
		String mPath;
		
		public SkyDrivePath() 
		{
		}
		
		public SkyDrivePath(String path) throws UnsupportedEncodingException, FileNotFoundException, InvalidPathException, LiveOperationException, SkyDriveException   
		{
			setPath(path);
		}
		
		public SkyDrivePath(String parentPath, JSONObject fileToAppend) throws UnsupportedEncodingException, FileNotFoundException, IOException, InvalidPathException, JSONException, LiveOperationException, SkyDriveException
		{
			setPath(parentPath);

			if ((!mPath.endsWith("/")) && (!mPath.equals("")))
				mPath = mPath + "/";
			mPath += encode(fileToAppend.getString("name"))+NAME_ID_SEP+encode(fileToAppend.getString("id"));
		}

		public void setPath(String path) throws UnsupportedEncodingException, InvalidPathException, FileNotFoundException, LiveOperationException, SkyDriveException {
			setPathWithoutVerify(path);
			verifyWithRetry();
		}
		

		private void verifyWithRetry() throws FileNotFoundException, LiveOperationException, SkyDriveException, UnsupportedEncodingException {
			try
			{
				verify();
			}
			catch (FileNotFoundException e)
			{
				initializeFoldersCache();				
				verify();
			}
		}

		
		public void setPathWithoutVerify(String path) throws UnsupportedEncodingException, InvalidPathException
		{
			mPath = path.substring(getProtocolPrefix().length());
			//Log.d(TAG, "  mAccount=" + mAccount);
			//Log.d(TAG, "  mAccountLocalPath=" + mAccountLocalPath);
		}
		
		
		//make sure the path exists
		private void verify() throws FileNotFoundException, UnsupportedEncodingException {
			
			if (mPath.equals(""))
				return;
			
			String[] parts = mPath.split("/");
			
			String parentId = mRootFolderId;
			
			for (int i=0;i<parts.length;i++)
			{
				String part = parts[i];
				//Log.d(TAG, "parsing part " + part);
				int indexOfSeparator = part.lastIndexOf(NAME_ID_SEP);
				if (indexOfSeparator < 0)
					throw new FileNotFoundException("invalid path " + mPath);
				String id = decode(part.substring(indexOfSeparator+NAME_ID_SEP.length()));
				String name = decode(part.substring(0, indexOfSeparator));
				//Log.d(TAG, "   name=" + name);
				SkyDriveObject thisFolder = mFolderCache.get(id);
				if (thisFolder == null)
				{
					thisFolder =  tryAddFileToCache(this);

					//check if it's still null
					if (thisFolder == null)
						throw new FileNotFoundException("couldn't find id " + id + " being part of "+ mPath+" in SkyDrive ");
				}
				if (thisFolder.getParentId().equals(parentId) == false)
					throw new FileNotFoundException("couldn't find parent id " + parentId + " as parent of "+thisFolder.getName() +" in  "+ mPath+" in SkyDrive");
				if (thisFolder.getName().equals(name) == false)
					throw new FileNotFoundException("Name of "+id+" changed from "+name+" to "+thisFolder.getName() +" in  "+ mPath+" in SkyDrive " );
				
				parentId = id;				
			}
			
		}
		

		public String getDisplayName()
		{
			//skydrive://
			String displayName = getProtocolPrefix();
						
			if (mPath.equals(""))
				return displayName;
			
			String[] parts = mPath.split("/");
			
			for (int i=0;i<parts.length;i++)
			{
				String part = parts[i];
				//Log.d(TAG, "parsing part " + part);
				int indexOfSeparator = part.lastIndexOf(NAME_ID_SEP);
				if (indexOfSeparator < 0)
				{
					//seems invalid, but we're very generous here
					displayName += "/"+part;
					continue;
				}
				String name = part.substring(0, indexOfSeparator);
				try {
					name = decode(name);
				} catch (UnsupportedEncodingException e) {
					//ignore
				}
				displayName += "/"+name;								
			}
			return displayName;
		}


		public String getSkyDriveId() throws InvalidPathException, UnsupportedEncodingException {
			String pathWithoutTrailingSlash = mPath;
			if (pathWithoutTrailingSlash.endsWith("/"))
				pathWithoutTrailingSlash = pathWithoutTrailingSlash.substring(0,pathWithoutTrailingSlash.length()-1);
			if (pathWithoutTrailingSlash.equals(""))
			{
				return mRootFolderId;
			}
			String lastPart = pathWithoutTrailingSlash.substring(pathWithoutTrailingSlash.lastIndexOf(NAME_ID_SEP)+NAME_ID_SEP.length());
			if (lastPart.contains("/"))
				throw new InvalidPathException("error extracting SkyDriveId from "+mPath);
			return decode(lastPart);
		}

		public String getFullPath() throws UnsupportedEncodingException {
			return getProtocolPrefix()+mPath;
		}


			
	};

	
	public SkyDriveFileStorage(String clientId, Context appContext)
	{
		mAuthClient = new LiveAuthClient(appContext, clientId);
        
	}
	
	void login(final FileStorageSetupActivity activity)
	{
		mAuthClient.login((Activity)activity,
                Arrays.asList(SCOPES),
                new LiveAuthListener() {
		  @Override
		  public void onAuthComplete(LiveStatus status,
		                             LiveConnectSession session,
		                             Object userState) {
		      if (status == LiveStatus.CONNECTED) {
	    	  	try
          		{
          			initializeSession(session);
          			finishActivityWithSuccess(activity);
          		}
          		catch (Exception e)
          		{
          			finishWithError((Activity)activity, e);
          		}
		      } else {
		          finishWithError((Activity)activity, new Exception("Error connecting to SkdDrive. Status is "+status));
		      }
		  }
		
		
		@Override
		  public void onAuthError(LiveAuthException exception, Object userState) {
		      finishWithError((Activity)activity, exception);
		  }
		});
	}
	
	private void initializeSession(LiveConnectSession session) throws LiveOperationException, SkyDriveException {
		  mSession = session;
	      mConnectClient = new LiveConnectClient(session);
	      
	      initializeFoldersCache();
	}

	
	private void initializeFoldersCache() throws LiveOperationException, SkyDriveException {
		mRootFolderId = "me/skydrive";
		
		LiveOperation operation= mConnectClient.get(mRootFolderId + "/files");
		
		JSONObject result = operation.getResult();
        checkResult(result);

        mFolderCache.clear();

        JSONArray data = result.optJSONArray(JsonKeys.DATA);
        for (int i = 0; i < data.length(); i++) {
            SkyDriveObject skyDriveObj = SkyDriveObject.create(data.optJSONObject(i));
            if (skyDriveObj == null)
            	continue; //ignored type
            mFolderCache.put(skyDriveObj.getId(), skyDriveObj);
        }
	}

	private void checkResult(JSONObject result) throws SkyDriveException {
		if (result.has(JsonKeys.ERROR)) {
            JSONObject error = result.optJSONObject(JsonKeys.ERROR);
            String message = error.optString(JsonKeys.MESSAGE);
            String code = error.optString(JsonKeys.CODE);
            throw new SkyDriveException(message, code);
        }
	}
	

	
	private SkyDriveObject tryAddFileToCache(SkyDrivePath skyDrivePath) {
		try
		{
			LiveOperation operation = mConnectClient.get(skyDrivePath.getSkyDriveId());
			JSONObject result = operation.getResult();
			checkResult(result);
			SkyDriveObject obj = SkyDriveObject.create(result);
			if (obj != null)
			{
				mFolderCache.put(obj.getId(), obj);
			}
			return obj;
		}
		catch (Exception e)
		{
			return null;
		}
		
	}

	@Override
	public boolean requiresSetup(String path) {
		//always go through the setup process:
		return true;
	}

	@Override
	public void startSelectFile(FileStorageSetupInitiatorActivity activity,
			boolean isForSave, int requestCode) {

		((JavaFileStorage.FileStorageSetupInitiatorActivity)(activity)).startSelectFileProcess(getProtocolId()+"://", isForSave, requestCode);
		
	}

	@Override
	public void prepareFileUsage(FileStorageSetupInitiatorActivity activity,
			String path, int requestCode) {
		((JavaFileStorage.FileStorageSetupInitiatorActivity)(activity)).startFileUsageProcess(path, requestCode);
		
	}

	@Override
	public String getProtocolId() {
		return "skydrive";
	}

	@Override
	public String getDisplayName(String path) {
		
		return "";
		/*
		SkyDrivePath skydrivePath = new SkyDrivePath();
		try {
			skydrivePath.setPathWithoutVerify(path);
		} catch (Exception e) {
			e.printStackTrace();
			return path;
		}
		return skydrivePath.getDisplayName();*/
	}

	@Override
	public boolean checkForFileChangeFast(String path,
			String previousFileVersion) throws Exception {
		// TODO Auto-generated method stub
		return false;
	}

	@Override
	public String getCurrentFileVersionFast(String path) {
		// TODO Auto-generated method stub
		return null;
	}

	@Override
	public InputStream openFileForRead(String path) throws Exception {
		// TODO Auto-generated method stub
		return null;
	}

	@Override
	public void uploadFile(String path, byte[] data, boolean writeTransactional)
			throws Exception {
		// TODO Auto-generated method stub
		
	}

	@Override
	public String createFolder(String parentPath, String newDirName)
			throws Exception {
		
		try
		{
			SkyDrivePath skyDriveParentPath = new SkyDrivePath(parentPath);
			
			JSONObject newFolder = new JSONObject();
			newFolder.put("name", newDirName);
			
			LiveOperation operation = mConnectClient.put(skyDriveParentPath.getSkyDriveId(), newFolder);
			JSONObject result = operation.getResult();
			checkResult(result);
			return new SkyDrivePath(parentPath, result).getFullPath();
		}
		catch(Exception e)
		{
			throw convertException(e);
		}
		
	}

	private Exception convertException(Exception e) throws Exception {
		
		e.printStackTrace();
		
		Log.w(TAG, e);
		
		throw e;
	}

	@Override
	public String createFilePath(String parentPath, String newFileName)
			throws Exception {
		// TODO Auto-generated method stub
		return null;
	}

	@Override
	public List<FileEntry> listFiles(String parentPath) throws Exception {
		// TODO Auto-generated method stub
		return null;
	}

	@Override
	public FileEntry getFileEntry(String filename) throws Exception {
		// TODO Auto-generated method stub
		return null;
	}

	@Override
	public void delete(String path) throws Exception {
		// TODO Auto-generated method stub
		
	}

	@Override
	public void onCreate(FileStorageSetupActivity activity,
			Bundle savedInstanceState) {
		
		
	}

	@Override
	public void onResume(FileStorageSetupActivity activity) {
		
		
	}
	
	private void finishWithError(final Activity activity,
			Exception error) {
		Log.e("KP2AJ", "Exception: "+error.toString());
		error.printStackTrace();
		
		Intent retData = new Intent();
		retData.putExtra(EXTRA_ERROR_MESSAGE, error.getMessage());
		activity.setResult(Activity.RESULT_CANCELED, retData);
		activity.finish();
	};


	private void finishActivityWithSuccess(FileStorageSetupActivity setupActivity) {
		Log.d("KP2AJ", "Success with authenticating!");
		Activity activity = (Activity)setupActivity;

		if (setupActivity.getProcessName().equals(PROCESS_NAME_FILE_USAGE_SETUP))
		{
			Intent data = new Intent();
			data.putExtra(EXTRA_IS_FOR_SAVE, setupActivity.isForSave());
			data.putExtra(EXTRA_PATH, setupActivity.getPath());
			activity.setResult(RESULT_FILEUSAGE_PREPARED, data);
			activity.finish();
			return;
		}
		if (setupActivity.getProcessName().equals(PROCESS_NAME_SELECTFILE))
		{
			Intent data = new Intent();

			String path = setupActivity.getState().getString(EXTRA_PATH);
			if (path != null)
				data.putExtra(EXTRA_PATH, path);
			activity.setResult(RESULT_FILECHOOSER_PREPARED, data);
			activity.finish();
			return;
		}	

		Log.w("KP2AJ", "Unknown process: " + setupActivity.getProcessName());


	}


	@Override
	public void onStart(final FileStorageSetupActivity activity) {
		 mAuthClient.initialize(Arrays.asList(SCOPES), new LiveAuthListener() {
	            @Override
	            public void onAuthError(LiveAuthException exception, Object userState) {
	            	finishWithError( ((Activity)activity), exception);
	            }

	            @Override
	            public void onAuthComplete(LiveStatus status,
	                                       LiveConnectSession session,
	                                       Object userState) {
	                
	            	if (status == LiveStatus.CONNECTED) {
	            		try
	            		{
	            			initializeSession(session);
	            			finishActivityWithSuccess(activity);
	            		}
	            		catch (Exception e)
	            		{
	            			finishWithError((Activity)activity, e);
	            		}
	                } else {
	                    login(activity);
	                }
	            }
	        });
		
	}

	@Override
	public void onActivityResult(FileStorageSetupActivity activity,
			int requestCode, int resultCode, Intent data) {
		
	}

}
