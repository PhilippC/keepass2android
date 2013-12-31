package keepass2android.javafilestorage;

import java.io.UnsupportedEncodingException;

import org.apache.http.protocol.HTTP;

import android.app.Activity;
import android.content.Intent;
import android.util.Log;

public abstract class JavaFileStorageBase implements JavaFileStorage{

	private static final String ISO_8859_1 = "ISO-8859-1";
	
	private static final String UTF8_PREFIX = ".U8-";
	final static protected String NAME_ID_SEP = "-KP2A-";	
	final static protected String TAG = "KP2AJ";
	
	protected void logDebug(String text)
	{
		Log.d(TAG, text);
	}
	
	protected String getProtocolPrefix()
	{
		return getProtocolId()+"://";
	}

	
	protected String encode(final String unencoded)
			throws UnsupportedEncodingException {
		return UTF8_PREFIX+java.net.URLEncoder.encode(unencoded, HTTP.UTF_8);
	}


	protected String decode(String encodedString)
			throws UnsupportedEncodingException {
		//the first version of encode/decode used ISO 8859-1 which doesn't work with Cyrillic characters
		//this is why we need to check for the prefix, even though all new strings are UTF8 encoded. 
		if (encodedString.startsWith(UTF8_PREFIX))
			return java.net.URLDecoder.decode(encodedString.substring(UTF8_PREFIX.length()), HTTP.UTF_8);
		else
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
	
	
	protected void finishWithError(final FileStorageSetupActivity setupAct, Exception error) {
		Log.e("KP2AJ", "Exception: " + error.toString());
		error.printStackTrace();
		
		final Activity activity = (Activity)setupAct;
		
		int resultCode = Activity.RESULT_CANCELED;
		
		//check if we should return OK anyways.
		//This can make sense if there is a higher-level FileStorage which has the file cached.
		if (activity.getIntent().getBooleanExtra(EXTRA_ALWAYS_RETURN_SUCCESS, false))
		{
			Log.d(TAG, "Returning success as desired in intent despite of exception.");
			finishActivityWithSuccess(setupAct);
			return;
		}

		Intent retData = new Intent();
		retData.putExtra(EXTRA_ERROR_MESSAGE, error.getMessage());
		activity.setResult(resultCode, retData);
		activity.finish();
	};

	protected void finishActivityWithSuccess(
			FileStorageSetupActivity setupActivity) {
		//Log.d("KP2AJ", "Success with authenticating!");
		Activity activity = (Activity) setupActivity;

		if (setupActivity.getProcessName()
				.equals(PROCESS_NAME_FILE_USAGE_SETUP)) {
			Intent data = new Intent();
			data.putExtra(EXTRA_IS_FOR_SAVE, setupActivity.isForSave());
			data.putExtra(EXTRA_PATH, setupActivity.getPath());
			activity.setResult(RESULT_FILEUSAGE_PREPARED, data);
			activity.finish();
			return;
		}
		if (setupActivity.getProcessName().equals(PROCESS_NAME_SELECTFILE)) {
			Intent data = new Intent();

			String path = setupActivity.getState().getString(EXTRA_PATH);
			if (path != null)
				data.putExtra(EXTRA_PATH, path);
			activity.setResult(RESULT_FILECHOOSER_PREPARED, data);
			activity.finish();
			return;
		}

		Log.w("KP2AJ", "Unknown process: " + setupActivity.getProcessName());

	}


}
