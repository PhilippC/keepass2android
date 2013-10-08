package keepass2android.javafilestorage;

import java.io.InputStream;
import java.io.UnsupportedEncodingException;
import java.net.URLEncoder;

import java.util.ArrayList;
import java.util.List;

import com.google.api.client.extensions.android.http.AndroidHttp;
import com.google.api.client.googleapis.extensions.android.gms.auth.GoogleAccountCredential;
import com.google.api.client.googleapis.extensions.android.gms.auth.UserRecoverableAuthIOException;
import com.google.api.client.json.gson.GsonFactory;
import com.google.api.services.drive.Drive;
import com.google.api.services.drive.DriveScopes;
import com.google.api.services.drive.Drive.Files;

import android.accounts.AccountManager;
import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.preference.PreferenceManager;
import android.util.Log;

/*public class GoogleDriveFileStorage
{};*/

public class GoogleDriveFileStorage implements JavaFileStorage {

	private static Drive service;
	//private GoogleAccountCredential credential;
		
	static final int MAGIC_GDRIVE=2082334;
	static final int REQUEST_ACCOUNT_PICKER = MAGIC_GDRIVE+1;
	static final int REQUEST_AUTHORIZATION = MAGIC_GDRIVE+2;
	
	final static private String TAG = "KP2AJ";
	
	/*
	private static void printFilesInFolder(Drive service, String folderId)
		      throws IOException {
		    Children.List request = service.files().list();

		    do {
		      try {
		        ChildList children = request.execute();

		        for (ChildReference child : children.getItems()) {
		          System.out.println("File Id: " + child.getId());
		        }
		        request.setPageToken(children.getNextPageToken());
		      } catch (IOException e) {
		        System.out.println("An error occurred: " + e);
		        request.setPageToken(null);
		      }
		    } while (request.getPageToken() != null &&
		             request.getPageToken().length() > 0);
		  }
	
		
	}

	*/

	@Override
	public boolean checkForFileChangeFast(String path,
			String previousFileVersion) throws Exception {
		// TODO Auto-generated method stub
		return false;
	}

