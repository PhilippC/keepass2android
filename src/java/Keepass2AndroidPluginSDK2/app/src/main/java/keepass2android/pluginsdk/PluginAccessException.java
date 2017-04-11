package keepass2android.pluginsdk;

import java.util.ArrayList;

public class PluginAccessException extends Exception {
	
	public PluginAccessException(String what)
	{
		super(what);
	}

	public PluginAccessException(String hostPackage, ArrayList<String> scopes) {
		
	}

	/**
	 * 
	 */
	private static final long serialVersionUID = 1L;

}
