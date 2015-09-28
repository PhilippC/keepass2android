//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import java.util.List;

import org.apache.http.NameValuePair;
import org.apache.http.client.HttpClient;
import org.apache.http.message.BasicNameValuePair;

import android.text.TextUtils;

import com.microsoft.live.OAuth.GrantType;

/**
 * AccessTokenRequest represents a request for an Access Token.
 * It subclasses the abstract class TokenRequest, which does most of the work.
 * This class adds the proper parameters for the access token request via the
 * constructBody() hook.
 */
class AccessTokenRequest extends TokenRequest {

    /**
     * REQUIRED.  The authorization code received from the
     * authorization server.
     */
    private final String code;

    /** REQUIRED.  Value MUST be set to "authorization_code". */
    private final GrantType grantType;

    /**
     * REQUIRED, if the "redirect_uri" parameter was included in the
     * authorization request as described in Section 4.1.1, and their
     * values MUST be identical.
     */
    private final String redirectUri;

    /**
     * Constructs a new AccessTokenRequest, and initializes its member variables
     *
     * @param client the HttpClient to make HTTP requests on
     * @param clientId the client_id of the calling application
     * @param redirectUri the redirect_uri to be called back
     * @param code the authorization code received from the AuthorizationRequest
     */
    public AccessTokenRequest(HttpClient client,
                              String clientId,
                              String redirectUri,
                              String code) {
        super(client, clientId);

        assert !TextUtils.isEmpty(redirectUri);
        assert !TextUtils.isEmpty(code);

        this.redirectUri = redirectUri;
        this.code = code;
        this.grantType = GrantType.AUTHORIZATION_CODE;
    }

    /**
     * Adds the "code", "redirect_uri", and "grant_type" parameters to the body.
     *
     * @param body the list of NameValuePairs to be placed in the body of the HTTP request
     */
    @Override
    protected void constructBody(List<NameValuePair> body) {
        body.add(new BasicNameValuePair(OAuth.CODE, this.code));
        body.add(new BasicNameValuePair(OAuth.REDIRECT_URI, this.redirectUri));
        body.add(new BasicNameValuePair(OAuth.GRANT_TYPE,
                                        this.grantType.toString().toLowerCase()));
    }
}
