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

import android.Manifest;
import android.accounts.AccountManager;
import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.AsyncTask;
import android.os.Build;
import android.os.Bundle;
import android.text.TextUtils;

import static android.content.pm.PackageManager.PERMISSION_GRANTED;


public class GoogleDriveFileStorage extends JavaFileStorageBase {

	private static final String GDRIVE_PROTOCOL_ID = "gdrive";
	private static final String FOLDER_MIME_TYPE = "application/vnd.google-apps.folder";
	static final int MAGIC_GDRIVE=2082334;
	static final int REQUEST_ACCOUNT_PICKER = MAGIC_GDRIVE+1;
	static final int REQUEST_AUTHORIZATION = MAGIC_GDRIVE+2;
	private boolean mRequiresRuntimePermissions = false;


	class FileSystemEntryData
	{
		String displayName;
		String id;
		HashSet<String> parentIds = new HashSet<String>();
	};
	
	class AccountData
	{
		//guaranteed to be set if AccountData is in HashMap
		Drive drive;
		
		//may be null if first initialization failed
		protected String mRootFolderId;
	};
	
	HashMap<String /*accountName*/, AccountData> mAccountData = new HashMap<String, AccountData>();

	
	
	public String getRootPathForAccount(String accountName) throws UnsupportedEncodingException {
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
			verify();
		}
		
		public void setPathWithoutVerify(String path) throws UnsupportedEncodingException, InvalidPathException
		{
			logDebug("setPath: "+path);
			mAccount = extractAccount(path);
			mAccountLocalPath = path.substring(getProtocolPrefix().length()+encode(mAccount).length()+1);
			logDebug("  mAccount=" + mAccount);
			logDebug("  mAccountLocalPath=" + mAccountLocalPath);
		}
		
		public GDrivePath(String parentPath, File fileToAppend) throws UnsupportedEncodingException, FileNotFoundException, IOException, InvalidPathException
		{
			setPath(parentPath);

			if ((!mAccountLocalPath.endsWith("/")) && (!mAccountLocalPath.equals("")))
				mAccountLocalPath = mAccountLocalPath + "/";
			mAccountLocalPath += encode(fileToAppend.getTitle())+NAME_ID_SEP+fileToAppend.getId();
		}
		
		//make sure the path exists
		/*Note: in earlier versions, this method checked the full path. This was causing trouble 
		 * for some users, it seems like the IDs of parent folders can behave unexpectedly.
		 * Now the display name does no longer contain the parent folders, which is why it is no longer 
		 * necessary to check if they were renamed.
		 * (The path still contains the parents for file browsing, but this is only required temporarily 
		 * during setup where everything seems fine.)*/
		private void verify() throws IOException {
			
			if (mAccountLocalPath.equals(""))
				return;
			
			String[] parts = mAccountLocalPath.split("/");
			
			AccountData accountData = mAccountData.get(mAccount);
			if (accountData == null)
			{
				throw new IllegalStateException("Looks like account "+mAccount+" was not properly initialized!");
			}
			
			//if initialization failed, try to repeat: 
			finishInitialization(accountData, mAccount);
			
			String part = parts[parts.length-1];
			logDebug("parsing part " + part);
			int indexOfSeparator = part.lastIndexOf(NAME_ID_SEP);
			if (indexOfSeparator < 0)
				throw new FileNotFoundException("invalid path " + mAccountLocalPath);
			String id = part.substring(indexOfSeparator+NAME_ID_SEP.length());
			String name = decode(part.substring(0, indexOfSeparator));
			logDebug("   name=" + name);
			
			File fl;
			try {
				fl = getDriveService(getAccount()).files().get(getGDriveId()).execute();
			} catch (Exception e) {
				e.printStackTrace();
				throw new FileNotFoundException("error getting file with for "+ this.getFullPath());
			}
			
			String displayName = fl.getTitle();
			
			if (displayName.equals(name) == false)
				throw new FileNotFoundException("Name of "+id+" changed from "+name+" to "+displayName +" in  "+ mAccountLocalPath+" in GDrive account " + mAccount);
			
		}
		
		private String extractAccount(String path) throws InvalidPathException, UnsupportedEncodingException {
			if (!path.startsWith(getProtocolPrefix()))
				throw new InvalidPathException("Invalid path: "+path);
			String pathWithoutProtocol = path.substring(getProtocolPrefix().length());
			int slashPos = pathWithoutProtocol.indexOf("/");
			String accountNameEncoded;
			if (slashPos < 0)
				accountNameEncoded = pathWithoutProtocol;
			else
				accountNameEncoded = pathWithoutProtocol.substring(0, slashPos);
			return decode(accountNameEncoded);
		}
		
