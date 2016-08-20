/*
 *    Copyright (c) 2012 Hai Bison
 *
 *    See the file LICENSE at the root directory of this project for copying
 *    permission.
 */

package group.pals.android.lib.ui.filechooser.providers;

import android.content.ContentResolver;
import android.net.Uri;

/**
 * Utilities for providers.
 * 
 * @author Hai Bison
 * @since v5.1 beta
 */
public class ProviderUtils {

    /**
     * The scheme part for default provider's URI.
     */
    public static final String SCHEME = ContentResolver.SCHEME_CONTENT + "://";

    /**
     * Gets integer parameter.
     * 
     * @param uri
     *            the original URI.
     * @param key
     *            the key of query parameter.
     * @param defaultValue
     *            will be returned if nothing found or parsing value failed.
     * @return the integer value.
     */
    public static int getIntQueryParam(Uri uri, String key, int defaultValue) {
        try {
            return Integer.parseInt(uri.getQueryParameter(key));
        } catch (NumberFormatException e) {
            return defaultValue;
        }
    }// getIntQueryParam()

    /**
     * Gets long parameter.
     * 
     * @param uri
     *            the original URI.
     * @param key
     *            the key of query parameter.
     * @param defaultValue
     *            will be returned if nothing found or parsing value failed.
     * @return the long value.
     */
    public static long getLongQueryParam(Uri uri, String key, long defaultValue) {
        try {
            return Long.parseLong(uri.getQueryParameter(key));
        } catch (NumberFormatException e) {
            return defaultValue;
        }
    }// getLongQueryParam()

    /**
     * Gets boolean parameter.
     * 
     * @param uri
     *            the original URI.
     * @param key
     *            the key of query parameter.
     * @return {@code false} if the parameter does not exist, or it is either
     *         {@code "false"} or {@code "0"}. {@code true} otherwise.
     */
    public static boolean getBooleanQueryParam(Uri uri, String key) {
        String param = uri.getQueryParameter(key);
        if (param == null || Boolean.FALSE.toString().equalsIgnoreCase(param)
                || Integer.toString(0).equalsIgnoreCase(param))
            return false;
        return true;
    }// getBooleanQueryParam()

    /**
     * Gets boolean parameter.
     * 
     * @param uri
     *            the original URI.
     * @param key
     *            the key of query parameter.
     * @param defaultValue
     *            the default value if the parameter does not exist.
     * @return {@code defaultValue} if the parameter does not exist, or it is
     *         either {@code "false"} or {@code "0"}. {@code true} otherwise.
     */
    public static boolean getBooleanQueryParam(Uri uri, String key,
            boolean defaultValue) {
        String param = uri.getQueryParameter(key);
        if (param == null)
            return defaultValue;
        if (param.matches("(?i)false|(0+)"))
            return false;
        return true;
    }// getBooleanQueryParam()

}
