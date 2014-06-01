package com.inputstick.init;

import com.inputstick.api.Packet;
import com.inputstick.api.PacketManager;

public class InitManager {
	
	protected PacketManager mPacketManager;
	protected InitManagerListener mListener;	
	protected byte[] mKey;
	
	public InitManager(byte[] key) {
		mKey = key;
	}
	
	public void init(InitManagerListener listener, PacketManager packetManager) {
		mListener = listener;
		mPacketManager = packetManager;
	}	
	
	public void onConnected() {
		mListener.onInitReady();
	}
		
	public void onData(byte[] data) {
		
	}
	
	public void sendPacket(Packet p) {
		mPacketManager.sendPacket(p);
	}
	
}
