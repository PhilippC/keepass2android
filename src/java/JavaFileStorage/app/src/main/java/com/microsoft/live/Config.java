//------------------------------------------------------------------------------
// Copyright (c) 2012 Microsoft Corporation. All rights reserved.
//
// Description: See the class level JavaDoc comments.
//------------------------------------------------------------------------------

package com.microsoft.live;

import android.net.Uri;
import android.text.TextUtils;

/**
 * Config is a singleton class that contains the values used throughout the SDK.
 */
enum Config {
    INSTANCE;

    private Uri apiUri;
    private String apiVersion;
    private Uri oAuthAuthorizeUri;
    private Uri oAuthDesktopUri;
    private Uri oAuthLogoutUri;
    private Uri oAuthTokenUri;

    Config() {
        // initialize default values for constants
        apiUri = Uri.parse("https://apis.live.net/v5.0");
        apiVersion = "5.0";
        oAuthAuthorizeUri = Uri.parse("https://login.live.com/oauth20_authorize.srf");
        oAuthDesktopUri = Uri.parse("https://login.live.com/oauth20_desktop.srf");
        oAuthLogoutUri = Uri.parse("https://login.live.com/oauth20_logout.srf");
        oAuthTokenUri = Uri.parse("https://login.live.com/oauth20_token.srf");
    }

    public Uri getApiUri() {
        return apiUri;
    }

    public String getApiVersion() {
        return apiVersion;
    }

    public Uri getOAuthAuthorizeUri() {
        return oAuthAuthorizeUri;
    }

    public Uri getOAuthDesktopUri() {
        return oAuthDesktopUri;
    }

    public Uri getOAuthLogoutUri() {
        return oAuthLogoutUri;
    }

    public Uri getOAuthTokenUri() {
        return oAuthTokenUri;
    }

    public void setApiUri(Uri apiUri) {
        assert apiUri != null;
        this.apiUri = apiUri;
    }

    public void setApiVersion(String apiVersion) {
        assert !TextUtils.isEmpty(apiVersion);
        this.apiVersion = apiVersion;
    }

    public void setOAuthAuthorizeUri(Uri oAuthAuthorizeUri) {
        assert oAuthAuthorizeUri != null;
        this.oAuthAuthorizeUri = oAuthAuthorizeUri;
    }

    public void setOAuthDesktopUri(Uri oAuthDesktopUri) {
        assert oAuthDesktopUri != null;
        this.oAuthDesktopUri = oAuthDesktopUri;
    }

    public void setOAuthLogoutUri(Uri oAuthLogoutUri) {
        assert oAuthLogoutUri != null;

        this.oAuthLogoutUri = oAuthLogoutUri;
    }

    public void setOAuthTokenUri(Uri oAuthTokenUri) {
        assert oAuthTokenUri != null;
        this.oAuthTokenUri = oAuthTokenUri;
    }
}
