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
import com.inputstick.api.hid.HIDTransaction;
import com.inputstick.api.hid.HIDTransactionQueue;
import com.inputstick.init.InitManager;

public class InputStickHID implements InputStickStateListener, InputStickDataListener  {
	
	public static final int INTERFACE_KEYBOARD = 0;
	public static final int INTERFACE_CONSUMER = 1;
	public static final int INTERFACE_MOUSE = 2;
	
	
	//private static final String mTag = "InputStickBasic";		
	private static ConnectionManager mConnectionManager;
	
	private static Vector<InputStickStateListener> mStateListeners = new Vector<InputStickStateListener>();
	
	
	private static InputStickHID instance = new InputStickHID();
	private static HIDInfo mHIDInfo;
	
	private static HIDTransactionQueue keyboardQueue;
	private static HIDTransactionQueue mouseQueue;
	private static HIDTransactionQueue consumerQueue;
	
	// >= FW 0.93
	private static Timer t1;
	private static boolean constantUpdateMode;
	
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
	
	//direct Bluetooth connection, custom InitManager
	public static void connect(Application app, String mac, byte[] key, InitManager initManager) {
		mConnectionManager = new BTConnectionManager(initManager, app, mac, key);		
		init();
	}	
	
	//direct Bluetooth connection
	public static void connect(Application app, String mac, byte[] key) {
		mConnectionManager = new BTConnectionManager(new InitManager(key), app, mac, key);
		init();
	}
	
	//use background service & DeviceManager
	public static void connect(Application app) {
		mConnectionManager = new IPCConnectionManager(app);
		init();
	}			
	
	public static void disconnect() { 
		//TODO check state?
		mConnectionManager.disconnect();
	}
	
	public static int getState() {
		if (mConnectionManager != null) {
			return mConnectionManager.getState();
		} else {
			return ConnectionManager.STATE_DISCONNECTED;
		}
	}
	
	public static int getErrorCode() {
		if (mConnectionManager != null) {
			return mConnectionManager.getErrorCode();
		} else {
			return InputStickError.ERROR_UNKNOWN;
		} 
	}
	
	
	
	public static boolean isReady() {
		if (getState() == ConnectionManager.STATE_READY) {
			return true;
		} else {
			return false;
		}
	}
	
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
	
	public static void addBufferEmptyListener(OnEmptyBufferListener listener) {
		if (listener != null) {
			keyboardQueue.addBufferEmptyListener(listener);
			mouseQueue.addBufferEmptyListener(listener);
			consumerQueue.addBufferEmptyListener(listener);
		}
	}
	
	public static void removeBufferEmptyListener(OnEmptyBufferListener listener) {
		if (listener != null) {
			keyboardQueue.removeBufferEmptyListener(listener);
			mouseQueue.removeBufferEmptyListener(listener);
			consumerQueue.removeBufferEmptyListener(listener);
		}
	}

	public static void addKeyboardTransaction(HIDTransaction transaction) {
		keyboardQueue.addTransaction(transaction);
	}
	
	public static void addMouseTransaction(HIDTransaction transaction) {
		mouseQueue.addTransaction(transaction);
	}
	
	public static void addConsumerTransaction(HIDTransaction transaction) {
		consumerQueue.addTransaction(transaction);
	}
	
	public static void clearKeyboardBuffer() {
		keyboardQueue.clearBuffer();
	}
	
	public static void clearMouseBuffer() {
		mouseQueue.clearBuffer();
	}
	
	public static void clearConsumerBuffer() {
		consumerQueue.clearBuffer();
	}
	
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
	
	public static boolean isKeyboardLocalBufferEmpty() {
		return keyboardQueue.isLocalBufferEmpty();
	}
	public static boolean isMouseLocalBufferEmpty() {
		return mouseQueue.isLocalBufferEmpty();
	}
	public static boolean isConsumerLocalBufferEmpty() {
		return consumerQueue.isLocalBufferEmpty();
	}
	
	public static boolean isKeyboardRemoteBufferEmpty() {
		return keyboardQueue.isRemoteBufferEmpty();
	}
	public static boolean isMouseRemoteBufferEmpty() {
		return mouseQueue.isRemoteBufferEmpty();
	}
	public static boolean isConsumerRemoteBufferEmpty() {
		return consumerQueue.isRemoteBufferEmpty();
	}

	@Override
	public void onInputStickData(byte[] data) {
		if (data[0] == Packet.CMD_HID_STATUS) {
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
	
	
	public static AlertDialog getDownloadDialog(final Context ctx) {
		if (mConnectionManager.getErrorCode() == InputStickError.ERROR_ANDROID_NO_UTILITY_APP) {
			AlertDialog.Builder downloadDialog = new AlertDialog.Builder(ctx);
			downloadDialog.setTitle("No InputStickUtility app installed");
			downloadDialog.setMessage("InputStickUtility is required to run this application. Download now?");
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
