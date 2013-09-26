/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.utils;

import group.pals.android.lib.ui.filechooser.BuildConfig;
import android.content.Context;
import android.content.pm.PackageManager;

/**
 * Utilities.
 */
public class Utils {

    /**
     * Checks if the app has <b>all</b> {@code permissions} granted.
     * 
     * @param context
     *            {@link Context}
     * @param permissions
     *            list of permission names.
     * @return {@code true} if the app has all {@code permissions} asked.
     */
    public static boolean hasPermissions(Context context, String... permissions) {
        for (String p : permissions)
            if (context.checkCallingOrSelfPermission(p) == PackageManager.PERMISSION_DENIED)
                return false;
        return true;
    }// hasPermissions()
    
    
    public static boolean doLog()
    {
    	return false;
    	//return BuildConfig.DEBUG; //not working with Mono for Android
    	
    }
}
