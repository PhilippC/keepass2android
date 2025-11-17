/*
 * This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
 *
 *   Keepass2Android is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   Keepass2Android is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
 */

package keepass2android.javafilestorage.webdav;

import java.security.cert.CertificateException;
import java.security.cert.X509Certificate;

import javax.net.ssl.X509TrustManager;

import keepass2android.javafilestorage.ICertificateErrorHandler;

/**
 * Created by Philipp on 22.11.2016.
 */
public class DecoratedTrustManager implements X509TrustManager {
    private final X509TrustManager mTrustManager;
    private final ICertificateErrorHandler mCertificateErrorHandler;

    public DecoratedTrustManager(X509TrustManager trustManager, ICertificateErrorHandler certificateErrorHandler) {
        mTrustManager = trustManager;
        this.mCertificateErrorHandler = certificateErrorHandler;
    }

    @Override
    public void checkClientTrusted(X509Certificate[] x509Certificates, String s) throws CertificateException {
        try
        {
            mTrustManager.checkClientTrusted(x509Certificates,s);
        }
        catch (CertificateException e)
        {
            if ((mCertificateErrorHandler == null) || (!mCertificateErrorHandler.onValidationError(getMessage(e))))
                throw e;
        }

    }

    @Override
    public void checkServerTrusted(X509Certificate[] x509Certificates, String s) throws CertificateException {
        try
        {
            mTrustManager.checkServerTrusted(x509Certificates,s);
        }
        catch (CertificateException e)
        {
            if ((mCertificateErrorHandler == null) || (!mCertificateErrorHandler.onValidationError(getMessage(e))))
                throw e;
        }

    }

    private String getMessage(CertificateException e) {
            String msg = e.getLocalizedMessage();
    if (msg == null)
    msg = e.getMessage();
    if (msg == null)
    msg = e.toString();
    return msg;
    }

    @Override
    public X509Certificate[] getAcceptedIssuers() {
        return mTrustManager.getAcceptedIssuers();
    }
}
