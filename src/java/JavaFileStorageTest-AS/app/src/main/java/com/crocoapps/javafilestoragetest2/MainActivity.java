package com.crocoapps.javafilestoragetest2;

//
//import java.io.IOException;
//import java.util.ArrayList;
//import java.util.List;
//import android.accounts.AccountManager;
//import android.app.Activity;
//import android.content.Intent;
//import android.os.Bundle;
//import android.util.Log;
//import android.widget.Toast;
//
//import com.google.api.client.extensions.android.http.AndroidHttp;
//import com.google.api.client.googleapis.extensions.android.gms.auth.GoogleAccountCredential;
//import com.google.api.client.googleapis.extensions.android.gms.auth.UserRecoverableAuthIOException;
//import com.google.api.client.http.ByteArrayContent;
//import com.google.api.client.json.gson.GsonFactory;
//import com.google.api.services.drive.Drive;
//import com.google.api.services.drive.DriveScopes;
//import com.google.api.services.drive.model.File;
//import com.google.api.services.drive.model.FileList;
//
//public class MainActivity extends Activity {
//  static final int REQUEST_ACCOUNT_PICKER = 1;
//  static final int REQUEST_AUTHORIZATION = 2;
//  static final int CAPTURE_IMAGE = 3;
//
//  private static Drive service;
//  
//
//  @Override
//  public void onCreate(Bundle savedInstanceState) {
//    super.onCreate(savedInstanceState);
//
//    List<String> scopes = new ArrayList<String>();
//    scopes.add(DriveScopes.DRIVE);
//    GoogleAccountCredential credential = GoogleAccountCredential.usingOAuth2(this, scopes);
//    startActivityForResult(credential.newChooseAccountIntent(), REQUEST_ACCOUNT_PICKER);
//  }
//
//  @Override
//  protected void onActivityResult(final int requestCode, final int resultCode, final Intent data) {
//    switch (requestCode) {
//    case REQUEST_ACCOUNT_PICKER:
//      if (resultCode == RESULT_OK && data != null && data.getExtras() != null) {
//        String accountName = data.getStringExtra(AccountManager.KEY_ACCOUNT_NAME);
//        if (accountName != null) {
//        	List<String> scopes = new ArrayList<String>();
//            scopes.add(DriveScopes.DRIVE);
//        	GoogleAccountCredential credential = GoogleAccountCredential.usingOAuth2(this, scopes);
//          credential.setSelectedAccountName(accountName);
//          service = getDriveService(credential);
//          saveFileToDrive();
//        }
//      }
//      break;
//    case REQUEST_AUTHORIZATION:
//      if (resultCode == Activity.RESULT_OK) {
//        saveFileToDrive();
//      } else {
//    	  List<String> scopes = new ArrayList<String>();
//    	    scopes.add(DriveScopes.DRIVE);
//    	  GoogleAccountCredential credential = GoogleAccountCredential.usingOAuth2(this, scopes);
//        startActivityForResult(credential.newChooseAccountIntent(), REQUEST_ACCOUNT_PICKER);
//      }
//      break;
//    }
//  }
//  private void saveFileToDrive() {
//    Thread t = new Thread(new Runnable() {
//      @Override
//      public void run() {
//        try {
//          // File's binary content
//          ByteArrayContent mediaContent = new ByteArrayContent("text/plain","abcnrt".getBytes());
//
//          // File's metadata.
//          File body = new File();
//          body.setTitle("sometext.txt");
//          body.setMimeType("text/plain");
//          
//          listFolders("root", 0);
//        
///*          FileList folders=service.files().list().setQ("mimeType='application/vnd.google-apps.folder' and trashed=false and hidden=false").execute();
//          for(File fl: folders.getItems()){
//               Log.v("JFS"+" fOLDER name:",fl.getTitle());
//          }  
//  *//*        
//          
//
//          File file = service.files().insert(body, mediaContent).execute();
//          if (file != null) {
//            showToast("File uploaded: " + file.getTitle());
//            
//          }*/
//        } catch (UserRecoverableAuthIOException e) {
//          startActivityForResult(e.getIntent(), REQUEST_AUTHORIZATION);
//        } catch (IOException e) {
//          e.printStackTrace();
//        }
//      }
//
//	private void listFolders(String id, int level) throws IOException {
//		FileList folders=service.files().list().setQ("mimeType='application/vnd.google-apps.folder' and trashed=false and hidden=false and '"+id+"' in parents").execute();
//        for(File fl: folders.getItems()){
//        	String pre = "";
//        	for (int i=0;i<level;i++)
//        		pre += "> ";
//             Log.v("JFS fOLDER name:",pre+fl.getTitle());
//             listFolders(fl.getId(), level+1);
//        }
//		
//	}
//    });
//    t.start();
//  }
//
//  private Drive getDriveService(GoogleAccountCredential credential) {
//    return new Drive.Builder(AndroidHttp.newCompatibleTransport(), new GsonFactory(), credential)
//        .build();
//  }
//
//  public void showToast(final String toast) {
//    runOnUiThread(new Runnable() {
//      @Override
//      public void run() {
//        Toast.makeText(getApplicationContext(), toast, Toast.LENGTH_SHORT).show();
//      }
//    });
//  }
//}


