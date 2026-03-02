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

/**
 * Created by dain on 2/17/14.
 */
public class USKeyboardLayout extends KeyboardLayout {
    private static final String[] usb2key1 = new String[]{
            "",
            "",
            "",
            "",
            "a",
            "b",
            "c",
            "d",
            "e",
            "f",
            "g", /* 0xa */
            "h",
            "i",
            "j",
            "k",
            "l",
            "m",
            "n",
            "o",
            "p",
            "q", /* 0x14 */
            "r",
            "s",
            "t",
            "u",
            "v",
            "w",
            "x",
            "y",
            "z",
            "1", /* 0x1e */
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "0",
            "\n", /* 0x28 */
            "",
            "",
            "\t",
            " ",
            "-",
            "=",
            "[",
            "]",
            "",
            "\\",
            ";",
            "'",
            "`",
            ",",
            ".",
            "/", /* 0x38 */
    };
    private static final String[] usb2key2 = new String[]{
            "",
            "",
            "",
            "",
            "A",
            "B",
            "C",
            "D",
            "E",
            "F",
            "G", /* 0x8a */
            "H",
            "I",
            "J",
            "K",
            "L",
            "M",
            "N",
            "O",
            "P",
            "Q", /* 0x94 */
            "R",
            "S",
            "T",
            "U",
            "V",
            "W",
            "X",
            "Y",
            "Z",
            "!",
            "@",
            "#",
            "$",
            "%",
            "^",
            "&",
            "*",
            "(",
            ")",
            "",
            "",
            "",
            "",
            "",
            "_",
            "+",
            "{",
            "}",
            "",
            "|",
            ":",
            "\"",
            "~",
            "<",
            ">",
            "?",
    };

    @Override
    protected String fromScanCode(int code) {
        if (code < SHIFT) {
            if (code < usb2key1.length) {
                return usb2key1[code];
            }
        } else {
            code = code ^ SHIFT;
            if (code < usb2key2.length) {
                return usb2key2[code];
            }
        }

        return "";
    }
}
