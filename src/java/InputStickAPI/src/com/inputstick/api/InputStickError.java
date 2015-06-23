package com.inputstick.api;

import android.util.SparseArray;

public class InputStickError {		
	
	public static String ERROR_UNKNOWN_MSG = "Unknown";
	
	public static final int ERROR_NONE = 0;
	public static final int ERROR_UNKNOWN = 1;
	
	//Bluetooth comm errors:
	public static final int ERROR_BLUETOOTH 							= 0x0100;	
	public static final int ERROR_BLUETOOTH_CONNECTION_FAILED 			= ERROR_BLUETOOTH | 0x01;
	public static final int ERROR_BLUETOOTH_CONNECTION_LOST 			= ERROR_BLUETOOTH | 0x02;
	public static final int ERROR_BLUETOOTH_NOT_SUPPORTED 				= ERROR_BLUETOOTH | 0x03;
	public static final int ERROR_BLUETOOTH_INVALID_MAC 				= ERROR_BLUETOOTH | 0x04;
	public static final int ERROR_BLUETOOTH_ECHO_TIMEDOUT 				= ERROR_BLUETOOTH | 0x05;
	public static final int ERROR_BLUETOOTH_NO_REMOTE_DEVICE			= ERROR_BLUETOOTH | 0x06;
	public static final int ERROR_BLUETOOTH_BT40_NOT_SUPPRTED			= ERROR_BLUETOOTH | 0x07;
	public static final int ERROR_BLUETOOTH_BT40_NO_SPP_SERVICE			= ERROR_BLUETOOTH | 0x08;
			
	//Hardware-related errors:
	public static final int ERROR_HARDWARE								= 0x0200;	
	public static final int ERROR_HARDWARE_WDG_RESET 					= ERROR_HARDWARE | 0x01;		
	
	//Packet
	public static final int ERROR_PACKET	 							= 0x0300;	
	public static final int ERROR_PACKET_INVALID_CRC 					= ERROR_PACKET | 0x01;
	public static final int ERROR_PACKET_INVALID_LENGTH 				= ERROR_PACKET | 0x02;
	public static final int ERROR_PACKET_INVALID_HEADER 				= ERROR_PACKET | 0x03;
	
	//Init
	public static final int ERROR_INIT									= 0x0400;	
	public static final int ERROR_INIT_UNSUPPORTED_CMD 					= ERROR_INIT | 0x01;
	public static final int ERROR_INIT_TIMEDOUT 						= ERROR_INIT | 0x02;
	public static final int ERROR_INIT_FW_TYPE_NOT_SUPPORTED 			= ERROR_INIT | 0x03;
	public static final int ERROR_INIT_FW_VERSION_NOT_SUPPORTED 		= ERROR_INIT | 0x04;

	//Security
	public static final int ERROR_SECURITY								= 0x0500;	
	public static final int ERROR_SECURITY_NOT_SUPPORTED				= ERROR_SECURITY | 0x01;
	public static final int ERROR_SECURITY_NO_KEY						= ERROR_SECURITY | 0x02;
	public static final int ERROR_SECURITY_INVALID_KEY					= ERROR_SECURITY | 0x03;
	public static final int ERROR_SECURITY_CHALLENGE					= ERROR_SECURITY | 0x04;
	public static final int ERROR_SECURITY_NOT_PROTECTED				= ERROR_SECURITY | 0x05;
	
	//Android
	public static final int ERROR_ANDROID								= 0x1000;	
	public static final int ERROR_ANDROID_NO_UTILITY_APP				= ERROR_ANDROID | 0x01;
	public static final int ERROR_ANDROID_SERVICE_DISCONNECTED			= ERROR_ANDROID | 0x02;
	public static final int ERROR_ANDROID_UTIL_FORCE_DISC				= ERROR_ANDROID | 0x03;
	public static final int ERROR_ANDROID_UTIL_IDLE_DISC				= ERROR_ANDROID | 0x04;
	
	// 0000 - ERROR_NONE 
	// xx00 - Category / Unknown
	// xxyy - Category / Details 	
	
