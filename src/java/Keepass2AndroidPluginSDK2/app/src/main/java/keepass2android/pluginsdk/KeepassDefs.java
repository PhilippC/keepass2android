package keepass2android.pluginsdk;

public class KeepassDefs {

	/// <summary>
	/// Default identifier string for the title field. Should not contain
	/// spaces, tabs or other whitespace.
	/// </summary>
	public static String TitleField = "Title";

	/// <summary>
	/// Default identifier string for the user name field. Should not contain
	/// spaces, tabs or other whitespace.
	/// </summary>
	public static String UserNameField = "UserName";

	/// <summary>
	/// Default identifier string for the password field. Should not contain
	/// spaces, tabs or other whitespace.
	/// </summary>
	public static String PasswordField = "Password";

	/// <summary>
	/// Default identifier string for the URL field. Should not contain
	/// spaces, tabs or other whitespace.
	/// </summary>
	public static String UrlField = "URL";

	/// <summary>
	/// Default identifier string for the notes field. Should not contain
	/// spaces, tabs or other whitespace.
	/// </summary>
	public static String NotesField = "Notes";

	
	public static boolean IsStandardField(String strFieldName)
	{
		if(strFieldName == null)
			return false;
		if(strFieldName.equals(TitleField)) return true;
		if(strFieldName.equals(UserNameField)) return true;
		if(strFieldName.equals(PasswordField)) return true;
		if(strFieldName.equals(UrlField)) return true;
		if(strFieldName.equals(NotesField)) return true;

		return false;
	}
}
