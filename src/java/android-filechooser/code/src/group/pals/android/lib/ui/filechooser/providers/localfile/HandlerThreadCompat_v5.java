/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers.localfile;

import android.os.HandlerThread;

/**
 * Helper class for backward compatibility of {@link HandlerThread} from API 5+.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class HandlerThreadCompat_v5 {

    /**
     * Wrapper for {@link HandlerThread#quit()}.
     * 
     * @param thread
     *            the handler thread.
     */
    public static void quit(HandlerThread thread) {
        thread.quit();
    }// quit()

}
