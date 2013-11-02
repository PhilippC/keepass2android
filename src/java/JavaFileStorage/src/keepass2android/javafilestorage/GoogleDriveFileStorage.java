package keepass2android.javafilestorage;

import java.io.ByteArrayInputStream;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.InputStream;
import java.io.UnsupportedEncodingException;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;

import com.google.api.client.extensions.android.http.AndroidHttp;
import com.google.api.client.googleapis.extensions.android.gms.auth.GoogleAccountCredential;
import com.google.api.client.googleapis.extensions.android.gms.auth.UserRecoverableAuthIOException;
import com.google.api.client.googleapis.json.GoogleJsonResponseException;
import com.google.api.client.http.ByteArrayContent;
import com.google.api.client.http.GenericUrl;
import com.google.api.client.http.HttpResponse;
import com.google.api.client.json.gson.GsonFactory;
import com.google.api.services.drive.Drive;
import com.google.api.services.drive.DriveScopes;
import com.google.api.services.drive.Drive.Files;
import com.google.api.services.drive.model.About;
import com.google.api.services.drive.model.File;
import com.google.api.services.drive.model.FileList;
import com.google.api.services.drive.model.ParentReference;

import android.accounts.AccountManager;
import android.app.Activity;
import android.content.Intent;
import android.os.AsyncTask;
import android.os.Bundle;
import android.util.Log;


public class GoogleDriveFileStorage extends JavaFileStorageBase {

	private static final String GDRIVE_PROTOCOL_ID = "gdrive";
	private static final String FOLDER_MIME_TYPE = "application/vnd.google-apps.folder";
	static final int MAGIC_GDRIVE=2082334;
	static final int REQUEST_ACCOUNT_PICKER = MAGIC_GDRIVE+1;
	static final int REQUEST_AUTHORIZATION = MAGIC_GDRIVE+2;

		
	
	class FileSystemEntryData
	{
		String displayName;
		String id;
		HashSet<String> parentIds = new HashSet<String>();
	};
	
	class AccountData
	{
		Drive drive;
		
		HashMap<String /*fileId*/, FileSystemEntryData> mFolderCache;

		protected String mRootFolderId;
	};
	
	HashMap<String /*accountName*/, AccountData> mAccountData = new HashMap<String, AccountData>();

	
	
	public static String getRootPathForAccount(String accountName) throws UnsupportedEncodingException {
		return GDRIVE_PROTOCOL_ID+"://"+encode(accountName)+"/";
	}
	
	class GDrivePath
	{
		String mAccount;
		String mAccountLocalPath; // the path after the "gdrive://account%40%0Agmail.com/"
		
		public GDrivePath() 
		{
		}
		
		public GDrivePath(String path) throws InvalidPathException, IOException 
		{
			setPath(path);
		}

		public void setPath(String path) throws 
				InvalidPathException, IOException {
			setPathWithoutVerify(path);
			verifyWithRetry();
		}
		
		public void setPathWithoutVerify(String path) throws UnsupportedEncodingException, InvalidPathException
		{
			//Log.d(TAG, "setPath: "+path);
			mAccount = extractAccount(path);
			mAccountLocalPath = path.substring(getProtocolPrefix().length()+encode(mAccount).length()+1);
			//Log.d(TAG, "  mAccount=" + mAccount);
			//Log.d(TAG, "  mAccountLocalPath=" + mAccountLocalPath);
		}
		
		public GDrivePath(String parentPath, File fileToAppend) throws UnsupportedEncodingException, FileNotFoundException, IOException, InvalidPathException
		{
			setPath(parentPath);

			if ((!mAccountLocalPath.endsWith("/")) && (!mAccountLocalPath.equals("")))
				mAccountLocalPath = mAccountLocalPath + "/";
			mAccountLocalPath += encode(fileToAppend.getTitle())+NAME_ID_SEP+fileToAppend.getId();
		}

		private void verifyWithRetry() throws IOException,
				FileNotFoundException {
			try
			{
				verify();
			}
			catch (FileNotFoundException e)
			{
				//the folders cache might be out of date -> rebuild and try again:
				AccountData accountData = mAccountData.get(mAccount);
				accountData.mFolderCache = buildFoldersCache(mAccount);
				
				verify();
			}
		}
		
