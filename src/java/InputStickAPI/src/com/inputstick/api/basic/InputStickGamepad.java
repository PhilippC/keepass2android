package com.inputstick.api.basic;

import com.inputstick.api.Packet;


public class InputStickGamepad {
	
	private InputStickGamepad() {
		
	}

	//buttons1 - button0, button1, ..., button7
	//buttons2 - button8, button1, ..., button15
	public static void customReport(byte buttons1, byte buttons2, byte x, byte y, byte z, byte rX) {
		if (InputStickHID.isReady()) {
			Packet p = new Packet(false, (byte)0x2B, (byte)0x03); //write directly to endp3in, no buffering
			p.addByte((byte)0x07); //report bytes cnt
			p.addByte((byte)0x03); //report ID
			p.addByte(buttons1);
			p.addByte(buttons2);
			p.addByte(x); 
			p.addByte(y); 
			p.addByte(z); 
			p.addByte(rX);
			InputStickHID.sendPacket(p);
		}
	}
	
}
