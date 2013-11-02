package keepass2android.javafilestorage;

import java.io.UnsupportedEncodingException;

public abstract class JavaFileStorageBase implements JavaFileStorage{

	private static final String ISO_8859_1 = "ISO-8859-1";
	
	final static protected String TAG = "KP2AJ";
	final static protected String NAME_ID_SEP = "-KP2A-";
	
	protected String getProtocolPrefix()
	{
		return getProtocolId()+"://";
	}

	
	protected static String encode(final String unencoded)
			throws UnsupportedEncodingException {
		return java.net.URLEncoder.encode(unencoded, ISO_8859_1);
	}


	protected String decode(String encodedString)
			throws UnsupportedEncodingException {
		return java.net.URLDecoder.decode(encodedString, ISO_8859_1);
	}


	public class InvalidPathException extends Exception
	{
	      /**
		 * 
		 */
		private static final long serialVersionUID = 8579741509182446681L;

		public InvalidPathException() {}

	      public InvalidPathException(String message)
	      {
	         super(message);
	      }
	 }

}
