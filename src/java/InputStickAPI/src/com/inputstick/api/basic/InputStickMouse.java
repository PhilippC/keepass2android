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
	
	protected void setReportProtocol(boolean reportProtocol) {
		mReportProtocol = reportProtocol;				
	}
	
	public boolean isReportProtocol() {
		return mReportProtocol;
	}		
	
	//clicks button (BUTTON_LEFT..BUTTON_MIDDLE) n times. 
	public static void click(byte button, int n) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new MouseReport()); //release
		for (int i = 0; i < n; i++) {								
			t.addReport(new MouseReport(button, NONE, NONE, NONE)); //press
			t.addReport(new MouseReport()); //release			
		}
		InputStickHID.addMouseTransaction(t);	
	}
	
	//moves mouse pointer by x,y
	public static void move(byte x, byte y) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new MouseReport(NONE, x, y, NONE));
		InputStickHID.addMouseTransaction(t);	
	}
	
	//moves scroll wheel by "wheel"
	public static void scroll(byte wheel) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new MouseReport(NONE, NONE, NONE, wheel));
		InputStickHID.addMouseTransaction(t);		
	}	
	
	//sends custom mouse report (buttons will remain in pressed state until released by next report)
	public static void customReport(byte buttons, byte x, byte y, byte wheel) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new MouseReport(buttons, x, y, wheel));
		InputStickHID.addMouseTransaction(t);		
	}		
	
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

}
