package com.inputstick.api.hid;

import android.util.SparseArray;

public class HIDKeycodes {
	
	public static final byte NONE = 0x00;
	
	public static final byte CTRL_LEFT = 0x01;
	public static final byte SHIFT_LEFT = 0x02;
	public static final byte ALT_LEFT = 0x04;
	public static final byte GUI_LEFT = 0x08;
	public static final byte CTRL_RIGHT = 0x10;
	public static final byte SHIFT_RIGHT = 0x20;
	public static final byte ALT_RIGHT = 0x40;
	public static final byte GUI_RIGHT = (byte)0x80;	
	

	public static final byte KEY_ENTER = 0x28;
	public static final byte KEY_ESCAPE = 0x29;
	public static final byte KEY_BACKSPACE = 0x2A;
	public static final byte KEY_TAB = 0x2B;
	public static final byte KEY_SPACEBAR = 0x2C;
		
	public static final byte KEY_CAPS_LOCK = 0x39;
	
	
	public static final byte KEY_1 = 0x1E;
	public static final byte KEY_2 = 0x1F;
	public static final byte KEY_3 = 0x20;
	public static final byte KEY_4 = 0x21;
	public static final byte KEY_5 = 0x22;
	public static final byte KEY_6 = 0x23;
	public static final byte KEY_7 = 0x24;
	public static final byte KEY_8 = 0x25;
	public static final byte KEY_9 = 0x26;
	public static final byte KEY_0 = 0x27;
	
	public static final byte KEY_F1 = 0x3A;
	public static final byte KEY_F2 = 0x3B;
	public static final byte KEY_F3 = 0x3C;
	public static final byte KEY_F4 = 0x3D;
	public static final byte KEY_F5 = 0x3E;
	public static final byte KEY_F6 = 0x3F;
	public static final byte KEY_F7 = 0x40;
	public static final byte KEY_F8 = 0x41;
	public static final byte KEY_F9 = 0x42;
	public static final byte KEY_F10 = 0x43;
	public static final byte KEY_F11 = 0x44;
	public static final byte KEY_F12 = 0x45;		
	
	public static final byte KEY_PRINT_SCREEN = 0x46;
	public static final byte KEY_SCROLL_LOCK = 0x47;
	public static final byte KEY_PASUE = 0x48;
	public static final byte KEY_INSERT = 0x49;
	public static final byte KEY_HOME = 0x4A;
	public static final byte KEY_PAGE_UP = 0x4B;
	public static final byte KEY_DELETE = 0x4C;
	public static final byte KEY_END = 0x4D;
	public static final byte KEY_PAGE_DOWN = 0x4E;
	
	public static final byte KEY_ARROW_RIGHT = 0x4F;
	public static final byte KEY_ARROW_LEFT = 0x50;
	public static final byte KEY_ARROW_DOWN = 0x51;
	public static final byte KEY_ARROW_UP = 0x52;		
	
	public static final byte KEY_NUM_LOCK = 0x53;	
	public static final byte KEY_NUM_BACKSLASH = 	0x54;
	public static final byte KEY_NUM_STAR = 		0x55;
	public static final byte KEY_NUM_MINUS = 		0x56;
	public static final byte KEY_NUM_PLUS = 		0x57;	
	public static final byte KEY_NUM_ENTER = 		0x58;	
	public static final byte KEY_NUM_1 = 			0x59;
	public static final byte KEY_NUM_2 = 			0x5A;
	public static final byte KEY_NUM_3 = 			0x5B;
	public static final byte KEY_NUM_4 = 			0x5C;
	public static final byte KEY_NUM_5 = 			0x5D;
	public static final byte KEY_NUM_6 = 			0x5E;
	public static final byte KEY_NUM_7 = 			0x5F;
	public static final byte KEY_NUM_8 = 			0x60;
	public static final byte KEY_NUM_9 = 			0x61;
	public static final byte KEY_NUM_0 = 			0x62;		
	public static final byte KEY_NUM_DOT = 			0x63;
	
	public static final byte KEY_BACKSLASH_NON_US =	0x64;
	
