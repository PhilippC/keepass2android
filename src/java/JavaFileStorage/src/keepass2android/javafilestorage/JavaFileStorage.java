package keepass2android.javafilestorage;

import java.io.InputStream;
import android.app.Activity;

public interface JavaFileStorage {
	public boolean tryConnect(Activity activity);
	
	public void onResume();
	
    	
	public boolean isConnected();
	
	
	public boolean checkForFileChangeFast(String path, String previousFileVersion) throws Exception;
	
	public String getCurrentFileVersionFast(String path);
	
	public InputStream openFileForRead(String path) throws Exception;
	
	public void uploadFile(String path, byte[] data, boolean writeTransactional) throws Exception;
}