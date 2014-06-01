package com.inputstick.api.hid;

import java.util.Vector;
import com.inputstick.api.ConnectionManager;
import com.inputstick.api.Packet;

public class HIDTransactionQueue {
	
	public static final int KEYBOARD = 1;
	public static final int MOUSE = 2;
	public static final int CONSUMER = 3;
	
	private static final int BUFFER_SIZE = 32;

	private final Vector<HIDTransaction> queue;
	private final ConnectionManager mConnectionManager;
	private final byte cmd;
	private boolean ready;
	
	
	private long lastTime;
	private int lastReports;
	
	
	public HIDTransactionQueue(int type, ConnectionManager connectionManager) {
		queue = new Vector<HIDTransaction>();
		mConnectionManager = connectionManager;
		ready = false;
		switch (type) {
			case KEYBOARD:
				cmd = Packet.CMD_HID_DATA_KEYB;
				break;
			case MOUSE:
				cmd = Packet.CMD_HID_DATA_MOUSE;
				break;
			case CONSUMER:
				cmd = Packet.CMD_HID_DATA_CONSUMER;
				break;
			default:
				cmd = Packet.CMD_DUMMY;
		}
	}
	
	private void sendNext() {
		HIDTransaction transaction;
		byte reports = 0;
		ready = false;
		Packet p = new Packet(false, cmd, reports);
		
		//assume there is at least 1 element in queue		
		transaction = queue.firstElement();
		if (transaction.getReportsCount() > BUFFER_SIZE) {
			//transaction too big! split
			transaction = transaction.split(BUFFER_SIZE);			
		} else {
			queue.removeElementAt(0);						
		}
				
		while (transaction.hasNext()) {
			p.addBytes(transaction.getNextReport());
			reports++;
		}		
		//TODO add next transactions if possible
		
		while(true) {
			if (queue.isEmpty()) {
				break;
			}
			
			transaction = queue.firstElement();			
			if (reports + transaction.getReportsCount() < BUFFER_SIZE) {
				queue.removeElementAt(0);	
				while (transaction.hasNext()) {
					p.addBytes(transaction.getNextReport());
					reports++;
				}				
			} else {
				break;
			}
		}
		
		
		p.modifyByte(1, reports); //set reports count
		mConnectionManager.sendPacket(p);	
		
		lastReports = reports;
		lastTime = System.currentTimeMillis();
	}
	
	public void addTransaction(HIDTransaction transaction) {
		if (queue.isEmpty()) {
			if (System.currentTimeMillis() > lastTime + (lastReports * 8 * 2/*just to be safe*/)) {
				ready = true;
			}
		}		
		
		queue.add(transaction);						
		if (ready) {
			sendNext();
		} 		
	}
	
	public void deviceReady() {
		if (!queue.isEmpty()) {
			sendNext();
		} else {
			ready = true;
		}
	}			
	
}
