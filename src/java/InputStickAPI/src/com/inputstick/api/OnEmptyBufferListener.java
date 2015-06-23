package com.inputstick.api;

public interface OnEmptyBufferListener {

	public void onLocalBufferEmpty(int interfaceId);
	public void onRemoteBufferEmpty(int interfaceId);
	
}
