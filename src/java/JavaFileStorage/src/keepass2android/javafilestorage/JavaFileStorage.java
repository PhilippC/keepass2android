package keepass2android.javafilestorage;

import java.io.InputStream;
import java.util.List;

import android.app.Activity;

public interface JavaFileStorage {
	

public class FileEntry {
	public String path;
	public boolean isDirectory;
	public long lastModifiedTime;
	public boolean canRead;
	public boolean canWrite;
	public long sizeInBytes;
	
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
	
	public boolean tryConnect(Activity activity);
	
	public void onResume();
    	
	public boolean isConnected();
	
	public boolean checkForFileChangeFast(String path, String previousFileVersion) throws Exception;
	
	public String getCurrentFileVersionFast(String path);
	
	public InputStream openFileForRead(String path) throws Exception;
	
	public void uploadFile(String path, byte[] data, boolean writeTransactional) throws Exception;
	
	public void createFolder(String path) throws Exception;
	
	public List<FileEntry> listFiles(String dirName) throws Exception;
	
	public void delete(String path) throws Exception;
	
}