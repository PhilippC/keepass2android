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
