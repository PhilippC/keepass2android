package keepass2android.javafilestorage;

import java.io.ByteArrayInputStream;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.InputStream;
import java.io.UnsupportedEncodingException;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;

import keepass2android.javafilestorage.skydrive.PrepareFileUsageListener;
import keepass2android.javafilestorage.skydrive.SkyDriveException;
import keepass2android.javafilestorage.skydrive.SkyDriveFile;
import keepass2android.javafilestorage.skydrive.SkyDriveFolder;
import keepass2android.javafilestorage.skydrive.SkyDriveObject;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import com.microsoft.live.LiveAuthClient;
import com.microsoft.live.LiveAuthException;
import com.microsoft.live.LiveAuthListener;
import com.microsoft.live.LiveConnectClient;
import com.microsoft.live.LiveConnectSession;
import com.microsoft.live.LiveDownloadOperation;
import com.microsoft.live.LiveOperation;
import com.microsoft.live.LiveOperationException;
import com.microsoft.live.LiveStatus;
import com.microsoft.live.OverwriteOption;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.AsyncTask;
import android.os.Bundle;
import android.util.Log;

public class SkyDriveFileStorage extends JavaFileStorageBase {

	private LiveAuthClient mAuthClient;

	private LiveConnectClient mConnectClient;

	private String mRootFolderId;

	private HashMap<String /* id */, SkyDriveObject> mFolderCache = new HashMap<String, SkyDriveObject>();

	public static final String[] SCOPES = { "wl.signin", "wl.skydrive_update", "wl.offline_access"};

	// see http://stackoverflow.com/questions/17997688/howto-to-parse-skydrive-api-date-in-java
	SimpleDateFormat SKYDRIVE_DATEFORMATTER = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ssZ", Locale.ENGLISH);

	private Context mAppContext;

	private String mClientId;

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

	class SkyDrivePath {
		String mPath;

		public SkyDrivePath() {
		}

		public SkyDrivePath(String path) throws UnsupportedEncodingException,
				FileNotFoundException, InvalidPathException,
				LiveOperationException, SkyDriveException {
			setPath(path);
		}

		public SkyDrivePath(String parentPath, JSONObject fileToAppend)
				throws UnsupportedEncodingException, FileNotFoundException,
				IOException, InvalidPathException, JSONException,
				LiveOperationException, SkyDriveException {
			setPath(parentPath);

			if ((!mPath.endsWith("/")) && (!mPath.equals("")))
				mPath = mPath + "/";
			mPath += encode(fileToAppend.getString("name")) + NAME_ID_SEP
					+ encode(fileToAppend.getString("id"));
		}

		public void setPath(String path) throws UnsupportedEncodingException,
				InvalidPathException, FileNotFoundException,
				LiveOperationException, SkyDriveException {
			setPathWithoutVerify(path);
			verifyWithRetry();
		}

		private void verifyWithRetry() throws FileNotFoundException,
				LiveOperationException, SkyDriveException,
				UnsupportedEncodingException {
			try {
				verify();
			} catch (FileNotFoundException e) {
				initializeFoldersCache();
				verify();
			}
		}

		public void setPathWithoutVerify(String path)
				throws UnsupportedEncodingException, InvalidPathException {
			mPath = path.substring(getProtocolPrefix().length());
			logDebug( "  mPath=" + mPath);
		}

		// make sure the path exists
		private void verify() throws FileNotFoundException,
				UnsupportedEncodingException {

			if (mPath.equals(""))
				return;

			String[] parts = mPath.split("/");

			String parentId = mRootFolderId;

			for (int i = 0; i < parts.length; i++) {
				String part = parts[i];
				logDebug( "parsing part " + part);
				int indexOfSeparator = part.lastIndexOf(NAME_ID_SEP);
				if (indexOfSeparator < 0)
					throw new FileNotFoundException("invalid path " + mPath);
				String id = decode(part.substring(indexOfSeparator
						+ NAME_ID_SEP.length()));
				String name = decode(part.substring(0, indexOfSeparator));
				logDebug( "   name=" + name);
				SkyDriveObject thisFolder = mFolderCache.get(id);
				if (thisFolder == null) {
					logDebug( "adding to cache");
					thisFolder = tryAddToCache(id);

					// check if it's still null
					if (thisFolder == null)
						throw new FileNotFoundException("couldn't find id "
								+ id + " being part of " + mPath
								+ " in SkyDrive ");
				}
				if (thisFolder.getParentId().equals(parentId) == false)
					throw new FileNotFoundException("couldn't find parent id "
							+ parentId + " as parent of "
							+ thisFolder.getName() + " in  " + mPath
							+ " in SkyDrive");
				if (thisFolder.getName().equals(name) == false)
					throw new FileNotFoundException("Name of " + id
							+ " changed from " + name + " to "
							+ thisFolder.getName() + " in  " + mPath
							+ " in SkyDrive ");

				parentId = id;
			}

		}

