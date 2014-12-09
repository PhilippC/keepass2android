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
		if (mState != state) {							
			mState = state;
			for (InputStickStateListener listener : mStateListeners) {
				listener.onStateChanged(state);
			}	
		}
	}    
	
	public int getState() {
		return mState;
	}
	
	public int getErrorCode() {
		return mErrorCode;
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
