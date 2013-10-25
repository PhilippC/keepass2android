/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers.localfile;

import android.content.Context;

/**
 * Contract for local file.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class LocalFileContract {

    /**
     * The raw authority of this provider.
     */
    private static final String AUTHORITY = "android-filechooser.localfile";

    /**
     * Gets the authority of this provider.
     * 
     * @param context
     *            the context.
     * @return the authority.
     */
    public static final String getAuthority(Context context) {
        return context.getPackageName() + "." + AUTHORITY;
    }// getAuthority()

    /**
     * The unique ID of this provider.
     */
    public static final String _ID = "7dab9818-0a8b-47ef-88cc-10fe538bfaf7";

}