		public String getDisplayName() {
			// skydrive://
			String displayName = getProtocolPrefix();

			if (mPath.equals(""))
				return displayName;

			String[] parts = mPath.split("/");

			for (int i = 0; i < parts.length; i++) {
				String part = parts[i];
				logDebug("parsing part " + part);
				int indexOfSeparator = part.lastIndexOf(NAME_ID_SEP);
				if (indexOfSeparator < 0) {
					// seems invalid, but we're very generous here
					displayName += "/" + part;
					continue;
				}
				String name = part.substring(0, indexOfSeparator);
				try {
					name = decode(name);
				} catch (UnsupportedEncodingException e) {
					// ignore
				}
				displayName += "/" + name;
			}
			return displayName;
		}

		public String getSkyDriveId() throws InvalidPathException,
				UnsupportedEncodingException {
			String pathWithoutTrailingSlash = mPath;
			if (pathWithoutTrailingSlash.endsWith("/"))
				pathWithoutTrailingSlash = pathWithoutTrailingSlash.substring(
						0, pathWithoutTrailingSlash.length() - 1);
			if (pathWithoutTrailingSlash.equals("")) {
				return mRootFolderId;
			}
			String lastPart = pathWithoutTrailingSlash
					.substring(pathWithoutTrailingSlash
							.lastIndexOf(NAME_ID_SEP) + NAME_ID_SEP.length());
			if (lastPart.contains("/"))
				throw new InvalidPathException(
						"error extracting SkyDriveId from " + mPath);
			return decode(lastPart);
		}

		public String getFullPath() throws UnsupportedEncodingException {
			return getProtocolPrefix() + mPath;
		}

		public SkyDrivePath getParentPath() throws UnsupportedEncodingException, FileNotFoundException, InvalidPathException, LiveOperationException, SkyDriveException {
			String pathWithoutTrailingSlash = mPath;
			if (pathWithoutTrailingSlash.endsWith("/"))
				pathWithoutTrailingSlash = pathWithoutTrailingSlash.substring(
						0, pathWithoutTrailingSlash.length() - 1);
			if (pathWithoutTrailingSlash.equals(""))
			{
				return null;
			}
			int indexOfLastSlash = pathWithoutTrailingSlash.lastIndexOf("/");
			if (indexOfLastSlash == -1)
			{
				return new SkyDrivePath(getProtocolPrefix());
			}
			String parentPath = pathWithoutTrailingSlash.substring(0, indexOfLastSlash);
			return new SkyDrivePath(getProtocolPrefix()+parentPath);
		}

		public String getFilename() throws InvalidPathException {
			String pathWithoutTrailingSlash = mPath;
			if (pathWithoutTrailingSlash.endsWith("/"))
			pathWithoutTrailingSlash = pathWithoutTrailingSlash.substring(
					0, pathWithoutTrailingSlash.length() - 1);
			
			String[] parts = mPath.split("/");

			String lastPart = parts[parts.length-1];
			int indexOfSeparator = lastPart.lastIndexOf(NAME_ID_SEP);
			if (indexOfSeparator < 0) {
				throw new InvalidPathException("cannot extract filename from " + mPath);
			}
			String name = lastPart.substring(0, indexOfSeparator);
			try {
				name = decode(name);
			} catch (UnsupportedEncodingException e) {
				// ignore
			}
			return name;
		
		
		}

	};

	public SkyDriveFileStorage(String clientId, Context appContext) {
		logDebug("Constructing SkyDriveFileStorage");
		mAuthClient = new LiveAuthClient(appContext, clientId);
		mAppContext = appContext;
		mClientId = clientId;

	}

