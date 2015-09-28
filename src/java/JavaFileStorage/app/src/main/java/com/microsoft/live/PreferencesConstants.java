//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * Static class that holds constants used by an application's preferences.
 */
final class PreferencesConstants {
    public static final String COOKIES_KEY = "cookies";

    /** Name of the preference file */
    public static final String FILE_NAME = "com.microsoft.live";

    public static final String REFRESH_TOKEN_KEY = "refresh_token";
    public static final String COOKIE_DELIMITER = ",";

    private PreferencesConstants() { throw new AssertionError(); }
}
