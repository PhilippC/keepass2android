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

	@Override
	public String toString() {
		StringBuilder s = new StringBuilder("kp2afilechooser.FileEntry{")
				.append(displayName).append("|")
				.append("path=").append(path).append(",sz=").append(sizeInBytes)
				.append(",").append(isDirectory ? "dir" : "file")
				.append(",lastMod=").append(lastModifiedTime);

		StringBuilder perms = new StringBuilder();
		if (canRead)
			perms.append("r");
		if (canWrite)
			perms.append("w");
		if (perms.length() > 0) {
			s.append(",").append(perms);
		}

		return s.append("}").toString();
	}
}
