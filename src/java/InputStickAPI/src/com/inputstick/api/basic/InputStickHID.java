package com.inputstick.api.basic;

import java.util.Timer;
import java.util.TimerTask;
import java.util.Vector;

import android.app.AlertDialog;
import android.app.Application;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.net.Uri;

import com.inputstick.api.BTConnectionManager;
import com.inputstick.api.ConnectionManager;
import com.inputstick.api.HIDInfo;
import com.inputstick.api.IPCConnectionManager;
import com.inputstick.api.InputStickDataListener;
import com.inputstick.api.InputStickError;
import com.inputstick.api.InputStickStateListener;
import com.inputstick.api.OnEmptyBufferListener;
import com.inputstick.api.Packet;
import com.inputstick.api.Util;
import com.inputstick.api.hid.HIDReport;
import com.inputstick.api.hid.HIDTransaction;
import com.inputstick.api.hid.HIDTransactionQueue;
import com.inputstick.init.BasicInitManager;
import com.inputstick.init.DeviceInfo;
import com.inputstick.init.InitManager;

public class InputStickHID implements InputStickStateListener, InputStickDataListener  {
	
	public static final int INTERFACE_KEYBOARD = 0;
	public static final int INTERFACE_CONSUMER = 1;
	public static final int INTERFACE_MOUSE = 2;
	
	
	//private static final String mTag = "InputStickBasic";		
	private static ConnectionManager mConnectionManager;
	
	private static Vector<InputStickStateListener> mStateListeners = new Vector<InputStickStateListener>();
	protected static Vector<OnEmptyBufferListener> mBufferEmptyListeners = new Vector<OnEmptyBufferListener>();	
	
	private static InputStickHID instance = new InputStickHID();
	private static HIDInfo mHIDInfo;
	private static DeviceInfo mDeviceInfo;
	
	private static HIDTransactionQueue keyboardQueue;
	private static HIDTransactionQueue mouseQueue;
	private static HIDTransactionQueue consumerQueue;
	
	
	
	// >= FW 0.93
	private static Timer t1;
	private static boolean constantUpdateMode;
	
	
	private static int mKeyboardReportMultiplier; //enables "slow" typing by multiplying HID reports
	
	private InputStickHID() {
	}
	
	public static InputStickHID getInstance() {
		return instance;
	}
	
	private static void init() {
		mHIDInfo = new HIDInfo();
		constantUpdateMode = false;
		keyboardQueue = new HIDTransactionQueue(INTERFACE_KEYBOARD, mConnectionManager);
		mouseQueue = new HIDTransactionQueue(INTERFACE_MOUSE, mConnectionManager);
		consumerQueue = new HIDTransactionQueue(INTERFACE_CONSUMER, mConnectionManager);		
		
		mConnectionManager.addStateListener(instance);
		mConnectionManager.addDataListener(instance);
		mConnectionManager.connect();		
	}
	
	//direct Bluetooth connection, custom InitManager (use BT2.0)
	//mac - Bluetooth MAC address
	//key - use null if InputStick is not password protected, otherwise provide 16byte key: MD5(password)
	public static void connect(Application app, String mac, byte[] key, InitManager initManager) {
		connect(app, mac, key, initManager, false);
	}	
	
	//direct Bluetooth connection, custom InitManager
	//is40 - use true if target device is BluetoothLowEnergy (4.0) type
	//mac - Bluetooth MAC address
	//key - use null if InputStick is not password protected, otherwise provide 16byte key: MD5(password)
	//is40 - use true if target device is BluetoothLowEnergy (4.0) type
	public static void connect(Application app, String mac, byte[] key, InitManager initManager, boolean isBT40) {
		mConnectionManager = new BTConnectionManager(initManager, app, mac, key, isBT40);		
		init();
	}
	
