package com.inputstick.api.basic;

import com.inputstick.api.hid.HIDTransaction;
import com.inputstick.api.hid.MouseReport;

public class InputStickMouse {
	
	private static final byte NONE = 0x00;

	public static final byte BUTTON_NONE = 0x00;
	public static final byte BUTTON_LEFT = 0x01;
	public static final byte BUTTON_RIGHT = 0x02;
	public static final byte BUTTON_MIDDLE = 0x04;
	
	private static boolean mReportProtocol;
	
	private InputStickMouse() {
		
	}
	
	protected void setReportProtocol(boolean reportProtocol) {
		mReportProtocol = reportProtocol;				
	}
	
	public boolean isReportProtocol() {
		return mReportProtocol;
	}		
	
	public static void click(byte button, int n) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new MouseReport()); //release
		for (int i = 0; i < n; i++) {								
			t.addReport(new MouseReport(button, NONE, NONE, NONE)); //press
			t.addReport(new MouseReport()); //release			
		}
		InputStickHID.addMouseTransaction(t);	
	}
	
	public static void move(byte x, byte y) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new MouseReport(NONE, x, y, NONE));
		InputStickHID.addMouseTransaction(t);	
	}
	
	public static void scroll(byte wheel) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new MouseReport(NONE, NONE, NONE, wheel));
		InputStickHID.addMouseTransaction(t);		
	}	
	
	public static void customReport(byte buttons, byte x, byte y, byte wheel) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new MouseReport(buttons, x, y, wheel));
		InputStickHID.addMouseTransaction(t);		
	}		

}
