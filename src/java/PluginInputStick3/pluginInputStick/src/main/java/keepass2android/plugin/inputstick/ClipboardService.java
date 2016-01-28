package keepass2android.plugin.inputstick;

import com.inputstick.api.hid.HIDKeycodes;

import android.app.Service;
import android.content.ClipData;
import android.content.ClipDescription;
import android.content.ClipboardManager;
import android.content.Intent;
import android.os.Handler;
import android.os.IBinder;
import android.widget.Toast;

public class ClipboardService extends Service {		
	
	private String layout;

	Handler delayhandler = new Handler();
	private Runnable mUpdateTimeTask = new Runnable() {
		public void run() {			
			stopSelf();
		}
	};

	@Override
	public IBinder onBind(Intent arg0) {
		return null;
	}

	@Override
	public void onDestroy() {
		Toast.makeText(this, R.string.text_clipboard_disabled, Toast.LENGTH_SHORT).show();
		delayhandler.removeCallbacksAndMessages(null);
		if (myClipBoard != null) {
			myClipBoard.removePrimaryClipChangedListener(mPrimaryClipChangedListener);
			myClipBoard = null;
		}
		super.onDestroy();

	}

	@Override
	public int onStartCommand(Intent intent, int flags, int startId) {
		layout = intent.getStringExtra(Const.EXTRA_LAYOUT);
		if (myClipBoard == null) {
			myClipBoard = (ClipboardManager)getSystemService(android.content.Context.CLIPBOARD_SERVICE);
			myClipBoard.addPrimaryClipChangedListener(mPrimaryClipChangedListener);
		}		
		delayhandler.removeCallbacksAndMessages(null);
		delayhandler.postDelayed(mUpdateTimeTask, Const.CLIPBOARD_TIMEOUT_MS);
		Toast.makeText(this, R.string.text_clipboard_copy_now, Toast.LENGTH_LONG).show();
		return START_NOT_STICKY;
	}

	
	ClipboardManager myClipBoard ;
	ClipboardManager.OnPrimaryClipChangedListener mPrimaryClipChangedListener = new ClipboardManager.OnPrimaryClipChangedListener() {
	    public void onPrimaryClipChanged() {
	        ClipData clipData = myClipBoard.getPrimaryClip();
	        if (clipData.getDescription().hasMimeType(ClipDescription.MIMETYPE_TEXT_PLAIN)) {
	           String text = clipData.getItemAt(0).getText().toString();
	           if (text != null) {
	        	   UserPreferences userPrefs = ActionManager.getUserPrefs();
	        	   ActionManager.queueText(text, layout);
	        	   if (userPrefs.isClipboardAutoEnter()) {
	        		   ActionManager.queueKey(HIDKeycodes.NONE, HIDKeycodes.KEY_ENTER);
	        	   }
	        	   if (userPrefs.isClipboardAutoDisable()) {
	        		   delayhandler.removeCallbacksAndMessages(null);
	        		   stopSelf();
	        	   }
	           }
	    	}
	    }
	};

}
