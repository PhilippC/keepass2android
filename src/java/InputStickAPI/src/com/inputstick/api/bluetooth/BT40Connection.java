package com.inputstick.api.bluetooth;

import java.util.LinkedList;
import java.util.List;
import java.util.UUID;

import android.annotation.SuppressLint;
import android.app.Application;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothGatt;
import android.bluetooth.BluetoothGattCallback;
import android.bluetooth.BluetoothGattCharacteristic;
import android.bluetooth.BluetoothGattDescriptor;
import android.bluetooth.BluetoothGattService;
import android.bluetooth.BluetoothManager;
import android.bluetooth.BluetoothProfile;
import android.content.Context;
import android.os.Handler;

import com.inputstick.api.InputStickError;
import com.inputstick.api.Util;

@SuppressLint("NewApi")
public class BT40Connection extends BTConnection {	
	
	private static final int CONNECTION_TIMEOUT = 10000;
    
	private static final String MOD_CHARACTERISTIC_CONFIG = 	"00002902-0000-1000-8000-00805f9b34fb";
	private static final String MOD_CONF = 					"0000ffe0-0000-1000-8000-00805f9b34fb";
	private static final String MOD_RX_TX = 					"0000ffe1-0000-1000-8000-00805f9b34fb";															  	
	private static final UUID UUID_HM_RX_TX = UUID.fromString(MOD_RX_TX);	   
    
    private BluetoothManager mBluetoothManager;
    private BluetoothAdapter mBluetoothAdapter;
    private BluetoothGatt mBluetoothGatt;
    private BluetoothGattCharacteristic characteristicTX;
    private BluetoothGattCharacteristic characteristicRX;

    private LinkedList<byte[]> txBuffer;
    private boolean canSend;
    
    private boolean isConnecting;
    private Handler handler;
	
    public BT40Connection(Application app, BTService btService, String mac, boolean reflections) {
    	super(app, btService, mac, reflections);    	     	
		mBluetoothManager = (BluetoothManager) (mCtx.getSystemService(Context.BLUETOOTH_SERVICE));
		mBluetoothAdapter = mBluetoothManager.getAdapter();
    	
    }
    
	@Override
	public void connect() {		
		final BluetoothDevice device = mBluetoothAdapter.getRemoteDevice(mMac);
		if (device != null) {			
			mBluetoothGatt = device.connectGatt(mCtx, false, mGattCallback);
			
			isConnecting = true;
			handler = new Handler();
			handler.postDelayed(new Runnable() {
			    @Override
			    public void run() {
					if (isConnecting) {
						disconnect();
						mBTservice.connectionFailed(true, 0);
					}
			    }
			}, CONNECTION_TIMEOUT);
		} else {
			mBTservice.connectionFailed(false, InputStickError.ERROR_BLUETOOTH_NO_REMOTE_DEVICE);
		}		
	}

	@Override
	public void disconnect() {
		txBuffer = null;
		try {
			if (mBluetoothGatt != null) {
				mBluetoothGatt.close();
				mBluetoothGatt.disconnect();
				mBluetoothGatt = null;
			}
		} catch (Exception e) {

		}
	}
	


	@Override
	public void write(byte[] out) {
    	byte[] tmp;
    	int offset = 0;
    	
    	//SPECIAL CASES for flashing utility
    	if (Util.flashingToolMode) {
    		//txBuffer.add(out);
    		//return;
	    	if (out.length == 1) {
	    		txBuffer.add(out);
	    		return;
	    	}
	    	if (out.length == 1026) {
	    		tmp = new byte[2];
	    		tmp[0] = out[0];
	    		tmp[1] = out[1];
	    		txBuffer.add(tmp);    		
	    		offset = 2;
	    		for (int i = 0; i < 64; i++) {
	    			tmp = new byte[16];
	    			System.arraycopy(out, offset, tmp, 0, 16);
	    			offset += 16;
	    			txBuffer.add(tmp);
	    		}
	    		return;
	    	}
    	}
    	    	
    	if (out.length == 2) {
    		addHeader(out);
    	} else {    		
    		Util.log("ADDING: " + out.length);
    		int loops = out.length / 16;
    		offset = 0;
    		for (int i = 0; i < loops; i++) {
    			tmp = new byte[16];
    			System.arraycopy(out, offset, tmp, 0, 16);
    			offset += 16;
    			addData16(tmp);
    		}
    		sendNext();
    	}
	}	
	
	
	private byte h0;
	private byte h1;
	private boolean header;
	
	private synchronized void addHeader(byte[] data) {
		h0 = data[0];
		h1 = data[1];
		header = true;
	}
	
