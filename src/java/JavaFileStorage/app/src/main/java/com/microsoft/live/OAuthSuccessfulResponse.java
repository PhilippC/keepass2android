//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import java.util.Map;

import org.json.JSONException;
import org.json.JSONObject;

import android.text.TextUtils;

import com.microsoft.live.OAuth.TokenType;

/**
 * OAuthSuccessfulResponse represents a successful response form an OAuth server.
 */
class OAuthSuccessfulResponse implements OAuthResponse {

    /**
     * Builder is a utility class that is used to build a new OAuthSuccessfulResponse.
     * It must be constructed with the required fields, and can add on the optional ones.
     */
    public static class Builder {
        private final String accessToken;
        private String authenticationToken;
        private int expiresIn = UNINITIALIZED;
        private String refreshToken;
        private String scope;
        private final TokenType tokenType;

        public Builder(String accessToken, TokenType tokenType) {
            assert accessToken != null;
            assert !TextUtils.isEmpty(accessToken);
            assert tokenType != null;

            this.accessToken = accessToken;
            this.tokenType = tokenType;
        }

        public Builder authenticationToken(String authenticationToken) {
            this.authenticationToken = authenticationToken;
            return this;
        }

        /**
         * @return a new instance of an OAuthSuccessfulResponse with the given
         *         parameters passed into the builder.
         */
        public OAuthSuccessfulResponse build() {
            return new OAuthSuccessfulResponse(this);
        }

        public Builder expiresIn(int expiresIn) {
            this.expiresIn = expiresIn;
            return this;
        }

        public Builder refreshToken(String refreshToken) {
            this.refreshToken = refreshToken;
            return this;
        }

        public Builder scope(String scope) {
            this.scope = scope;
            return this;
        }
    }

    /** Used to declare expiresIn uninitialized */
    private static final int UNINITIALIZED = -1;

    public static OAuthSuccessfulResponse createFromFragment(
            Map<String, String> fragmentParameters) throws LiveAuthException {
        String accessToken = fragmentParameters.get(OAuth.ACCESS_TOKEN);
        String tokenTypeString = fragmentParameters.get(OAuth.TOKEN_TYPE);

        // must have accessToken and tokenTypeString to be a valid OAuthSuccessfulResponse
        assert accessToken != null;
        assert tokenTypeString != null;

        TokenType tokenType;
        try {
            tokenType = TokenType.valueOf(tokenTypeString.toUpperCase());
        } catch (IllegalArgumentException e) {
            throw new LiveAuthException(ErrorMessages.SERVER_ERROR, e);
        }

        OAuthSuccessfulResponse.Builder builder =
                new OAuthSuccessfulResponse.Builder(accessToken, tokenType);

        String authenticationToken = fragmentParameters.get(OAuth.AUTHENTICATION_TOKEN);
        if (authenticationToken != null) {
            builder.authenticationToken(authenticationToken);
        }

        String expiresInString = fragmentParameters.get(OAuth.EXPIRES_IN);
        if (expiresInString != null) {
            final int expiresIn;
            try {
                expiresIn = Integer.parseInt(expiresInString);
            } catch (final NumberFormatException e) {
                throw new LiveAuthException(ErrorMessages.SERVER_ERROR, e);
            }

            builder.expiresIn(expiresIn);
        }

        String scope = fragmentParameters.get(OAuth.SCOPE);
        if (scope != null) {
            builder.scope(scope);
        }

        return builder.build();
    }

