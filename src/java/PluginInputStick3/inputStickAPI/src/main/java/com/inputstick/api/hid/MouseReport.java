package com.inputstick.api.hid;

public class MouseReport extends HIDReport {
	
	public static final int SIZE = 4;
	
	private byte[] data;

	public MouseReport(byte buttons, byte x, byte y, byte wheel) {
		data = new byte[SIZE];
		data[0] = buttons;
		data[1] = x;
		data[2] = y;
		data[3] = wheel;
	}
	
	public MouseReport() {
		this((byte)0, (byte)0, (byte)0, (byte)0);
	}	
	
	public byte[] getBytes() {
		return data;
	}
	
	public int getBytesCount() {
		return SIZE;
	}	
	
}