	void login(final FileStorageSetupActivity activity) {
		logDebug("skydrive login");
		mAuthClient.login((Activity) activity, Arrays.asList(SCOPES),
				new LiveAuthListener() {
					@Override
					public void onAuthComplete(LiveStatus status,
							LiveConnectSession session, Object userState) {
						if (status == LiveStatus.CONNECTED) {
							initialize(activity, session);
						} else {
							finishWithError(activity, new Exception(
									"Error connecting to SkdDrive. Status is "
											+ status));
						}
					}

					@Override
					public void onAuthError(LiveAuthException exception,
							Object userState) {
						finishWithError(activity, exception);
					}
				});
	}

	private void initialize(final FileStorageSetupActivity setupAct,
			LiveConnectSession session) {
		logDebug("skydrive initialize");
		mConnectClient = new LiveConnectClient(session);

		final Activity activity = (Activity)setupAct;
		
		AsyncTask<Object, Void, AsyncTaskResult<String> > task = new AsyncTask<Object, Void, AsyncTaskResult<String>>()
		{

			@Override
			protected AsyncTaskResult<String> doInBackground(Object... arg0) {
				try {
					initializeFoldersCache();
					if (setupAct.getProcessName().equals(PROCESS_NAME_SELECTFILE))
						setupAct.getState().putString(EXTRA_PATH, getProtocolPrefix());
					return new AsyncTaskResult<String>("ok");
				} catch ( Exception anyError) {
					return new AsyncTaskResult<String>(anyError);
				}
			}

			@Override
			protected void onPostExecute(AsyncTaskResult<String> result) {
				Exception error = result.getError();
				if (error  != null ) {
					finishWithError(setupAct, error);
				}  else if ( isCancelled()) {
					activity.setResult(Activity.RESULT_CANCELED);
					activity.finish();
				} else {
					//all right!
					finishActivityWithSuccess(setupAct);
				}
			}
		};

		task.execute(new Object[]{});

	}
	

	
	private void initializeFoldersCache() throws LiveOperationException,
			SkyDriveException, FileNotFoundException {
		
		logDebug("skydrive initializeFoldersCache");
		//use alias for now (overwritten later):
		mRootFolderId = "me/skydrive";

		LiveOperation operation = mConnectClient.get(mRootFolderId + "/files");

		JSONObject result = operation.getResult();
		checkResult(result);

		mFolderCache.clear();

		JSONArray data = result.optJSONArray(JsonKeys.DATA);
		for (int i = 0; i < data.length(); i++) {
			SkyDriveObject skyDriveObj = SkyDriveObject.create(data
					.optJSONObject(i));
			if (skyDriveObj == null)
				continue; // ignored type
			logDebug( "adding "+skyDriveObj.getName()+" to cache with id " + skyDriveObj.getId()+" in "+skyDriveObj.getParentId());
			mFolderCache.put(skyDriveObj.getId(), skyDriveObj);
			
			mRootFolderId = skyDriveObj.getParentId();
		}
		
		//check if we received anything. If not: query the root folder directly
		if (data.length() == 0)
		{
			operation = mConnectClient.get(mRootFolderId);
			result = operation.getResult();
			checkResult(result);
			mRootFolderId = SkyDriveObject.create(result).getId();
			
		}
	}

	private void checkResult(JSONObject result) throws SkyDriveException, FileNotFoundException {
		if (result.has(JsonKeys.ERROR)) {
			JSONObject error = result.optJSONObject(JsonKeys.ERROR);
			String message = error.optString(JsonKeys.MESSAGE);
			String code = error.optString(JsonKeys.CODE);
			logDebug( "Code: "+code);
			if ("resource_not_found".equals(code))
				throw new FileNotFoundException(message);
			else
				throw new SkyDriveException(message, code);
		}
	}
	
	private SkyDriveObject tryAddToCache(String skyDriveId) {
		try {
			SkyDriveObject obj = getSkyDriveObject(skyDriveId);
			if (obj != null) {
				mFolderCache.put(obj.getId(), obj);
			}
			return obj;
		} catch (Exception e) {
			return null;
		}

	}

