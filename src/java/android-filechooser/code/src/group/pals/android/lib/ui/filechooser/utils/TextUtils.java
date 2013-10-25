/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils;

import java.util.regex.Pattern;

/**
 * Text utilities.
 * 
 * @author Hai Bison
 * @since v4.3 beta
 */
public class TextUtils {

    /**
     * Quotes a text in double quotation mark.
     * 
     * @param s
     *            the text, if {@code null}, empty string will be used
     * @return the quoted text
     */
    public static String quote(String s) {
        return String.format("\"%s\"", s != null ? s : "");
    }// quote()

    /**
     * Compiles {@code regex}.
     * 
     * @param regex
     *            the regex.
     * @return a compiled {@link Pattern}, or {@code null} if there is an error
     *         while compiling.
     */
    public static Pattern compileRegex(String regex) {
        if (android.text.TextUtils.isEmpty(regex))
            return null;
        try {
            return Pattern.compile(regex);
        } catch (Throwable t) {
            return null;
        }
    }// compileRegex()

}
