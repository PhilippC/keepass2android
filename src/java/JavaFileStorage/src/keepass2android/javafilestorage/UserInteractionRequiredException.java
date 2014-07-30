package keepass2android.javafilestorage;

import com.google.api.client.googleapis.extensions.android.gms.auth.UserRecoverableAuthIOException;

public class UserInteractionRequiredException extends Exception {

	public UserInteractionRequiredException(UserRecoverableAuthIOException e) {
		super(e);
	}
	
	public UserInteractionRequiredException() {

	}

	/**
	 * 
	 */
	private static final long serialVersionUID = 1L;
	
	

}
