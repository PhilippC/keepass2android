package com.inputstick.init;

import com.inputstick.api.Packet;


public class BasicInitManager extends InitManager {		
	
	public BasicInitManager(byte[] key) {
		super(key);
	}
	

	@Override
	public void onConnected() {		
		/*Packet p = new Packet(false, Packet.RAW_OLD_BOOTLOADER); //compatibility with old protocol version
		sendPacket(p);*/		
		sendPacket(new Packet(true, Packet.CMD_RUN_FW));			
	}
	
	@Override
	public void onData(byte[] data) {
		byte cmd = data[0];
		byte respCode = data[1];
		byte param = data[1];
		
		switch (cmd) {
			case Packet.CMD_RUN_FW:
				sendPacket(new Packet(true, Packet.CMD_FW_INFO));
				break;
			case Packet.CMD_FW_INFO:
				onFWInfo(data, true, true, new Packet(true, Packet.CMD_INIT)); //TODO next FW: params!	
				break;
			case Packet.CMD_INIT:
				if (respCode == Packet.RESP_OK) {
					initDone = true;
					sendPacket(new Packet(true, Packet.CMD_HID_STATUS_REPORT));			
				} else {
					mListener.onInitFailure(respCode);
				}				
				break;
			case Packet.CMD_INIT_AUTH:
				onAuth(data, true, new Packet(true, Packet.CMD_INIT)); //TODO next FW: params!	
				break;
			case Packet.CMD_HID_STATUS:
				if (initDone) {
					if (param == 0x05) {						
						mListener.onInitReady();
					} else {
						mListener.onInitNotReady();
					}
				}				
				break;
		}
	}
	

}
