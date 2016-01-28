package com.inputstick.init;

import com.inputstick.api.Packet;

public class BootloaderInitManager extends InitManager {

	public BootloaderInitManager(byte[] key) {
		super(key);
	}
	
	@Override
	public void onConnected() {
		//TODO key
		sendPacket(new Packet(true, Packet.CMD_RUN_BL));			
	}

	@Override
	public void onData(byte[] data) {
		byte cmd = data[0];
		//byte respCode = data[1];
		//byte param = data[1];
		
		if (cmd == Packet.CMD_RUN_BL) {
			mListener.onInitReady();
		}
	}

}