		//make sure the path exists
		private void verify() throws FileNotFoundException, UnsupportedEncodingException {
			
			if (mAccountLocalPath.equals(""))
				return;
			
			String[] parts = mAccountLocalPath.split("/");
			
			AccountData accountData = mAccountData.get(mAccount);
			
			String parentId = accountData.mRootFolderId;
			
			for (int i=0;i<parts.length;i++)
			{
				String part = parts[i];
				//Log.d(TAG, "parsing part " + part);
				int indexOfSeparator = part.lastIndexOf(NAME_ID_SEP);
				if (indexOfSeparator < 0)
					throw new FileNotFoundException("invalid path " + mAccountLocalPath);
				String id = part.substring(indexOfSeparator+NAME_ID_SEP.length());
				String name = decode(part.substring(0, indexOfSeparator));
				//Log.d(TAG, "   name=" + name);
				FileSystemEntryData thisFolder = accountData.mFolderCache.get(id);
				if (thisFolder == null)
				{
					if (i== parts.length-1)
					{
						//not all files are cached
						thisFolder =  tryAddFileToCache(this);
					}
					//check if it's still null
					if (thisFolder == null)
						throw new FileNotFoundException("couldn't find id " + id + " being part of "+ mAccountLocalPath+" in GDrive account " + mAccount);
				}
				if (thisFolder.parentIds.contains(parentId) == false)
					throw new FileNotFoundException("couldn't find parent id " + parentId + " as parent of "+thisFolder.displayName +" in  "+ mAccountLocalPath+" in GDrive account " + mAccount);
				if (thisFolder.displayName.equals(name) == false)
					throw new FileNotFoundException("Name of "+id+" changed from "+name+" to "+thisFolder.displayName +" in  "+ mAccountLocalPath+" in GDrive account " + mAccount);
				
				parentId = id;				
			}
			
		}
		
		private String extractAccount(String path) throws InvalidPathException, UnsupportedEncodingException {
			if (!path.startsWith(getProtocolPrefix()))
				throw new InvalidPathException("Invalid path: "+path);
			String pathWithoutProtocol = path.substring(getProtocolPrefix().length());
			String accountNameEncoded = pathWithoutProtocol.substring(0, pathWithoutProtocol.indexOf("/"));
			return decode(accountNameEncoded);
		}
		
		public String getDisplayName()
		{
			//gdrive://
			String displayName = getProtocolPrefix();
			
			//gdrive//me@google.com/
			
			displayName += mAccount;
			
			if (mAccountLocalPath.equals(""))
				return displayName;
			
			String[] parts = mAccountLocalPath.split("/");
			
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


		public String getGDriveId() throws InvalidPathException, UnsupportedEncodingException {
			String pathWithoutTrailingSlash = mAccountLocalPath;
			if (pathWithoutTrailingSlash.endsWith("/"))
				pathWithoutTrailingSlash = pathWithoutTrailingSlash.substring(0,pathWithoutTrailingSlash.length()-1);
			if (pathWithoutTrailingSlash.equals(""))
			{
				return mAccountData.get(mAccount).mRootFolderId;
			}
			String lastPart = pathWithoutTrailingSlash.substring(pathWithoutTrailingSlash.lastIndexOf(NAME_ID_SEP)+NAME_ID_SEP.length());
			if (lastPart.contains("/"))
				throw new InvalidPathException("error extracting GDriveId from "+mAccountLocalPath);
			return decode(lastPart);
		}

		public String getFullPath() throws UnsupportedEncodingException {
			return getProtocolPrefix()+encode(mAccount)+"/"+mAccountLocalPath;
		}

		public String getAccount() {
			return mAccount;
		}


			
	};
	

	@Override
	public boolean checkForFileChangeFast(String path,
			String previousFileVersion) throws Exception {
		String currentVersion = getCurrentFileVersionFast(path);
		if (currentVersion == null)
			return false;
		return currentVersion.equals(previousFileVersion) == false;
	}

	public FileSystemEntryData tryAddFileToCache(GDrivePath path) {
		FileSystemEntryData thisFile = new FileSystemEntryData();
		
		File fl;
		try {
			fl = getDriveService(path.getAccount()).files().get(path.getGDriveId()).execute();
		} catch (Exception e) {
			e.printStackTrace();
			return null;
		}
		
		thisFile.id = fl.getId();
		thisFile.displayName = fl.getTitle();
		
		for (ParentReference parent: fl.getParents())
		{
			thisFile.parentIds.add(parent.getId());				
		}
		mAccountData.get(path.getAccount()).mFolderCache.put(thisFile.id, thisFile);
		/*try {
			Log.d(TAG, "Added "+path.getFullPath()+" to cache");
		} catch (UnsupportedEncodingException e) {
			e.printStackTrace();
		}*/
		return thisFile;
	}