import group.pals.android.lib.ui.filechooser.FileChooserActivity;
import group.pals.android.lib.ui.filechooser.providers.BaseFileProviderUtils;

import java.io.FileInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.Reader;
import java.io.UnsupportedEncodingException;
import java.util.ArrayList;
import java.util.List;

//import keepass2android.javafilestorage.DropboxCloudRailStorage;
import keepass2android.javafilestorage.GoogleDriveAppDataFileStorage;
import keepass2android.javafilestorage.JavaFileStorage;
import keepass2android.javafilestorage.JavaFileStorage.FileEntry;
import keepass2android.javafilestorage.SftpStorage;
import keepass2android.javafilestorage.UserInteractionRequiredException;
import keepass2android.javafilestorage.WebDavStorage;
import keepass2android.kp2afilechooser.StorageFileProvider;
import android.net.Uri;
import android.os.AsyncTask;
import android.os.Build;
import android.os.Bundle;
import android.preference.PreferenceManager;
import android.app.Activity;
import android.app.AlertDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import androidx.annotation.RequiresApi;
import android.util.Log;
import android.view.Menu;
import android.view.View;
import android.view.View.OnClickListener;
import android.widget.EditText;
import android.widget.Toast;

/**
 * @author Philipp
 *
 */
public class MainActivity extends Activity implements JavaFileStorage.FileStorageSetupInitiatorActivity {
	
	//a little dirty hack: make the file storage available to the whole app
	//this is implemented nicer in the real app...
	public static JavaFileStorage storageToTest;
	
	class PerformTestTask extends AsyncTask<Object, Void, Void>

	{

		String convertStreamToString(java.io.InputStream is) {
		    java.util.Scanner s = new java.util.Scanner(is).useDelimiter("\\A");
		    return s.hasNext() ? s.next() : "";
		}
		
