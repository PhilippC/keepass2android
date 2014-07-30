//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * QueryParameters is a non-instantiable utility class that holds query parameter constants
 * used by the API service.
 */
final class QueryParameters {

    public static final String PRETTY = "pretty";
    public static final String CALLBACK = "callback";
    public static final String SUPPRESS_REDIRECTS = "suppress_redirects";
    public static final String SUPPRESS_RESPONSE_CODES = "suppress_response_codes";
    public static final String METHOD = "method";
    public static final String OVERWRITE = "overwrite";
    public static final String RETURN_SSL_RESOURCES = "return_ssl_resources";

    /** Private to present instantiation. */
    private QueryParameters() {
        throw new AssertionError(ErrorMessages.NON_INSTANTIABLE_CLASS);
    }
}