	@Override
	public String getCurrentFileVersionFast(String path) {

		try {
			GDrivePath gdrivePath = new GDrivePath(path);
			return getFileForPath(gdrivePath, getDriveService(gdrivePath.getAccount())).getMd5Checksum();
		} catch (Exception e) {
			e.printStackTrace();
			return null;
		}
	}

	@Override
	public InputStream openFileForRead(String path) throws Exception {

		GDrivePath gdrivePath = new GDrivePath(path);
		Drive driveService = getDriveService(gdrivePath.getAccount());

		try
		{
			File file = getFileForPath(gdrivePath, driveService);
			return getFileContent(file, driveService);
		}
		catch (Exception e)
		{
			throw convertException(e);
		}
	}

	
	private File getFileForPath(GDrivePath path, Drive driveService)
			throws IOException, InvalidPathException {
		//Log.d(TAG,"getFileForPath... ");
		try
		{
			//throw new IOException("argh");
			String driveId = path.getGDriveId();
			//Log.d(TAG, "id"+driveId);
			File file = driveService.files().get(driveId).execute();
			//Log.d(TAG,"...done.");
			return file;
		}
		catch (IOException e)
		{
			e.printStackTrace();
			throw e;
		}
		catch (InvalidPathException e)
		{
			e.printStackTrace();
			throw e;
		}
		
		
	}

	private InputStream getFileContent(File driveFile, Drive driveService) throws IOException {
		if (driveFile.getDownloadUrl() != null && driveFile.getDownloadUrl().length() > 0) {

			GenericUrl downloadUrl = new GenericUrl(driveFile.getDownloadUrl());

			HttpResponse resp = driveService.getRequestFactory().buildGetRequest(downloadUrl).execute();
			return resp.getContent();
		} else {
			//return an empty input stream
			return new ByteArrayInputStream("".getBytes());
		}

	}

	
	@Override
	public void uploadFile(String path, byte[] data, boolean writeTransactional)
			throws Exception {
		
		ByteArrayContent content = new ByteArrayContent(null, data);
		GDrivePath gdrivePath = new GDrivePath(path);
		Drive driveService = getDriveService(gdrivePath.getAccount());
		try
		{
			File driveFile = getFileForPath(gdrivePath, driveService);
			getDriveService(gdrivePath.getAccount()).files()
					.update(driveFile.getId(), driveFile, content).execute();
		}
		catch (Exception e)
		{
			throw convertException(e);
		}

	}

	@Override
	public String createFolder(String parentPath, String newDirName) throws Exception {
		File body = new File();
		body.setTitle(newDirName);
		body.setMimeType(FOLDER_MIME_TYPE);
		
		GDrivePath parentGdrivePath = new GDrivePath(parentPath);
		
		body.setParents(
		          Arrays.asList(new ParentReference().setId(parentGdrivePath.getGDriveId())));
		try
		{
			File file = getDriveService(parentGdrivePath.getAccount()).files().insert(body).execute();
			
			//Log.d(TAG, "created folder "+newDirName+" in "+parentPath+". id: "+file.getId());

			//add to cache to avoid network traffic if this folder is accessed (which is likely to happen soon)
			FileSystemEntryData newCacheEntry = new FileSystemEntryData();
			newCacheEntry.displayName = newDirName;
			newCacheEntry.id = file.getId();
			newCacheEntry.parentIds.add(parentGdrivePath.getGDriveId());
			mAccountData.get(parentGdrivePath.getAccount()).mFolderCache.put(file.getId(), newCacheEntry);
	
			return new GDrivePath(parentPath, file).getFullPath();
		}
		catch (Exception e)
		{
			throw convertException(e);
		}

	}

