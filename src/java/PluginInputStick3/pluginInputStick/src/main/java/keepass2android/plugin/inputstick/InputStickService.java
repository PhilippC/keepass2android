package keepass2android.plugin.inputstick;

import java.util.ArrayList;

import android.app.AlertDialog;
import android.app.Service;
import android.content.Intent;
import android.os.Bundle;
import android.os.IBinder;
import android.util.Log;
import android.widget.Toast;

import com.inputstick.api.ConnectionManager;
import com.inputstick.api.InputStickStateListener;
import com.inputstick.api.basic.InputStickHID;
import com.inputstick.api.basic.InputStickKeyboard;
import com.inputstick.api.hid.HIDTransaction;
import com.inputstick.api.hid.KeyboardReport;

public class InputStickService extends Service implements InputStickStateListener {
	
	private static final String _TAG = "KP2AINPUTSTICK";	
	
	private ArrayList<ItemToExecute> items = new ArrayList<ItemToExecute>(); 

	
	@Override
	public void onCreate() {
		InputStickHID.addStateListener(this);
		super.onCreate();
	}	

	@Override
	public int onStartCommand(Intent intent, int flags, int startId) {
		
		if (intent != null)
			Log.d(_TAG, "starting with "+intent.getAction());
		else
			Log.d(_TAG, "starting with null intent");

		if (Const.SERVICE_DISCONNECT.equals(intent.getAction())) {
			Log.d(_TAG, "disconnecting");
			try {
				int state = InputStickHID.getState();
				switch (state) {
					case ConnectionManager.STATE_CONNECTED:
					case ConnectionManager.STATE_CONNECTING:
					case ConnectionManager.STATE_READY:
						InputStickHID.disconnect();	
						break;
					case ConnectionManager.STATE_DISCONNECTED:
					case ConnectionManager.STATE_FAILURE:	
						break;
					default:
						InputStickHID.disconnect();	
				}
			} catch (NullPointerException e) {
				Log.d(_TAG, "couldn't disconnect. Probably we never connected.");
			}
			
			stopSelf();
			return Service.START_NOT_STICKY;
		} else if (Const.SERVICE_CONNECT.equals(intent.getAction())) {
			if ( !InputStickHID.isConnected()) {
				InputStickHID.connect(getApplication());
			}			
		} else if (Const.SERVICE_EXEC.equals(intent.getAction())) {
			int state = InputStickHID.getState();		
			Bundle b = intent.getExtras();
			//Log.d(_TAG, "type params: "+params);			
			switch (state) {
				case ConnectionManager.STATE_CONNECTED:
				case ConnectionManager.STATE_CONNECTING:
					synchronized (items) {
						items.add(new ItemToExecute(b));
					}						
					break;
				case ConnectionManager.STATE_READY:
					new ItemToExecute(b).execute();
					break;
				case ConnectionManager.STATE_DISCONNECTED:
				case ConnectionManager.STATE_FAILURE:	
					synchronized (items) {
						items.add(new ItemToExecute(b));
					}										
					Log.d(_TAG, "trigger connect");
					InputStickHID.connect(getApplication());					
					break;											
			}				
		} else {
			//unknown action
		}
		return Service.START_NOT_STICKY;

	}
	
	@Override
	public IBinder onBind(Intent arg0) {
		return null;
	}
	
	@Override
	public void onDestroy() {
		InputStickHID.removeStateListener(this);
		super.onDestroy();
	}

	@Override
	public void onStateChanged(int state) {
		Log.d(_TAG, "state changed: "+state);
		switch (state) {
			case ConnectionManager.STATE_READY:					
				executeQueue();
				break;
			case ConnectionManager.STATE_DISCONNECTED:
				Log.d(_TAG, "stopping service. State = "+state);
				stopSelf();
				break;
			case ConnectionManager.STATE_FAILURE:
				Log.d(_TAG, "stopping service. State = "+state);				
				AlertDialog ad = InputStickHID.getDownloadDialog(this); 
				if (ad != null) {
					//InputStickUtility application not installed
					ad.show();
				} else {
					Toast.makeText(this, R.string.text_connection_failed, Toast.LENGTH_LONG).show();
				}
				stopSelf();
				break;		
			default:
				break;
		}	
		
	}
	
	private void executeQueue() {
		dummyKeyPresses(15);
		synchronized (items) {
			for (ItemToExecute itt : items) {
				Log.d(_TAG, "executing (after callback) ");
				itt.execute();
			}
			items.clear();
		}
	}
	
	//short delay achieved by sending empty keyboard reports 
	private void dummyKeyPresses(int keys) {		
		HIDTransaction t = new HIDTransaction();
		for (int i = 0; i < keys * 3; i++) {  // 1 keypress = 3 HID reports (modifier, modifier+key, all released)
			t.addReport(new KeyboardReport((byte)0x00, (byte)0x00));
		}
		InputStickHID.addKeyboardTransaction(t);
	}
	
	
	
	private class ItemToExecute {
		public Bundle mBundle;
		ItemToExecute(Bundle b) {
			mBundle = b;
		}
		
		public void execute() {
			if ((InputStickHID.getState() == ConnectionManager.STATE_READY) && (mBundle != null)) {				
				String action = mBundle.getString(Const.EXTRA_ACTION);
				InputStickHID.setKeyboardReportMultiplier(mBundle.getInt(Const.EXTRA_REPORT_MULTIPLIER, 1));				
				
				if (Const.ACTION_TYPE.equals(action)) {
					String text = mBundle.getString(Const.EXTRA_TEXT);
					String layout = mBundle.getString(Const.EXTRA_LAYOUT);
					InputStickKeyboard.type(text, layout);							
				} else if (Const.ACTION_KEY_PRESS.equals(action)) {
					byte modifier = mBundle.getByte(Const.EXTRA_MODIFIER);
					byte key = mBundle.getByte(Const.EXTRA_KEY);					
					InputStickKeyboard.pressAndRelease(modifier, key);
				} else if (Const.ACTION_DELAY.equals(action)) {
					int reports = mBundle.getInt(Const.EXTRA_DELAY, 0) / 4; //1 report / 4ms
					dummyKeyPresses(reports);
				} else {
					//unknown action type!
				}				
				InputStickHID.setKeyboardReportMultiplier(1);			
			}
		}
	}

}
