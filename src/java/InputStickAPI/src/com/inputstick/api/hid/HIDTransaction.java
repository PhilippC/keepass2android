package com.inputstick.api.hid;

import java.util.Vector;

public class HIDTransaction {

	private int mID;
	private Vector<HIDReport> reports;
	
	public HIDTransaction() {
		reports = new Vector<HIDReport>();
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
		byte[] report;
		report = reports.elementAt(0).getBytes();
		reports.removeElementAt(0);
		return report;
	}
	
	public HIDTransaction split(int n) {
		HIDTransaction result = new HIDTransaction();
		HIDReport report;
		if (n <= reports.size()) {
			while(n > 0) {
				report = reports.firstElement();
				reports.remove(0);
				result.addReport(report);
				n--;
			}		
		}
		
		return result;
	}
	
}