	@Override
	public String createFilePath(String parentPath, String newFileName) throws Exception {
		File body = new File();
		body.setTitle(newFileName);
		GDrivePath parentGdrivePath = new GDrivePath(parentPath);
		
		body.setParents(
		          Arrays.asList(new ParentReference().setId(parentGdrivePath.getGDriveId())));
		try
		{
			File file = getDriveService(parentGdrivePath.getAccount()).files().insert(body).execute();
	
			return new GDrivePath(parentPath, file).getFullPath();
		}
		catch (Exception e)
		{
			throw convertException(e);
		}
	}


	
	@Override
	public List<FileEntry> listFiles(String parentPath) throws Exception {
		GDrivePath gdrivePath = new GDrivePath(parentPath);
		String parentId = gdrivePath.getGDriveId();

		List<FileEntry> result = new ArrayList<FileEntry>();
		
		Drive driveService = getDriveService(gdrivePath.getAccount());
		
		try
		{
		
			if (driveService.files().get(parentId).execute().getLabels().getTrashed())
				throw new FileNotFoundException(parentPath + " is trashed!");
			
			Files.List request = driveService.files().list()
					.setQ("trashed=false and '"+parentId+"' in parents");
	
			do {
				try {
					FileList files = request.execute();
	
					for (File file : files.getItems()) {
	
						String path = new GDrivePath(parentPath, file).getFullPath();
						FileEntry e = convertToFileEntry(file, path);
	
						result.add(e);
					}
					request.setPageToken(files.getNextPageToken());
				} catch (IOException e) {
					System.out.println("An error occurred: " + e);
					request.setPageToken(null);
					throw e;
				}
			} while (request.getPageToken() != null && request.getPageToken().length() > 0);
		}
		catch (Exception e)
		{
			throw convertException(e);
		}
		return result;

	}

	private Exception convertException(Exception e) {
		if (UserRecoverableAuthIOException.class.isAssignableFrom(e.getClass()))
		{
			//this is not really nice because it removes data from the cache which might still be valid but we don't have the account name here...
			mAccountData.clear();
		}
		if (GoogleJsonResponseException.class.isAssignableFrom(e.getClass()) )
		{
			GoogleJsonResponseException jsonEx = (GoogleJsonResponseException)e;
			if (jsonEx.getDetails().getCode() == 404)
				return new FileNotFoundException(jsonEx.getMessage());
		}
		
		return e;
		
	}


	private FileEntry convertToFileEntry(File file, String path) {
		FileEntry e = new FileEntry();
		e.canRead = e.canWrite = true; 
		e.isDirectory = FOLDER_MIME_TYPE.equals(file.getMimeType());
		e.lastModifiedTime = file.getModifiedDate().getValue();
		e.path = path; 
		try
		{
			e.sizeInBytes = file.getFileSize();			
		}
		catch (NullPointerException ex)
		{
			e.sizeInBytes = 0;
		}
		e.displayName = file.getTitle();
		return e;
	}



	@Override
	public FileEntry getFileEntry(String filename) throws Exception {
		
		try
		{
			//Log.d(TAG, "getFileEntry "+filename);
			GDrivePath gdrivePath = new GDrivePath(filename);
			FileEntry res =  convertToFileEntry(
					getFileForPath(gdrivePath, getDriveService(gdrivePath.getAccount())),
					filename);
			//Log.d(TAG, "getFileEntry res"+res);
			return res;
		}
		catch (Exception e)
		{
			Log.d(TAG, "Exception in getFileEntry! "+e);
			throw convertException(e);
		}
	}

	@Override
	public void delete(String path) throws Exception {
		
		GDrivePath gdrivePath = new GDrivePath(path);
		Drive driveService = getDriveService(gdrivePath.getAccount());
		try
		{
			driveService.files().delete(gdrivePath.getGDriveId()).execute();
			mAccountData.get(gdrivePath.getAccount()).mFolderCache.remove(gdrivePath.getGDriveId());
		}
		catch (Exception e)
		{
			throw convertException(e);
		}
	}


	private Drive createDriveService(String accountName, Activity activity) {
		GoogleAccountCredential credential = createCredential(activity);
		credential.setSelectedAccountName(accountName);

		return new Drive.Builder(AndroidHttp.newCompatibleTransport(), new GsonFactory(), credential)
		.setApplicationName("Keepass2Android")
		.build();
	}

	private Drive getDriveService(String accountName)
	{
		//Log.d(TAG, "getDriveService "+accountName);
		AccountData accountData = mAccountData.get(accountName);
		//Log.d(TAG, "accountData "+accountData);
		return accountData.drive;
	}

