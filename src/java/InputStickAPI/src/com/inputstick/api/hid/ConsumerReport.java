package com.inputstick.api.hid;

import com.inputstick.api.Util;

public class ConsumerReport extends HIDReport {
	
	public static final byte CONSUMER_REPORT_ID = 1;
	public static final byte SYSTEM_REPORT_ID = 2;
	public static final byte GAMEPAD_REPORT_ID = 3;
	
	public static final int SIZE = 3;
	
	private byte[] data;
	
	public ConsumerReport(byte id, byte b1, byte b2) {
		data = new byte[SIZE];
		data[0] = id;
		data[1] = b1;
		data[2] = b2;
	}
	
	public ConsumerReport(int usage) {
		data = new byte[SIZE];
		data[0] = CONSUMER_REPORT_ID;
		data[1] = Util.getLSB(usage);
		data[2] = Util.getMSB(usage);		
	}
	
	public ConsumerReport() {
		this(0);
	}
	
	public byte[] getBytes() {
		return data;
	}
	
	public int getBytesCount() {
		return SIZE;
	}	
	


}
