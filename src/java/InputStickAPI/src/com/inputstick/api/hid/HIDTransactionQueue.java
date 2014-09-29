package com.inputstick.api.hid;

import java.util.Timer;
import java.util.TimerTask;
import java.util.Vector;

import com.inputstick.api.ConnectionManager;
import com.inputstick.api.HIDInfo;
import com.inputstick.api.OnEmptyBufferListener;
import com.inputstick.api.Packet;
import com.inputstick.api.basic.InputStickHID;

public class HIDTransactionQueue {
	
	private static final int BUFFER_SIZE = 32;
	private static final int BT_DELAY = 50; //additional delay for BT overhead
	
	private static final int MAX_PACKETS_PER_UPDATE = 10;
	private static final int MAX_IMMEDIATE_PACKETS = 3;

	private final Vector<HIDTransaction> queue;
	private final ConnectionManager mConnectionManager;
	private final byte cmd;
	private boolean ready;
	
	private int mInterfaceType;
	private boolean mustNotify;
	private Vector<OnEmptyBufferListener> mBufferEmptyListeners = new Vector<OnEmptyBufferListener>();
	
	private Timer t;
	private boolean timerCancelled;
	private boolean sentAhead;
	private long lastTime;
	private long minNextTime;
	private int lastReports;	
	
	
	// >= FW 0.93
	private boolean bufferInitDone;
	private boolean constantUpdateMode;
	private int bufferFreeSpace;
	private int immediatePacketsLeft;
	//private int reportsSentSinceLastUpdate;
	private int packetsSentSinceLastUpdate;
	
	
	
	public HIDTransactionQueue(int interfaceType, ConnectionManager connectionManager) {
		constantUpdateMode = false;
		bufferFreeSpace = BUFFER_SIZE;
		
		queue = new Vector<HIDTransaction>();
		mConnectionManager = connectionManager;
		ready = false;
		sentAhead = false;
		minNextTime = 0;
		
		mustNotify = false;
		
		mInterfaceType = interfaceType;
		switch (interfaceType) {
			case InputStickHID.INTERFACE_KEYBOARD:
				cmd = Packet.CMD_HID_DATA_KEYB;
				break;
			case InputStickHID.INTERFACE_MOUSE:
				cmd = Packet.CMD_HID_DATA_MOUSE;
				break;
			case InputStickHID.INTERFACE_CONSUMER:
				cmd = Packet.CMD_HID_DATA_CONSUMER;
				break;
			default:
				cmd = Packet.CMD_DUMMY;
		}
	}
	
	private int sendNext(int maxReports) {
		HIDTransaction transaction;		
		
		//assume there is at least 1 element in queue		
		transaction = queue.firstElement();
		if (transaction.getReportsCount() > maxReports) {
			// v0.92
			if (maxReports < BUFFER_SIZE) {
				//don't split transactions until there is no other way  left!
				return 0;
			}
			
			//transaction too big to fit single packet! split
			transaction = transaction.split(BUFFER_SIZE);
		} else {
			queue.removeElementAt(0);						
		}
		
		byte reports = 0;
		ready = false;		
		Packet p = new Packet(false, cmd, reports);
				
		while (transaction.hasNext()) {
			p.addBytes(transaction.getNextReport());
			reports++;
		}		
		
		while(true) {
			if (queue.isEmpty()) {
				break;
			}
			
			transaction = queue.firstElement();			
			if (reports + transaction.getReportsCount() < maxReports) {
				queue.removeElementAt(0);	
				while (transaction.hasNext()) {
					p.addBytes(transaction.getNextReport());
					reports++;
				}				
			} else {
				break;
			}
		}
		
		//!! total number of reports must be < 32 ! (max packet limitation)
		p.modifyByte(1, reports); //set reports count
		mConnectionManager.sendPacket(p);			
		
		lastReports = reports;
		lastTime = System.currentTimeMillis();
		minNextTime = lastTime + (lastReports * 4) + BT_DELAY;
		
		if (queue.isEmpty()) {
			notifyOnLocalBufferEmpty();
		}
		
		return reports;
	}
	
	public void addBufferEmptyListener(OnEmptyBufferListener listener) {
		if (listener != null) {
			if ( !mBufferEmptyListeners.contains(listener)) {
				mBufferEmptyListeners.add(listener);
			}
		}
	}
	
