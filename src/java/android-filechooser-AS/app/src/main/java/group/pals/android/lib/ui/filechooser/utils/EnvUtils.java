/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils;

/**
 * Environment utilities :-)
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class EnvUtils {

    /**
     * The starting ID. This is used to calculate next unique ID in a session.
     */
    private static int mId = 0;

    /**
     * Generates a unique ID (in a working session).
     * 
     * @return the UID.
     */
    public static final int genId() {
        return mId++;
    }// genId()

}
