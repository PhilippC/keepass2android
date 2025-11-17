/*
 * This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
 *
 *   Keepass2Android is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   Keepass2Android is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
 */

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
