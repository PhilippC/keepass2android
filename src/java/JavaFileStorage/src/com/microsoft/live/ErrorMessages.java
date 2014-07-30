//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * ErrorMessages is a non-instantiable class that contains all the String constants
 * used in for errors and exceptions.
 */
final class ErrorMessages {
    public static final String ABSOLUTE_PARAMETER =
            "Input parameter '%1$s' is invalid. '%1$s' cannot be absolute.";
    public static final String CLIENT_ERROR =
            "An error occured on the client during the operation.";
    public static final String EMPTY_PARAMETER =
            "Input parameter '%1$s' is invalid. '%1$s' cannot be empty.";
    public static final String INVALID_URI =
            "Input parameter '%1$s' is invalid. '%1$s' must be a valid URI.";
    public static final String LOGGED_OUT = "The user has is logged out.";
    public static final String LOGIN_IN_PROGRESS =
            "Another login operation is already in progress.";
    public static final String MISSING_UPLOAD_LOCATION =
            "The provided path does not contain an upload_location.";
    public static final String NON_INSTANTIABLE_CLASS = "Non-instantiable class";
    public static final String NULL_PARAMETER =
            "Input parameter '%1$s' is invalid. '%1$s' cannot be null.";
    public static final String SERVER_ERROR =
            "An error occured while communicating with the server during the operation. " +
            "Please try again later.";
    public static final String SIGNIN_CANCEL = "The user cancelled the login operation.";

    private ErrorMessages() { throw new AssertionError(NON_INSTANTIABLE_CLASS); }
}
