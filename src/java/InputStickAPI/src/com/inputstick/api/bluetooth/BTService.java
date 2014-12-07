package com.inputstick.api.bluetooth;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.lang.reflect.Method;
import java.util.UUID;

import android.app.Application;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothSocket;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.os.Handler;
import android.os.Message;

import com.inputstick.api.Packet;
import com.inputstick.api.Util;
import com.inputstick.api.InputStickError;

public class BTService {
	
	public static final int DEFAULT_CONNECT_TIMEOUT = 30000;
	
	public static final int EVENT_NONE = 0;
	public static final int EVENT_DATA = 1;
	public static final int EVENT_CONNECTED = 2;
	public static final int EVENT_CANCELLED = 3;
	public static final int EVENT_ERROR = 4;	
	
       
    private static final UUID MY_UUID = UUID.fromString("00001101-0000-1000-8000-00805F9B34FB"); //SPP
    
    //private final String dTag = "BTService";
    
    
    private final BluetoothAdapter mAdapter;
    private final Handler mHandler;
    private ConnectThread mConnectThread;
    private ConnectedThread mConnectedThread;
    private int mLastEvent;
    
    private String mMac;
    private boolean mReflection;
    private final Application mApp;
    private final Context mCtx;    
    
    private boolean mUseReflection;
    private int mConnectTimeout;    
    
    private boolean turnBluetoothOn;
    private boolean receiverRegistered;
    private long timeout;
    private int retryCnt;    
    
    private boolean disconnecting;
    private boolean connected;
    
    //================================================================
    private static final int RX_TIMEOUT = 3000;
    
    private long lastRxTime;
    private int rxState;
    private int rxPos;
    private int rxLength;
    private byte[] rxData;
    private int rxWdgCnt;
    
    private static final int RX_TAG = 0;
    private static final int RX_LENGTH = 1;
    private static final int RX_DATA = 2;
  
    
	private final BroadcastReceiver mReceiver = new BroadcastReceiver() {
		@Override
		public void onReceive(Context context, Intent intent) {
			final String action = intent.getAction();	
			if (action.equals(BluetoothAdapter.ACTION_STATE_CHANGED)) {
				final int state = intent.getIntExtra(BluetoothAdapter.EXTRA_STATE, BluetoothAdapter.ERROR);
				if ((state == BluetoothAdapter.STATE_ON)  && (turnBluetoothOn)) {					
					turnBluetoothOn = false;					
					doConnect(false);
				}
			}
		}
	};    
    
    
    
    
    public BTService(Application app, Handler handler) {
        mAdapter = BluetoothAdapter.getDefaultAdapter();
        mLastEvent = EVENT_NONE;
        mHandler = handler;
        mApp = app;
        mCtx = app.getApplicationContext();
        mConnectTimeout = DEFAULT_CONNECT_TIMEOUT; //30s - default value
    }    
    
    public void setConnectTimeout(int timeout) {
    	mConnectTimeout = timeout;
    }
    
    public void enableReflection(boolean enabled) {
    	mUseReflection = enabled;
    }
    

    private synchronized void event(int event, int arg1) {
    	Util.log("event() " + mLastEvent + " -> " + event);
        mLastEvent = event;        
        Message msg = Message.obtain(null, mLastEvent, arg1, 0);
        mHandler.sendMessage(msg);        
    }        
    
    public synchronized int getLastEvent() {
        return mLastEvent;
    }    
    
    
    
    private void enableBluetooth(boolean doNotAsk) {
    	if (mApp != null) {
	    	turnBluetoothOn = true;
	    	
	    	if ( !receiverRegistered) {
	    		IntentFilter filter = new IntentFilter(BluetoothAdapter.ACTION_STATE_CHANGED);
	    		mCtx.registerReceiver(mReceiver, filter);
	    		receiverRegistered = true;
	    	}	    	
	    	
	    	if (doNotAsk) {
	    		BluetoothAdapter.getDefaultAdapter().enable();
	    	} else {
		    	Intent enableBtIntent = new Intent(BluetoothAdapter.ACTION_REQUEST_ENABLE);
		    	enableBtIntent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
		    	mApp.startActivity(enableBtIntent);
	    	}
    	}
    }    
    
    private void doConnect(boolean reconnecting) {
    	if (reconnecting) {
    		retryCnt++;
    	} else {	
    		retryCnt = 0;
			timeout = System.currentTimeMillis() + mConnectTimeout;
    	}
        
        if (mConnectThread != null) {
        	mConnectThread.cancel();
        	mConnectThread = null;
        }        
        if (mConnectedThread != null) {
        	mConnectedThread.cancel(); 
        	mConnectedThread = null;
        }
        
        mConnectThread = new ConnectThread(mAdapter.getRemoteDevice(mMac), mReflection);
        mConnectThread.start();        
    }
    
    
    public synchronized void connect(String mac) {
    	connect(mac, false);
    }
    
    public synchronized void connect(String mac, boolean doNotAsk) {
    	Util.log("connect to: " + mac + " REFLECTION: " + mUseReflection);
		disconnecting = false;
		connected = false;
		mMac = mac;
		
		if (BluetoothAdapter.checkBluetoothAddress(mac)) {
			BluetoothAdapter mBluetoothAdapter = BluetoothAdapter.getDefaultAdapter();
			if (mBluetoothAdapter == null) {
				event(EVENT_ERROR, InputStickError.ERROR_BLUETOOTH_NOT_SUPPORTED);
			} else {
				if (mBluetoothAdapter.isEnabled()) {					
					doConnect(false);
				} else {					
					enableBluetooth(doNotAsk);
				}
			}
		} else {
			event(EVENT_ERROR, InputStickError.ERROR_BLUETOOTH_INVALID_MAC);
		}
    }    
    