		@Override
		protected Void doInBackground(Object... params) {
			
			try {
				
				String parentPath = (String)params[0];
				String testPath = (String)params[1];
				JavaFileStorage fs = (JavaFileStorage)params[2];
				
				String path;
				try
				{
					path = fs.createFolder(parentPath, testPath);
				}
				catch (Exception e)
				{
					Log.d("KP2AJ",e.toString());
					//if exception because folder exists
					path = fs.createFilePath(parentPath, testPath);
				}

				String textToUpload2 = "abcdefg";
				String filename2 = fs.createFilePath(parentPath, "file.txt");
				/*if (!path.endsWith("/"))
					path += "/";
				String filename = path+"file.text";*/
				fs.uploadFile(filename2,textToUpload2.getBytes(),true);

			//	FileEntry e1 = fs.getFileEntry(parentPath);
				FileEntry e2 = fs.getFileEntry(path);

				boolean receivedFileNotFoundException;
				/*
				if (e1.displayName == null) throw new Exception("displayName of "+parentPath+" is null!");
				if (e2.displayName.equals(testPath) == false) throw new Exception("displayName of "+path+" is "+e2.displayName+"!");
				
				//try to delete the file to prepare the test. if this fails, we ignore it for now
				try
				{
					fs.delete(path);
				}
				catch (Exception e)
				{
					e.printStackTrace();
				}
				
				Log.d("KP2AJ", "checking if folder "+path+" exists...");
				receivedFileNotFoundException = false;
				try
				{
					fs.listFiles(path);
				}
				catch (java.io.FileNotFoundException ex) 
				{
					receivedFileNotFoundException = true;
				}
				if (!receivedFileNotFoundException)
					throw new Exception("Either listFiles() didn't throw when listing an unexisting path or the path "+path+" already exists. Please make sure it doesn't!");
				
				Log.d("KP2AJ", "creating folder "+path);
				path = fs.createFolder(parentPath, testPath);
				Log.d("KP2AJ", "creating folder returned without exception. Now list its contents.");
				*/
				List<FileEntry> filesInEmptyDir = fs.listFiles(path);
				if (!filesInEmptyDir.isEmpty())
				{
					for (FileEntry fe: filesInEmptyDir)
						Log.d("KP2AJ", fe.path+", "+fe.displayName);
					throw new Exception("Received non-empty list with "+filesInEmptyDir.size()+" entries after creating directory!");
				}
				
				

				Log.d("KP2AJ", "Ok. Write a file to the folder:");
				String textToUpload = "abcdefg";
				String filename = fs.createFilePath(path, "file.txt");
				/*if (!path.endsWith("/"))
					path += "/";
				String filename = path+"file.text";*/
				fs.uploadFile(filename,textToUpload.getBytes(),true);
				Log.d("KP2AJ", "Ok. Read contents:");
				InputStream s = fs.openFileForRead(filename);
				String receivedText = convertStreamToString(s);
				if (!receivedText.equals(textToUpload))
					throw new Exception("Received unexpected contents: "+receivedText+" vs. " + textToUpload);
				Log.d("KP2AJ", "Ok. Query version:");
				String version0 = fs.getCurrentFileVersionFast(filename);
				
				Log.d("KP2AJ", "Ok. Get FileEntry:");
				FileEntry e = fs.getFileEntry(filename);
				if (!e.path.toLowerCase().equals(filename.toLowerCase()) || e.isDirectory)
					throw new Exception("invalid file entry record!");
				
				if (version0 == null)
					Log.d("KP2AJ", "WARNING: getCurrentFileVersionFast shouldn't return null");
				
				Log.d("KP2AJ", "Ok. Modify the file:");

				//sleep a second to ensure we have some time between the two modifications (if this is contained in the file version, they should be different)
				Thread.sleep(1000);

				String newTextToUpload = "xyz123";
				fs.uploadFile(filename,newTextToUpload.getBytes(),true);
				Log.d("KP2AJ", "Ok. Read contents:");
				s = fs.openFileForRead(filename);
				receivedText = convertStreamToString(s);
				if (!receivedText.equals(newTextToUpload))
					throw new Exception("Received unexpected contents: "+receivedText+" vs. " + newTextToUpload);
				
				String version1 = fs.getCurrentFileVersionFast(filename);
				
				if (version0 != null)
				{
					if (version0.equals(version1))
						throw new Exception("getCurrentFileVersionFast returned same version string "+version0+" after modification!");
				}
				
				if (fs.checkForFileChangeFast(filename, version0) == false)
				{
					//no failure because it's allowed to return false even if there was a change - but it's not good, so warn:
					Log.d("KP2AJ", "WARNING! checkForFileChangeFast returned false even though the files were modified!");
				}
				
				Log.d("KP2AJ", "Try to open an unexisting file:");
				receivedFileNotFoundException = false;
				try
				{
					fs.openFileForRead(path+"/unexisting.txt");
				}
				catch (java.io.FileNotFoundException ex) 
				{
					receivedFileNotFoundException = true;
				}
				if (!receivedFileNotFoundException)
					throw new Exception("Didn't received file not found exception for unexisting file!");
				
				Log.d("KP2AJ", "Create some more folders and files: ");
				String subfolderPath = fs.createFolder(path,"subfolder");
				String anotherFileInSubfolderPath = fs.createFilePath(subfolderPath, "anotherfile.txt");
				String anotherFilePath = fs.createFilePath(path, "anotherfile.txt");
				fs.uploadFile(anotherFileInSubfolderPath, textToUpload.getBytes(), true);
				fs.uploadFile(anotherFilePath, textToUpload.getBytes(), false); // try non-transacted as well
				
				Log.d("KP2AJ", "List files:");
				List<FileEntry> fileList = fs.listFiles(path);
				checkFileList(path, fileList, true, true);
				
				Log.d("KP2AJ", "getFilename:");
				testGetFilename(fileList, fs);
				
				Log.d("KP2AJ", "Delete a file");
				fs.delete(filename);
				
				Log.d("KP2AJ", "List files again to check if deleting the file was successful:");
				fileList = fs.listFiles(path);
				checkFileList(path, fileList, false, true); //second param indicates the file must be gone
				
				Log.d("KP2AJ", "Delete a folder recursive");
				fs.delete(subfolderPath);
				
				Log.d("KP2AJ", "List files again to check if deleting the folder was successful:");
				fileList = fs.listFiles(path);
				checkFileList(path, fileList, false, false); //third param indicates the folder must be gone
				
				Log.d("KP2AJ", "Delete the main test folder");
				fs.delete(path);

				Log.d("KP2AJ", "ALL TESTS OK!");
			
			
				
			} catch (Exception e) {

				Log.d("KP2AJ", "Test failed with exception!");
				Log.d("KP2AJ",e.toString());
				
				e.printStackTrace();
			}
			
			

			return null;
		}

