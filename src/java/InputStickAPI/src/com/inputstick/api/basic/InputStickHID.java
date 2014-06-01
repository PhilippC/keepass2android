package com.inputstick.api.basic;

import java.util.Vector;

import android.app.Application;

import com.inputstick.api.BTConnectionManager;
import com.inputstick.api.ConnectionManager;
import com.inputstick.api.HIDInfo;
import com.inputstick.api.IPCConnectionManager;
import com.inputstick.api.InputStickDataListener;
import com.inputstick.api.InputStickStateListener;
import com.inputstick.api.Packet;
import com.inputstick.api.hid.HIDTransaction;
import com.inputstick.api.hid.HIDTransactionQueue;
import com.inputstick.init.InitManager;

public class InputStickHID implements InputStickStateListener, InputStickDataListener  {
	
	//private static final String mTag = "InputStickBasic";		
	
	private static ConnectionManager mConnectionManager;
	
	private static Vector<InputStickStateListener> mStateListeners = new Vector<InputStickStateListener>();
	
	private static InputStickHID instance = new InputStickHID();
	private static HIDInfo mHIDInfo = new HIDInfo();
	
	private static HIDTransactionQueue keyboardQueue;
	private static HIDTransactionQueue mouseQueue;
	private static HIDTransactionQueue consumerQueue;
	
	private InputStickHID() {
	}
	
	public static InputStickHID getInstance() {
		return instance;
	}
	
	private static void init() {
		keyboardQueue = new HIDTransactionQueue(HIDTransactionQueue.KEYBOARD, mConnectionManager);
		mouseQueue = new HIDTransactionQueue(HIDTransactionQueue.MOUSE, mConnectionManager);
		consumerQueue = new HIDTransactionQueue(HIDTransactionQueue.CONSUMER, mConnectionManager);		
		
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
		//mConnectionManager = new BTConnectionManager(new BasicInitManager(key), app, mac, reflections, key);
		//mConnectionManager = new BTConnectionManager(new BasicInitManager(key), app, mac, key);
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
	
	public static boolean isReady() {
		if (getState() == ConnectionManager.STATE_READY) {
			return true;
		} else {
			return false;
		}
	}
	
	public static void addStateListener(InputStickStateListener listener) {
		if (listener != null) {
			mStateListeners.add(listener);
		}
	}
	
	public static void removeStateListener(InputStickStateListener listener) {
		if (listener != null) {
			mStateListeners.remove(listener);
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
		for (InputStickStateListener listener : mStateListeners) {
			listener.onStateChanged(state);
		}		
	}

	@Override
	public void onInputStickData(byte[] data) {
		if (data[0] == Packet.CMD_HID_STATUS) {
			mHIDInfo.update(data);

			if (mHIDInfo.isKeyboardReady()) {
				keyboardQueue.deviceReady();
			}
			if (mHIDInfo.isMouseReady()) {
				mouseQueue.deviceReady();
			}
			if (mHIDInfo.isConsumerReady()) {
				consumerQueue.deviceReady();
			}			
			
			InputStickKeyboard.setLEDs(mHIDInfo.getNumLock(), mHIDInfo.getCapsLock(), mHIDInfo.getScrollLock());			
		}
	}	
	
	

}
