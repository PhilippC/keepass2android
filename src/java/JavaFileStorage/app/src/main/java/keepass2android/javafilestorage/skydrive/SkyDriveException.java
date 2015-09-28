package keepass2android.javafilestorage.skydrive;

public class SkyDriveException extends Exception {

	/**
	 * 
	 */
	private static final long serialVersionUID = 4594684204315150764L;
	private String mCode;

	public SkyDriveException(String message, String code)
	{
		super(message);
		mCode = code;
	}

	public String getCode() {
		return mCode;
	}


}
