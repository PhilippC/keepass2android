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
