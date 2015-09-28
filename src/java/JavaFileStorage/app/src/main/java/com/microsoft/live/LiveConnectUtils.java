//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;


import android.text.TextUtils;

/**
 * LiveConnectUtils is a non-instantiable utility class that contains various helper
 * methods and constants.
 */
final class LiveConnectUtils {

    /**
     * Checks to see if the passed in Object is null, and throws a
     * NullPointerException if it is.
     *
     * @param object to check
     * @param parameterName name of the parameter that is used in the exception message
     * @throws NullPointerException if the Object is null
     */
    public static void assertNotNull(Object object, String parameterName) {
        assert !TextUtils.isEmpty(parameterName);

        if (object == null) {
            final String message = String.format(ErrorMessages.NULL_PARAMETER, parameterName);
            throw new NullPointerException(message);
        }
    }

    /**
     * Checks to see if the passed in is an empty string, and throws an
     * IllegalArgumentException if it is.
     *
     * @param parameter to check
     * @param parameterName name of the parameter that is used in the exception message
     * @throws IllegalArgumentException if the parameter is empty
     * @throws NullPointerException if the String is null
     */
    public static void assertNotNullOrEmpty(String parameter, String parameterName) {
        assert !TextUtils.isEmpty(parameterName);

        assertNotNull(parameter, parameterName);

        if (TextUtils.isEmpty(parameter)) {
            final String message = String.format(ErrorMessages.EMPTY_PARAMETER, parameterName);
            throw new IllegalArgumentException(message);
        }
    }

    /**
     * Private to prevent instantiation
     */
    private LiveConnectUtils() { throw new AssertionError(ErrorMessages.NON_INSTANTIABLE_CLASS); }
}