	private static final SparseArray<String> errorCodeMap;
    static
    {
    	errorCodeMap = new SparseArray<String>();
    	errorCodeMap.put(ERROR_NONE, 								"None");
    	errorCodeMap.put(ERROR_UNKNOWN, 							"Unknown");
    	//Bluetooth    	
    	errorCodeMap.put(ERROR_BLUETOOTH, 							"Bluetooth");
    	errorCodeMap.put(ERROR_BLUETOOTH_CONNECTION_FAILED, 		"Failed to connect");
    	errorCodeMap.put(ERROR_BLUETOOTH_CONNECTION_LOST, 			"Connection lost");
    	errorCodeMap.put(ERROR_BLUETOOTH_NOT_SUPPORTED, 			"Not supported");
    	errorCodeMap.put(ERROR_BLUETOOTH_INVALID_MAC, 				"Invalid MAC");
    	errorCodeMap.put(ERROR_BLUETOOTH_ECHO_TIMEDOUT, 			"Echo timedout");
    	errorCodeMap.put(ERROR_BLUETOOTH_NO_REMOTE_DEVICE, 			"Can't find remote device");  
    	errorCodeMap.put(ERROR_BLUETOOTH_BT40_NOT_SUPPRTED, 		"BT 4.0 is not supported");
    	errorCodeMap.put(ERROR_BLUETOOTH_BT40_NO_SPP_SERVICE, 		"BT 4.0 RXTX not found");  
    	
    	//Hardware
    	errorCodeMap.put(ERROR_HARDWARE, 							"Hardware");
    	errorCodeMap.put(ERROR_HARDWARE_WDG_RESET, 					"WDG reset");
    	
    	//Packet
    	errorCodeMap.put(ERROR_PACKET, 								"Invalid packet");
    	errorCodeMap.put(ERROR_PACKET_INVALID_CRC, 					"Invalid CRC");
    	errorCodeMap.put(ERROR_PACKET_INVALID_LENGTH, 				"Invalid length");
    	errorCodeMap.put(ERROR_PACKET_INVALID_HEADER, 				"Invalid header");
    	
    	//Init
    	errorCodeMap.put(ERROR_INIT, 								"Init");
    	errorCodeMap.put(ERROR_INIT_UNSUPPORTED_CMD, 				"Command not supported");
    	errorCodeMap.put(ERROR_INIT_TIMEDOUT, 						"Timedout");
    	errorCodeMap.put(ERROR_INIT_FW_TYPE_NOT_SUPPORTED, 			"FW type not supported");
    	errorCodeMap.put(ERROR_INIT_FW_VERSION_NOT_SUPPORTED, 		"FW version not supported");
    	
    	//Security
    	errorCodeMap.put(ERROR_SECURITY, 							"Security");
    	errorCodeMap.put(ERROR_SECURITY_NOT_SUPPORTED, 				"Not supported");
    	errorCodeMap.put(ERROR_SECURITY_NO_KEY, 					"No key provided");
    	errorCodeMap.put(ERROR_SECURITY_INVALID_KEY, 				"Invalid key");
    	errorCodeMap.put(ERROR_SECURITY_CHALLENGE, 					"Challenge failed");
    	errorCodeMap.put(ERROR_SECURITY_NOT_PROTECTED, 				"Key was provided, but device is not password protected");    
    	
    	//Android
    	errorCodeMap.put(ERROR_ANDROID, 							"Android");
    	errorCodeMap.put(ERROR_ANDROID_NO_UTILITY_APP, 				"InputStickUtility app not installed");
    	errorCodeMap.put(ERROR_ANDROID_SERVICE_DISCONNECTED, 		"Service connection lost");
    	errorCodeMap.put(ERROR_ANDROID_UTIL_FORCE_DISC, 			"Connection closed by InputStickUtility");
    	errorCodeMap.put(ERROR_ANDROID_UTIL_IDLE_DISC, 				"Connection closed due to inactivity");
    	
    }
	
	public static String getErrorType(int errorCode) {
		String result;
		errorCode &= 0xFF00;
		result = errorCodeMap.get(errorCode);
		if (result != null) {
			return result;
		} else {
			return ERROR_UNKNOWN_MSG;
		}
	}
	
	public static String getErrorMessage(int errorCode) {
		String result;		
		if (errorCode == ERROR_NONE) {
			return errorCodeMap.get(ERROR_NONE);
		}
		
		//handle case: "Bluetooth: Unknown" etc
		if ((errorCode & 0x00FF) == 0) {
			return ERROR_UNKNOWN_MSG;
		}
		
		result = errorCodeMap.get(errorCode);
		if (result != null) {
			return result;
		} else {
			return ERROR_UNKNOWN_MSG;
		}			
	}
	
	public static String getFullErrorMessage(int errorCode) {
		return getErrorType(errorCode) + " - " + getErrorMessage(errorCode);
	}

}
