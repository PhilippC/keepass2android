package keepass2android.yubiclip.scancode;

import java.util.HashMap;
import java.util.Map;
import java.util.Set;
import java.util.TreeSet;

/**
 * Created by dain on 2/17/14.
 */
public abstract class KeyboardLayout {
    private static final Map<String, KeyboardLayout> layouts = new HashMap<String, KeyboardLayout>();

    static {
        layouts.put("US", new USKeyboardLayout());
    }

    public static KeyboardLayout forName(String name) {
        return layouts.get(name.toUpperCase());
    }

    public static Set<String> availableLayouts() {
        return new TreeSet<String>(layouts.keySet());
    }

    protected static final int SHIFT = 0x80;

    protected abstract String fromScanCode(int code);

    public final String fromScanCodes(byte[] bytes) {
        StringBuilder buf = new StringBuilder();
        for (byte b : bytes) {
            buf.append(fromScanCode(b & 0xff));
        }

        return buf.toString();
    }
}
