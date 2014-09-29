package com.inputstick.api.basic;

import java.util.Vector;

import android.util.SparseArray;

import com.inputstick.api.InputStickKeyboardListener;
import com.inputstick.api.hid.HIDKeycodes;
import com.inputstick.api.hid.HIDTransaction;
import com.inputstick.api.hid.KeyboardReport;

public class InputStickKeyboard {
	
	//private static final String mTag = "InputStickKeyboard";
	
	private static final byte NONE = (byte)0;
	
	private static final byte LED_NUM_LOCK = 1;
	private static final byte LED_CAPS_LOCK = 2;
	private static final byte LED_SCROLL_LOCK = 4;
	
	private static boolean mReportProtocol;		
	private static boolean mNumLock;
	private static boolean mCapsLock;
	private static boolean mScrollLock;
	
	private static Vector<InputStickKeyboardListener> mKeyboardListeners = new Vector<InputStickKeyboardListener>();
	
	private static final SparseArray<String> ledsMap;
    static
    {
    	ledsMap = new SparseArray<String>();
    	ledsMap.put(LED_NUM_LOCK, 								"NumLock");
    	ledsMap.put(LED_CAPS_LOCK, 								"CapsLock");
    	ledsMap.put(LED_SCROLL_LOCK, 							"ScrollLock");
    }
	
	private InputStickKeyboard() {
	}
	
	public static void addKeyboardListener(InputStickKeyboardListener listener) {
		if (listener != null) {
			if ( !mKeyboardListeners.contains(listener)) {
				mKeyboardListeners.add(listener);
			}
		}
	}
	
	public static void removeKeyboardListener(InputStickKeyboardListener listener) {
		if (listener != null) {
			mKeyboardListeners.remove(listener);
		}
	}		
	
	protected void setReportProtocol(boolean reportProtocol) {
		mReportProtocol = reportProtocol;				
	}
	
	public boolean isReportProtocol() {
		return mReportProtocol;
	}
	
	protected static void setLEDs(boolean numLock, boolean capsLock, boolean scrollLock) {
		mNumLock = numLock;
		mCapsLock = capsLock;
		mScrollLock = scrollLock;
		
		for (InputStickKeyboardListener listener : mKeyboardListeners) {
			listener.onLEDsChanged(mNumLock, mCapsLock, mScrollLock);
		}			
	}
	
	public static boolean isNumLock() {
		return mNumLock;
	}
	
	public static boolean isCapsLock() {
		return mCapsLock;
	}
	
	public static boolean isScrollLock() {
		return mScrollLock;
	}
	
	public static void toggleNumLock() {
		pressAndRelease(NONE, HIDKeycodes.KEY_NUM_LOCK);
	}
	
	public static void toggleCapsLock() {
		pressAndRelease(NONE, HIDKeycodes.KEY_CAPS_LOCK);
	}
	
	public static void toggleScrollLock() {
		pressAndRelease(NONE, HIDKeycodes.KEY_SCROLL_LOCK);
	}
	
	public static void pressAndRelease(byte modifier, byte key) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new KeyboardReport(modifier, NONE));
		t.addReport(new KeyboardReport(modifier, key));
		t.addReport(new KeyboardReport(NONE, NONE));
		InputStickHID.addKeyboardTransaction(t);
	}	
		
	public static void typeASCII(String toType) {
		int keyCode;
		int index;
		for (int i = 0; i < toType.length(); i++) {
			index = toType.charAt(i);
			if (index > 127) {
				index = 127;
			}
			keyCode = HIDKeycodes.getKeyCode(index);
			if (keyCode > 128) {
				keyCode -= 128;
				pressAndRelease(HIDKeycodes.SHIFT_LEFT, (byte)keyCode);
			} else {
				pressAndRelease(NONE, (byte)keyCode);
			}
		}
	}		
	
	public static void customReport(byte modifier, byte key0, byte key1, byte key2, byte key3, byte key4, byte key5) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new KeyboardReport(modifier, key0, key1, key2, key3, key4, key5));
		InputStickHID.addKeyboardTransaction(t);
	}
	
	/*public static void customReport(byte[] report) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(report);
		InputStickHID.addKeyboardTransaction(t);	
	}*/
	
	public static String ledsToString(byte leds) {
    	String result = "None";
    	boolean first = true;
    	byte mod;
    	for (int i = 0; i < 8; i++) {
    		mod = (byte)(LED_NUM_LOCK << i);
    		if ((leds & mod) != 0) {  
    			if ( !first) {
    				result += ", ";
    			} else {
    				result = "";
    			}
    			first = false;
    			result += ledsMap.get(mod);
    		}
    	}
    	
    	return result;
	}

}