    public synchronized void disconnect() {
    	Util.log("disconnect");
        disconnecting = true;
        cancelThreads();            
        event(EVENT_CANCELLED, 0);
    }


    public synchronized void write(byte[] out) {    	
        // Create temporary object
    	/*
        ConnectedThread r;
        // Synchronize a copy of the ConnectedThread
        synchronized (this) {            
            r = mConnectedThread;            
        }
        // Perform the write unsynchronized
        if (connected) {
        	r.write(out);  	
        }*/
        if (connected) {
        	mConnectedThread.write(out);  	
        }    	
    }          
    
    
    private synchronized void cancelThreads() {
        if (mConnectThread != null) {
        	mConnectThread.cancel(); 
        	mConnectThread = null;
        }
        if (mConnectedThread != null) {
        	mConnectedThread.cancel(); 
        	mConnectedThread = null;
        }    	
    }
    
    private synchronized void connected(BluetoothSocket socket, BluetoothDevice device) {
        timeout = 0;
        cancelThreads();        
        if (receiverRegistered) {
        	mCtx.unregisterReceiver(mReceiver);
        	receiverRegistered = false;
        }

        // Start the thread to manage the connection and perform transmissions
        mConnectedThread = new ConnectedThread(socket);
        mConnectedThread.start();

        connected = true;
        event(EVENT_CONNECTED, 0);
    }    
    
    
    private void connectionFailed() {    	
    	connected = false;
    	if (disconnecting) {
    		disconnecting = false;
    	} else {	
	    	if ((timeout > 0) && (System.currentTimeMillis() < timeout)) {
	    		Util.log("RETRY: "+retryCnt + " time left: " + (timeout - System.currentTimeMillis()));    		
	    		doConnect(true);
	    	} else {    	
	    		event(EVENT_ERROR, InputStickError.ERROR_BLUETOOTH_CONNECTION_FAILED);
	    	}     
    	}
    }    
    
    private void connectionLost() { 
    	connected = false;
    	if (disconnecting) {
    		disconnecting = false;
    	} else {
    		event(EVENT_ERROR, InputStickError.ERROR_BLUETOOTH_CONNECTION_LOST);
    	}
    }    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    private class ConnectThread extends Thread {
    	
        private final BluetoothSocket mmSocket;
        private final BluetoothDevice mmDevice;

        public ConnectThread(BluetoothDevice device, boolean useReflection) {
            mmDevice = device;
            BluetoothSocket tmp = null;

            try {    
            	if (useReflection) {
            		Method m = device.getClass().getMethod("createRfcommSocket", new Class[] {int.class});
                    tmp = (BluetoothSocket) m.invoke(device, 1);            		
            	} else {
            		tmp = device.createRfcommSocketToServiceRecord(MY_UUID);
            	}
                
            } catch (IOException e) {
                Util.log("Socket create() failed");
            } catch (Exception e) {
            	Util.log("Socket create() REFLECTION failed");
				e.printStackTrace();
			} 
            mmSocket = tmp;
        }

        public void run() {
        	Util.log("BEGIN mConnectThread");
            
            mAdapter.cancelDiscovery(); //else it will slow down connection

            try {
                mmSocket.connect();
            } catch (IOException e) {
                try {
                    mmSocket.close();
                } catch (IOException e2) {
                	Util.log("unable to close() socket during connection failure");
                }
                connectionFailed();
                return;
            }

            // Reset the ConnectThread
            synchronized (BTService.this) {
                mConnectThread = null;
            }

            connected(mmSocket, mmDevice);
        }

        public void cancel() {
            try {
                mmSocket.close();
            } catch (IOException e) {
            	Util.log("close() of connect socket failed");
            }
        }
    }



    private class ConnectedThread extends Thread {
    	
        private final BluetoothSocket mmSocket;
        private final InputStream mmInStream;
        private final OutputStream mmOutStream;

        public ConnectedThread(BluetoothSocket socket) {
        	Util.log("create ConnectedThread");
            mmSocket = socket;
            InputStream tmpIn = null;
            OutputStream tmpOut = null;

            try {
                tmpIn = socket.getInputStream();
                tmpOut = socket.getOutputStream();
            } catch (IOException e) {
            	Util.log("temp sockets not created");
            }
           
            mmInStream = tmpIn;
            mmOutStream = tmpOut;
        }

        
        private void rxByte(byte b) {
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
                			event(EVENT_ERROR, InputStickError.ERROR_HARDWARE_WDG_RESET);
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
        					mHandler.obtainMessage(EVENT_DATA, 0, 0, rxData).sendToTarget();
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

        public void run() {
        	Util.log("BEGIN mConnectedThread");
        	int rxTmp;
            while (true) {
                try {
                	rxTmp = mmInStream.read();
                	rxByte((byte)rxTmp);
                } catch (IOException e) {
                    connectionLost();
                    break;
                }
            }
        }

        public void write(byte[] buffer) {      	
            try {
                mmOutStream.write(buffer);
                mmOutStream.flush();
            } catch (IOException e) {
            	Util.log("write() exception");
            }
        }          

        public void cancel() {
            try {
                mmSocket.close();
            } catch (IOException e) {
            	Util.log("socket close() exception");
            }
        }
    }            

}