		private void testGetFilename(List<FileEntry> fileList,
				JavaFileStorage fs) throws Exception {
			for (FileEntry e: fileList)
			{
				String fileName = fs.getFilename(e.path);
				if (!fileName.equals(e.displayName))
				{
					Log.e("KP2AJ", "Received "+fileName+" for " + e.path + " but expected " + e.displayName);
					throw new Exception("error!");
				}
			}
			
		}

		private void checkFileList(String basepath, List<FileEntry> fileList, boolean expectDeletableFile, boolean expectDeletableFolder) throws Exception {
			
				FileEntry expectedFile = new FileEntry();
				expectedFile.canRead = expectedFile.canWrite = true;
				expectedFile.isDirectory = false;
				expectedFile.displayName = "anotherfile.txt";
				expectedFile.sizeInBytes = 7; //("abcdefg")
				//lastModifiedTime is not known
				checkFileIsContained(fileList, expectedFile);

				int expectedSize = 1;

				if (expectDeletableFile)
				{
					expectedFile.displayName = "file.txt";
					expectedFile.sizeInBytes = 6; //"xyz123"
					checkFileIsContained(fileList, expectedFile);
					expectedSize++;
				}

				if (expectDeletableFolder)
				{
					FileEntry expectedDir = new FileEntry();
					expectedDir.canRead = expectedFile.canWrite = true;
					expectedDir.isDirectory = true;
					expectedDir.displayName = "subfolder";
					checkFileIsContained(fileList, expectedDir);
					expectedSize++;
				}

				if (fileList.size() != expectedSize)
					throw new Exception("Unexpected number of entries in fileList: " + fileList.size());

		}

		private void checkFileIsContained(List<FileEntry> fileList,
				FileEntry file) throws Exception {

			
			for (FileEntry e: fileList)
			{
				if ((e.canRead == file.canRead)
						&& (e.canWrite == file.canWrite)
						&& (e.isDirectory == file.isDirectory)
						&& (e.displayName.equals(file.displayName))
						&& (file.isDirectory || (e.sizeInBytes == file.sizeInBytes )))
					return;
			}
				
			throw new Exception("didn't find file " + file.path + " (" + file.displayName + ") in file list!");
		
		}
	}
			

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		setContentView(R.layout.activity_main);
		
		if (storageToTest == null)
		{
			createStorageToTest(this, getApplicationContext(), false);
		}

		findViewById(R.id.button1).setOnClickListener(new OnClickListener() {
            public void onClick(View v) {
            	storageToTest.startSelectFile(MainActivity.this, false, 1);

            }
        });
		findViewById(R.id.button_test_filechooser).setOnClickListener(new OnClickListener() {
            public void onClick(View v) {
            	storageToTest.startSelectFile(MainActivity.this, false, 2);
             
            }
        });
		