	@Override
	public String getCurrentFileVersionFast(String path) {
		// TODO Auto-generated method stub
		return "";
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
	public void createFolder(String path) throws Exception {
		// TODO Auto-generated method stub

	}

	@Override
	public List<FileEntry> listFiles(String dirName) throws Exception {
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


	  private Drive getDriveService(GoogleAccountCredential credential) {
	    return new Drive.Builder(AndroidHttp.newCompatibleTransport(), new GsonFactory(), credential)
	    .setApplicationName("JFSTest")
	        .build();
	  }

	@Override
	public void onActivityResult(final JavaFileStorage.FileStorageSetupActivity setupAct, int requestCode, int resultCode, Intent data) {
		Log.d(TAG, "ActivityResult: "+requestCode+"/"+resultCode);
		switch (requestCode) {
			case REQUEST_ACCOUNT_PICKER:
				Log.d(TAG, "ActivityResult: REQUEST_ACCOUNT_PICKER");
		      if (resultCode == Activity.RESULT_OK && data != null && data.getExtras() != null) {
		        String accountName = data.getStringExtra(AccountManager.KEY_ACCOUNT_NAME);
		        if (accountName != null) {
		        	final Activity activity = (Activity)setupAct;
		        	final boolean[] result = { false };
		        	Log.d(TAG, "Account name="+accountName);
		        	try {
		        		testAuthAndReturn(setupAct, accountName, activity, result);

						
					} catch (UnsupportedEncodingException e) {
						Log.e(TAG, "UnsupportedEncodingException: "+e.toString());
						Intent retData = new Intent();
						retData.putExtra(EXTRA_ERROR_MESSAGE, e.getMessage());
		            	((Activity)activity).setResult(Activity.RESULT_CANCELED, retData);
		            	((Activity)activity).finish();
					}
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
	    	        finishActivityWithSuccess(setupAct);
	    	      } else {
	    	    	  Log.i(TAG, "Error authenticating");
	              	//Intent retData = new Intent();
	              	//retData.putExtra(EXTRA_ERROR_MESSAGE, t.getMessage());
	              	((Activity)setupAct).setResult(Activity.RESULT_CANCELED, data);
	              	((Activity)setupAct).finish();
	    	      }
		    
		}
		
	}

	private void testAuthAndReturn(
			final JavaFileStorage.FileStorageSetupActivity setupAct,
			String accountName, final Activity activity, final boolean[] result)
			throws UnsupportedEncodingException {
		setupAct.getState().putString(EXTRA_PATH, getProtocolId()+"://"+URLEncoder.encode(accountName, "ISO-8859-1")+"/");
		
		Thread thread = new Thread() {

		    @Override
		    public void run() {
		    	
		    	//try to list files:
				//todo: is there a simpler way to test if the user is authorized?
				try
				{
					Log.d(TAG,"createCred");
					GoogleAccountCredential credential = createCredential(activity);
					Log.d(TAG,"get files");
					Files.List request = getDriveService(credential).files().list();
					Log.d(TAG,"get files exec");
					request.execute();
					Log.d(TAG,"ok!");
					result[0] = true;
				}
				catch (UserRecoverableAuthIOException e) {
					Log.d(TAG,"UserRecoverableAuthIOException ");
					  activity.startActivityForResult(e.getIntent(), REQUEST_AUTHORIZATION);
				}
				catch (Throwable t)
				{
					Log.d(TAG, "Exception: " +t.getMessage());
					t.printStackTrace();
					Intent data = new Intent();
		        	data.putExtra(EXTRA_ERROR_MESSAGE, t.getMessage());
		        	activity.setResult(Activity.RESULT_CANCELED, data);
		        	activity.finish();
				}

		    }// run()
		};
		thread.start();
		try {
		    thread.join();
		    if (result[0])
		    {
		    	finishActivityWithSuccess(setupAct);
		    }
		} catch (InterruptedException e) {
			Intent retData = new Intent();
			retData.putExtra(EXTRA_ERROR_MESSAGE, e.getMessage());
			activity.setResult(Activity.RESULT_CANCELED, retData);
			activity.finish();
		}
	}
	
	private void finishActivityWithSuccess(FileStorageSetupActivity setupActivity) {
		Log.d("KP2AJ", "Success with authentcating!");
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
			data.putExtra(EXTRA_PATH, setupActivity.getState().getString(EXTRA_PATH));
			activity.setResult(RESULT_FILECHOOSER_PREPARED, data);
			activity.finish();
			return;
		}	
		
		Log.w("KP2AJ", "Unknown process: " + setupActivity.getProcessName());
		
		
	}

	

	@Override
	public void startSelectFile(JavaFileStorage.FileStorageSetupInitiatorActivity activity, boolean isForSave,
			int requestCode) {
		((JavaFileStorage.FileStorageSetupInitiatorActivity)(activity)).startSelectFileProcess(getProtocolId()+"://", isForSave, requestCode);		
	}

	@Override
	public void prepareFileUsage(JavaFileStorage.FileStorageSetupInitiatorActivity activity, String path, int requestCode) {
		((JavaFileStorage.FileStorageSetupInitiatorActivity)(activity)).startFileUsageProcess(path, requestCode);
		
	}

	@Override
	public String getProtocolId() {
		
		return "gdrive";
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
				
			Log.d(TAG, "starting REQUEST_ACCOUNT_PICKER");
			activity.startActivityForResult(credential.newChooseAccountIntent(), REQUEST_ACCOUNT_PICKER);
		}
		
		if (PROCESS_NAME_FILE_USAGE_SETUP.equals(setupAct.getProcessName()))
		{
			GoogleAccountCredential credential = createCredential(activity);

			String storedAccountName = PreferenceManager.getDefaultSharedPreferences(activity).getString("GDRIVE_ACCOUNT_NAME", null);
			
			if (storedAccountName != null)
			{
				credential.setSelectedAccountName(storedAccountName);
				Thread thread = new Thread() {

		            @Override
		            public void run() {
		            	
		            	Activity activity = (Activity)setupAct;
		            	
		            	//try to list files:
		    			//todo: is there a simpler way to test if the user is authorized?
		    			try
		    			{
		    				service.files().list().execute();
		    			}
		    			catch (UserRecoverableAuthIOException e) {
		    				  activity.startActivityForResult(e.getIntent(), REQUEST_AUTHORIZATION);
		    			}
		    			catch (Throwable t)
		    			{
		    				Intent data = new Intent();
		                	data.putExtra(EXTRA_ERROR_MESSAGE, t.getMessage());
		                	activity.setResult(Activity.RESULT_CANCELED, data);
		                	activity.finish();
		    			}

		            }// run()
		        };
		        thread.start();
		        try {
		            thread.join();
		        } catch (InterruptedException e) {
    				Intent data = new Intent();
                	data.putExtra(EXTRA_ERROR_MESSAGE, e.getMessage());
                	activity.setResult(Activity.RESULT_CANCELED, data);
                	activity.finish();
		        }
			}
		}
	}

	private GoogleAccountCredential createCredential(Activity activity) {
		List<String> scopes = new ArrayList<String>();
		scopes.add(DriveScopes.DRIVE);
		GoogleAccountCredential credential = GoogleAccountCredential.usingOAuth2(activity, scopes);
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
		// TODO Auto-generated method stub
		
	}

}
