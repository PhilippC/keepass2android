package keepass2android.plugin.inputstick;

import android.annotation.SuppressLint;
import com.inputstick.api.hid.HIDKeycodes;

public class MacroHelper {
	
	public static final String MACRO_ACTION_URL = "url";
	public static final String MACRO_ACTION_USER_NAME = "user";
	public static final String MACRO_ACTION_PASSWORD = "pass";
	public static final String MACRO_ACTION_PASSWORD_MASKED = "masked_pass";
	public static final String MACRO_ACTION_CLIPBOARD = "clipboard";
	public static final String MACRO_ACTION_TYPE = "type";
	public static final String MACRO_ACTION_KEY = "key";
	public static final String MACRO_ACTION_DELAY = "delay";
	public static final String MACRO_ACTION_BACKGROUND = "background";
	
	public static final String MACRO_BACKGROUND_EXEC_STRING = "%" + MACRO_ACTION_BACKGROUND;
	
	private static class KeyLabel {		
		byte keyCode;
		String primary;
		String secondary;
		
		public KeyLabel(byte keyCode, String primary, String secondary) {
			this.keyCode = keyCode;
			this.primary = primary;
			this.secondary = secondary;
		}

	}
	
	private static final KeyLabel[] modLUT = {
		
		new KeyLabel(HIDKeycodes.ALT_LEFT, 		"alt", "lalt"),
		new KeyLabel(HIDKeycodes.CTRL_LEFT, 	"ctrl", "lctrl"),
		new KeyLabel(HIDKeycodes.SHIFT_LEFT, 	"shift", "lshift"),
		new KeyLabel(HIDKeycodes.GUI_LEFT, 		"gui", "win"),
		
		new KeyLabel(HIDKeycodes.ALT_RIGHT, 	"ralt", "altgr"),
		new KeyLabel(HIDKeycodes.CTRL_RIGHT, 	"rctrl", null),
		new KeyLabel(HIDKeycodes.SHIFT_RIGHT, 	"rshift", null),
		new KeyLabel(HIDKeycodes.GUI_RIGHT, 	"rgui", "rwin"),
	};
	
