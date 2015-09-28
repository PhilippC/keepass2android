package keepass2android.javafilestorage;

import android.util.Log;

import com.jcraft.jsch.UserInfo;

public class SftpUserInfo implements UserInfo {
	String _password;
	
	public SftpUserInfo(String password) {
		_password = password;
	}

	@Override
	public String getPassphrase() {
		
		return null;
	}

	@Override
	public String getPassword() {
		
		return _password;
	}

	@Override
	public boolean promptPassword(String message) {
		return true;
	}

	@Override
	public boolean promptPassphrase(String message) {
		return false; //passphrase not supported
	}

	@Override
	public boolean promptYesNo(String message) {
		return true; //continue all operations without user action
	}

	@Override
	public void showMessage(String message) {
		Log.d("KP2AJ", message);
	}

}
