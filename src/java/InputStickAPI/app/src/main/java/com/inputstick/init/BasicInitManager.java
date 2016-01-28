package com.inputstick.init;

import com.inputstick.api.Packet;


public class BasicInitManager extends InitManager {
	
	private static final int UPDATES_LIMIT = 50;
	private static final int RETRY_LIMIT = 3;
	
	
	private int lastStatusParam;
	private int noInitUpdatesCnt;
	private int noInitRetryCnt;
	
	public BasicInitManager(byte[] key) {
		super(key);
		lastStatusParam = 0;
	}
	

	@Override
	public void onConnected() {		
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
					noInitUpdatesCnt = 0;
					noInitRetryCnt = 0;
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
					if (param != lastStatusParam) {
						lastStatusParam = param;
						if (param == 0x05) {						
							mListener.onInitReady();
						} else {
							mListener.onInitNotReady();
						}
					}
				} else {
					noInitUpdatesCnt++;
					if (noInitUpdatesCnt == UPDATES_LIMIT) {
						noInitUpdatesCnt = 0;
						if (noInitRetryCnt < RETRY_LIMIT) {				
							sendPacket(new Packet(true, Packet.CMD_RUN_FW));
							noInitRetryCnt++;
						}
					}
				}
				break;
		}
	}
	

}
