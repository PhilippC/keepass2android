package com.inputstick.init;

import com.inputstick.api.Packet;


public class BasicInitManager extends InitManager {
	
	private boolean initDone = false;	
	
	public BasicInitManager(byte[] key) {
		super(key);
	}
	

	@Override
	public void onConnected() {		
		/*Packet p = new Packet(false, Packet.RAW_OLD_BOOTLOADER); //compatibility
		sendPacket(p);*/
		
		sendPacket(new Packet(true, Packet.CMD_RUN_FW));			
	}
	

	@Override
	public void onData(byte[] data) {
		byte cmd = data[0];
		byte respCode = data[1];
		byte param = data[1];
		
		if (cmd == Packet.CMD_RUN_FW) {
			sendPacket(new Packet(true, Packet.CMD_GET_INFO));
		}
		
		if (cmd == Packet.CMD_GET_INFO) {
			//store info
			sendPacket(new Packet(true, Packet.CMD_INIT)); //TODO params!	
		}
		
		if (cmd == Packet.CMD_INIT) {
			if (respCode == Packet.RESP_OK) {						
				initDone = true;
				sendPacket(new Packet(false, Packet.CMD_HID_STATUS_REPORT));
			} else {
				mListener.onInitFailure(respCode);
			}
		}
		
		if (cmd == Packet.CMD_HID_STATUS) {
			if (initDone) {
				if (param == 0x05) {
					mListener.onInitReady();
				} else {
					mListener.onInitNotReady();
				}
			}
		}
	}

	

}
