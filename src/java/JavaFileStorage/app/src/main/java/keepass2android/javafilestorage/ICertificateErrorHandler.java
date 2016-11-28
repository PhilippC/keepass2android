package keepass2android.javafilestorage;

/**
 * Created by Philipp on 22.11.2016.
 */
public interface ICertificateErrorHandler {
    //callback when certificate validation fails.
    //must return true to ignore the error and false to throw a CertificateException
    boolean onValidationError(String error);

    //indicates whether the handler is configured to never accept validation errors. If true, the ssl default configuration can be used.
    boolean alwaysFailOnValidationError();
}
