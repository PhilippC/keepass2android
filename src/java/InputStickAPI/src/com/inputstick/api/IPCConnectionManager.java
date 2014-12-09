package com.inputstick.api;

import java.lang.ref.WeakReference;

import android.app.Application;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.content.pm.PackageManager;
import android.content.pm.PackageManager.NameNotFoundException;
import android.os.Bundle;
import android.os.Handler;
import android.os.IBinder;
import android.os.Message;
import android.os.Messenger;
import android.os.RemoteException;

public class IPCConnectionManager extends ConnectionManager {
	
	//private static final String mTag = "IPCConnectionManager";
	
	public static final int SERVICE_CMD_CONNECT = 1;
	public static final int SERVICE_CMD_DISCONNECT = 2;
	public static final int SERVICE_CMD_DATA = 3;
	public static final int SERVICE_CMD_STATE = 4;
	
    Context mCtx;
	Messenger mService = null;    
	boolean mBound;
	boolean initSent;
    final Messenger mMessenger = new Messenger(new IncomingHandler(this)); 
    
    private static class IncomingHandler extends Handler {    	
    	private final WeakReference<IPCConnectionManager> ref; 

    	IncomingHandler(IPCConnectionManager manager) { 
    		ref = new WeakReference<IPCConnectionManager>(manager); 
        }    	
    	
        @Override
        public void handleMessage(Message msg) {      	
        	IPCConnectionManager manager = ref.get();
        	
        	switch (msg.what) {     	
    		case SERVICE_CMD_DATA:
            	byte[] data = null;        	
            	Bundle b = msg.getData();
            	if (b != null) {
            		data = b.getByteArray("data");
            		manager.onData(data);
            	}             	
    			break;
    		case SERVICE_CMD_STATE:
    			manager.stateNotify(msg.arg1);
    			break;             	
        	}         	
        }
    }     
    
    private ServiceConnection mConnection = new ServiceConnection() {
        public void onServiceConnected(ComponentName className, IBinder service) {
            mService = new Messenger(service);
            mBound = true;                  
            sendMessage(SERVICE_CMD_CONNECT, 0, 0); 
        }

        public void onServiceDisconnected(ComponentName className) {
            // unexpectedly disconnected from service
            mService = null;
            mBound = false;
			mErrorCode = InputStickError.ERROR_ANDROID_SERVICE_DISCONNECTED;
			stateNotify(STATE_FAILURE);
            stateNotify(STATE_DISCONNECTED);
        }
    };  
    //SERVICE=========================================================            

	
	
	
	
	

    
    private void sendMessage(int what, int arg1, int arg2, Bundle b) {
		Message msg;
		try {
			msg = Message.obtain(null, what, arg1, 0, null);
			msg.replyTo = mMessenger;
			msg.setData(b);				
			mService.send(msg);
		} catch (RemoteException e) {
			e.printStackTrace();
		}    	
    }
    
    private void sendMessage(int what, int arg1, int arg2, byte[] data) {
    	Bundle b;
		b = new Bundle();
		b.putByteArray("data", data);
		sendMessage(what, arg1, arg2, b);
    }    
    
    private void sendMessage(int what, int arg1, int arg2) {
    	sendMessage(what, arg1, arg2, (Bundle)null);	
    }  	
	
	
	private void onData(byte[] data) {
		for (InputStickDataListener listener : mDataListeners) {
			listener.onInputStickData(data);
		} 		
	}    		
	
	
	public IPCConnectionManager(Application app) {
		mCtx = app.getApplicationContext();
	}

	@Override
	public void connect() {
		PackageManager pm = mCtx.getPackageManager();
		boolean exists = true;
		try {
			pm.getPackageInfo("com.inputstick.apps.inputstickutility", PackageManager.GET_META_DATA);
		} catch (NameNotFoundException e) {
			exists = false;
		}		
		
		if (exists) {
			mErrorCode = InputStickError.ERROR_NONE;
			Intent intent = new Intent();									
			intent.setComponent(new ComponentName("com.inputstick.apps.inputstickutility","com.inputstick.apps.inputstickutility.service.InputStickService"));
			mCtx.startService(intent);
			mCtx.bindService(intent, mConnection, Context.BIND_AUTO_CREATE); 
	        if (mBound) {
	        	//already bound
	        	sendMessage(SERVICE_CMD_CONNECT, 0, 0); 
	        } 
		} else {
			mErrorCode = InputStickError.ERROR_ANDROID_NO_UTILITY_APP;
			stateNotify(STATE_FAILURE);
			stateNotify(STATE_DISCONNECTED);
		}
	}

	@Override
	public void disconnect() {
		if (mBound) {
			sendMessage(SERVICE_CMD_DISCONNECT, 0, 0); 
			Intent intent = new Intent();		   
			intent.setComponent(new ComponentName("com.inputstick.apps.inputstickutility","com.inputstick.apps.inputstickutility.service.InputStickService"));	
			mCtx.unbindService(mConnection);
			mCtx.stopService(intent);			
			mBound = false;
			//service will pass notification message (disconnected)
		} else {
			//just set state, there is nothing else to do
			stateNotify(STATE_DISCONNECTED);
		}
	}
	
	@Override
	public void sendPacket(Packet p) {
		if (mState == ConnectionManager.STATE_READY) {			
			if (p.getRespond()) {
				sendMessage(IPCConnectionManager.SERVICE_CMD_DATA, 1, 0, p.getBytes());
			} else {
				sendMessage(IPCConnectionManager.SERVICE_CMD_DATA, 0, 0, p.getBytes());
			}
		}		
	}


}
