package com.inputstick.api.init;

public class DeviceInfo {
	
	private int firmwareType;
	private int versionMajor;
	private int versionMinor;
	private int versionHardware;		
	private int securityStatus;
	
	private boolean passwordProtected;
	
	public DeviceInfo(byte[] data) {
		//cmd, param
		firmwareType = data[2];
		versionMajor = data[3];
		versionMinor = data[4];
		versionHardware = data[5];
		
		
		//6,7,8,9		
		//10,11,12,13		
		//14,15,16,17
		
		//18,19
		securityStatus = data[19];
		if (data[20] == 0) {
			passwordProtected = false;
		} else {
			passwordProtected = true;
		}
	}
	
	public int getSecurityStatus() {
		return securityStatus;
	}
	
	public boolean isAuthenticated() {
		return ((securityStatus & 0x10) != 0);
	}
	
	public boolean isUnlocked() {
		if (getFirmwareVersion() < 96) {
			return true;
		} else {
			return ((securityStatus & 0x08) != 0);
		}
	}
	
	public int getFirmwareType() {
		return firmwareType;
	}
	
	public boolean isPasswordProtected() {
		return passwordProtected;
	}

	public int getVersionMinor() {
		return versionMinor;
	}
	
	public int getVersionMajor() {
		return versionMajor;
	}
	
	public int getHardwareVersion() {
		return versionHardware;
	}
	
	public int getFirmwareVersion() {
		return (versionMajor) * 100 + versionMinor;
	}
	
	
	
	public boolean supportsEncryption() {
		return (getFirmwareVersion() >= 91);
	}
	
	public boolean supportsPinChange() {
		return (getFirmwareVersion() >= 97);
	}
	
	public boolean supportsGamepad() {
		return (getFirmwareVersion() >= 97);
	}
	
	public boolean supportsRestoreOptions() {
		return (getFirmwareVersion() >= 98);
	}

	
}
