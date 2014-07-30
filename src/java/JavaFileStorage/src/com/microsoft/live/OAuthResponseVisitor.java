//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * OAuthResponseVisitor is used to visit various OAuthResponse.
 */
interface OAuthResponseVisitor {

    /**
     * Called when an OAuthSuccessfulResponse is visited.
     *
     * @param response being visited
     */
    public void visit(OAuthSuccessfulResponse response);

    /**
     * Called when an OAuthErrorResponse is being visited.
     *
     * @param response being visited
     */
    public void visit(OAuthErrorResponse response);
}
