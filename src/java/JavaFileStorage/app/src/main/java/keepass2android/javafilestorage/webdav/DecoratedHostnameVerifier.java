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
