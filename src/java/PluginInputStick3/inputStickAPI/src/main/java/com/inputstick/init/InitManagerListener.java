package com.inputstick.init;

public interface InitManagerListener {
	
	public void onInitReady();
	public void onInitNotReady();
	public void onInitFailure(int code);

}
