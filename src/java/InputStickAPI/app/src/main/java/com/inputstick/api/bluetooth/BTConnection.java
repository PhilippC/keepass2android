package com.inputstick.api.bluetooth;

import android.app.Application;
import android.content.Context;


public abstract class BTConnection {
	
    protected final Application mApp;    
    protected final Context mCtx; 
    protected final String mMac;
    protected boolean mReflections;
    protected final BTService mBTservice;    
	
	public BTConnection(Application app, BTService btService, String mac, boolean reflections) {
        mApp = app;
        mCtx = app.getApplicationContext();
        mMac = mac;
        mReflections = reflections;        
    	mBTservice = btService;    	
	}
	
	public abstract void connect();
	public abstract void disconnect();
	public abstract void write(byte[] out);
}