	private SkyDriveObject getSkyDriveObject(SkyDrivePath skyDrivePath)
			throws LiveOperationException, InvalidPathException,
			UnsupportedEncodingException, SkyDriveException, FileNotFoundException {
		String skyDriveID = skyDrivePath.getSkyDriveId();
		return getSkyDriveObject(skyDriveID);
	}

	private SkyDriveObject getSkyDriveObject(String skyDriveID)
			throws LiveOperationException, SkyDriveException,
			FileNotFoundException {
		LiveOperation operation = mConnectClient.get(skyDriveID);
		JSONObject result = operation.getResult();
		checkResult(result);
		SkyDriveObject obj = SkyDriveObject.create(result);
		return obj;
	}

	@Override
	public boolean requiresSetup(String path) {
		// always go through the setup process:
		return true;
	}

	@Override
	public void startSelectFile(FileStorageSetupInitiatorActivity activity,
			boolean isForSave, int requestCode) {

		((JavaFileStorage.FileStorageSetupInitiatorActivity) (activity))
				.startSelectFileProcess(getProtocolId() + "://", isForSave,
						requestCode);

	}

	@Override
	public void prepareFileUsage(FileStorageSetupInitiatorActivity activity,
			String path, int requestCode, boolean alwaysReturnSuccess) {
		
		//tell the activity which requests the file usage that it must launch the FileStorageSetupActivity
		// which will then go through the onCreate/onStart/onResume process which is used by our FileStorage
		((JavaFileStorage.FileStorageSetupInitiatorActivity) (activity))
				.startFileUsageProcess(path, requestCode, alwaysReturnSuccess);

	}
	@Override
	public void prepareFileUsage(Context appContext, String path) throws Exception 
	{
		
		PrepareFileUsageListener listener = new PrepareFileUsageListener();
		
		mAuthClient.initializeSynchronous(Arrays.asList(SCOPES), listener, null);
		
		
		if (listener.exception != null)
			throw listener.exception;
		
		if (listener.status == LiveStatus.CONNECTED) {
			
			mConnectClient = new LiveConnectClient(listener.session);
			if (mFolderCache.isEmpty())
			{
				initializeFoldersCache();
			}

		} else {
			if (listener.status == LiveStatus.NOT_CONNECTED)
				logDebug( "not connected");
			else if (listener.status == LiveStatus.UNKNOWN)
				logDebug( "unknown");
			else
				logDebug( "unexpected status " + listener.status);
			
			throw new UserInteractionRequiredException();			
			
		}


	}

	@Override
	public String getProtocolId() {
		return "skydrive";
	}

	@Override
	public String getDisplayName(String path) {

		SkyDrivePath skydrivePath = new SkyDrivePath(); 
		try {
		  skydrivePath.setPathWithoutVerify(path); 
		} 
		catch (Exception e) 
		{
		  e.printStackTrace(); 
		  return path; 
		} 
		
		return skydrivePath.getDisplayName();
		
	}

	@Override
	public boolean checkForFileChangeFast(String path,
			String previousFileVersion) throws Exception {
		
		String currentVersion = getCurrentFileVersionFast(path);
		if (currentVersion == null)
			return false;
		return currentVersion.equals(previousFileVersion) == false;
	}

	@Override
	public String getCurrentFileVersionFast(String path) {
		try
		{
			SkyDrivePath drivePath = new SkyDrivePath(path);
			SkyDriveObject obj = getSkyDriveObject(drivePath);
			if (obj == null)
				return null;
			return obj.getUpdatedTime();
		}
		catch (Exception e)
		{
			logDebug("Error getting file version.");
			Log.w(TAG,"Error getting file version:");
			e.printStackTrace();
			return null;
		}
		
	}

	@Override
	public InputStream openFileForRead(String path) throws Exception {
		try
		{
			logDebug("openFileForRead: " + path);
			LiveDownloadOperation op = mConnectClient.download(new SkyDrivePath(path).getSkyDriveId()+"/content");
			logDebug("openFileForRead ok" + path);
			return op.getStream();
		}
		catch (Exception e)
		{
			throw convertException(e);
		}
	}	