	public static final byte KEY_A = 				0x04;
	public static final byte KEY_B = 				0x05;
	public static final byte KEY_C = 				0x06;
	public static final byte KEY_D = 				0x07;
	public static final byte KEY_E = 				0x08;
	public static final byte KEY_F = 				0x09;
	public static final byte KEY_G = 				0x0A;
	public static final byte KEY_H = 				0x0B;
	public static final byte KEY_I = 				0x0C;
	public static final byte KEY_J = 				0x0D;
	public static final byte KEY_K = 				0x0E;
	public static final byte KEY_L = 				0x0F;
	public static final byte KEY_M = 				0x10;
	public static final byte KEY_N = 				0x11;
	public static final byte KEY_O = 				0x12;
	public static final byte KEY_P = 				0x13;
	public static final byte KEY_Q = 				0x14;
	public static final byte KEY_R = 				0x15;
	public static final byte KEY_S = 				0x16;
	public static final byte KEY_T = 				0x17;
	public static final byte KEY_U = 				0x18;	
	public static final byte KEY_V = 				0x19;
	public static final byte KEY_W = 				0x1A;
	public static final byte KEY_X = 				0x1B;
	public static final byte KEY_Y = 				0x1C;
	public static final byte KEY_Z = 				0x1D;
	

	
	public static final byte KEY_MINUS = 			0x2D;
	public static final byte KEY_EQUALS = 			0x2E;	
	public static final byte KEY_LEFT_BRACKET = 	0x2F;
	public static final byte KEY_RIGHT_BRACKET = 	0x30;
	public static final byte KEY_BACKSLASH =		0x31;
	//public static final byte KEY_GRAVE = 			0x32;
	public static final byte KEY_SEMICOLON = 		0x33;
	public static final byte KEY_APOSTROPHE = 		0x34;
	public static final byte KEY_GRAVE = 			0x35;
	public static final byte KEY_COMA = 			0x36;
	public static final byte KEY_DOT = 				0x37;	
	public static final byte KEY_SLASH = 			0x38;

	
	public static final byte KEY_APPLICATION = 		0x65;	
	
	
	
	public static final SparseArray<String> modifiersMap;
    static
    {
    	modifiersMap = new SparseArray<String>();
    	modifiersMap.put(CTRL_LEFT, 								"Left Ctrl");
    	modifiersMap.put(SHIFT_LEFT, 								"Left Shift");
    	modifiersMap.put(ALT_LEFT, 									"Left Alt");
    	modifiersMap.put(GUI_LEFT, 									"Left GUI");
    	modifiersMap.put(CTRL_RIGHT, 								"Right Ctrl");
    	modifiersMap.put(SHIFT_RIGHT, 								"Right Shift");
    	modifiersMap.put(ALT_RIGHT, 								"Right Alt");
    	modifiersMap.put(GUI_RIGHT, 								"Right GUI");
    }
	
