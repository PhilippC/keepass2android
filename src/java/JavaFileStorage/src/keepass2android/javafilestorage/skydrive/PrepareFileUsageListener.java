package keepass2android.javafilestorage.skydrive;

import com.microsoft.live.LiveAuthException;
import com.microsoft.live.LiveAuthListener;
import com.microsoft.live.LiveConnectSession;
import com.microsoft.live.LiveStatus;

public class PrepareFileUsageListener implements LiveAuthListener {

	public Exception exception;
	public LiveStatus status;
	
	volatile boolean done;
	public LiveConnectSession session;
	
	public boolean isDone()
	{
		return done;
	}
	
	@Override
	public void onAuthError(LiveAuthException _exception,
			Object userState) {
		exception = _exception;
		done = true;
	}

	@Override
	public void onAuthComplete(LiveStatus _status,
			LiveConnectSession _session, Object userState) 
	{
		status = _status;
		session = _session;
		done = true;
	}

}
