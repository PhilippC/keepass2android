package com.inputstick.api;

public class HIDInfo {
	
	private int state;
	
	private boolean numLock;
	private boolean capsLock;
	private boolean scrollLock;
	
	private boolean keyboardReportProtocol;
	private boolean mouseReportProtocol;
	
	private boolean keyboardReady;
	private boolean mouseReady;
	private boolean consumerReady;
	

	public HIDInfo() {
		keyboardReportProtocol = true;
		mouseReportProtocol = true;
	}
	
	public void update(byte[] data) {
		state = data[1];
		
		int leds = data[2];
		if ((leds & 0x01) != 0) {
			numLock = true;
		} else {
			numLock = false;
		}
		if ((leds & 0x02) != 0) {
			capsLock = true;
		} else {
			capsLock = false;
		}
		if ((leds & 0x04) != 0) {
			scrollLock = true;
		} else {
			scrollLock = false;
		}	
		
		if (data[3] == 0) {
			keyboardReportProtocol = true;
		} else {
			keyboardReportProtocol = false;
		}
		
		if (data[4] == 0) {
			keyboardReady = false;
		} else {
			keyboardReady = true;
		}
		
		if (data[5] == 0) {
			mouseReportProtocol = true;
		} else {
			mouseReportProtocol = false;
		}	
		
		if (data[6] == 0) {
			mouseReady = false;
		} else {
			mouseReady = true;
		}		
		
		if (data[7] == 0) {
			consumerReady = false;
		} else {
			consumerReady = true;
		}			
	}
	
	public void setKeyboardBusy() {
		keyboardReady = false;
	}
	
	public int getState() {
		return state;
	}
	
	public boolean getNumLock() {
		return numLock;
	}
	
	public boolean getCapsLock() {
		return capsLock;
	}
	
	public boolean getScrollLock() {
		return scrollLock;
	}	
	
	public boolean isKeyboardReportProtocol() {
		return keyboardReportProtocol;
	}
	
	public boolean isMouseReportProtocol() {
		return mouseReportProtocol;
	}	
	
	public boolean isKeyboardReady() {
		return keyboardReady;
	}
	
	public boolean isMouseReady() {
		return mouseReady;
	}
	
	public boolean isConsumerReady() {
		return consumerReady;
	}	
	
}
