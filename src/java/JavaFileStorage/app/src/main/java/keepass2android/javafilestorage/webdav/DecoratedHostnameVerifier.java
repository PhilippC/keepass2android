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

import javax.net.ssl.HostnameVerifier;
import javax.net.ssl.SSLSession;

import keepass2android.javafilestorage.ICertificateErrorHandler;
import okhttp3.internal.tls.OkHostnameVerifier;

/**
 * Created by Philipp on 27.01.2017.
 */
public class DecoratedHostnameVerifier implements HostnameVerifier
{

        public DecoratedHostnameVerifier(HostnameVerifier hostnameVerifier, ICertificateErrorHandler mCertificateErrorHandler) {
            this.hostnameVerifier = hostnameVerifier;
            certificateErrorHandler = mCertificateErrorHandler;
        }

        @Override
        public boolean verify(String host, SSLSession sslSession) {

            boolean baseResult = hostnameVerifier.verify(host, sslSession);
            if (baseResult)
                return true; //verification ok


            if ((certificateErrorHandler == null) || (!certificateErrorHandler.onValidationError("Failed to verify host " + host))) {
             //certificate error handler does not allow to ignore the error
                return false;
            }
            //certificate error handler did display a warning probably and allowed to ignore the error.
            return true;



        }

        private final HostnameVerifier hostnameVerifier;
    private final ICertificateErrorHandler certificateErrorHandler;
}