	//direct Bluetooth connection
	//key - use null if InputStick is not password protected, otherwise provide 16byte key: MD5(password)
	//is40 - use true if target device is BluetoothLowEnergy (4.0) type
	public static void connect(Application app, String mac, byte[] key, boolean isBT40) {
		mConnectionManager = new BTConnectionManager(new BasicInitManager(key), app, mac, key, isBT40);
		init();
	}
	
	//direct Bluetooth connection (use BT2.0)
	//key - use null if InputStick is not password protected, otherwise provide 16byte key: MD5(password)
	public static void connect(Application app, String mac, byte[] key) {
		connect(app, mac, key, false);
	}
	
	//use InputStickUtility to connect with InputStick
	public static void connect(Application app) {
		mConnectionManager = new IPCConnectionManager(app);
		init();
	}			
	
	//closes Bluetooth connection
	public static void disconnect() { 
		if (mConnectionManager != null) {
			mConnectionManager.disconnect();
		}
	}
	
	//requests USB host to resume from sleep mode (must be supported by USB host)
	//note: InputStick will most likely be in STATE_CONNECTED state instead of STATE_READY
	public static void wakeUpUSBHost() {
		if (isConnected()) {
			Packet p = new Packet(false, Packet.CMD_USB_RESUME);
            InputStickHID.sendPacket(p);
			mConnectionManager.sendPacket(p);
		}
	}
	
	public static DeviceInfo getDeviceInfo() {
		if ((isReady()) && (mDeviceInfo != null)) {
			return mDeviceInfo;
		} else {
			return null;
		}
	}
	
	public static HIDInfo getHIDInfo() {
		return mHIDInfo;
	}
	
	//returns current connection state
	public static int getState() {
		if (mConnectionManager != null) {
			return mConnectionManager.getState();
		} else {
			return ConnectionManager.STATE_DISCONNECTED;
		}
	}
	
	//returns last error code
	public static int getErrorCode() {
		if (mConnectionManager != null) {
			return mConnectionManager.getErrorCode();
		} else {
			return InputStickError.ERROR_UNKNOWN;
		} 
	}
	
	//returns true if Bluetooth connection is established between the device and InputStick.
	//note - InputStick may be not ready yet to accept keyboard/mouse data
	public static boolean isConnected() {
		if ((getState() == ConnectionManager.STATE_READY) ||  (getState() == ConnectionManager.STATE_CONNECTED)) {
			return true;
		} else {
			return false;
		}
	}
	
	//returns true if InputStick is ready for keyboard/mouse data
	public static boolean isReady() {
		if (getState() == ConnectionManager.STATE_READY) {
			return true;
		} else {
			return false;
		}
	}
	
	//adds state listener. Listeners will be notified about change of connection state
	public static void addStateListener(InputStickStateListener listener) {
		if (listener != null) {
			if ( !mStateListeners.contains(listener)) {
				mStateListeners.add(listener);
			}
		}
	}
	
	
	public static void removeStateListener(InputStickStateListener listener) {
		if (listener != null) {
			mStateListeners.remove(listener);
		}
	}
	
	//adds buffer listener. Listeners will be notified when local (application) or remote (InputStick) HID report buffer is empty
	public static void addBufferEmptyListener(OnEmptyBufferListener listener) {
		if (listener != null) {
			if ( !mBufferEmptyListeners.contains(listener)) {
				mBufferEmptyListeners.add(listener);
			}
		}
	}
	
	public static void removeBufferEmptyListener(OnEmptyBufferListener listener) {
		if (listener != null) {
			mBufferEmptyListeners.remove(listener);
		}		
	}
	
	public static Vector<OnEmptyBufferListener> getBufferEmptyListeners() {
		return mBufferEmptyListeners;
	}
	
	//reports added to keyboard queue will be multiplied by reportMultiplier times. This allows to type text slower.
	//NOTE: using high multiplier values can make transactions larger than available buffer and as a result they will be splitted!
	//this can cause problem if connection is lost (stuck keys)
	//TIP: remember to manually set multiplier value back to 1 when slow typing mode is no longer needed!!!
	public static void setKeyboardReportMultiplier(int reportMultiplier) {
		mKeyboardReportMultiplier = reportMultiplier;
	}