	@Override
	public void uploadFile(String path, byte[] data, boolean writeTransactional)
			throws Exception {

		try
		{
			logDebug("uploadFile to "+path);
			SkyDrivePath driveTargetPath = new SkyDrivePath(path);
			SkyDrivePath driveUploadPath = driveTargetPath;
			SkyDrivePath driveTempPath = null;
			ByteArrayInputStream bis = new ByteArrayInputStream(data);
			
			//if writeTransactional, upload the file to a temp destination.
			//this is a somewhat ugly way because it requires two uploads, but renaming/copying doesn't work 
			//nicely in SkyDrive, and SkyDrive doesn't provide file histories by itself, so we need to make sure
			//no file gets corrupt if upload is canceled.
			if (writeTransactional)
			{
				LiveOperation uploadOp = uploadFile(driveUploadPath.getParentPath(), driveUploadPath.getFilename()+".tmp", bis);
				driveTempPath = new SkyDrivePath(driveUploadPath.getParentPath().getFullPath(), uploadOp.getResult());
				//recreate ByteArrayInputStream for use in uploadFile below
				bis = new ByteArrayInputStream(data);
			}
			
			//upload the file
			uploadFile(driveUploadPath.getParentPath(), driveUploadPath.getFilename(), bis);
			
			if (writeTransactional)
			{
				//delete old file
				mConnectClient.delete(driveTempPath.getSkyDriveId());
				// don't check result. If delete fails -> not a big deal
			}
			
		}
		catch (Exception e)
		{
			throw convertException(e);
		}

	}

	private LiveOperation uploadFile(SkyDrivePath parentPath, String filename, ByteArrayInputStream bis)
			throws LiveOperationException, InvalidPathException,
			UnsupportedEncodingException, FileNotFoundException,
			SkyDriveException {
		LiveOperation op = mConnectClient.upload(parentPath.getSkyDriveId(), filename, bis, OverwriteOption.Overwrite);
		checkResult(op.getResult());
		return op;
	}

	@Override
	public String createFolder(String parentPath, String newDirName)
			throws Exception {

		try {
			SkyDrivePath skyDriveParentPath = new SkyDrivePath(parentPath);
			
			String parentId = skyDriveParentPath.getSkyDriveId();

			JSONObject newFolder = new JSONObject();
			newFolder.put("name", newDirName);
			newFolder.put("description", "folder");

			LiveOperation operation = mConnectClient.post(
					parentId, newFolder);
			JSONObject result = operation.getResult();
			checkResult(result);
			return new SkyDrivePath(parentPath, result).getFullPath();
		} catch (Exception e) {
			throw convertException(e);
		}

	}

	private Exception convertException(Exception e) throws Exception {

		e.printStackTrace();
		logDebug(e.toString());
		Log.w(TAG, e);

		throw e;
	}

	@Override
	public String createFilePath(String parentPath, String newFileName)
			throws Exception {
		try {
			SkyDrivePath skyDriveParentPath = new SkyDrivePath(parentPath);
			
			LiveOperation op = uploadFile(skyDriveParentPath, newFileName, new ByteArrayInputStream(new byte[0]));
			checkResult(op.getResult());
			
			return new SkyDrivePath(parentPath, op.getResult()).getFullPath();
		} catch (Exception e) {
			throw convertException(e);
		}
	}

	@Override
	public List<FileEntry> listFiles(String parentPath) throws Exception {
		
		try
		{
			SkyDrivePath parentDrivePath = new SkyDrivePath(parentPath);
			LiveOperation operation = mConnectClient.get(parentDrivePath.getSkyDriveId() + "/files");
	
			JSONObject result = operation.getResult();
			checkResult(result);
	
			JSONArray data = result.optJSONArray(JsonKeys.DATA);
			List<FileEntry> resultList = new ArrayList<FileEntry>(data.length());
	
			for (int i = 0; i < data.length(); i++) {
				SkyDriveObject skyDriveObj = SkyDriveObject.create(data
						.optJSONObject(i));
				if (skyDriveObj == null)
					continue; // ignored type
				logDebug( "listing "+skyDriveObj.getName()+" with id " + skyDriveObj.getId()+" in "+skyDriveObj.getParentId());
				
				resultList.add(convertToFileEntry(parentDrivePath, skyDriveObj));
			}	
			return resultList;
		}
		catch (Exception e)
		{
			throw convertException(e);
		}
	}

