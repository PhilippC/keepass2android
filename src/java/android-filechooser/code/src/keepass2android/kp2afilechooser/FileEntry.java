package keepass2android.kp2afilechooser;


public class FileEntry {
	public String path;
	public String displayName;
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
}
