package com.inputstick.api.hid;

import java.util.LinkedList;

public class HIDTransaction {

	private int mID;
	private LinkedList<HIDReport> reports;
	
	public HIDTransaction() {
		reports = new LinkedList<HIDReport>();
	}
	
	public void addReport(HIDReport report) {
		reports.add(report);
	}
	
	public int getReportsCount() {
		return reports.size();
	}
	
	public void setID(int id) {
		mID = id;
	}
	
	public int getID() {
		return mID;
	}
	
	public boolean hasNext() {
		return !reports.isEmpty();
	}
	
	public byte[] getNextReport() {
		return reports.poll().getBytes();
	}
	
	public HIDReport getHIDReportAt(int pos) {
		return reports.get(pos);
	}
	
	public HIDTransaction split(int n) {
		HIDTransaction result = new HIDTransaction();
		HIDReport report;
		if (n <= reports.size()) {
			while(n > 0) {
				report = reports.poll();
				result.addReport(report);
				n--;
			}		
		}
		
		return result;
	}
	
}