    /**
     * Static constructor used to create a new OAuthSuccessfulResponse from an
     * OAuth server's JSON response.
     *
     * @param response from an OAuth server that is used to create the object.
     * @return a new instance of OAuthSuccessfulResponse that is created from the given JSONObject
     * @throws LiveAuthException if there is a JSONException or the token_type is unknown.
     */
    public static OAuthSuccessfulResponse createFromJson(JSONObject response)
            throws LiveAuthException {
        assert validOAuthSuccessfulResponse(response);

        final String accessToken;
        try {
            accessToken = response.getString(OAuth.ACCESS_TOKEN);
        } catch (final JSONException e) {
            throw new LiveAuthException(ErrorMessages.SERVER_ERROR, e);
        }

        final String tokenTypeString;
        try {
            tokenTypeString = response.getString(OAuth.TOKEN_TYPE);
        } catch (final JSONException e) {
            throw new LiveAuthException(ErrorMessages.SERVER_ERROR, e);
        }

        final TokenType tokenType;
        try {
            tokenType = TokenType.valueOf(tokenTypeString.toUpperCase());
        } catch (final IllegalArgumentException e) {
            throw new LiveAuthException(ErrorMessages.SERVER_ERROR, e);
        } catch (final NullPointerException e) {
            throw new LiveAuthException(ErrorMessages.SERVER_ERROR, e);
        }

        final Builder builder = new Builder(accessToken, tokenType);

        if (response.has(OAuth.AUTHENTICATION_TOKEN)) {
            final String authenticationToken;
            try {
                authenticationToken = response.getString(OAuth.AUTHENTICATION_TOKEN);
            } catch (final JSONException e) {
                throw new LiveAuthException(ErrorMessages.CLIENT_ERROR, e);
            }
            builder.authenticationToken(authenticationToken);
        }

        if (response.has(OAuth.REFRESH_TOKEN)) {
            final String refreshToken;
            try {
                refreshToken = response.getString(OAuth.REFRESH_TOKEN);
            } catch (final JSONException e) {
                throw new LiveAuthException(ErrorMessages.CLIENT_ERROR, e);
            }
            builder.refreshToken(refreshToken);
        }

        if (response.has(OAuth.EXPIRES_IN)) {
            final int expiresIn;
            try {
                expiresIn = response.getInt(OAuth.EXPIRES_IN);
            } catch (final JSONException e) {
                throw new LiveAuthException(ErrorMessages.CLIENT_ERROR, e);
            }
            builder.expiresIn(expiresIn);
        }

        if (response.has(OAuth.SCOPE)) {
            final String scope;
            try {
                scope = response.getString(OAuth.SCOPE);
            } catch (final JSONException e) {
                throw new LiveAuthException(ErrorMessages.CLIENT_ERROR, e);
            }
            builder.scope(scope);
        }

        return builder.build();
    }

    /**
     * @param response
     * @return true if the given JSONObject has the required fields to construct an
     *         OAuthSuccessfulResponse (i.e., has access_token and token_type)
     */
    public static boolean validOAuthSuccessfulResponse(JSONObject response) {
        return response.has(OAuth.ACCESS_TOKEN) &&
               response.has(OAuth.TOKEN_TYPE);
    }

    /** REQUIRED. The access token issued by the authorization server. */
    private final String accessToken;

    private final String authenticationToken;

    /**
     * OPTIONAL.  The lifetime in seconds of the access token.  For
     * example, the value "3600" denotes that the access token will
     * expire in one hour from the time the response was generated.
     */
    private final int expiresIn;

    /**
     * OPTIONAL.  The refresh token which can be used to obtain new
     * access tokens using the same authorization grant.
     */
    private final String refreshToken;

    /** OPTIONAL. */
    private final String scope;

    /** REQUIRED. */
    private final TokenType tokenType;

    /**
     * Private constructor to enforce the user of the builder.
     * @param builder to use to construct the object from.
     */
    private OAuthSuccessfulResponse(Builder builder) {
        this.accessToken = builder.accessToken;
        this.authenticationToken = builder.authenticationToken;
        this.tokenType = builder.tokenType;
        this.refreshToken = builder.refreshToken;
        this.expiresIn = builder.expiresIn;
        this.scope = builder.scope;
    }

    @Override
    public void accept(OAuthResponseVisitor visitor) {
        visitor.visit(this);
    }

    public String getAccessToken() {
        return this.accessToken;
    }

    public String getAuthenticationToken() {
        return this.authenticationToken;
    }

    public int getExpiresIn() {
        return this.expiresIn;
    }

    public String getRefreshToken() {
        return this.refreshToken;
    }

    public String getScope() {
        return this.scope;
    }

    public TokenType getTokenType() {
        return this.tokenType;
    }

    public boolean hasAuthenticationToken() {
        return this.authenticationToken != null && !TextUtils.isEmpty(this.authenticationToken);
    }

    public boolean hasExpiresIn() {
        return this.expiresIn != UNINITIALIZED;
    }

    public boolean hasRefreshToken() {
        return this.refreshToken != null && !TextUtils.isEmpty(this.refreshToken);
    }

    public boolean hasScope() {
        return this.scope != null && !TextUtils.isEmpty(this.scope);
    }

    @Override
    public String toString() {
        return String.format("OAuthSuccessfulResponse [accessToken=%s, authenticationToken=%s, tokenType=%s, refreshToken=%s, expiresIn=%s, scope=%s]",
                             this.accessToken,
                             this.authenticationToken,
                             this.tokenType,
                             this.refreshToken,
                             this.expiresIn,
                             this.scope);
    }
}
