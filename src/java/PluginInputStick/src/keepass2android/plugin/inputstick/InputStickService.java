package keepass2android.plugin.inputstick;

import java.util.ArrayList;

import android.app.AlertDialog;
import android.app.Service;
import android.content.Intent;
import android.os.AsyncTask;
import android.os.IBinder;
import android.util.Log;
import android.widget.Toast;

import com.inputstick.api.ConnectionManager;
import com.inputstick.api.InputStickStateListener;
import com.inputstick.api.basic.InputStickHID;
import com.inputstick.api.basic.InputStickKeyboard;
import com.inputstick.api.hid.HIDKeycodes;
import com.inputstick.api.layout.KeyboardLayout;

public class InputStickService extends Service implements InputStickStateListener {
	
	private class ItemToType {
		public String mText;
		public String mLayout;
		
		ItemToType(String text, String layout) {
			mText = text;
			mLayout = layout;
		}
		
		public void type() {
			typeString(mText, mLayout);
		}
	}

	private ArrayList<ItemToType> items = new ArrayList<ItemToType>(); 
	
	private static final String _TAG = "KP2AINPUTSTICK";
	public static final String DISCONNECT = "disconnect";
	public static final String TYPE = "type";
	
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

		if (DISCONNECT.equals(intent.getAction())) {
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
		}
		
		if (TYPE.equals(intent.getAction())) {
			int state = InputStickHID.getState();		
			String stringToType = intent.getStringExtra(Const.EXTRA_TEXT);
			String layoutToUse = intent.getStringExtra(Const.EXTRA_LAYOUT);
			
			switch (state) {
				case ConnectionManager.STATE_CONNECTED:
				case ConnectionManager.STATE_CONNECTING:
					synchronized (items) {
						items.add(new ItemToType(stringToType, layoutToUse));
					}						
					break;
				case ConnectionManager.STATE_READY:
					typeString(stringToType, layoutToUse);
					break;
				case ConnectionManager.STATE_DISCONNECTED:
				case ConnectionManager.STATE_FAILURE:	
					synchronized (items) {
						items.add(new ItemToType(stringToType, layoutToUse));
					}										
					Log.d(_TAG, "trigger connect");
					InputStickHID.connect(getApplication());
					
					break;											
			}				
		}
		return Service.START_NOT_STICKY;

	}

	private void typeString(String stringToType, String layoutToUse) {
		if (InputStickHID.getState() == ConnectionManager.STATE_READY) {
			Log.d(_TAG, "typing "+stringToType + " @ " + layoutToUse);		
			if (stringToType.equals("\n")) {
				InputStickKeyboard.pressAndRelease(HIDKeycodes.NONE, HIDKeycodes.KEY_ENTER);
				return;
			} else if (stringToType.equals("\t")) {
				InputStickKeyboard.pressAndRelease(HIDKeycodes.NONE, HIDKeycodes.KEY_TAB);
				return;
			} else {		
				KeyboardLayout l = KeyboardLayout.getLayout(layoutToUse);
				l.type(stringToType);
			}
		} else {
			Log.d(_TAG, "typing NOT READY");		
		}
	}

	private void typeQueue() {
		synchronized (items) {
			for (ItemToType itt : items)
			{
				Log.d(_TAG, "typing (after callback) " + itt.mText);
				itt.type();
			}
			items.clear();
		}
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
				/*
				Intent typeQueue = new Intent(this, InputStickService.class);
				typeQueue.setAction(TYPE_QUEUE);
				startService(typeQueue);
					*/
				new AsyncTask<Object, Object, Object>() {

					@Override
					protected Object doInBackground(Object... params) {						
						try {
							Thread.sleep(1000, 0);
						} catch (InterruptedException e) {
							e.printStackTrace();
						}
						typeQueue();						
						return null;
					}

				};
				typeQueue();
				break;
			case ConnectionManager.STATE_DISCONNECTED:
				Log.d(_TAG, "stopping service. State = "+state);
				stopSelf();
				break;
			case ConnectionManager.STATE_FAILURE:
				Log.d(_TAG, "stopping service. State = "+state);
				stopSelf();
				AlertDialog ad = InputStickHID.getDownloadDialog(this); 
				if (ad != null) {
					//Utility application not installed
					ad.show();
				} else {
					Toast.makeText(this, "Failure connecting to InputStick!", Toast.LENGTH_LONG).show();
				}
				break;		
			default:
				break;
		}	
		
	}

}