	private static final KeyLabel[] keyLUT = {
								
			new KeyLabel(HIDKeycodes.KEY_Q, 		"q", null),
			new KeyLabel(HIDKeycodes.KEY_W, 		"w", null),
			new KeyLabel(HIDKeycodes.KEY_E, 		"e", null),
			new KeyLabel(HIDKeycodes.KEY_R, 		"r", null),
			new KeyLabel(HIDKeycodes.KEY_T, 		"t", null),
			new KeyLabel(HIDKeycodes.KEY_Y, 		"y", null),
			new KeyLabel(HIDKeycodes.KEY_U, 		"u", null),
			new KeyLabel(HIDKeycodes.KEY_I, 		"i", null),
			new KeyLabel(HIDKeycodes.KEY_O, 		"o", null),
			new KeyLabel(HIDKeycodes.KEY_P, 		"p", null),
			new KeyLabel(HIDKeycodes.KEY_LEFT_BRACKET, 	"[", "{"),
			new KeyLabel(HIDKeycodes.KEY_RIGHT_BRACKET, "]", "}"),			
						
			new KeyLabel(HIDKeycodes.KEY_A, 		"a", null),
			new KeyLabel(HIDKeycodes.KEY_S, 		"s", null),
			new KeyLabel(HIDKeycodes.KEY_D, 		"d", null),
			new KeyLabel(HIDKeycodes.KEY_F, 		"f", null),
			new KeyLabel(HIDKeycodes.KEY_G, 		"g", null),
			new KeyLabel(HIDKeycodes.KEY_H, 		"h", null),
			new KeyLabel(HIDKeycodes.KEY_J, 		"j", null),
			new KeyLabel(HIDKeycodes.KEY_K, 		"k", null),
			new KeyLabel(HIDKeycodes.KEY_L, 		"l", null),
			new KeyLabel(HIDKeycodes.KEY_SEMICOLON, ";", ":"),
			new KeyLabel(HIDKeycodes.KEY_APOSTROPHE,"'", "\""),
			
			new KeyLabel(HIDKeycodes.KEY_Z, 		"z", null),
			new KeyLabel(HIDKeycodes.KEY_X, 		"x", null),
			new KeyLabel(HIDKeycodes.KEY_C, 		"c", null),
			new KeyLabel(HIDKeycodes.KEY_V, 		"v", null),
			new KeyLabel(HIDKeycodes.KEY_B, 		"b", null),
			new KeyLabel(HIDKeycodes.KEY_N, 		"n", null),
			new KeyLabel(HIDKeycodes.KEY_M, 		"m", null),
			new KeyLabel(HIDKeycodes.KEY_COMA, 		",", "<"),
			new KeyLabel(HIDKeycodes.KEY_DOT, 		".", ">"),
			new KeyLabel(HIDKeycodes.KEY_SLASH, 	"/", "?"),
			new KeyLabel(HIDKeycodes.KEY_BACKSLASH,	"\\", "|"),
			
			new KeyLabel(HIDKeycodes.KEY_GRAVE,		"`", "~"),
			new KeyLabel(HIDKeycodes.KEY_1, 		"1", "!"),
			new KeyLabel(HIDKeycodes.KEY_2, 		"2", "@"),
			new KeyLabel(HIDKeycodes.KEY_3, 		"3", "#"),
			new KeyLabel(HIDKeycodes.KEY_4, 		"4", "$"),
			new KeyLabel(HIDKeycodes.KEY_5, 		"5", "%"),
			new KeyLabel(HIDKeycodes.KEY_6, 		"6", "^"),
			new KeyLabel(HIDKeycodes.KEY_7, 		"7", "&"),
			new KeyLabel(HIDKeycodes.KEY_8, 		"8", "*"),
			new KeyLabel(HIDKeycodes.KEY_9, 		"9", "("),
			new KeyLabel(HIDKeycodes.KEY_0, 		"0", ")"),
			new KeyLabel(HIDKeycodes.KEY_MINUS, 	"-", "_"),
			new KeyLabel(HIDKeycodes.KEY_EQUALS, 	"=", "+"),
			
			new KeyLabel(HIDKeycodes.KEY_BACKSPACE, "backspace", null),
			new KeyLabel(HIDKeycodes.KEY_ENTER, "enter", null),
			new KeyLabel(HIDKeycodes.KEY_TAB,		"tab", null),
			new KeyLabel(HIDKeycodes.KEY_SPACEBAR, "space", null),
			new KeyLabel(HIDKeycodes.KEY_CAPS_LOCK, "capslock", "caps"),							
			new KeyLabel(HIDKeycodes.KEY_ESCAPE, "esc", "escape"),
			new KeyLabel(HIDKeycodes.KEY_APPLICATION, "application", "app"),
			
			new KeyLabel(HIDKeycodes.KEY_F1, "f1", null),
			new KeyLabel(HIDKeycodes.KEY_F2, "f2", null),
			new KeyLabel(HIDKeycodes.KEY_F3, "f3", null),
			new KeyLabel(HIDKeycodes.KEY_F4, "f4", null),
			new KeyLabel(HIDKeycodes.KEY_F5, "f5", null),
			new KeyLabel(HIDKeycodes.KEY_F6, "f6", null),
			new KeyLabel(HIDKeycodes.KEY_F7, "f7", null),
			new KeyLabel(HIDKeycodes.KEY_F8, "f8", null),
			new KeyLabel(HIDKeycodes.KEY_F9, "f9", null),
			new KeyLabel(HIDKeycodes.KEY_F10, "f10", null),
			new KeyLabel(HIDKeycodes.KEY_F11, "f11", null),
			new KeyLabel(HIDKeycodes.KEY_F12, "f12", null),
			
			new KeyLabel(HIDKeycodes.KEY_PRINT_SCREEN, "printscrn", "printscreen"),
			new KeyLabel(HIDKeycodes.KEY_SCROLL_LOCK, "scrolllock", "scroll"),
			new KeyLabel(HIDKeycodes.KEY_PASUE, "pause", "break"),
			
			new KeyLabel(HIDKeycodes.KEY_INSERT, "insert", "ins"),
			new KeyLabel(HIDKeycodes.KEY_HOME, "home", null),
			new KeyLabel(HIDKeycodes.KEY_PAGE_UP, "pageup", "pgup"),
			new KeyLabel(HIDKeycodes.KEY_DELETE, "delete", "del"),
			new KeyLabel(HIDKeycodes.KEY_END, "end", null),
			new KeyLabel(HIDKeycodes.KEY_PAGE_DOWN, "pagedown", "pgdn"),
			
			new KeyLabel(HIDKeycodes.KEY_ARROW_LEFT, "left", null),
			new KeyLabel(HIDKeycodes.KEY_ARROW_RIGHT, "right", null),
			new KeyLabel(HIDKeycodes.KEY_ARROW_UP, "up", null),
			new KeyLabel(HIDKeycodes.KEY_ARROW_DOWN, "down", null),

			new KeyLabel(HIDKeycodes.KEY_NUM_1, 	"num_1", "num_end"),
			new KeyLabel(HIDKeycodes.KEY_NUM_2, 	"num_2", "num_down"),
			new KeyLabel(HIDKeycodes.KEY_NUM_3, 	"num_3", "num_pagedown"),
			new KeyLabel(HIDKeycodes.KEY_NUM_4, 	"num_4", "num_left"),
			new KeyLabel(HIDKeycodes.KEY_NUM_5, 	"num_5", "num_center"),
			new KeyLabel(HIDKeycodes.KEY_NUM_6, 	"num_6", "num_right"),
			new KeyLabel(HIDKeycodes.KEY_NUM_7, 	"num_7", "num_home"),
			new KeyLabel(HIDKeycodes.KEY_NUM_8, 	"num_8", "num_up"),
			new KeyLabel(HIDKeycodes.KEY_NUM_9, 	"num_9", "num_pageup"),
			new KeyLabel(HIDKeycodes.KEY_NUM_0, 	"num_0", "num_insert"),			
			new KeyLabel(HIDKeycodes.KEY_NUM_ENTER, "num_enter", null),
			new KeyLabel(HIDKeycodes.KEY_NUM_DOT, 	"num_dot", "num_delete"),
			new KeyLabel(HIDKeycodes.KEY_NUM_PLUS, 	"num_3", null),
			new KeyLabel(HIDKeycodes.KEY_NUM_MINUS, "num_4", null),
			new KeyLabel(HIDKeycodes.KEY_NUM_STAR, 	"num_5", null),
			new KeyLabel(HIDKeycodes.KEY_NUM_SLASH, "num_6", null),
	};


