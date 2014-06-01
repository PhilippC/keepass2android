package keepass2android.plugin.inputstick;

import java.util.ArrayList;

import com.inputstick.api.ConnectionManager;
import com.inputstick.api.InputStickStateListener;
import com.inputstick.api.basic.InputStickHID;
import com.inputstick.api.basic.InputStickKeyboard;
import com.inputstick.api.hid.HIDKeycodes;
import com.inputstick.api.layout.KeyboardLayout;

import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.content.res.Resources;
import android.os.AsyncTask;
import android.os.IBinder;
import android.preference.PreferenceManager;
import android.support.v4.app.NotificationCompat.Builder;
import android.util.Log;
import android.widget.Toast;

public class InputStickService extends Service implements InputStickStateListener {

	private static final String _TAG = "KP2AINPUTSTICK";
	public static final String DISCONNECT = "disconnect";
	private static final int NOTIFICATION_ID = 1;
	public static final String TYPE = "type";
	private static final String TYPE_QUEUE = "type_queue";
	
	@Override
	public void onCreate() {
		InputStickHID.addStateListener(this);
		super.onCreate();
	}
	private ArrayList<String> stringsToType = new ArrayList<String>(); 

	@Override
	public int onStartCommand(Intent intent, int flags, int startId) {
		
		if (intent != null)
			Log.d(_TAG, "starting with "+intent.getAction());
		else
			Log.d(_TAG, "starting with null intent");

		if (DISCONNECT.equals(intent.getAction()))
		{
			Log.d(_TAG, "disconnecting");
			try
			{
				InputStickHID.disconnect();	
			}
			catch (NullPointerException e)
			{
				Log.d(_TAG, "couldn't disconnect. Probably we never connected.");
			}
			
			stopSelf();
			return Service.START_NOT_STICKY;
		}
		showNotification();
		
		if (TYPE_QUEUE.equals(intent.getAction()))
		{
			typeQueue();
		}
		
		if (TYPE.equals(intent.getAction()))
		{
			int state = InputStickHID.getState();
			String stringToType = intent.getStringExtra(Intent.EXTRA_TEXT);
			
			switch (state) {
				case ConnectionManager.STATE_CONNECTED:
				case ConnectionManager.STATE_CONNECTING:
				case ConnectionManager.STATE_READY:
					typeString(stringToType);
					break;
				case ConnectionManager.STATE_DISCONNECTED:
				case ConnectionManager.STATE_FAILURE:	
					synchronized (stringsToType) {
						stringsToType.add(stringToType);
					}
					Log.d(_TAG, "trigger connect");
					InputStickHID.connect(getApplication());
					
					break;											
			}	
			
		}
		

		return Service.START_NOT_STICKY;

	}

	private void typeString(String stringToType) {
		Log.d(_TAG, "typing "+stringToType);
		
		if (stringToType.equals("\n"))
		{
			InputStickKeyboard.pressAndRelease(HIDKeycodes.NONE, HIDKeycodes.KEY_ENTER);
			return;
		}
		if (stringToType.equals("\t"))
		{
			InputStickKeyboard.pressAndRelease(HIDKeycodes.NONE, HIDKeycodes.KEY_TAB);
			return;
		}
		
		KeyboardLayout layout = KeyboardLayout.getLayout(PreferenceManager.getDefaultSharedPreferences(this).getString("kbd_layout", "en-US"));
		
		//currently supported layouts: de-DE, en-US, pl-PL, ru-RU
		layout.type(stringToType);
		
		
		//InputStickKeyboard.typeASCII(stringToType);
	}

	private void typeQueue() {
		synchronized (stringsToType) {
			for (String t:stringsToType)
			{
				Log.d(_TAG, "typing (after callback) "+t);
				typeString(t);	
			}
			stringsToType.clear();
		}
	}

	private void showNotification() {
		android.support.v4.app.NotificationCompat.Builder b = new Builder(this);
		b.setSmallIcon(R.drawable.ic_notification);

		b.setPriority(android.support.v4.app.NotificationCompat.PRIORITY_MIN);
		b.setContentTitle("KP2A InputStick");
		
		int state = InputStickHID.getState();
		
		Intent disconnectIntent = new Intent(this, InputStickService.class);
		disconnectIntent.setAction(DISCONNECT);
		
		switch (state) {
			case ConnectionManager.STATE_CONNECTED:
			case ConnectionManager.STATE_CONNECTING:
				b.setContentText("connecting...");

				b.addAction(android.R.drawable.ic_menu_close_clear_cancel, "disconnect", PendingIntent.getService(this, 1, disconnectIntent, 0));
				
				break;
			case ConnectionManager.STATE_READY:
				b.setContentText("InputStick connected.");

				b.addAction(android.R.drawable.ic_menu_close_clear_cancel, "disconnect", PendingIntent.getService(this, 1, disconnectIntent, 0));
				
				break;
			case ConnectionManager.STATE_DISCONNECTED:
			case ConnectionManager.STATE_FAILURE:	
				b.setContentText("InputStick failure");
				
				break;											
		}	
		startForeground(NOTIFICATION_ID, b.build());
	}
	
	@Override
	public IBinder onBind(Intent arg0) {
		// TODO Auto-generated method stub
		return null;
	}
	
	@Override
	public void onDestroy() {
		InputStickHID.removeStateListener(this);
		super.onDestroy();
	}

	@Override
	public void onStateChanged(int state) {
		showNotification();
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
							// TODO Auto-generated catch block
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
				Toast.makeText(this, "Failure connecting to InputStick! Do you have InputStickUtility installed?", Toast.LENGTH_LONG).show();
				
				break;		
			default:
				break;
		}	
		
	}

}