	@Override
	public void onActivityResult(final JavaFileStorage.FileStorageSetupActivity setupAct, int requestCode, int resultCode, Intent data) {
		//Log.d(TAG, "ActivityResult: "+requestCode+"/"+resultCode);
		switch (requestCode) {
		case REQUEST_ACCOUNT_PICKER:
			//Log.d(TAG, "ActivityResult: REQUEST_ACCOUNT_PICKER");
			if (resultCode == Activity.RESULT_OK && data != null && data.getExtras() != null) {
				String accountName = data.getStringExtra(AccountManager.KEY_ACCOUNT_NAME);
				if (accountName != null) {
					//Log.d(TAG, "Initialize Account name="+accountName);
					initializeAccountOrPath(setupAct, accountName);

					return;
				}
			}
			Log.i(TAG, "Error selecting account");
			//Intent retData = new Intent();
			//retData.putExtra(EXTRA_ERROR_MESSAGE, t.getMessage());
			((Activity)setupAct).setResult(Activity.RESULT_CANCELED, data);
			((Activity)setupAct).finish();

		case REQUEST_AUTHORIZATION:
			if (resultCode == Activity.RESULT_OK) {
				//for (String k: data.getExtras().keySet())
				//{
					//Log.d(TAG, data.getExtras().get(k).toString());
				//}
				String accountName = data.getStringExtra(AccountManager.KEY_ACCOUNT_NAME);
				if (accountName != null) {
					Log.d(TAG, "Account name="+accountName);
					initializeAccountOrPath(setupAct, accountName);
				}
				else
				{
					Log.d(TAG, "Account name is null");
				}
			} else {
				Log.i(TAG, "Error authenticating");
				//Intent retData = new Intent();
				//retData.putExtra(EXTRA_ERROR_MESSAGE, t.getMessage());
				((Activity)setupAct).setResult(Activity.RESULT_CANCELED, data);
				((Activity)setupAct).finish();
			}

		}

	}

