package keepass2android.kp2afilechooser;

import java.util.List;

import com.crocoapps.javafilestoragetest.MainActivity;

public class StorageFileProvider extends Kp2aFileProvider {

	public static String authority = "keepass2android.kp2afilechooser.StorageFileProvider";

	@Override
	public String getAuthority() {
		return authority;
	}

	@Override
	protected FileEntry getFileEntry(String path, StringBuilder errorMessageBuilder) throws Exception {

			keepass2android.javafilestorage.JavaFileStorage.FileEntry entry = MainActivity.storageToTest.getFileEntry(path);
			keepass2android.kp2afilechooser.FileEntry chooserEntry = convertEntry(entry);
			return chooserEntry;

	}

	private keepass2android.kp2afilechooser.FileEntry convertEntry(
			keepass2android.javafilestorage.JavaFileStorage.FileEntry entry) {
		keepass2android.kp2afilechooser.FileEntry chooserEntry = new FileEntry();
		chooserEntry.canRead = entry.canRead;
		chooserEntry.canWrite = entry.canWrite;
		chooserEntry.displayName = entry.displayName;
		chooserEntry.isDirectory = entry.isDirectory;
		chooserEntry.lastModifiedTime = entry.lastModifiedTime;
		chooserEntry.path = entry.path;
		chooserEntry.sizeInBytes = entry.sizeInBytes;
		return chooserEntry;
	}

	@Override
	protected void listFiles(int taskId, String dirName,
			boolean showHiddenFiles, int filterMode, int limit,
			String positiveRegex, String negativeRegex,
			List<keepass2android.kp2afilechooser.FileEntry> results, boolean[] hasMoreFiles) {
		
		List<keepass2android.javafilestorage.JavaFileStorage.FileEntry> entries;
		try {
			entries = MainActivity.storageToTest.listFiles(dirName);
			for (keepass2android.javafilestorage.JavaFileStorage.FileEntry e: entries)
			{
				keepass2android.kp2afilechooser.FileEntry chooserEntry = convertEntry(e);
				results.add(chooserEntry);
			}
		} catch (Exception e1) {
			e1.printStackTrace();
		}
		
		

	}

	@Override
	protected boolean deletePath(String filename, boolean isRecursive) {
		try
		{
			MainActivity.storageToTest.delete(filename);
			return true;
		}
		catch (Exception e)
		{
			e.printStackTrace();
			return false;
		}
	}

	@Override
	protected boolean createDirectory(String dirname, String newDirName) {
		try
		{
			MainActivity.storageToTest.createFolder(dirname, newDirName);
			return true;
		}
		catch (Exception e)
		{
			e.printStackTrace();
			return false;
		}
	}

}
