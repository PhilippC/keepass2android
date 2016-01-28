package com.inputstick.api.basic;

import java.util.Vector;

import android.util.SparseArray;

import com.inputstick.api.InputStickKeyboardListener;
import com.inputstick.api.hid.HIDKeycodes;
import com.inputstick.api.hid.HIDTransaction;
import com.inputstick.api.hid.KeyboardReport;
import com.inputstick.api.layout.KeyboardLayout;

public class InputStickKeyboard {
	
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
	
	
	/*
	 * Uses InputStick to press and then immediately release key combination specified by parameters.
	 * 
	 * @param modifier	state of modifier keys (CTRL_LEFT .. GUI_RIGHT, see HIDKeycodes)
	 * @param key	non-modifier key (see HIDKeycodes)	 
	 */
	public static void pressAndRelease(byte modifier, byte key) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new KeyboardReport(modifier, NONE));
		t.addReport(new KeyboardReport(modifier, key));
		t.addReport(new KeyboardReport(NONE, NONE));
		InputStickHID.addKeyboardTransaction(t);
	}	
	
	
	/*
	 * Type text via InputStick, using selected keyboard layout. USB host must use matching keyboard layout.	 
	 * For available keyboard layouts see: com.inputstick.api.layout.
	 * If layout is null or not found, en-US will be used.
	 * 
	 * @param toType	text to type
	 * @param layoutCode	code of keyboard layout ("en-US", "de-DE", etc.)
	 */
	public static void type(String toType, String layoutCode) {
		KeyboardLayout layout = KeyboardLayout.getLayout(layoutCode);
		layout.type(toType);
	}

	
	/*
	 * Type text via InputStick. ASCII characters only! It is assumed that USB host uses en-US keyboard layout.
	 * 
	 * @param toType	text to type
	 */
	public static void typeASCII(String toType) {
		int keyCode;
		int index;
		
		for (int i = 0; i < toType.length(); i++) {					
			index = toType.charAt(i);
			if (index == '\n') {
				pressAndRelease(NONE, HIDKeycodes.KEY_ENTER);
			} else if (index == '\t') {
				pressAndRelease(NONE, HIDKeycodes.KEY_TAB);
			} else {			
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
	}		
	

	/*
	 * Sends custom keyboard HID report.
	 * Note: keys must be "manually" released by sending next custom HID report (with 0x00s as key0..key5).
	 * 
	 * @param modifier	state of modifier keys (CTRL_LEFT .. GUI_RIGHT, see HIDKeycodes)
	 * @param key0	non modifier keyboard key (see HIDKeycodes). Use 0x00 when no key is pressed.
	 * @param key1	non modifier keyboard key (see HIDKeycodes). Use 0x00 when no key is pressed.
	 * @param key2	non modifier keyboard key (see HIDKeycodes). Use 0x00 when no key is pressed.
	 * @param key3	non modifier keyboard key (see HIDKeycodes). Use 0x00 when no key is pressed.
	 * @param key4	non modifier keyboard key (see HIDKeycodes). Use 0x00 when no key is pressed.
	 * @param key5	non modifier keyboard key (see HIDKeycodes). Use 0x00 when no key is pressed. 
	 */
	public static void customReport(byte modifier, byte key0, byte key1, byte key2, byte key3, byte key4, byte key5) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new KeyboardReport(modifier, key0, key1, key2, key3, key4, key5));
		InputStickHID.addKeyboardTransaction(t);
	}	
	
	
	/*
	 * Checks is report protocol is used.
	 * Report protocol is in most cases used by OS
	 * Boot protocol is used by BIOS, or when OS is booting
	 * 
	 * @return true if USB host uses report protocol, false if USB host uses boot protocol
	 */
	public boolean isReportProtocol() {
		return mReportProtocol;
	}

	
	/*
	 * Checks states of NumLock keyboard LED
	 * 
	 * @return true if NumLock LED is on, false if off.
	 */
	public static boolean isNumLock() {
		return mNumLock;
	}
	
	
	/*
	 * Checks states of CapsLock keyboard LED
	 * 
	 * @return true if CapsLock LED is on, false if off.
	 */
	public static boolean isCapsLock() {
		return mCapsLock;
	}
	
	
	/*
	 * Checks states of ScrollLock keyboard LED
	 * 
	 * @return true if ScrollLock LED is on, false if off.
	 */
	public static boolean isScrollLock() {
		return mScrollLock;
	}
	
	
	/*
	 * Toggle state of NumLock by press and release NumLock key.
	 */
	public static void toggleNumLock() {
		pressAndRelease(NONE, HIDKeycodes.KEY_NUM_LOCK);
	}
	
	
	/*
	 * Toggle state of CapsLock by press and release CapsLock key.
	 */
	public static void toggleCapsLock() {
		pressAndRelease(NONE, HIDKeycodes.KEY_CAPS_LOCK);
	}
	
	
	/*
	 * Toggle state of ScrollLock by press and release ScrollLock key.
	 */
	public static void toggleScrollLock() {
		pressAndRelease(NONE, HIDKeycodes.KEY_SCROLL_LOCK);
	}

	
	
	
	
	
	/*
	 * Converts state of keyboard LEDs to String. Example: "CapsLock, ScrollLock".
	 * 
	 * @return String description of keyboard LEDs.
	 */
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
	
	
	/*
	 * Adds InputStickKeyboardListener. Listener will be notified when state of keyboard LEDs changes (NumLock, CapsLock, ScrollLock). 
	 * 
	 * @param listener	listener to add
	 */
	public static void addKeyboardListener(InputStickKeyboardListener listener) {
		if (listener != null) {
			if ( !mKeyboardListeners.contains(listener)) {
				mKeyboardListeners.add(listener);
			}
		}
	}
	
	
	/*
	 * Removes InputStickKeyboardListener.
	 * 
	 * @param listener	listener to remove
	 */	
	public static void removeKeyboardListener(InputStickKeyboardListener listener) {
		if (listener != null) {
			mKeyboardListeners.remove(listener);
		}
	}		
	
	
	
	protected void setReportProtocol(boolean reportProtocol) {
		mReportProtocol = reportProtocol;				
	}
	
	protected static void setLEDs(boolean numLock, boolean capsLock, boolean scrollLock) {
		boolean mustUpdate = false;
		if ((numLock != mNumLock) || (capsLock != mCapsLock) || (scrollLock != mScrollLock)) {
			mustUpdate = true;
		}
		mNumLock = numLock;
		mCapsLock = capsLock;
		mScrollLock = scrollLock;
		
		if (mustUpdate) {
			for (InputStickKeyboardListener listener : mKeyboardListeners) {
				listener.onLEDsChanged(mNumLock, mCapsLock, mScrollLock);
			}			
		}
	}

}
