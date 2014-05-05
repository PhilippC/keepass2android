package keepass2android.pluginsdk;

public class Strings {
	/**
	 * Plugin is notified about actions like open/close/update a database.
	 */
	public static final String SCOPE_DATABASE_ACTIONS = "keepass2android.SCOPE_DATABASE_ACTIONS";
	/**
	 * Plugin is notified when an entry is opened. 
	 */
	public static final String SCOPE_CURRENT_ENTRY = "keepass2android.SCOPE_CURRENT_ENTRY";
	
	/**
	 * Extra key to transfer a (json serialized) list of scopes
	 */
	public static final String EXTRA_SCOPES = "keepass2android.EXTRA_SCOPES";
	
	/**
	 * Extra key for sending the package name of the sender of a broadcast.
	 * Should be set in every broadcast. 
	 */
	public static final String EXTRA_SENDER = "keepass2android.EXTRA_SENDER";
	
	/**
	 * Extra key for sending a request token. The request token is passed from 
	 * KP2A to the plugin. It's used in the authorization process. 
	 */
	public static final String EXTRA_REQUEST_TOKEN = "keepass2android.EXTRA_REQUEST_TOKEN";

	/**
	 * Action sent from KP2A to the plugin to indicate that the plugin should request
	 * access (sending it's scopes)
	 */
	public static final String ACTION_TRIGGER_REQUEST_ACCESS = "keepass2android.ACTION_TRIGGER_REQUEST_ACCESS";
	/** 
	 * Action sent from the plugin to KP2A including the scopes.
	 */
	public static final String ACTION_REQUEST_ACCESS = "keepass2android.ACTION_REQUEST_ACCESS";
	/**
	 * Action sent from the KP2A to the plugin when the user grants access.
	 * Will contain an access token.
	 */
	public static final String ACTION_RECEIVE_ACCESS = "keepass2android.ACTION_RECEIVE_ACCESS";
	/**
	 * Action sent from KP2A to the plugin to indicate that access is not or no longer valid.
	 */
	public static final String ACTION_REVOKE_ACCESS = "keepass2android.ACTION_REVOKE_ACCESS";
	
	/**
	 * Action sent from KP2A to the plugin to indicate that an entry was opened.
	 * The Intent contains the full entry data.
	 */
	public static final String ACTION_OPEN_ENTRY= "keepass2android.ACTION_OPEN_ENTRY";
	
	/**
	 * Action sent from KP2A to the plugin to indicate that an entry activity was closed.
	 */
	public static final String ACTION_CLOSE_ENTRY_VIEW= "keepass2android.ACTION_CLOSE_ENTRY_VIEW";
	
	/**
	 * Extra key for a string containing the GUID of the entry. 
	 */
	public static final String EXTRA_ENTRY_ID= "keepass2android.EXTRA_ENTRY_DATA";
	
	/** 
	 * Json serialized data of the PwEntry (C# class) representing the opened entry.
	 * currently not implemented.
	 */
	//public static final String EXTRA_ENTRY_DATA = "keepass2android.EXTRA_ENTRY_DATA";
	
	/**
	 * Json serialized list of fields, compiled using the database context (i.e. placeholders are replaced already)
	 */
	public static final String EXTRA_COMPILED_ENTRY_DATA = "keepass2android.EXTRA_COMPILED_ENTRY_DATA";

	/**
	 * Extra key for passing the access token (both ways)
	 */
	public static final String EXTRA_ACCESS_TOKEN = "keepass2android.EXTRA_ACCESS_TOKEN";
	
	/**
	 * Action for an intent from the plugin to KP2A to add menu options regarding the currently open entry.
	 * Requires SCOPE_CURRENT_ENTRY. 
	 */
	public static final String ACTION_ADD_ENTRY_ACTION = "keepass2android.ACTION_ADD_ENTRY_ACTION";
	
	public static final String EXTRA_ACTION_DISPLAY_TEXT = "keepass2android.EXTRA_ACTION_DISPLAY_TEXT";
	public static final String EXTRA_ACTION_ICON_RES_ID = "keepass2android.EXTRA_ACTION_ICON_RES_ID";

	public static final String EXTRA_FIELD_ID = "keepass2android.EXTRA_FIELD_ID";

	/** Extra for ACTION_ADD_ENTRY_ACTION and ACTION_ENTRY_ACTION_SELECTED to pass data specifying the action parameters.*/
	public static final String EXTRA_ACTION_DATA = "keepass2android.EXTRA_ACTION_DATA";
	
	/**
	 * Action for an intent from KP2A to the plugin when an action added with ACTION_ADD_ENTRY_ACTION was selected by the user.
	 * 
	 */
	public static final String ACTION_ENTRY_ACTION_SELECTED = "keepass2android.ACTION_ENTRY_ACTION_SELECTED";
	
	/**
	 * Action for an intent from the plugin to KP2A to set (i.e. add or update) a field in the entry.
	 * May be used to update existing or add new fields at any time while the entry is opened.
	 */
	public static final String ACTION_SET_ENTRY_FIELD = "keepass2android.ACTION_SET_ENTRY_FIELD";
	
	public static final String EXTRA_FIELD_VALUE = "keepass2android.EXTRA_FIELD_VALUE";
	public static final String EXTRA_FIELD_PROTECTED = "keepass2android.EXTRA_FIELD_PROTECTED";
	
	public static final String PREFIX_STRING = "STRING_";
	public static final String PREFIX_BINARY = "BINARY_";
	

}
