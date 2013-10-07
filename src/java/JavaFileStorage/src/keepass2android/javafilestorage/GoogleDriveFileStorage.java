package keepass2android.javafilestorage;

import java.io.IOException;
import java.io.InputStream;

import java.util.ArrayList;
import java.util.List;

import com.google.api.client.extensions.android.http.AndroidHttp;
import com.google.api.client.googleapis.extensions.android.gms.auth.GoogleAccountCredential;
import com.google.api.client.googleapis.extensions.android.gms.auth.UserRecoverableAuthIOException;
import com.google.api.client.json.gson.GsonFactory;
import com.google.api.services.drive.Drive;
import com.google.api.services.drive.Drive.Children;
import com.google.api.services.drive.DriveScopes;
import com.google.api.services.drive.model.ChildList;
import com.google.api.services.drive.model.ChildReference;

import android.accounts.AccountManager;
import android.app.Activity;
import android.content.Intent;
import android.os.AsyncTask;
import android.os.Bundle;
import android.preference.PreferenceManager;

public class GoogleDriveFileStorage
{};
/*/
public class GoogleDriveFileStorage implements JavaFileStorage {

	private static Drive service;
	//private GoogleAccountCredential credential;
		
	static final int MAGIC_GDRIVE=2082334;
	static final int REQUEST_ACCOUNT_PICKER = MAGIC_GDRIVE+1;
	static final int REQUEST_AUTHORIZATION = MAGIC_GDRIVE+2;
	
	class TestConnectionTask extends AsyncTask<Object, Void, Void>
	{
		@Override
		protected Void doInBackground(Object... params) {
			
			Activity activity = (Activity) params[0];

			//try to list files:
			//todo: is there a simpler way to test if the user is authorized?
			try
			{
				
				
				return true;
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

		}
	}
	
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
	
	public boolean tryConnect(Activity activity) {
		
		List<String> scopes = new ArrayList<String>();
	    scopes.add(DriveScopes.DRIVE);
		GoogleAccountCredential credential = GoogleAccountCredential.usingOAuth2(activity, scopes);

		String storedAccountName = PreferenceManager.getDefaultSharedPreferences(activity).getString("GDRIVE_ACCOUNT_NAME", null);
		
		if (storedAccountName != null)
		{
			credential.setSelectedAccountName(storedAccountName);
			//try to list files:
			//todo: is there a simpler way to test if the user is authorized?
			try
			{
				
				return true;
			}
			catch (UserRecoverableAuthIOException e) {
				  activity.startActivityForResult(e.getIntent(), REQUEST_AUTHORIZATION);
			}

			catch (Throwable t)
			{
				return false;
			}

		}
		else
		{
			
			activity.startActivityForResult(credential.newChooseAccountIntent(), REQUEST_ACCOUNT_PICKER);
			return false;
		}
		
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
	        .build();
	  }

	@Override
	public void onActivityResult(Activity activity, int requestCode, int resultCode, Intent data) {
		switch (requestCode) {
			case REQUEST_ACCOUNT_PICKER:
			
		      if (resultCode == Activity.RESULT_OK && data != null && data.getExtras() != null) {
		        String accountName = data.getStringExtra(AccountManager.KEY_ACCOUNT_NAME);
		        if (accountName != null) {
		          //credential.setSelectedAccountName(accountName);
		        	todo
		        	return;
		        }
		      }
		      todo
		      
		    case REQUEST_AUTHORIZATION:
		    	 if (resultCode == Activity.RESULT_OK) {
	    	        // App is authorized
		    		 todo
	    	      } else {
	    	        // User denied access, show him the account chooser again
	    	        activity.startActivityForResult(credential.newChooseAccountIntent(), REQUEST_ACCOUNT_PICKER);
	    	      }
		}
		
	}

	@Override
	public void startSelectFile(Activity activity, boolean isForSave,
			int requestCode) {
		((JavaFileStorage.FileStorageSetupInitiatorActivity)(activity)).startSelectFileProcess(getProtocolId()+"://", isForSave, requestCode);		
	}

	@Override
	public void prepareFileUsage(Activity activity, String path, int requestCode) {
		((JavaFileStorage.FileStorageSetupInitiatorActivity)(activity)).startFileUsageProcess(path, requestCode);
		
	}

	@Override
	public String getProtocolId() {
		
		return "gdrive";
	}



	@Override
	public void onResume(Activity activity) {

		JavaFileStorage.FileStorageSetupActivity setupAct = (FileStorageSetupActivity) activity;
		
		if (activity.isFinishing())
			return;
		
		if (!hasAccount(setupAct))
		{
			List<String> scopes = new ArrayList<String>();
		    scopes.add(DriveScopes.DRIVE);
			GoogleAccountCredential credential = GoogleAccountCredential.usingOAuth2(activity, scopes);

			activity.startActivityForResult(credential.newChooseAccountIntent(), REQUEST_ACCOUNT_PICKER);
			
		}
		else
		{
			
			
			
		}
		
	}

	@Override
	public void onStart(Activity activity) {
		// TODO Auto-generated method stub
		
	}

}
*/