		public String getDisplayName()
		{
			//gdrive://
			String displayName = getProtocolPrefix();
			
			//gdrive://me@google.com/
			
			displayName += mAccount;
			
			if (mAccountLocalPath.equals(""))
				return displayName;
			
			String[] parts = mAccountLocalPath.split("/");
			
			String part = parts[parts.length-1];
			logDebug("parsing part " + part);
			int indexOfSeparator = part.lastIndexOf(NAME_ID_SEP);
			if (indexOfSeparator < 0)
			{
				//seems invalid, but we're very generous here
				displayName += "/"+part;
			}
			String name = part.substring(0, indexOfSeparator);
			try {
				name = decode(name);
			} catch (UnsupportedEncodingException e) {
				//ignore
			}
			displayName += "/"+name;								
		
			return displayName;
		}


		public String getGDriveId() throws InvalidPathException, IOException {
			String pathWithoutTrailingSlash = mAccountLocalPath;
			if (pathWithoutTrailingSlash.endsWith("/"))
				pathWithoutTrailingSlash = pathWithoutTrailingSlash.substring(0,pathWithoutTrailingSlash.length()-1);
			if (pathWithoutTrailingSlash.equals(""))
			{
				AccountData accountData = mAccountData.get(mAccount);
				finishInitialization(accountData, mAccount);
				return accountData.mRootFolderId;
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

		public String getFilename() throws InvalidPathException {
			
			String[] parts = mAccountLocalPath.split("/");

			String lastPart = parts[parts.length-1];
			int indexOfSeparator = lastPart.lastIndexOf(NAME_ID_SEP);
			if (indexOfSeparator < 0) {
				throw new InvalidPathException("cannot extract filename from " + mAccountLocalPath);
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
	
	public GoogleDriveFileStorage()
	{
		logDebug("Creating GDrive FileStorage.");
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

		logDebug("openFileForRead...");
		GDrivePath gdrivePath = new GDrivePath(path);
		Drive driveService = getDriveService(gdrivePath.getAccount());

		try
		{
			File file = getFileForPath(gdrivePath, driveService);
			InputStream res =  getFileContent(file, driveService);
			logDebug("openFileForRead ok.");
			return res;
		}
		catch (Exception e)
		{
			throw convertException(e);
		}
	}

	
	private File getFileForPath(GDrivePath path, Drive driveService)
			throws IOException, InvalidPathException {
		logDebug("getFileForPath... ");
		try
		{
			//throw new IOException("argh");
			String driveId = path.getGDriveId();
			logDebug("id"+driveId);
			File file = driveService.files().get(driveId).execute();
			if (file.getLabels().getTrashed())
				throw new FileNotFoundException(path.getDisplayName() + " is trashed!");
			logDebug("...done.");
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
		logDebug("upload file...");		
		try
		{
			ByteArrayContent content = new ByteArrayContent(null, data);
			GDrivePath gdrivePath = new GDrivePath(path);
			Drive driveService = getDriveService(gdrivePath.getAccount());
			
			File driveFile = getFileForPath(gdrivePath, driveService);
			getDriveService(gdrivePath.getAccount()).files()
					.update(driveFile.getId(), driveFile, content).execute();
			logDebug("upload file ok.");
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
			
			logDebug("created folder "+newDirName+" in "+parentPath+". id: "+file.getId());

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
			logDebug("listing files in "+parentId);
			Files.List request = driveService.files().list()
					.setQ("trashed=false and '" + parentId + "' in parents");
	
			do {
				try {
					FileList files = request.execute();
	
					for (File file : files.getItems()) {
	
						String path = new GDrivePath(parentPath, file).getFullPath();
						logDebug("listing file "+path);
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
		logDebug("Exception: " + e.toString());
		e.printStackTrace();
		if (UserRecoverableAuthIOException.class.isAssignableFrom(e.getClass()))
		{
			logDebug("clearing account data.");
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
			logDebug("getFileEntry "+filename);
			GDrivePath gdrivePath = new GDrivePath(filename);
			FileEntry res =  convertToFileEntry(
					getFileForPath(gdrivePath, getDriveService(gdrivePath.getAccount())),
					filename);
			logDebug("getFileEntry res" + res);
			return res;
		}
		catch (Exception e)
		{
			logDebug("Exception in getFileEntry! "+e);
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
			
		}
		catch (Exception e)
		{
			throw convertException(e);
		}
	}


	private Drive createDriveService(String accountName, Context appContext) {
		logDebug("createDriveService "+accountName);
		GoogleAccountCredential credential = createCredential(appContext);
		credential.setSelectedAccountName(accountName);

		return new Drive.Builder(AndroidHttp.newCompatibleTransport(), new GsonFactory(), credential)
		.setApplicationName(getApplicationName())
		.build();
	}
	
	protected String getApplicationName()
	{
		return "Keepass2Android";
	}

	private Drive getDriveService(String accountName)
	{
		logDebug("getDriveService "+accountName);
		AccountData accountData = mAccountData.get(accountName);
		logDebug("accountData " + accountData);
		return accountData.drive;
	}

	@Override
	public void onActivityResult(final JavaFileStorage.FileStorageSetupActivity setupAct, int requestCode, int resultCode, Intent data) {
		logDebug("ActivityResult: " + requestCode + "/" + resultCode);
		switch (requestCode) {
		case REQUEST_ACCOUNT_PICKER:
			logDebug("ActivityResult: REQUEST_ACCOUNT_PICKER");
			if (resultCode == Activity.RESULT_OK && data != null && data.getExtras() != null) {
				String accountName = data.getStringExtra(AccountManager.KEY_ACCOUNT_NAME);
				if (accountName != null) {
					logDebug("Initialize Account name="+accountName);
					initializeAccountOrPath(setupAct, accountName);

					return;
				}
			}
			logDebug("Error selecting account");
			//Intent retData = new Intent();
			//retData.putExtra(EXTRA_ERROR_MESSAGE, t.getMessage());
			((Activity)setupAct).setResult(Activity.RESULT_CANCELED, data);
			((Activity)setupAct).finish();

		case REQUEST_AUTHORIZATION:
			if (resultCode == Activity.RESULT_OK) {
				//for (String k: data.getExtras().keySet())
				//{
					//logDebug(data.getExtras().get(k).toString());
				//}
				String accountName = data.getStringExtra(AccountManager.KEY_ACCOUNT_NAME);
				if (accountName != null) {
					logDebug("Account name="+accountName);
					initializeAccountOrPath(setupAct, accountName);
				}
				else
				{
					logDebug("Account name is null");
				}
			} else {
				logDebug("Error authenticating");
				//Intent retData = new Intent();
				//retData.putExtra(EXTRA_ERROR_MESSAGE, t.getMessage());
				((Activity)setupAct).setResult(Activity.RESULT_CANCELED, data);
				((Activity)setupAct).finish();
			}

		}

	}

	private void initializeAccountOrPath(final JavaFileStorage.FileStorageSetupActivity setupAct, final String accountNameOrPath) {

		final Activity activity = ((Activity)setupAct);
		final Context appContext = activity.getApplicationContext();
		
		String accountNameTemp;
		GDrivePath gdrivePath = null;
		if (accountNameOrPath.startsWith(getProtocolPrefix()))
		{
			gdrivePath = new GDrivePath();
			//don't verify yet, we're not yet initialized:
			try {
				gdrivePath.setPathWithoutVerify(accountNameOrPath);
			} catch (Exception e) {
				finishWithError(setupAct, e);
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
					
					
					initializeAccount(appContext, accountName);
					
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
						finishWithError(setupAct, error);
					}
				}  else if ( isCancelled()) {
					// cancel handling here
					logDebug("Async Task cancelled!");

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
	
	private void initializeAccount(final Context appContext,
			final String accountName) throws IOException {
		logDebug("Init account for " + accountName);
		if (!mAccountData.containsKey(accountName))
		{
			AccountData newAccountData = new AccountData();
			newAccountData.drive = createDriveService(accountName, appContext);
			mAccountData.put(accountName, newAccountData);
			logDebug("Added account data for " + accountName);
		}
		AccountData accountData = mAccountData.get(accountName);
		//try to finish the initialization. If this fails, we throw.
		//in case of "Always return true" (inside CachingFileStorage) this means
		//we have a partially uninitialized AccountData object. 
		//We'll try to initialize later in verify() if (e.g.) network is available again.
		finishInitialization(accountData, accountName);
	}



	private void finishInitialization(AccountData newAccountData, String accountName) throws IOException
	{
		
		if (TextUtils.isEmpty(newAccountData.mRootFolderId))
		{
			logDebug("Finish init account for " + accountName);
			About about = newAccountData.drive.about().get().execute();
			newAccountData.mRootFolderId = about.getRootFolderId();
		}
		else
		{
			logDebug("Account for " + accountName + " already fully initialized.");
		}
	}

	@Override
	public void startSelectFile(JavaFileStorage.FileStorageSetupInitiatorActivity activity, boolean isForSave,
			int requestCode) {
		((JavaFileStorage.FileStorageSetupInitiatorActivity)(activity)).startSelectFileProcess(getProtocolPrefix(), isForSave, requestCode);		
	}


	@Override
	public void prepareFileUsage(JavaFileStorage.FileStorageSetupInitiatorActivity activity, String path, int requestCode, boolean alwaysReturnSuccess) {
		((JavaFileStorage.FileStorageSetupInitiatorActivity)(activity)).startFileUsageProcess(path, requestCode, alwaysReturnSuccess);

	}
	
	@Override
	
	public void prepareFileUsage(Context appContext, String path) throws UserInteractionRequiredException, Throwable 
	{
		try
		{
			logDebug("prepareFileUsage " + path + "...");
			String accountName;
			GDrivePath gdrivePath = null;
			if (path.startsWith(getProtocolPrefix()))
			{
				gdrivePath = new GDrivePath();
				//don't verify yet, we're not yet initialized:
				gdrivePath.setPathWithoutVerify(path);
				
				accountName = gdrivePath.getAccount();
			}
			else
				accountName = path;
			
			initializeAccount(appContext, accountName);	
			logDebug("prepareFileUsage ok");
		}
		catch (UserRecoverableAuthIOException e)
		{
			logDebug("prepareFileUsage: UserInteractionRequiredException");
			throw new UserInteractionRequiredException(e);
		}
		
		
	}

	@Override
	public String getProtocolId() {
		return GDRIVE_PROTOCOL_ID;
	}


	@Override
	public void onRequestPermissionsResult(FileStorageSetupActivity setupAct, int requestCode, String[] permissions, int[] grantResults)
	{
		logDebug("onRequestPermissionsResult");
		if (grantResults[0] == PERMISSION_GRANTED)
		{
			logDebug("granted");
			initFileStorage(setupAct);
		}
		else
		{
			logDebug("denied");
			finishWithError(setupAct, new Exception("Please grant the requested permissions. Access to your accounts is required to let you choose from the available Google accounts on this device."));
		}
	}

	private void initFileStorage(FileStorageSetupActivity setupAct) {
		Activity activity = (Activity)setupAct;

		if (PROCESS_NAME_SELECTFILE.equals(setupAct.getProcessName()))
        {
            GoogleAccountCredential credential = createCredential(activity.getApplicationContext());

            logDebug("starting REQUEST_ACCOUNT_PICKER");
            activity.startActivityForResult(credential.newChooseAccountIntent(), REQUEST_ACCOUNT_PICKER);
        }

		if (PROCESS_NAME_FILE_USAGE_SETUP.equals(setupAct.getProcessName()))
        {
            initializeAccountOrPath(setupAct, setupAct.getPath());
        }
	}

	@Override
	public void onResume(JavaFileStorage.FileStorageSetupActivity setupAct) {

	}

	@Override
	public void onStart(final JavaFileStorage.FileStorageSetupActivity setupAct) {
		logDebug("onStart "+mRequiresRuntimePermissions);
		if (!mRequiresRuntimePermissions)
		{
			initFileStorage(setupAct);
		}


	}

	private GoogleAccountCredential createCredential(Context appContext) {
		List<String> scopes = new ArrayList<String>();
		scopes.add(DriveScopes.DRIVE);
		GoogleAccountCredential credential = GoogleAccountCredential.usingOAuth2(appContext, scopes);
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

		logDebug("onCreate");
		mRequiresRuntimePermissions = false;
		if (Build.VERSION.SDK_INT >= 23)
		{
			Activity act = (Activity)activity;

			String[] permissions = new String[] {Manifest.permission.GET_ACCOUNTS, Manifest.permission.WRITE_EXTERNAL_STORAGE, Manifest.permission.READ_EXTERNAL_STORAGE};
			boolean allOk = true;
			for (String s: permissions)
			{
				int permissionRes = act.checkSelfPermission(s);
				logDebug("permissionRes "+s+"="+permissionRes);
				if (permissionRes != PERMISSION_GRANTED)
					allOk = false;
			}

			if (!allOk)
			{
				logDebug("requestPermissions");
				mRequiresRuntimePermissions = true;
				act.requestPermissions(permissions, 0);
			}
		}

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
	
	@Override
	public String getFilename(String path) throws Exception
	{
		GDrivePath gdrivePath = new GDrivePath();
		gdrivePath.setPathWithoutVerify(path);
	
		return gdrivePath.getFilename();
	}

	
}
