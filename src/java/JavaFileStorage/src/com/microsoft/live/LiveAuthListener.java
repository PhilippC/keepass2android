//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * Handles callback methods for LiveAuthClient init, login, and logout methods.
 * Returns the * status of the operation when onAuthComplete is called. If there was an error
 * during the operation, onAuthError is called with the exception that was thrown.
 */
public interface LiveAuthListener {

    /**
     * Invoked when the operation completes successfully.
     *
     * @param status The {@link LiveStatus} for an operation. If successful, the status is
     *               CONNECTED. If unsuccessful, NOT_CONNECTED or UNKNOWN are returned.
     * @param session The {@link LiveConnectSession} from the {@link LiveAuthClient}.
     * @param userState An arbitrary object that is used to determine the caller of the method.
     */
    public void onAuthComplete(LiveStatus status, LiveConnectSession session, Object userState);

    /**
     * Invoked when the method call fails.
     *
     * @param exception The {@link LiveAuthException} error.
     * @param userState An arbitrary object that is used to determine the caller of the method.
     */
    public void onAuthError(LiveAuthException exception, Object userState);
}
