package keepass2android.javafilestorage;

import java.io.InputStream;
import java.util.List;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;

public interface JavaFileStorage {
	
	public static final String PROCESS_NAME_SELECTFILE = "SELECT_FILE";
	public static final String PROCESS_NAME_FILE_USAGE_SETUP = "FILE_USAGE_SETUP";
	

	public static final String EXTRA_PROCESS_NAME = "EXTRA_PROCESS_NAME";
	public static final String EXTRA_PATH = "fileName"; //match KP2A PasswordActivity Ioc-Path Extra key
	public static final String EXTRA_IS_FOR_SAVE = "IS_FOR_SAVE";
	public static final String EXTRA_ERROR_MESSAGE = "EXTRA_ERROR_MESSAGE";
	public static final String EXTRA_ALWAYS_RETURN_SUCCESS = "EXTRA_ALWAYS_RETURN_SUCCESS";

	
public interface FileStorageSetupInitiatorActivity
{
	void startSelectFileProcess(String path, boolean isForSave, int requestCode);
	void startFileUsageProcess(String path, int requestCode, boolean alwaysReturnSuccess);
	void onImmediateResult(int requestCode, int result,	Intent intent);
	void performManualFileSelect(boolean isForSave, int requestCode, String protocolId);
	Activity getActivity();
}

public interface FileStorageSetupActivity
{
	String getPath();
	String getProcessName();
	//int getRequestCode();
	boolean isForSave();
	Bundle getState();	
}
	

public class FileEntry {
	public String path;
	public boolean isDirectory;
	public long lastModifiedTime;
	public boolean canRead;
	public boolean canWrite;
	public long sizeInBytes;
	public String displayName;
	
	public FileEntry()
	{
		isDirectory = false;
		canRead = canWrite = true;
	}

	@Override
	public int hashCode() {
		final int prime = 31;
		int result = 1;
		result = prime * result + (canRead ? 1231 : 1237);
		result = prime * result + (canWrite ? 1231 : 1237);
		result = prime * result + (isDirectory ? 1231 : 1237);
		result = prime * result
				+ (int) (lastModifiedTime ^ (lastModifiedTime >>> 32));
		result = prime * result + ((path == null) ? 0 : path.hashCode());
		result = prime * result + (int) (sizeInBytes ^ (sizeInBytes >>> 32));
		return result;
	}

	@Override
	public boolean equals(Object obj) {
		if (this == obj)
			return true;
		if (obj == null)
			return false;
		if (getClass() != obj.getClass())
			return false;
		FileEntry other = (FileEntry) obj;
		if (canRead != other.canRead)
			return false;
		if (canWrite != other.canWrite)
			return false;
		if (isDirectory != other.isDirectory)
			return false;
		if (lastModifiedTime != other.lastModifiedTime)
			return false;
		if (path == null) {
			if (other.path != null)
				return false;
		} else if (!path.equals(other.path))
			return false;
		if (sizeInBytes != other.sizeInBytes)
			return false;
		return true;
	}
	
	
}
	
	//public boolean tryConnect(Activity activity);
	
	//public void onResume();
	
	//public void onActivityResult(Activity activity, final int requestCode, final int resultCode, final Intent data);
    	
	//public boolean isConnected();

	public static int MAGIC_NUMBER_JFS = 874345; 
	public static int RESULT_FULL_FILENAME_SELECTED = MAGIC_NUMBER_JFS+1;
	public static int RESULT_FILECHOOSER_PREPARED = MAGIC_NUMBER_JFS+2;
	public static int RESULT_FILEUSAGE_PREPARED = MAGIC_NUMBER_JFS+3;
	
	public boolean requiresSetup(String path);

	public void startSelectFile(FileStorageSetupInitiatorActivity activity, boolean isForSave, int requestCode);
	
	//prepare the file usage. if not possible, throw an exception. Must throw UserInteractionRequiredException if the
	// problem can be resolved by the user. Caller should then retry with prepareFileUsage(FileStorageSetupInitiatorActivity activity, String path, int requestCode, boolean alwaysReturnSuccess)
	public void prepareFileUsage(Context appContext, String path) throws UserInteractionRequiredException, Throwable;
	
	//prepare the file usage. if necessary, use the activity to interact with the user, e.g. to grant access.
	public void prepareFileUsage(FileStorageSetupInitiatorActivity activity, String path, int requestCode, boolean alwaysReturnSuccess);
	
	public String getProtocolId();
	
	public String getDisplayName(String path);
	
	//returns something like "myfile.txt" from the given path, i.e. it's displayable and only the last part of the path
	public String getFilename(String path) throws Exception;
	
	public boolean checkForFileChangeFast(String path, String previousFileVersion) throws Exception;
	
	public String getCurrentFileVersionFast(String path) throws Exception;
	
	public InputStream openFileForRead(String path) throws Exception;
	
	public void uploadFile(String path, byte[] data, boolean writeTransactional) throws Exception;
	
	//creates a folder "newDirName" in parentPath and returns the path of the new folder
	public String createFolder(String parentPath, String newDirName) throws Exception;
	
	//returns the path of a file "newFileName" in the folder "parentPath"
	//this may create the file if this is required to get a path (if a UUID is part of the file path)
	public String createFilePath(String parentPath, String newFileName) throws Exception;
	
	public List<FileEntry> listFiles(String parentPath) throws Exception;
	
	public FileEntry getFileEntry(String filename) throws Exception;
	
	public void delete(String path) throws Exception;
	
	public void onCreate(FileStorageSetupActivity activity, Bundle savedInstanceState);
	public void onResume(FileStorageSetupActivity activity);
	public void onStart(FileStorageSetupActivity activity);
	public void onActivityResult(FileStorageSetupActivity activity, int requestCode, int resultCode, Intent data);
	public void onRequestPermissionsResult(FileStorageSetupActivity activity, int requestCode, String[] permissions, int[] grantResults);
	
}