    public static final SparseArray<String> keyMap;
    static
    {
    	keyMap = new SparseArray<String>();
    	keyMap.put(0, 											"None");
    	keyMap.put(KEY_ENTER, 									"Enter");
    	keyMap.put(KEY_ESCAPE , 								"Esc");
    	keyMap.put(KEY_BACKSPACE  , 							"Backspace");
    	keyMap.put(KEY_TAB  , 									"Tab");
    	keyMap.put(KEY_SPACEBAR  , 								"Space");
    	
    	keyMap.put(KEY_CAPS_LOCK  , 							"CapsLock");    	
    	
    	keyMap.put(KEY_1  , 									"1");
    	keyMap.put(KEY_2  , 									"2");
    	keyMap.put(KEY_3  , 									"3");
    	keyMap.put(KEY_4  , 									"4");
    	keyMap.put(KEY_5  , 									"5");
    	keyMap.put(KEY_6  , 									"6");
    	keyMap.put(KEY_7  , 									"7");
    	keyMap.put(KEY_8  , 									"8");
    	keyMap.put(KEY_9  , 									"9");
    	keyMap.put(KEY_0  , 									"0");
    	
    	keyMap.put(KEY_F1  , 									"F1");
    	keyMap.put(KEY_F2  , 									"F2");
    	keyMap.put(KEY_F3  , 									"F3");
    	keyMap.put(KEY_F4  , 									"F4");
    	keyMap.put(KEY_F5  , 									"F5");
    	keyMap.put(KEY_F6  , 									"F6");
    	keyMap.put(KEY_F7  , 									"F7");
    	keyMap.put(KEY_F8  , 									"F8");
    	keyMap.put(KEY_F9  , 									"F9");
    	keyMap.put(KEY_F10  , 									"F10");
    	keyMap.put(KEY_F11  , 									"F11");
    	keyMap.put(KEY_F12  , 									"F12");
    	    	
    	keyMap.put(KEY_PRINT_SCREEN   , 						"Print Scrn");
    	keyMap.put(KEY_SCROLL_LOCK   , 							"ScrollLock");
    	keyMap.put(KEY_PASUE   , 								"Pause Break");
    	keyMap.put(KEY_INSERT   , 								"Insert");
    	keyMap.put(KEY_HOME   , 								"Home");
    	keyMap.put(KEY_PAGE_UP   , 								"PageUp");
    	keyMap.put(KEY_DELETE   , 								"Delete");
    	keyMap.put(KEY_END   , 									"End");
    	keyMap.put(KEY_PAGE_DOWN   , 							"PageDown");     	
    	
    	keyMap.put(KEY_ARROW_RIGHT   , 							"Right Arrow");
    	keyMap.put(KEY_ARROW_LEFT   , 							"Left Arrow");
    	keyMap.put(KEY_ARROW_DOWN   , 							"Down Arrow");
    	keyMap.put(KEY_ARROW_UP   , 							"Up Arrow");
    	
    	keyMap.put(KEY_NUM_LOCK   , 							"NumLock");
    	keyMap.put(KEY_NUM_BACKSLASH   , 						"Num /");
    	keyMap.put(KEY_NUM_STAR   , 							"Num *");
    	keyMap.put(KEY_NUM_MINUS   , 							"Num -");
    	keyMap.put(KEY_NUM_PLUS   , 							"Num +");
    	keyMap.put(KEY_NUM_ENTER   , 							"Num Enter");
    	keyMap.put(KEY_NUM_1   , 								"Num 1");
    	keyMap.put(KEY_NUM_2   , 								"Num 2");
    	keyMap.put(KEY_NUM_3   , 								"Num 3");
    	keyMap.put(KEY_NUM_4   , 								"Num 4");
    	keyMap.put(KEY_NUM_5   , 								"Num 5");
    	keyMap.put(KEY_NUM_6   , 								"Num 6");
    	keyMap.put(KEY_NUM_7   , 								"Num 7");
    	keyMap.put(KEY_NUM_8   , 								"Num 8");
    	keyMap.put(KEY_NUM_9   , 								"Num 9");
    	keyMap.put(KEY_NUM_0   , 								"Num 0");
    	keyMap.put(KEY_NUM_DOT   , 								"Num .");
    	    	
    	keyMap.put(KEY_A   , 									"A");
    	keyMap.put(KEY_B   , 									"B");
    	keyMap.put(KEY_C   , 									"C");
    	keyMap.put(KEY_D   , 									"D");
    	keyMap.put(KEY_E   , 									"E");
    	keyMap.put(KEY_F   , 									"F");
    	keyMap.put(KEY_G   , 									"G");
    	keyMap.put(KEY_H   , 									"H");
    	keyMap.put(KEY_I   , 									"I");
    	keyMap.put(KEY_J   , 									"J");
    	keyMap.put(KEY_K   , 									"K");
    	keyMap.put(KEY_L   , 									"L");
    	keyMap.put(KEY_M   , 									"M");
    	keyMap.put(KEY_N   , 									"N");
    	keyMap.put(KEY_O   , 									"O");    	
    	keyMap.put(KEY_P   , 									"P");
    	keyMap.put(KEY_Q   , 									"Q");
    	keyMap.put(KEY_R   , 									"R");
    	keyMap.put(KEY_S   , 									"S");
    	keyMap.put(KEY_T   , 									"T");
    	keyMap.put(KEY_U   , 									"U");
    	keyMap.put(KEY_V   , 									"V");
    	keyMap.put(KEY_W   , 									"W");    	
    	keyMap.put(KEY_X   , 									"X");
    	keyMap.put(KEY_Y   , 									"Y");
    	keyMap.put(KEY_Z   , 									"Z");
    	
    	keyMap.put(KEY_MINUS   , 								"-");
    	keyMap.put(KEY_EQUALS   , 								"=");
    	keyMap.put(KEY_LEFT_BRACKET   , 						"[");
    	keyMap.put(KEY_RIGHT_BRACKET   , 						"]");
    	keyMap.put(KEY_BACKSLASH   , 							"\\");
    	//keyMap.put(KEY_GRAVE  , 								"`");
    	keyMap.put(KEY_SEMICOLON   , 							";");
    	keyMap.put(KEY_APOSTROPHE   , 							"'");
    	keyMap.put(KEY_GRAVE   , 								"`");
    	keyMap.put(KEY_COMA   , 								",");
    	keyMap.put(KEY_DOT   , 									".");
    	keyMap.put(KEY_SLASH   , 								"/");
    	
    	keyMap.put(KEY_APPLICATION   , 							"Application");
    }
	