	private void initializeAccountOrPath(final JavaFileStorage.FileStorageSetupActivity setupAct, final String accountNameOrPath) {

		final Activity activity = ((Activity)setupAct);
		
		String accountNameTemp;
		GDrivePath gdrivePath = null;
		if (accountNameOrPath.startsWith(getProtocolPrefix()))
		{
			gdrivePath = new GDrivePath();
			//don't verify yet, we're not yet initialized:
			try {
				gdrivePath.setPathWithoutVerify(accountNameOrPath);
			} catch (Exception e) {
				finishWithError((Activity)setupAct, e);
			}
			accountNameTemp = gdrivePath.getAccount();
		}
		else
			accountNameTemp = accountNameOrPath;
		
		final String accountName = accountNameTemp;
		
		AsyncTask<Object, Void, AsyncTaskResult<String> > task = new AsyncTask<Object, Void, AsyncTaskResult<String>>()
				{

			@Override
			protected AsyncTaskResult<String> doInBackground(Object... arg0) {
				try {
					
					
					if (!mAccountData.containsKey(accountName))
					{
						AccountData newAccountData = new AccountData();
						newAccountData.drive = createDriveService(accountName, activity);
						mAccountData.put(accountName, newAccountData);
						//Log.d(TAG, "Added account data for " + accountName);
						newAccountData.mFolderCache = buildFoldersCache(accountName);
						
						About about = newAccountData.drive.about().get().execute();
						newAccountData.mRootFolderId = about.getRootFolderId();
					}
					
					if (setupAct.getProcessName().equals(PROCESS_NAME_SELECTFILE))
						setupAct.getState().putString(EXTRA_PATH, getRootPathForAccount(accountName));
					
					return new AsyncTaskResult<String>("ok");
				} catch ( Exception anyError) {
					return new AsyncTaskResult<String>(anyError);
				}


			}



			@Override
			protected void onPostExecute(AsyncTaskResult<String> result) {
				Exception error = result.getError();
				if (error  != null ) {
					if (UserRecoverableAuthIOException.class.isAssignableFrom(error.getClass()))
					{
						mAccountData.remove(accountName);
						activity.startActivityForResult(((UserRecoverableAuthIOException)error).getIntent(), REQUEST_AUTHORIZATION);
					}
					else
					{
						finishWithError(activity, error);
					}
				}  else if ( isCancelled()) {
					// cancel handling here
					//Log.d(TAG,"Async Task cancelled!");

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
	


	private void finishWithError(final Activity activity,
			Exception error) {
		Log.e(TAG, "Exception: "+error.toString());
		error.printStackTrace();
		
		Intent retData = new Intent();
		retData.putExtra(EXTRA_ERROR_MESSAGE, error.getMessage());
		activity.setResult(Activity.RESULT_CANCELED, retData);
		activity.finish();
	};


	private HashMap<String,FileSystemEntryData> buildFoldersCache(String accountName) throws IOException {

		HashMap<String, FileSystemEntryData> folderCache = new HashMap<String, GoogleDriveFileStorage.FileSystemEntryData>();
		//Log.d(TAG,"buildFoldersCache");
		FileList folders=getDriveService(accountName).files().list().setQ("mimeType='"+FOLDER_MIME_TYPE+"' and trashed=false")
				.setFields("items(id,title,parents),nextPageToken")
				.execute();
		for(File fl: folders.getItems()){
			//Log.d(TAG,"buildFoldersCache: " + fl.getTitle());
			FileSystemEntryData thisFolder = new FileSystemEntryData();
			thisFolder.id = fl.getId();
			thisFolder.displayName = fl.getTitle();
			
			for (ParentReference parent: fl.getParents())
			{
				thisFolder.parentIds.add(parent.getId());				
			}
			folderCache.put(thisFolder.id, thisFolder);
		}

		//Log.d(TAG,"that's it!");
		return folderCache;

	}

	private void finishActivityWithSuccess(FileStorageSetupActivity setupActivity) {
		//Log.d("KP2AJ", "Success with authentcating!");
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
			/*if (setupActivity.getState() == null)
				Log.d(TAG, "getState is null");
			else 
				if (setupActivity.getState().getString(EXTRA_PATH) == null)
					Log.d(TAG, "setupActivity.getState().getString(EXTRA_PATH) is null");
				else
					Log.d(TAG,setupActivity.getState().getString(EXTRA_PATH));
					*/
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
	public void startSelectFile(JavaFileStorage.FileStorageSetupInitiatorActivity activity, boolean isForSave,
			int requestCode) {
		((JavaFileStorage.FileStorageSetupInitiatorActivity)(activity)).startSelectFileProcess(getProtocolPrefix(), isForSave, requestCode);		
	}


	@Override
	public void prepareFileUsage(JavaFileStorage.FileStorageSetupInitiatorActivity activity, String path, int requestCode) {
		((JavaFileStorage.FileStorageSetupInitiatorActivity)(activity)).startFileUsageProcess(path, requestCode);

	}

	@Override
	public String getProtocolId() {
		return GDRIVE_PROTOCOL_ID;
	}



	@Override
	public void onResume(JavaFileStorage.FileStorageSetupActivity setupAct) {

	}

	@Override
	public void onStart(final JavaFileStorage.FileStorageSetupActivity setupAct) {

		Activity activity = (Activity)setupAct;

		if (PROCESS_NAME_SELECTFILE.equals(setupAct.getProcessName()))
		{
			GoogleAccountCredential credential = createCredential(activity);

			//Log.d(TAG, "starting REQUEST_ACCOUNT_PICKER");
			activity.startActivityForResult(credential.newChooseAccountIntent(), REQUEST_ACCOUNT_PICKER);
		}

		if (PROCESS_NAME_FILE_USAGE_SETUP.equals(setupAct.getProcessName()))
		{
			initializeAccountOrPath(setupAct, setupAct.getPath());	
		}
	}

	private GoogleAccountCredential createCredential(Activity activity) {
		List<String> scopes = new ArrayList<String>();
		scopes.add(DriveScopes.DRIVE);
		GoogleAccountCredential credential = GoogleAccountCredential.usingOAuth2(activity.getApplicationContext(), scopes);
		return credential;
	}

	@Override
	public boolean requiresSetup(String path) {
		//always send the user through the prepare file usage workflow if he needs to authorize 
		return true;
	}

	@Override
	public void onCreate(FileStorageSetupActivity activity,
			Bundle savedInstanceState) {

	}


	@Override
	public String getDisplayName(String path) {
		GDrivePath gdrivePath = new GDrivePath();
		try {
			gdrivePath.setPathWithoutVerify(path);
		} catch (Exception e) {
			e.printStackTrace();
			return path;
		}
		return gdrivePath.getDisplayName();
	}

	
}