	public static String[] getKeyList() {
		String[] result = new String[keyLUT.length];		
		for (int i = 0; i < keyLUT.length; i++) {
			result[i] = keyLUT[i].primary;
		}
		return result;
	}
	
	public static String getParam(String s) {
		if ((s != null) && (s.length() > 0)) {
			int index = s.indexOf("=");
			if (index > 0) {
				return s.substring(index + 1);
			}
		}
		return null;
	}
	
	//returns first found non-modifier key only!
	public static byte getKey(String param) {
		byte key = 0x00;
		String[] keys = prepareSearchArray(param);
		for (String s : keys) {
			if ((s != null) && (s.length() > 0)) {
				key = findKey(s);
				if (key != 0) {
					return key;
				}
			}
		}
		return key;
	}

	public static byte getModifiers(String param) {
		byte modifiers = 0x00;
		String[] keys = prepareSearchArray(param);
		for (String s : keys) {
			if ((s != null) && (s.length() > 0)) {
				modifiers |= findMod(s);
			}
		}
		return modifiers;
	}
	
	public static int getDelay(String s) {
		int delay = 0;
		try {
			delay = Integer.parseInt(s);
		} catch (Exception e) {	
			delay = 0;
		}
		return delay;
	}
	
	@SuppressLint("DefaultLocale")
	private static String[] prepareSearchArray(String param) {
		param = param.toLowerCase();
		param = param.replace(" ", ""); //remove spaces!
		param = param.replace("++", "+="); //handle special case!
		return param.split("\\+");
	}
	
	private static byte findMod(String str) {
		return searchLUT(str, modLUT);
	}
	
	private static byte findKey(String str) {
		return searchLUT(str, keyLUT);
	}
	
	private static byte searchLUT(String str, KeyLabel[] lut) {
		if (str != null) {
			for (KeyLabel l : lut) {
				if (l.primary != null) {
					if (str.equals(l.primary)) {
						return l.keyCode;
					}
				}
				if (l.secondary != null) {
					if (str.equals(l.secondary)) {
						return l.keyCode;
					}
				}
			}
		}
		return 0;
	}

}