	//adds transaction to keyboard queue. If possible, all reports form a signel transactions will be sent in a single packet
	public static void addKeyboardTransaction(HIDTransaction transaction) {
		if ((transaction != null) && (keyboardQueue != null)) {
			//keyboardQueue.addTransaction(transaction);
			
			if (mKeyboardReportMultiplier > 1) {
				HIDTransaction multipliedTransaction = new HIDTransaction();
				HIDReport r;
				for (int i = 0; i < transaction.getReportsCount(); i++) {
					r = transaction.getHIDReportAt(i);
					for (int j = 0; j < mKeyboardReportMultiplier; j++) {
						multipliedTransaction.addReport(r);
					}
				}
				keyboardQueue.addTransaction(multipliedTransaction);
			} else {
				keyboardQueue.addTransaction(transaction);
			}
		}
	}
	
	//adds transaction to mouse queue. If possible, all reports form a signel transactions will be sent in a single packet
	public static void addMouseTransaction(HIDTransaction transaction) {
		if ((transaction != null) && (mouseQueue != null)) {
			mouseQueue.addTransaction(transaction);
		}
	}
	
	//adds transaction to consumer queue. If possible, all reports form a signel transactions will be sent in a single packet
	public static void addConsumerTransaction(HIDTransaction transaction) {
		if ((transaction != null) && (consumerQueue != null)) {
			consumerQueue.addTransaction(transaction);
		}
	}
	
	//removes all reports from keyboard buffer
	public static void clearKeyboardBuffer() {
		if (keyboardQueue != null) {
			keyboardQueue.clearBuffer();
		}
	}
	
	//removes all reports from mouse buffer
	public static void clearMouseBuffer() {
		if (mouseQueue != null) {
			mouseQueue.clearBuffer();
		}
	}
	
	//removes all reports from consumer buffer
	public static void clearConsumerBuffer() {
		if (consumerQueue != null) {
			consumerQueue.clearBuffer();
		}
	}
	
	//sends packet to InputStick
	public static boolean sendPacket(Packet p) {
		if (mConnectionManager != null) {
			mConnectionManager.sendPacket(p);
			return true;
		} else {
			return false;
		}
	}				
	
	@Override
	public void onStateChanged(int state) {		
		if ((state == ConnectionManager.STATE_DISCONNECTED) && (t1 != null)) {
			t1.cancel();
			t1 = null;
		}
		for (InputStickStateListener listener : mStateListeners) {
			listener.onStateChanged(state);
		}		
	}
	
	//returns true if local (application) keyboard report buffer is empty. It is still possible that there are reports queued in InputStick's buffer.
	public static boolean isKeyboardLocalBufferEmpty() {
		if (keyboardQueue != null) {
			return keyboardQueue.isLocalBufferEmpty();
		} else {
			return true;
		}
	}
	
	//returns true if local (application) mouse report buffer is empty. It is still possible that there are reports queued in InputStick's buffer.
	public static boolean isMouseLocalBufferEmpty() {
		if (mouseQueue != null) {
			return mouseQueue.isLocalBufferEmpty();
		} else {
			return true;
		}
	}
	
	//returns true if local (application) consumer report buffer is empty. It is still possible that there are reports queued in InputStick's buffer.
	public static boolean isConsumerLocalBufferEmpty() {
		if (consumerQueue != null) {
			return consumerQueue.isLocalBufferEmpty();
		} else {
			return true;
		}
	}
	
	//returns true if remote (InputStick) keyboard report buffer is empty. No more keyboard reports will be send to USB host
	public static boolean isKeyboardRemoteBufferEmpty() {
		if (keyboardQueue != null) {
			return keyboardQueue.isRemoteBufferEmpty();
		} else {
			return true;
		}		
	}
	