	public void removeBufferEmptyListener(OnEmptyBufferListener listener) {
		if (listener != null) {
			mBufferEmptyListeners.remove(listener);
		}
	}
	
	private void notifyOnRemoteBufferEmpty() {
		for (OnEmptyBufferListener listener : mBufferEmptyListeners) {
			listener.onRemoteBufferEmpty(mInterfaceType);
		}
	}
	
	private void notifyOnLocalBufferEmpty() {
		for (OnEmptyBufferListener listener : mBufferEmptyListeners) {
			listener.onRemoteBufferEmpty(mInterfaceType);
		}
	}
	
	public synchronized boolean isLocalBufferEmpty() {
		return queue.isEmpty();
	}
	
	public synchronized boolean isRemoteBufferEmpty() {
		if ((queue.isEmpty()) && (bufferFreeSpace == BUFFER_SIZE)) {
			return true;
		}
		
		if (queue.isEmpty() && ( !mustNotify)) {
			return true;
		} else {
			return false;
		}
	}
	
	public synchronized void clearBuffer() {
		queue.removeAllElements();
	}
	
	public synchronized void addTransaction(HIDTransaction transaction) {
		if ( !bufferInitDone) {
			queue.add(transaction);		
			return;
		}
		
		
		if (constantUpdateMode) {
			queue.add(transaction);		
			sendToBuffer(true);
			return;
		}
		
		
		mustNotify = true;
		//using sentAhead will slow down mouse. FW0.92 will solve the problems
		if ((queue.isEmpty()) && (System.currentTimeMillis() > minNextTime) /*&& ( !sentAhead)*/) {
			sentAhead = true;
			ready = true;
		} 
		
		queue.add(transaction);						
		if (ready) {
			sendNext(BUFFER_SIZE);
		} 		
	}	

	private synchronized void timerAction() {
		if ( !timerCancelled) {
			if (sentAhead) {
				deviceReady(null, 0); //will set sentAhead to false;
				sentAhead = true; //restore value
			} else {
				deviceReady(null, 0);
			}
		}
	}	
	
	public synchronized void deviceReady(HIDInfo hidInfo, int reportsSentToHost) {
		//it is possible that in the meantime some packets has been sent to IS!!!
		
		bufferInitDone = true;
		
		if (hidInfo != null) {			
			if (hidInfo.isSentToHostInfoAvailable()) {
				constantUpdateMode = true;
				// >= FW 0.93
				bufferFreeSpace += reportsSentToHost;
				if ((bufferFreeSpace == BUFFER_SIZE) && (queue.isEmpty())) {
					notifyOnRemoteBufferEmpty();
				}
				immediatePacketsLeft = MAX_IMMEDIATE_PACKETS;
				//reportsSentSinceLastUpdate = 0;
				packetsSentSinceLastUpdate = 0;						
				sendToBuffer(false);
				return;
			} 			
		}
		
		
		
		long now = System.currentTimeMillis();
		//System.out.println("v90 HID update");
		if (now < minNextTime) {
			//set timer, just in case if deviceReady won't be called again					
			timerCancelled = false;
			t = new Timer();
			t.schedule(new TimerTask() {
				@Override
				public void run() {
					timerAction();
				}
			}, (minNextTime - now + 1));						
		} else {	
			timerCancelled = true;
			sentAhead = false;
			if (!queue.isEmpty()) {
				sendNext(BUFFER_SIZE);
			} else {			
				ready = true;
				//queue is empty, InputStick reported that buffer is empty, data was added since last notification
				if (mustNotify) {
					notifyOnRemoteBufferEmpty();
					mustNotify = false;
				}
			}
		}
	}		
	
	public synchronized void sendToBuffer(boolean justAdded) {
		if ((justAdded) && (immediatePacketsLeft <= 0)) {
			return;
		}
		
		if ( !InputStickHID.isReady()) {
			return;
		}	
		
		if (queue.isEmpty()) {
			return;
		}
		if (bufferFreeSpace <= 0) {
			return;
		}
		if (packetsSentSinceLastUpdate >= MAX_PACKETS_PER_UPDATE) {
			return;
		}		
		
		int reportsSent = sendNext(bufferFreeSpace);
		if (reportsSent > 0) {
			if (justAdded) {
				immediatePacketsLeft --;
			}
			bufferFreeSpace -= reportsSent;
			packetsSentSinceLastUpdate ++;	
		}
	}
	
}
