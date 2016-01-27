package com.inputstick.api.basic;

import android.util.SparseArray;

import com.inputstick.api.hid.HIDTransaction;
import com.inputstick.api.hid.MouseReport;

public class InputStickMouse {
	
	private static final byte NONE = 0x00;

	public static final byte BUTTON_NONE = 0x00;
	public static final byte BUTTON_LEFT = 0x01;
	public static final byte BUTTON_RIGHT = 0x02;
	public static final byte BUTTON_MIDDLE = 0x04;
	
	private static final SparseArray<String> buttonsMap;
    static
    {
    	buttonsMap = new SparseArray<String>();
    	buttonsMap.put(BUTTON_LEFT, 								"Left");
    	buttonsMap.put(BUTTON_RIGHT, 								"Right");
    	buttonsMap.put(BUTTON_MIDDLE, 							"Middle");
    }
	
	private static boolean mReportProtocol;
	
	private InputStickMouse() {
		
	}
		
	
	/*
	 * Clicks selected mouse button (BUTTON_LEFT etc) N times
	 * 
	 * @param button	code of mouse button 
	 * @param n	number of button clicks (press and release events)
	 */
	public static void click(byte button, int n) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new MouseReport()); //release
		for (int i = 0; i < n; i++) {								
			t.addReport(new MouseReport(button, NONE, NONE, NONE)); //press
			t.addReport(new MouseReport()); //release			
		}
		InputStickHID.addMouseTransaction(t);	
	}
	
	/*
	 * Move mouse pointer
	 * 
	 * @param x		x displacement
	 * @param y		y dispalcement
	 */
	public static void move(byte x, byte y) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new MouseReport(NONE, x, y, NONE));
		InputStickHID.addMouseTransaction(t);	
	}
	
	/*
	 * Moves mouse scroll wheel
	 * 
	 * @param wheel		scroll wheel displacement	 
	 */
	public static void scroll(byte wheel) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new MouseReport(NONE, NONE, NONE, wheel));
		InputStickHID.addMouseTransaction(t);		
	}	
	
	//sends custom mouse report (buttons will remain in pressed state until released by next report)
	
	/*
	 * Sends custom HID mouse report. Mouse buttons will remain in selected state until new report is received.
	 * 
	 * @param buttons	state of mouse buttons
	 * @param x		x displacement
	 * @param y		y dispalcement
	 * @param wheel		scroll wheel displacement	 
	 */
	public static void customReport(byte buttons, byte x, byte y, byte wheel) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new MouseReport(buttons, x, y, wheel));
		InputStickHID.addMouseTransaction(t);		
	}		
	
	
	/*
	 * Returns names of buttons in "pressed" state
	 * 
	 * @param buttons	state of mouse buttons
	 */
	public static String buttonsToString(byte buttons) {
    	String result = "None";
    	boolean first = true;
    	byte mod;
    	for (int i = 0; i < 8; i++) {
    		mod = (byte)(BUTTON_LEFT << i);
    		if ((buttons & mod) != 0) {  
    			if ( !first) {
    				result += ", ";
    			} else {
    				result = "";
    			}
    			first = false;
    			result += buttonsMap.get(mod);
    		}
    	}
    	
    	return result;
	}
	
	
	/*
	 * When report protocol is used, scroll wheel is enabled. Otherwise, simplified boot protocol is selected by USB host.
	 * Report protocol is in most cases used by OS.
	 * Boot protocol is used by BIOS, or when OS is booting.
	 *
	 * @return true if USB host uses report protocol, false if USB host uses boot protocol
	 */
	public boolean isReportProtocol() {
		return mReportProtocol;
	}
	
	
	protected void setReportProtocol(boolean reportProtocol) {
		mReportProtocol = reportProtocol;				
	}

}
