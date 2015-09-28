//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

/**
 * OAuth is a non-instantiable utility class that contains types and constants
 * for the OAuth protocol.
 *
 * See the <a href="http://tools.ietf.org/html/draft-ietf-oauth-v2-22">OAuth 2.0 spec</a>
 * for more information.
 */
final class OAuth {

    public enum DisplayType {
        ANDROID_PHONE,
        ANDROID_TABLET
    }

    public enum ErrorType {
        /**
         * Client authentication failed (e.g. unknown client, no
         * client authentication included, or unsupported
         * authentication method).  The authorization server MAY
         * return an HTTP 401 (Unauthorized) status code to indicate
         * which HTTP authentication schemes are supported.  If the
         * client attempted to authenticate via the "Authorization"
         * request header field, the authorization server MUST
         * respond with an HTTP 401 (Unauthorized) status code, and
         * include the "WWW-Authenticate" response header field
         * matching the authentication scheme used by the client.
         */
        INVALID_CLIENT,

        /**
         * The provided authorization grant (e.g. authorization
         * code, resource owner credentials, client credentials) is
         * invalid, expired, revoked, does not match the redirection
         * URI used in the authorization request, or was issued to
         * another client.
         */
        INVALID_GRANT,

        /**
         * The request is missing a required parameter, includes an
         * unsupported parameter value, repeats a parameter,
         * includes multiple credentials, utilizes more than one
         * mechanism for authenticating the client, or is otherwise
         * malformed.
         */
        INVALID_REQUEST,

        /**
         * The requested scope is invalid, unknown, malformed, or
         * exceeds the scope granted by the resource owner.
         */
        INVALID_SCOPE,

        /**
         * The authenticated client is not authorized to use this
         * authorization grant type.
         */
        UNAUTHORIZED_CLIENT,

        /**
         * The authorization grant type is not supported by the
         * authorization server.
         */
        UNSUPPORTED_GRANT_TYPE;
    }

    public enum GrantType {
        AUTHORIZATION_CODE,
        CLIENT_CREDENTIALS,
        PASSWORD,
        REFRESH_TOKEN;
    }

    public enum ResponseType {
        CODE,
        TOKEN;
    }

    public enum TokenType {
        BEARER
    }

    /**
     * Key for the access_token parameter.
     *
     * See <a href="http://tools.ietf.org/html/draft-ietf-oauth-v2-22#section-5.1">Section 5.1</a>
     * of the OAuth 2.0 spec for more information.
     */
    public static final String ACCESS_TOKEN = "access_token";

    /** The app's authentication token. */
    public static final String AUTHENTICATION_TOKEN = "authentication_token";

    /** The app's client ID. */
    public static final String CLIENT_ID = "client_id";

    /** Equivalent to the profile that is described in the OAuth 2.0 protocol spec. */
    public static final String CODE = "code";

    /**
     * The display type to be used for the authorization page. Valid values are
     * "popup", "touch", "page", or "none".
     */
    public static final String DISPLAY = "display";

    /**
     * Key for the error parameter.
     *
     * error can have the following values:
     * invalid_request, unauthorized_client, access_denied, unsupported_response_type,
     * invalid_scope, server_error, or temporarily_unavailable.
     */
    public static final String ERROR = "error";

    /**
     * Key for the error_description parameter. error_description is described below.
     *
     * OPTIONAL.  A human-readable UTF-8 encoded text providing
     * additional information, used to assist the client developer in
     * understanding the error that occurred.
     */
    public static final String ERROR_DESCRIPTION = "error_description";

    /**
     * Key for the error_uri parameter. error_uri is described below.
     *
     * OPTIONAL.  A URI identifying a human-readable web page with
     * information about the error, used to provide the client
     * developer with additional information about the error.
     */
    public static final String ERROR_URI = "error_uri";

    /**
     * Key for the expires_in parameter. expires_in is described below.
     *
     * OPTIONAL.  The lifetime in seconds of the access token.  For
     * example, the value "3600" denotes that the access token will
     * expire in one hour from the time the response was generated.
     */
    public static final String EXPIRES_IN = "expires_in";

    /**
     * Key for the grant_type parameter. grant_type is described below.
     *
     * grant_type is used in a token request. It can take on the following
     * values: authorization_code, password, client_credentials, or refresh_token.
     */
    public static final String GRANT_TYPE = "grant_type";

    /**
     * Optional. A market string that determines how the consent user interface
     * (UI) is localized. If the value of this parameter is missing or is not
     * valid, a market value is determined by using an internal algorithm.
     */
    public static final String LOCALE = "locale";

    /**
     * Key for the redirect_uri parameter.
     *
     * See <a href="http://tools.ietf.org/html/draft-ietf-oauth-v2-22#section-3.1.2">Section 3.1.2</a>
     * of the OAuth 2.0 spec for more information.
     */
    public static final String REDIRECT_URI = "redirect_uri";

    /**
     * Key used for the refresh_token parameter.
     *
     * See <a href="http://tools.ietf.org/html/draft-ietf-oauth-v2-22#section-5.1">Section 5.1</a>
     * of the OAuth 2.0 spec for more information.
     */
    public static final String REFRESH_TOKEN = "refresh_token";

    /**
     * The type of data to be returned in the response from the authorization
     * server. Valid values are "code" or "token".
     */
    public static final String RESPONSE_TYPE = "response_type";

    /**
     * Equivalent to the scope parameter that is described in the OAuth 2.0
     * protocol spec.
     */
    public static final String SCOPE = "scope";

    /** Delimiter for the scopes field response. */
    public static final String SCOPE_DELIMITER = " ";

    /**
     * Equivalent to the state parameter that is described in the OAuth 2.0
     * protocol spec.
     */
    public static final String STATE = "state";

    public static final String THEME = "theme";

    /**
     * Key used for the token_type parameter.
     *
     * See <a href="http://tools.ietf.org/html/draft-ietf-oauth-v2-22#section-5.1">Section 5.1</a>
     * of the OAuth 2.0 spec for more information.
     */
    public static final String TOKEN_TYPE = "token_type";

    /** Private to prevent instantiation */
    private OAuth() { throw new AssertionError(ErrorMessages.NON_INSTANTIABLE_CLASS); }
}