	private FileEntry convertToFileEntry(SkyDrivePath parentPath, SkyDriveObject skyDriveObj) throws UnsupportedEncodingException, FileNotFoundException, IOException, InvalidPathException, JSONException, LiveOperationException, SkyDriveException {
		
		FileEntry res = new FileEntry();
		res.canRead = true;
		res.canWrite = true;
		res.displayName = skyDriveObj.getName();
		res.isDirectory = SkyDriveFolder.class.isAssignableFrom(skyDriveObj.getClass());
		
		try
		{
			res.lastModifiedTime = SKYDRIVE_DATEFORMATTER.parse(skyDriveObj.getUpdatedTime()).getTime();
		}
		catch (Exception e)
		{
			Log.w(TAG, "Cannot parse time " + skyDriveObj.getUpdatedTime());
			logDebug("Cannot parse time! " + e.toString());
			res.lastModifiedTime = -1;
		}
		if (parentPath == null) //this is the case if we're listing the parent path itself
			res.path = getProtocolPrefix();
		else
			res.path = new SkyDrivePath(parentPath.getFullPath(), skyDriveObj.toJson()).getFullPath();
		logDebug( "path: "+res.path);
		if (SkyDriveFile.class.isAssignableFrom(skyDriveObj.getClass()))
		{
			res.sizeInBytes = ((SkyDriveFile)skyDriveObj).getSize();
		}
		
		return res;
	}

	@Override
	public FileEntry getFileEntry(String filename) throws Exception {
		try
		{
			SkyDrivePath drivePath = new SkyDrivePath(filename);
			logDebug( "getFileEntry for "+ filename +" = "+drivePath.getFullPath());
			logDebug( " parent is "+drivePath.getParentPath());
			return convertToFileEntry(drivePath.getParentPath(),getSkyDriveObject(drivePath));
		}
		catch (Exception e)
		{
			throw convertException(e);
		}
		
	}

	@Override
	public void delete(String path) throws Exception {
		try
		{
			SkyDrivePath drivePath = new SkyDrivePath(path);
			LiveOperation op = mConnectClient.delete(drivePath.getSkyDriveId());
			checkResult(op.getResult());
		}
		catch (Exception e)
		{
			throw convertException(e);
		}
		

	}

	@Override
	public void onCreate(FileStorageSetupActivity activity,
			Bundle savedInstanceState) {

	}

	@Override
	public void onResume(FileStorageSetupActivity activity) {

	}


	@Override
	public void onStart(final FileStorageSetupActivity activity) {
		try
		{
			logDebug("skydrive onStart");
			initialize(activity);
		}
		catch (Exception e)
		{
			finishWithError(activity, e);
		}
	}

	private void initialize(final FileStorageSetupActivity activity) {
		mAuthClient.initialize(Arrays.asList(SCOPES), new LiveAuthListener() {
			@Override
			public void onAuthError(LiveAuthException exception,
					Object userState) {
				finishWithError(( activity), exception);
			}

			@Override
			public void onAuthComplete(LiveStatus status,
					LiveConnectSession session, Object userState) {

				if (status == LiveStatus.CONNECTED) {
					logDebug("connected!");
					initialize(activity, session);

				} else {
					if (status == LiveStatus.NOT_CONNECTED)
						logDebug( "not connected");
					else if (status == LiveStatus.UNKNOWN)
						logDebug( "unknown");
					else
						logDebug( "unexpected status " + status);
					try
					{
						login(activity);
					}
					catch (IllegalStateException e)
					{
						//this may happen if an un-cancelled login progress is already in progress.
						//however, the activity might have been destroyed, so try again with another auth client next time
						logDebug("IllegalStateException: Recreating AuthClient");
						mAuthClient = new LiveAuthClient(mAppContext, mClientId);
						finishWithError(activity, e);
					}
					catch (Exception e)
					{
						finishWithError(activity, e);
					}
				}
			}
		});
	}

	@Override
	public void onActivityResult(FileStorageSetupActivity activity,
			int requestCode, int resultCode, Intent data) {

	}

	@Override
	public String getFilename(String path) throws Exception {
		SkyDrivePath p = new SkyDrivePath();
		p.setPathWithoutVerify(path);
		return p.getFilename();
	}

}
