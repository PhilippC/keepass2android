package com.inputstick.api;


public abstract class Util {
	
	private static final boolean debug = false;
	
	public static void log(String msg) {
		if (debug) {
			System.out.println("LOG: " + msg);
		}
	}
	
	public static void printHex(byte[] toPrint, String info) {
		if (debug) {
			System.out.println(info);
			printHex(toPrint);
		}
	}


	public static void printHex(byte[] toPrint) {
		if (debug) {
			int cnt = 0;
			String s;
			byte b;
	        for (int i = 0; i < toPrint.length; i++) {
	        	b = toPrint[i];    	
	        	if ((b < 10) && (b >= 0)) {
	        		s = Integer.toHexString((int)b);
	        		s = "0" + s;
	        	} else {
		        	s = Integer.toHexString((int)b);
		        	if (s.length() > 2) {
		        		s = s.substring(s.length() - 2);
		        	}
	        	}        	        	
	        	s = s.toUpperCase();
	        	System.out.print("0x" + s + " ");
	        	cnt++;
	        	if (cnt == 8) {
	        		System.out.println("");
	        		cnt = 0;
	        	}
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
	

}