	//returns true if remote (InputStick) mouse report buffer is empty. No more mouse reports will be send to USB host
	public static boolean isMouseRemoteBufferEmpty() {
		if (mouseQueue != null) {
			return mouseQueue.isRemoteBufferEmpty();
		} else {
			return true;
		}
	}
	
	//returns true if remote (InputStick) consumer report buffer is empty. No more consumer reports will be send to USB host
	public static boolean isConsumerRemoteBufferEmpty() {
		if (consumerQueue != null) {
			return consumerQueue.isRemoteBufferEmpty();
		} else {
			return true;
		}
	}

	@Override
	public void onInputStickData(byte[] data) {
		byte cmd = data[0];
		if (cmd == Packet.CMD_FW_INFO) {
			mDeviceInfo = new DeviceInfo(data);		
		}
		
		if (cmd == Packet.CMD_HID_STATUS) {
			mHIDInfo.update(data);
			
			if (mHIDInfo.isSentToHostInfoAvailable()) {
				// >= FW 0.93
				keyboardQueue.deviceReady(mHIDInfo, mHIDInfo.getKeyboardReportsSentToHost());
				mouseQueue.deviceReady(mHIDInfo, mHIDInfo.getMouseReportsSentToHost());
				consumerQueue.deviceReady(mHIDInfo, mHIDInfo.getConsumerReportsSentToHost());
				
				if ( !constantUpdateMode) {
					Util.log("Constatnt update mode enabled");
					constantUpdateMode = true;
					t1 = new Timer();
					t1.schedule(new TimerTask() {
						@Override
						public void run() {
							keyboardQueue.sendToBuffer(false);
							mouseQueue.sendToBuffer(false);
							consumerQueue.sendToBuffer(false);
						}
					}, 5,5);	
				}			
			} else { 					
				//previous FW versions
				if (mHIDInfo.isKeyboardReady()) {
					keyboardQueue.deviceReady(null, 0);
				}
				if (mHIDInfo.isMouseReady()) {
					mouseQueue.deviceReady(null, 0);
				}
				if (mHIDInfo.isConsumerReady()) {
					consumerQueue.deviceReady(null, 0);
				}			
			}
			
			InputStickKeyboard.setLEDs(mHIDInfo.getNumLock(), mHIDInfo.getCapsLock(), mHIDInfo.getScrollLock());			
		}
	}	
	
	//returns "Download InputStickUtility" dialog, if connection attepmt resulted in error caused by InputStickUtility not being installed on the device.
	//otherwise returns null
	public static AlertDialog getDownloadDialog(final Context ctx) {
		if (mConnectionManager.getErrorCode() == InputStickError.ERROR_ANDROID_NO_UTILITY_APP) {
			AlertDialog.Builder downloadDialog = new AlertDialog.Builder(ctx);
			downloadDialog.setTitle("No InputStickUtility app installed");
			downloadDialog.setMessage("InputStickUtility is required to run this application. Download now?\nNote: InputStick USB receiver hardware is also required.");
			downloadDialog.setPositiveButton("Yes",
					new DialogInterface.OnClickListener() {
						@Override
						public void onClick(DialogInterface dialogInterface, int i) {
							final String appPackageName = "com.inputstick.apps.inputstickutility";
							try {
								ctx.startActivity(new Intent(
										Intent.ACTION_VIEW, Uri
												.parse("market://details?id="
														+ appPackageName)));
							} catch (android.content.ActivityNotFoundException anfe) {
								ctx.startActivity(new Intent(
										Intent.ACTION_VIEW,
										Uri.parse("http://play.google.com/store/apps/details?id="
												+ appPackageName)));
							}
						}
					});
			downloadDialog.setNegativeButton("No",
					new DialogInterface.OnClickListener() {
						@Override
						public void onClick(DialogInterface dialogInterface, int i) {
						}
					});
			return downloadDialog.show();
		} else {
			return null;
		}
	}	
	

}
