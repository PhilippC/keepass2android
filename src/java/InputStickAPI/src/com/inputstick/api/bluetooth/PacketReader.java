package com.inputstick.api.bluetooth;

import android.os.Handler;

import com.inputstick.api.InputStickError;
import com.inputstick.api.Packet;
import com.inputstick.api.Util;

public class PacketReader {
	
    private static final int RX_TIMEOUT = 3000;
    
    private static final int RX_TAG = 0;
    private static final int RX_LENGTH = 1;
    private static final int RX_DATA = 2;
    
    private long lastRxTime;
    private int rxState;
    private int rxPos;
    private int rxLength;
    private byte[] rxData;
    private int rxWdgCnt;    
    
    private final BTService mBTService;
    private final Handler mHandler;
	
    public PacketReader(BTService btService, Handler handler) {
    	mBTService = btService;
    	mHandler = handler;
    }
	
	
	public void rxByte(int rxByte) {
		byte b = (byte)rxByte;
    	long time = System.currentTimeMillis();
    	if (time > lastRxTime + RX_TIMEOUT) {
    		rxState = RX_TAG;
    	}


    	switch (rxState) {
    		case RX_TAG:
    			if (b == Packet.START_TAG) {
    				rxState = RX_LENGTH;
    			} else {
            		Util.log("Unexpected RX byte" + b);
            		if (b == 0xAF) {
            			rxWdgCnt++;
            		}
            		if (rxWdgCnt > 1024) {
            			rxWdgCnt = 0;
            			mBTService.event(BTService.EVENT_ERROR, InputStickError.ERROR_HARDWARE_WDG_RESET);
            		}	        				
    			}
    			break;
    		case RX_LENGTH:
    			rxLength = b;
    			rxLength &= 0x3F;
    			rxLength *= 16;
    			rxLength += 2;
    			rxPos = 2;
    			
				rxData = new byte[rxLength];
				rxData[0] = Packet.START_TAG;
				rxData[1] = (byte)b;
				
				rxState = RX_DATA;
    			break;
    		case RX_DATA:
    			if (rxPos < rxLength) {
    				rxData[rxPos] = b;
    				rxPos++;
    				if (rxPos == rxLength) {
    					//done!        					
    					mHandler.obtainMessage(BTService.EVENT_DATA, 0, 0, rxData).sendToTarget();
    					rxState = RX_TAG;
    				}
    			} else {
    				//buffer overrun!
    				rxState = RX_TAG;
    			}
    			break;
    	}
    	
    	lastRxTime = time;
    }

}
