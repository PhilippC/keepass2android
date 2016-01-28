package com.inputstick.api.hid;

public class GamepadReport extends HIDReport {

	public static final int SIZE = 7;
	
	private byte[] data;

	public GamepadReport(byte b1, byte b2, byte x, byte y, byte z, byte rx) {
		data = new byte[SIZE];
		data[0] = 3;
		data[1] = b1;
		data[2] = b2;
		data[3] = x;
		data[4] = y;
		data[5] = z;
		data[6] = rx;
	}
	
	public GamepadReport() {
		this((byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0);
	}	
	
	public byte[] getBytes() {
		return data;
	}
	
	public int getBytesCount() {
		return SIZE;
	}	
	
}
