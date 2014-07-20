package keepass2android.javafilestorage.skydrive;

import com.microsoft.live.LiveAuthException;
import com.microsoft.live.LiveAuthListener;
import com.microsoft.live.LiveConnectSession;
import com.microsoft.live.LiveStatus;

public class PrepareFileUsageListener implements LiveAuthListener {

	public Exception exception;
	public LiveStatus status;
	
	@Override
	public void onAuthError(LiveAuthException _exception,
			Object userState) {
		exception = _exception;
	}

	@Override
	public void onAuthComplete(LiveStatus _status,
			LiveConnectSession session, Object userState) 
	{
		status = _status;
	}

}
