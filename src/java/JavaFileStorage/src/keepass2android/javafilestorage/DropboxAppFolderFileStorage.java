package keepass2android.javafilestorage;

import com.dropbox.client2.session.Session.AccessType;

import android.content.Context;

public class DropboxAppFolderFileStorage extends DropboxFileStorage {

	public DropboxAppFolderFileStorage(Context ctx, String _appKey,
			String _appSecret) {
		super(ctx, _appKey, _appSecret, false, AccessType.APP_FOLDER);
		
		
	}
	
	public DropboxAppFolderFileStorage(Context ctx, String _appKey, String _appSecret, boolean clearKeysOnStart)
	{
		super(ctx, _appKey, _appSecret, clearKeysOnStart, AccessType.APP_FOLDER);
		
	}
	
	
	@Override
	public String getProtocolId() {
		return "dropboxKP2A";
	}

}
