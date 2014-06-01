package com.inputstick.api;

import java.lang.ref.WeakReference;

import android.app.Application;
import android.os.Handler;
import android.os.Message;

import com.inputstick.api.bluetooth.BTService;
import com.inputstick.init.InitManager;
import com.inputstick.init.InitManagerListener;

public class BTConnectionManager extends ConnectionManager implements InitManagerListener {
	
	//private static final String mTag = "BTConnectionManager";
	
	private String mMac;
	private byte[] mKey;		
	
	private InitManager mInitManager;
	private Application mApp;
	private BTService mBTService;
	private PacketManager mPacketManager;
	//private PacketQueue mPacketQueue;
	private final BTHandler mBTHandler = new BTHandler(this);				
	
	
	
    private static class BTHandler extends Handler {    	
    	private final WeakReference<BTConnectionManager> ref; 

    	BTHandler(BTConnectionManager manager) { 
    		ref = new WeakReference<BTConnectionManager>(manager); 
        }    	
    	
		@Override
		public void handleMessage(Message msg) {
			BTConnectionManager manager = ref.get();
			switch (msg.what) {
				case BTService.EVENT_DATA:
					manager.onData((byte[])msg.obj);
					break;			
				case BTService.EVENT_CONNECTED:
					manager.onConnected();
					break;
				case BTService.EVENT_CANCELLED:
					manager.onDisconnected();
					break;					
				case BTService.EVENT_CONNECTION_FAILED:
					manager.onFailure(1); 
					break;
				case BTService.EVENT_CONNECTION_LOST:
					manager.onFailure(1); 
					break;					
				case BTService.EVENT_NO_BT_HW:
					manager.onFailure(1); 
					break;
				case BTService.EVENT_INVALID_MAC:
					manager.onFailure(1); 
					break;
				case BTService.EVENT_CMD_TIMEOUT:
					manager.onFailure(1); 
					break;
				case BTService.EVENT_INTERVAL_TIMEOUT:
					manager.onFailure(1); 
					break;
				case BTService.EVENT_TURN_ON_TIMEOUT:
					manager.onFailure(1); 
					break;					
				case BTService.EVENT_OTHER_ERROR:
					manager.onFailure(1); 
					break;									
			}
		}
    } 		
    
    private void onConnecting() {
    	stateNotify(ConnectionManager.STATE_CONNECTING);
    }
	
	private void onConnected() {		
		stateNotify(ConnectionManager.STATE_CONNECTED);
		mInitManager.onConnected();
	}
	
	private void onDisconnected() {
		stateNotify(ConnectionManager.STATE_DISCONNECTED);
	}
	
	private void onFailure(int code) {
		mErrorCode = code;
		stateNotify(ConnectionManager.STATE_FAILURE);
	}
	
	private void onData(byte[] rawData) {
		byte[] data;
		data = mPacketManager.bytesToPacket(rawData);
		
		if (data == null) {
			//TODO
			return;
		}
		
		mInitManager.onData(data);
		
		//sendNext(); TODO
		for (InputStickDataListener listener : mDataListeners) {
			listener.onInputStickData(data);
		}		
	}	
	
	
	public BTConnectionManager(InitManager initManager, Application app, String mac, byte[] key) {		
		mInitManager = initManager;		
		mMac = mac;		
		mKey = key;
		mApp = app;
	}
	
	@Override
	public void connect() {
		connect(false, BTService.DEFAULT_CONNECT_TIMEOUT);
	}

	
	public void connect(boolean reflection, int timeout) {
		mErrorCode = ConnectionManager.ERROR_NONE;
		if (mBTService == null) {
			mBTService = new BTService(mApp, mBTHandler);
			mPacketManager = new PacketManager(mBTService, mKey);
			mInitManager.init(this, mPacketManager);
		}
		mBTService.setConnectTimeout(timeout);
		mBTService.enableReflection(reflection);
		mBTService.connect(mMac);
		onConnecting();
	}

	@Override
	public void disconnect() {
		if (mBTService != null) {
			mBTService.disconnect();
		}
	}
	

	@Override
	public void sendPacket(Packet p) {
		mPacketManager.sendPacket(p); //TODO tmp; zalozmy z beda same NO_RESP ???
	}
	

	@Override
	public void onInitReady() {
		stateNotify(ConnectionManager.STATE_READY);
	}

	@Override
	public void onInitNotReady() {
		stateNotify(ConnectionManager.STATE_CONNECTED);
	}

	@Override
	public void onInitFailure(int code) {
		onFailure(code);
	}	

}
