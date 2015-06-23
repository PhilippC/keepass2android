package com.inputstick.api;

import java.io.UnsupportedEncodingException;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;


public abstract class Util {
	
	public static boolean debug = false;
	public static boolean flashingToolMode = false;
	
	public static void log(String msg) {
		log(msg, false);
	}
	
	public static void log(String msg, boolean displayTime) {
		if (debug) {
			System.out.print("LOG: " + msg);
			if (displayTime) {
				System.out.print(" @ " + System.currentTimeMillis());
			}
			System.out.println();
		}
	}
	
	public static void printHex(byte[] toPrint, String info) {
		if (debug) {
			System.out.println(info);
			printHex(toPrint);
		}
	}

	public static String byteToHexString(byte b) {
		String s;
    	//0x0..0xF = 0x00..0x0F
    	if ((b < 0x10) && (b >= 0)) {
    		s = Integer.toHexString((int)b);
    		s = "0" + s;
    	} else {
        	s = Integer.toHexString((int)b);
        	if (s.length() > 2) {
        		s = s.substring(s.length() - 2);
        	}
    	}        	        	
    	s = s.toUpperCase();
    	return s;
	}

	public static void printHex(byte[] toPrint) {
		if (debug) {
			if (toPrint != null) {
				int cnt = 0;
				byte b;
		        for (int i = 0; i < toPrint.length; i++) {
		        	b = toPrint[i];  

		        	System.out.print("0x" + byteToHexString(b) + " ");
		        	cnt++;
		        	if (cnt == 8) {
		        		System.out.println("");
		        		cnt = 0;
		        	}
		        }
		        
			} else {
				System.out.println("null");
			}
			System.out.println("\n#####");
		}
	}
	
	
    public static byte getLSB(int n) {
        return (byte)(n & 0x00FF);
    }
    
    public static byte getMSB(int n) {
        return (byte)((n & 0xFF00) >> 8);
    }   
    
    public static int getInt(byte b) {
    	int bInt = b & 0xFF;
    	return bInt;
    }
    
    public static int getInt(byte msb, byte lsb) {
    	int msbInt = msb & 0xFF;
    	int lsbInt = lsb & 0xFF;
    	return (msbInt << 8) + lsbInt;    	
    } 	
	
	public static long getLong(byte b0, byte b1, byte b2, byte b3) {
		long result;		
		result = (b0) & 0xFF;
		result <<= 8;
		result += (b1) & 0xFF;
		result <<= 8;
		result += (b2) & 0xFF;
		result <<= 8;
		result += (b3) & 0xFF;				
		return result;
	}
	
	
	public static byte[] getPasswordBytes(String plainText) {
		try {
			MessageDigest md = MessageDigest.getInstance("MD5");	
			return md.digest(plainText.getBytes("UTF-8"));
		} catch (NoSuchAlgorithmException e) {
			e.printStackTrace();
		} catch (UnsupportedEncodingException e) {
			e.printStackTrace();
		}
		return null;
	}
	

}
