package com.inputstick.api.basic;

import com.inputstick.api.hid.ConsumerReport;
import com.inputstick.api.hid.HIDTransaction;

public class InputStickConsumer {
	
	//CONSUMER PAGE
	public static final int VOL_UP = 0x00E9;
	public static final int VOL_DOWN = 0x00EA;
	public static final int VOL_MUTE = 0x00E2;
	public static final int TRACK_NEXT = 0x00B5;
	public static final int TRACK_PREV = 0x00B6;
	public static final int STOP = 0x00B7;
	public static final int PLAY_PAUSE = 0x00CD;
	
	public static final int LAUNCH_BROWSER = 0x0196;
	public static final int LAUNCH_EMAIL = 0x018A;
	public static final int LAUNCH_CALC = 0x0192;
	
	//SYSTEM CONTROL
	public static final int POWER_DOWN = 0x81;
	public static final int SLEEP = 0x82;
	public static final int WAKEUP = 0x83;		
	
	private InputStickConsumer() {
		
	}
	
	/*public static void systemAction(int action) {
	}*/	
	
	public static void consumerAction(int action) {
		HIDTransaction t = new HIDTransaction();
		t.addReport(new ConsumerReport(action));
		t.addReport(new ConsumerReport());
		InputStickHID.addConsumerTransaction(t);		
	}

}
