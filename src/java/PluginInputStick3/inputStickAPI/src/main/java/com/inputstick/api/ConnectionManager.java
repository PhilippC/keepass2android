package com.inputstick.api;

import java.util.Vector;

public abstract class ConnectionManager {
	
	public static final int STATE_DISCONNECTED = 0;
	public static final int STATE_FAILURE = 1;
	public static final int STATE_CONNECTING = 2;
	public static final int STATE_CONNECTED = 3;
	public static final int STATE_READY = 4;
	
	
	protected Vector<InputStickStateListener> mStateListeners = new Vector<InputStickStateListener>();
	protected Vector<InputStickDataListener> mDataListeners = new Vector<InputStickDataListener>();
	
	protected int mState;
	protected int mErrorCode;	
	
	public abstract void connect();
	public abstract void disconnect();
	public abstract void sendPacket(Packet p);
	
	protected void stateNotify(int state) {
		stateNotify(state, false);
	}    
	
	protected void stateNotify(int state, boolean forceNotification) {
		if (( !forceNotification) && (mState == state )) {
			//do nothing
		} else {
			//notify all listeners
			mState = state;
			for (InputStickStateListener listener : mStateListeners) {
				listener.onStateChanged(state);
			}
		}
	}  
	
	public int getState() {
		return mState;
	}
	
	public boolean isReady() {
		if (mState == STATE_READY) {
			return true;
		} else {
			return false;
		}
	}
	
	public boolean isConnected() {
		if ((mState == STATE_READY) || (mState == STATE_CONNECTED)) {
			return true;
		} else {
			return false;
		}
	}
	
	public int getErrorCode() {
		return mErrorCode;
	}
	
	
	protected void onData(byte[] data) {
		for (InputStickDataListener listener : mDataListeners) {
			listener.onInputStickData(data);
		} 
	}
	
	public void addStateListener(InputStickStateListener listener) {
		if (listener != null) {
			if ( !mStateListeners.contains(listener)) {
				mStateListeners.add(listener);
			}
		}	
	}
	
	public void removeStateListener(InputStickStateListener listener) {
		if (listener != null) {
			mStateListeners.remove(listener);
		}	
	}
	
	public void addDataListener(InputStickDataListener listener) {
		if (listener != null) {
			if ( !mDataListeners.contains(listener)) {
				mDataListeners.add(listener);
			}
		}				
	}
	
	public void removeDataListener(InputStickDataListener listener) {
		if (listener != null) {
			mDataListeners.remove(listener);
		}			
	}

}
