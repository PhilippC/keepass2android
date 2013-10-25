/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils;

/**
 * The converter.
 * 
 * @author Hai Bison
 * 
 */
public class Converter {

    /**
     * Converts {@code size} (in bytes) to string. This tip is from:
     * {@code http://stackoverflow.com/a/5599842/942821}.
     * 
     * @param size
     *            the size in bytes.
     * @return e.g.:
     *         <p/>
     *         <ul>
     *         <li>128 B</li>
     *         <li>1.5 KiB</li>
     *         <li>10 MiB</li>
     *         <li>...</li>
     *         </ul>
     */
    public static String sizeToStr(double size) {
        if (size <= 0)
            return "0 B";

        final String[] units = { "", "Ki", "Mi", "Gi", "Ti", "Pi", "Ei", "Zi",
                "Yi" };
        final short blockSize = 1024;

        int digitGroups = (int) (Math.log10(size) / Math.log10(blockSize));
        if (digitGroups >= units.length)
            digitGroups = units.length - 1;
        size = size / Math.pow(blockSize, digitGroups);

        return String.format(
                String.format("%s %%sB", digitGroups == 0 ? "%,.0f" : "%,.2f"),
                size, units[digitGroups]);
    }// sizeToStr()

}