	private synchronized void addData16(byte[] data) {
		byte[] tmp;
		int offset = 0;
		if (txBuffer != null) {
			if (header) {
				header = false;
				
	    		tmp = new byte[18];
	    		offset = 2;
	    		
	    		tmp[0] = h0;
	    		tmp[1] = h1;    		
			} else {
	    		tmp = new byte[16];
	    		offset = 0;
			}
			System.arraycopy(data, 0, tmp, offset, 16);
			txBuffer.add(tmp);
		}
	}
	
	private synchronized byte[] getData() {
		if (txBuffer != null) {
			if (!txBuffer.isEmpty()) {
				byte[] data = txBuffer.poll();
				return data;
			}
		}
		return null;
	}		
	
	private synchronized void sendNext() {
		if (canSend) {
			byte[] data = getData();
			if (data != null) {
				canSend = false;
				characteristicTX.setValue(data);
				characteristicTX.setWriteType(BluetoothGattCharacteristic.WRITE_TYPE_NO_RESPONSE); //TODO
				//characteristicTX.setWriteType(BluetoothGattCharacteristic.WRITE_TYPE_DEFAULT); //TODO				
				mBluetoothGatt.writeCharacteristic(characteristicTX);				
			}			
		}
	}
	
	
	
	private final BluetoothGattCallback mGattCallback = new BluetoothGattCallback() {
		
		@Override
		public void onConnectionStateChange(BluetoothGatt gatt, int status, int newState) {
			if (newState == BluetoothProfile.STATE_CONNECTED) {
				isConnecting = false;
				Util.log("Connected to GATT server.");
				Util.log("Attempting to start service discovery:" + mBluetoothGatt.discoverServices());
			} else if (newState == BluetoothProfile.STATE_DISCONNECTED) {
				isConnecting = false;
				Util.log("Disconnected from GATT server.");
				mBTservice.connectionFailed(false, InputStickError.ERROR_BLUETOOTH_CONNECTION_LOST);
			}			
		}

		@Override
		public void onServicesDiscovered(BluetoothGatt gatt, int status) {
			if (status == BluetoothGatt.GATT_SUCCESS) {
				Util.log("GATT onServicesDiscovered");
				List<BluetoothGattService> gattServices = null;		
				boolean serviceDiscovered = false;
		        if (mBluetoothGatt != null) {
		        	gattServices = mBluetoothGatt.getServices();
		        }
		        if (gattServices != null) {
			        String uuid = null;
			        characteristicRX = null;
			        for (BluetoothGattService gattService : gattServices) {
			            uuid = gattService.getUuid().toString();
			            if (MOD_CONF.equals(uuid)) {
			            	Util.log("BT LE - Serial Service Discovered");
			            	
				    		 characteristicTX = gattService.getCharacteristic(UUID_HM_RX_TX);
				    		 characteristicRX = gattService.getCharacteristic(UUID_HM_RX_TX);
				    		 if (characteristicRX == null) {
				    			 mBTservice.connectionFailed(false, InputStickError.ERROR_BLUETOOTH_BT40_NO_SPP_SERVICE);
				    		 } else {
				    			 serviceDiscovered = true;
				    		 }
			            }
			        }				
		        }
		        if (serviceDiscovered) {
		        	//enable notifications
		            mBluetoothGatt.setCharacteristicNotification(characteristicRX, true);
		            if (UUID_HM_RX_TX.equals(characteristicRX.getUuid())) {
		            	Util.log("RXTX SERVICE DISCOVERED!");
		                BluetoothGattDescriptor descriptor = characteristicRX.getDescriptor(UUID.fromString(MOD_CHARACTERISTIC_CONFIG));
		                descriptor.setValue(BluetoothGattDescriptor.ENABLE_NOTIFICATION_VALUE);		                
		                mBluetoothGatt.writeDescriptor(descriptor);	
		                
			            txBuffer = new LinkedList<byte[]>();		     			            
		                canSend = true;
		                sendNext();
			            
			            mBTservice.connectedEstablished();
		            } else {
		            	mBTservice.connectionFailed(false, InputStickError.ERROR_BLUETOOTH_BT40_NO_SPP_SERVICE);
		            }
		        } else {
		        	Util.log("BT LE - Serial Service NOT FOUND");
		        	mBTservice.connectionFailed(false, InputStickError.ERROR_BLUETOOTH_BT40_NO_SPP_SERVICE);
		        }
			} else {
				Util.log("onServicesDiscovered received: " + status);
			}
		}

		@Override
		public void onCharacteristicRead(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, int status) {
			if (status == BluetoothGatt.GATT_SUCCESS) {
			} 
		}

		@Override
		public void onCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic) {
			byte b[] = characteristic.getValue();
			if (b != null) {
				mBTservice.onByteRx(b);
			}	
		}

		@Override
		public void onCharacteristicWrite(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, int status) {
			Util.log("GATT onCharacteristicWrite");			
			if (status == BluetoothGatt.GATT_SUCCESS) {
				canSend = true;
				sendNext();
			}	//TODO error code?					
		}
		
	};	

}
