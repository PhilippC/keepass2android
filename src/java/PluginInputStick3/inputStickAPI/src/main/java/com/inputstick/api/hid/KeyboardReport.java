package com.inputstick.api.hid;

public class KeyboardReport extends HIDReport {
	
	public static final int SIZE = 8;
	
	private byte[] data;

	public KeyboardReport(byte modifier, byte key0, byte key1, byte key2, byte key3, byte key4, byte key5) {
		data = new byte[SIZE];
		data[0] = modifier;
		data[2] = key0;
		data[3] = key1;
		data[4] = key2;
		data[5] = key3;
		data[6] = key4;
		data[7] = key5;
	}
	
	public KeyboardReport() {
		this((byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0);
	}
	
	public KeyboardReport(byte modifier, byte key) {
		this(modifier, key, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0);
	}
	
	public byte[] getBytes() {
		return data;
	}
	
	public int getBytesCount() {
		return SIZE;
	}
	
}