    public static final int[] ASCIItoHID = {
        0, //000
        0, //001
        0, //002
        0, //003
        0, //004
        0, //005
        0, //006
        0, //007
        0, //008
        0, //009
        0, //010
        0, //011
        0, //012
        0, //013
        0, //014
        0, //015
        0, //016
        0, //017
        0, //018
        0, //019
        0, //020
        0, //021
        0, //022
        0, //023
        0, //024
        0, //025
        0, //026
        0, //027
        0, //028
        0, //029
        0, //030
        0, //031
        44, //032 space
        128 + 30, //033 ! [SHIFT]
        128 + 52, //034 " [SHIFT]
        128 + 32, //035 # [SHIFT]
        128 + 33, //036 $ [SHIFT]
        128 + 34, //037 % [SHIFT]
        128 + 36, //038 & [SHIFT]
        52, //039 '
        128 + 38, //040 ( [SHIFT]
        128 + 39, //041 ) [SHIFT]
        128 + 37, //042 * [SHIFT]
        128 + 46, //043 + [SHIFT]
        54, //044 ,
        45, //045 - (-)
        55, //046 . (.)
        56, //047 /
        39, //048 0
        30, //049 1
        31, //050 2
        32, //051 3
        33, //052 4
        34, //053 5
        35, //054 6
        36, //055 7
        37, //056 8
        38, //057 9
        128 + 51, //058 : [SHIFT]
        51, //059 ;
        128 + 54, //060 < [SHIFT]
        46, //061 =
        128 + 55, //062 > [SHIFT]
        128 + 56, //063 ? [SHIFT]
        128 + 31, //064 @ [SHIFT]
        128 + 4, //065 A [SHIFT]
        128 + 5, //066 B [SHIFT]
        128 + 6, //067 C [SHIFT]
        128 + 7, //068 D [SHIFT]
        128 + 8, //069 E [SHIFT]
        128 + 9, //070 F [SHIFT]
        128 + 10, //071 G [SHIFT]
        128 + 11, //072 H [SHIFT]
        128 + 12, //073 I [SHIFT]
        128 + 13, //074 J [SHIFT]
        128 + 14, //075 K [SHIFT]
        128 + 15, //076 L [SHIFT]
        128 + 16, //077 M [SHIFT]
        128 + 17, //078 N [SHIFT]
        128 + 18, //079 O [SHIFT]
        128 + 19, //080 P [SHIFT]
        128 + 20, //081 Q [SHIFT]
        128 + 21, //082 R [SHIFT]
        128 + 22, //083 S [SHIFT]
        128 + 23, //084 T [SHIFT]
        128 + 24, //085 U [SHIFT]
        128 + 25, //086 V [SHIFT]
        128 + 26, //087 W [SHIFT]
        128 + 27, //088 X [SHIFT]
        128 + 28, //089 Y [SHIFT]
        128 + 29, //090 Z [SHIFT]
        47, //091 [
        49, /*092 \   */
        48, //093 ]
        128 + 35, //094 ^ [SHIFT]
        128 + 45, //095 _ [SHIFT] (underscore)
        128 + 53, //096 ` [SHIFT] (grave accent)
        4, //097 a
        5, //098 b
        6, //099 c
        7, //100 d
        8, //101 e
        9, //102 f
        10, //103 g
        11, //104 h
        12, //105 i
        13, //106 j
        14, //107 k
        15, //108 l
        16, //109 m
        17, //110 n
        18, //111 o
        19, //112 p
        20, //113 q
        21, //114 r
        22, //115 s
        23, //116 t
        24, //117 u
        25, //118 v
        26, //119 w
        27, //120 x
        28, //121 y
        29, //122 z
        128 + 47, //123 { [SHIFT]
        128 + 49, //124 | [SHIFT]
        128 + 48, //125 } [SHIFT]
        128 + 53, //126 ~ [SHIFT]
        0       //127 just in case...
    };	
    
    public static char getChar(byte keyCode) {
    	for (int i = 0; i < ASCIItoHID.length; i++) {
    		if (ASCIItoHID[i] == keyCode) {
    			return (char)i;
    		}
    	}
    	return 0;
    }
    
	public static byte getKeyCode(char c) {
		return (byte)ASCIItoHID[c]; //TODO range
	}
	
	public static int getKeyCode(int c) {
		return ASCIItoHID[c]; //TODO range
	}    
    
    public static String modifiersToString(byte modifiers) {
    	String result = "None";
    	boolean first = true;
    	byte mod;
    	for (int i = 0; i < 8; i++) {
    		mod = (byte)(CTRL_LEFT << i);
    		if ((modifiers & mod) != 0) {  
    			if ( !first) {
    				result += ", ";
    			} else {
    				result = "";
    			}
    			first = false;
    			result += modifiersMap.get(mod);
    		}
    	}
    	
    	return result;
    }
    
    public static String keyToString(byte key) {
    	String result = keyMap.get(key);
    	if (result == null) {
    		result = "Unknown";
    	}
    	return result;
    }
}