		findViewById(R.id.button_test_filechooser_saveas).setOnClickListener(new OnClickListener() {
            public void onClick(View v) {
            	storageToTest.startSelectFile(MainActivity.this, true, 3);
               
            }
        });
		
		
		findViewById(R.id.button_test_preparefileusage).setOnClickListener(new OnClickListener() {
            @RequiresApi(api = Build.VERSION_CODES.CUPCAKE)
			public void onClick(View v) {
            	
            	final String path = PreferenceManager.getDefaultSharedPreferences(MainActivity.this).getString("selectedPath", "");
            	if (path.equals(""))
            	{
            		Toast.makeText(MainActivity.this, "select path with file chooser first", Toast.LENGTH_LONG).show();
            		return;
            	}
            		new AsyncTask<Object, Object, Object>() {
						
						@Override
						protected Object doInBackground(Object... params) {
			            	try
			            	{

							createStorageToTest(MainActivity.this, MainActivity.this.getApplicationContext(), false).prepareFileUsage(MainActivity.this, path);
							runOnUiThread(new Runnable() {

						        @Override
						        public void run() {
						        	Toast.makeText(MainActivity.this, "prepare ok: " + path, Toast.LENGTH_LONG).show();
						        }
						    });
		            		
			            	}
			            	catch (UserInteractionRequiredException e)
			            	{
			            		final UserInteractionRequiredException e2 = e;
			            		runOnUiThread(new Runnable() {

							        @Override
							        public void run() {
					            		Toast.makeText(MainActivity.this, "this requires user interaction! "+e2.getClass().getName()+ " "+e2.getMessage(), Toast.LENGTH_LONG).show();
							        }
							    });

			            	}
			            	catch (Throwable t)
			            	{
			            		final Throwable t2 = t;
			            		runOnUiThread(new Runnable() {

							        @Override
							        public void run() {
					            		Toast.makeText(MainActivity.this, t2.getClass().getName()+": "+ t2.getMessage(), Toast.LENGTH_LONG).show();
							        }
							    });


			            	}

							return null;
						}
					}.execute();
            		
               
            }
        });
		
		
	}

	static JavaFileStorage createStorageToTest(Context ctx, Context appContext, boolean simulateRestart) {
		storageToTest = new SftpStorage(ctx.getApplicationContext());
		//storageToTest = new PCloudFileStorage(ctx, "yCeH59Ffgtm");
		//storageToTest = new SkyDriveFileStorage("000000004010C234", appContext);


		//storageToTest = new GoogleDriveAppDataFileStorage();
		/*storageToTest = new WebDavStorage(new ICertificateErrorHandler() {
			@Override
			public boolean onValidationError(String error) {
				return false;
			}

			@Override
			public boolean alwaysFailOnValidationError() {
				return false;
			}
		});*/

		//storageToTest =  new DropboxV2Storage(ctx,"4ybka4p4a1027n6", "1z5lv528un9nre8", !simulateRestart);
		//storageToTest =  new DropboxFileStorage(ctx,"4ybka4p4a1027n6", "1z5lv528un9nre8", !simulateRestart);
		//storageToTest = new DropboxAppFolderFileStorage(ctx,"ax0268uydp1ya57", "3s86datjhkihwyc", true);


		return storageToTest;

	}
	
	@Override 
	protected void onResume()
	{
		super.onResume();
	}

	@Override
	public boolean onCreateOptionsMenu(Menu menu) {
		// Inflate the menu; this adds items to the action bar if it is present.
		getMenuInflater().inflate(R.menu.main, menu);
		return true;
	}
	
	@Override
	protected void onActivityResult(int requestCode, int resultCode, Intent data) {
		// TODO Auto-generated method stub
		super.onActivityResult(requestCode, resultCode, data);
		
		if (resultCode == JavaFileStorage.RESULT_FILECHOOSER_PREPARED)
		{
						
			String path = data.getStringExtra(JavaFileStorage.EXTRA_PATH);
			onReceivePathForFileSelect(requestCode, path);
			
		}
		

		if ((requestCode == 1) && (resultCode == RESULT_OK))
		{
			ArrayList<Uri> uris = data
					.getParcelableArrayListExtra(FileChooserActivity.EXTRA_RESULTS);
			String path = BaseFileProviderUtils.getRealUri(this, uris.get(0)).toString();
			
			PreferenceManager.getDefaultSharedPreferences(this).edit()
				.putString("selectedPath", path).commit();



			//create a new storage to simulate the case that the file name was saved and is used again after restarting the app:
			createStorageToTest(this, getApplicationContext(), true).prepareFileUsage(this, path, 2123, false);
			
			
		}
		if ((requestCode == 2123) && (resultCode == JavaFileStorage.RESULT_FILEUSAGE_PREPARED))
		{
			Toast.makeText(this, "Successfully prepared file usage!", Toast.LENGTH_LONG).show();
		}
		if ((requestCode == 2124) && (resultCode == RESULT_OK))
		{
			ArrayList<Uri> uris = data
					.getParcelableArrayListExtra(FileChooserActivity.EXTRA_RESULTS);
			String path = BaseFileProviderUtils.getRealUri(this, uris.get(0)).toString();
			boolean fileExists = data.getBooleanExtra(FileChooserActivity.EXTRA_RESULT_FILE_EXISTS, false);
		
			Toast.makeText(this, "Selected file path for save: "+path+". File exists: " +fileExists, Toast.LENGTH_LONG).show();
		}
		

	}

	private void onReceivePathForFileSelect(int requestCode, String path) {
		Toast.makeText(this, "requestCode: "+requestCode, Toast.LENGTH_LONG).show();
		if (requestCode == 1)
			//new PerformTestTask().execute(path,"TestFileStorage�", storageToTest); //use an umlaut to see how that works
			new PerformTestTask().execute(path,"TestFileStorage", storageToTest);
		else
		if (requestCode == 2)
		{
			Intent intent = keepass2android.kp2afilechooser.Kp2aFileChooserBridge.getLaunchFileChooserIntent(this, StorageFileProvider.authority, path);
			startActivityForResult(intent, 1);
		}
		if (requestCode == 3)
		{
			Intent intent = keepass2android.kp2afilechooser.Kp2aFileChooserBridge.getLaunchFileChooserIntent(this, StorageFileProvider.authority, path);
			intent.putExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.save_dialog", true);
			intent.putExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.default_file_ext", "kdbx");
			startActivityForResult(intent, 2124);
		}
	}

	@Override
	public void startSelectFileProcess(String path, boolean isForSave,
			int requestCode) {
		
		Intent intent = new Intent(this, FileStorageSetupActivity.class);
		intent.putExtra(JavaFileStorage.EXTRA_PROCESS_NAME, JavaFileStorage.PROCESS_NAME_SELECTFILE);
		intent.putExtra(JavaFileStorage.EXTRA_PATH, path);
		startActivityForResult(intent, requestCode);
		
	}

	@Override
	public void startFileUsageProcess(String path, int requestCode, boolean alwaysReturnSuccess) {

		Intent intent = new Intent(this, FileStorageSetupActivity.class);
		intent.putExtra(JavaFileStorage.EXTRA_PROCESS_NAME, JavaFileStorage.PROCESS_NAME_FILE_USAGE_SETUP);
		intent.putExtra(JavaFileStorage.EXTRA_PATH, path);
		startActivityForResult(intent, requestCode);
	}

	@Override
	public void onImmediateResult(int requestCode, int result, Intent intent) {
		onActivityResult(requestCode, result, intent);
		
	}

	@Override
	public Activity getActivity() {
		return this;
	}

	public static String readStream(InputStream is) {
		StringBuilder sb = new StringBuilder(512);
		try {
			Reader r = new InputStreamReader(is, "UTF-8");
			int c = 0;
			while ((c = r.read()) != -1) {
				sb.append((char) c);
			}
		} catch (IOException e) {
			throw new RuntimeException(e);
		}
		return sb.toString();
	}

	private void populateCsvMockValues(View view) {
		EditText etSpecs = view.findViewById(R.id.mock_csv_specs);
		etSpecs.setText("-bar,+first,-*d*");
		EditText etCfgs = view.findViewById(R.id.mock_csv_cfg);
		etCfgs.setText("foo,del1,bar,del2");
	}

	@Override
	public void performManualFileSelect(boolean isForSave, final int requestCode,
			String protocolId)
	{
		if (protocolId.equals("sftp"))
		{
			final View view = getLayoutInflater().inflate(R.layout.sftp_credentials, null);
			final SftpStorage sftpStorage = (SftpStorage)storageToTest;

			populateCsvMockValues(view);

			view.findViewById(R.id.send_public_key).setOnClickListener(v -> {
				Intent sendIntent = new Intent();

                try {
                    String pub_filename = sftpStorage.createKeyPair();

                    sendIntent.setAction(Intent.ACTION_SEND);
                    sendIntent.putExtra(Intent.EXTRA_TEXT, readStream(new FileInputStream(pub_filename)));

                    sendIntent.putExtra(Intent.EXTRA_SUBJECT, "Keepass2Android sftp public key");
                    sendIntent.setType("text/plain");
                    this.startActivity(Intent.createChooser(sendIntent, "Send public key to..."));
                }
                catch (Exception ex)
                {
                    Toast.makeText(this,"Failed to create key pair: " + ex.getMessage(), Toast.LENGTH_LONG).show();
                }
			});

			view.findViewById(R.id.list_private_keys).setOnClickListener(v -> {
				String[] keys = sftpStorage.getCustomKeyNames();
				Toast.makeText(this, "keys: " + String.join(",", keys), Toast.LENGTH_LONG).show();
			});

			view.findViewById(R.id.add_private_key).setOnClickListener(v -> {
				EditText etKeyName = view.findViewById(R.id.private_key_name);
				String keyName = etKeyName.getText().toString();
				EditText etKeyContent = view.findViewById(R.id.private_key_content);
				String keyContent = etKeyContent.getText().toString();

				try {
					sftpStorage.savePrivateKeyContent(keyName, keyContent);
					Toast.makeText(this, "Add successful", Toast.LENGTH_LONG).show();
				}
				catch (Exception e) {
					Toast.makeText(this, "Add failed: " + e.getMessage(), Toast.LENGTH_LONG).show();
				}
			});

			view.findViewById(R.id.delete_private_key).setOnClickListener(v -> {
				EditText etKeyName = view.findViewById(R.id.private_key_name);
				String keyName = etKeyName.getText().toString();

				String exMessage = null;
				boolean success = false;
				try {
					success = sftpStorage.deleteCustomKey(keyName);
				}
				catch (Exception e) {
					exMessage = e.getMessage();
				}
				StringBuilder msg = new StringBuilder("Delete ");
				msg.append(success ? "succeeded" : "FAILED");
				if (exMessage != null) {
					msg.append(" (").append(exMessage).append(")");
				}
				Toast.makeText(this, msg.toString(), Toast.LENGTH_LONG).show();
			});

			view.findViewById(R.id.validate_private_key).setOnClickListener(v -> {
				EditText etKeyName = view.findViewById(R.id.private_key_name);
				String inKeyName = etKeyName.getText().toString();

				if (!inKeyName.isEmpty()) {
					String keyResponse;
					try {
						keyResponse = sftpStorage.sanitizeCustomKeyName(inKeyName);
					} catch (Exception e) {
						keyResponse = "EX:" + e.getMessage();
					}
					String msg = "key: [" + inKeyName + "] -> [" + keyResponse + "]";
					Toast.makeText(this, msg, Toast.LENGTH_LONG).show();
				}

				EditText etKeyContent = view.findViewById(R.id.private_key_content);
				String inKeyContent = etKeyContent.getText().toString();
				String msg;
				if (!inKeyContent.isEmpty()) {
					try {
						// We could print the key, but I don't it's that helpful
						sftpStorage.getValidatedCustomKeyContent(inKeyContent);
						msg = "Key content is valid";
					} catch (Exception e) {
						msg = "Invalid key content: " + e.getMessage();
					}
					Toast.makeText(this, msg, Toast.LENGTH_LONG).show();
				}
			});

			view.findViewById(R.id.resolve_mock_csv).setOnClickListener(v -> {
				EditText etSpecs = view.findViewById(R.id.mock_csv_specs);
				String specs = etSpecs.getText().toString();
				EditText etCfg = view.findViewById(R.id.mock_csv_cfg);
				String cfg = etCfg.getText().toString();
				if (!specs.isBlank() && !cfg.isBlank()) {
					String result = sftpStorage.resolveCsvValues(cfg, specs);
					Toast.makeText(this, result, Toast.LENGTH_LONG).show();
				}
			});

			view.findViewById(R.id.reset_mock_csv).setOnClickListener(v -> {
				populateCsvMockValues(view);
			});

			new AlertDialog.Builder(this)
					.setView(view)
					.setTitle("Enter SFTP credentials")
					.setPositiveButton("OK", (dialog, which) -> {

						Toast.makeText(MainActivity.this, "Hey", Toast.LENGTH_LONG).show();

						SftpStorage sftpStorage1 = (SftpStorage)storageToTest;
						try {
							EditText etHost = view.findViewById(R.id.sftp_host);
							String host = etHost.getText().toString();
							EditText etUser = view.findViewById(R.id.sftp_user);
							String user = etUser.getText().toString();
							EditText etPwd = view.findViewById(R.id.sftp_password);
							String pwd = etPwd.getText().toString();
							EditText etPort = view.findViewById(R.id.sftp_port);
							int port = Integer.parseInt(etPort.getText().toString());
							EditText etInitDir = view.findViewById(R.id.sftp_initial_dir);
							String initialDir = etInitDir.getText().toString();
							EditText etConnectTimeout = view.findViewById(R.id.sftp_connect_timeout);
							int connectTimeout = SftpStorage.UNSET_SFTP_CONNECT_TIMEOUT;
							String ctStr = etConnectTimeout.getText().toString();
							if (!ctStr.isEmpty()) {
								try {
									int ct = Integer.parseInt(ctStr);
									if (connectTimeout != ct) {
										connectTimeout = ct;
									}
								} catch (NumberFormatException parseEx) {
								}
							}
							EditText etKeyName = view.findViewById(R.id.private_key_name);
							String keyName = etKeyName.getText().toString();
							EditText etKeyPassphrase = view.findViewById(R.id.private_key_passphrase);
							String keyPassphrase = etKeyPassphrase.getText().toString();
							EditText etKex = view.findViewById(R.id.kex);
							String kex = etKex.getText().toString();
							EditText etShk = view.findViewById(R.id.shk);
							String shk = etShk.getText().toString();

							onReceivePathForFileSelect(requestCode, sftpStorage1.buildFullPath(
									host, port, initialDir, user, pwd, connectTimeout,
									keyName, keyPassphrase, kex, shk));
						} catch (UnsupportedEncodingException e) {
							e.printStackTrace();
						}
					})
					.create()
					.show();

		}

		else
		{
			final View view = getLayoutInflater().inflate(R.layout.webdav_credentials, null);
			new AlertDialog.Builder(this)
					.setView(view)
					.setTitle("Enter WebDAV credentials")
					.setPositiveButton("OK",new DialogInterface.OnClickListener() {

						@Override
						public void onClick(DialogInterface dialog, int which) {

							Toast.makeText(MainActivity.this, "Hey", Toast.LENGTH_LONG).show();

							WebDavStorage storage = (WebDavStorage)storageToTest;
							try {
								EditText etHost = ((EditText)view.findViewById(R.id.webdav_host));
								String host = etHost.getText().toString();
								EditText etUser = ((EditText)view.findViewById(R.id.user));
								String user = etUser.getText().toString();
								EditText etPwd = ((EditText)view.findViewById(R.id.password));
								String pwd = etPwd.getText().toString();
								onReceivePathForFileSelect(requestCode, storage.buildFullPath( host, user, pwd));
							} catch (UnsupportedEncodingException e) {
								// TODO Auto-generated catch block
								e.printStackTrace();
							}
						}
					})
					.create()
					.show();

		}

				
		
		
		
	}

}
