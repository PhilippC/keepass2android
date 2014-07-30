//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * Indicates that an exception occurred during the Auth process.
 */
public class LiveAuthException extends Exception {

    private static final long serialVersionUID = 3368677530670470856L;

    private final String error;
    private final String errorUri;


    LiveAuthException(String errorMessage) {
        super(errorMessage);
        this.error = "";
        this.errorUri = "";
    }

    LiveAuthException(String errorMessage, Throwable throwable) {
        super(errorMessage, throwable);
        this.error = "";
        this.errorUri = "";
    }

    LiveAuthException(String error, String errorDescription, String errorUri) {
        super(errorDescription);

        assert error != null;

        this.error = error;
        this.errorUri = errorUri;
    }

    LiveAuthException(String error, String errorDescription, String errorUri, Throwable cause) {
        super(errorDescription, cause);

        assert error != null;

        this.error = error;
        this.errorUri = errorUri;
    }

    /**
     * @return Returns the authentication error.
     */
    public String getError() {
        return this.error;
    }

    /**
     * @return Returns the error URI.
     */
    public String getErrorUri() {
        return this.errorUri;
    }
}
