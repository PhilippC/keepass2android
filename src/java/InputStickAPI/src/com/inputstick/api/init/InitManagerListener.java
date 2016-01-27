package com.inputstick.api.init;

public interface InitManagerListener {
	
	public void onInitReady();
	public void onInitNotReady();
	public void onInitFailure(int code);

}
