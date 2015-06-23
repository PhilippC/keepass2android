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

import com.inputstick.api.InputStickError;
import com.inputstick.api.Util;

public class BT20Connection extends BTConnection {
	
	private static final UUID MY_UUID = UUID.fromString("00001101-0000-1000-8000-00805F9B34FB"); //SPP
	
	private final BluetoothAdapter mAdapter;	
	
    private ConnectThread mConnectThread;
    private ConnectedThread mConnectedThread;	

    
    public BT20Connection(Application app, BTService btService, String mac, boolean reflections) {
    	super(app, btService, mac, reflections);
    	mAdapter = BluetoothAdapter.getDefaultAdapter();    	    	
    }

	@Override
	public void connect() {
        if (mConnectThread != null) {
        	mConnectThread.cancel();
        	mConnectThread = null;
        }        
        if (mConnectedThread != null) {
        	mConnectedThread.cancel(); 
        	mConnectedThread = null;
        }
        final BluetoothDevice device = mAdapter.getRemoteDevice(mMac);
        if (device != null) {
	        mConnectThread = new ConnectThread(device, mReflections);
	        mConnectThread.start();
        } else {
        	mBTservice.connectionFailed(false, InputStickError.ERROR_BLUETOOTH_NO_REMOTE_DEVICE);
        }
	}

	@Override
	public void disconnect() {
		cancelThreads();          
	}

	@Override
	public void write(byte[] out) {
		mConnectedThread.write(out);  	
	}	
	
	
	
	
	
	//################################
	
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
    
    
    private class ConnectThread extends Thread {
    	
        private final BluetoothSocket mmSocket;
        //private final BluetoothDevice mmDevice;

        public ConnectThread(BluetoothDevice device, boolean useReflection) {
            //mmDevice = device;
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
                mBTservice.connectionFailed(true, 0);
                return;
            }

            //synchronized (BTService.this) {
                mConnectThread = null; //TODO
            //}

            //connected(mmSocket, mmDevice); :
            cancelThreads();        
            //receiver

            // Start the thread to manage the connection and perform transmissions
            mConnectedThread = new ConnectedThread(mmSocket);
            mConnectedThread.start();
            
            mBTservice.connectedEstablished();
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
        

        public void run() {
        	Util.log("BEGIN mConnectedThread");
        	int rxTmp;
            while (true) {
                try {
                	rxTmp = mmInStream.read();
                	mBTservice.onByteRx(rxTmp);
                } catch (IOException e) {
                	mBTservice.connectionFailed(false, InputStickError.ERROR_BLUETOOTH_CONNECTION_LOST);
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
