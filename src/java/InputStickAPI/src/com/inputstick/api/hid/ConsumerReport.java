package com.inputstick.api.hid;

import com.inputstick.api.Util;

public class ConsumerReport extends HIDReport {
	
	public static final int SIZE = 3;
	
	private byte[] data;
	
	public ConsumerReport(int usage) {
		data = new byte[SIZE];
		data[0] = 1;
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
