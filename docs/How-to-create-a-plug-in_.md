# How to create a plug-in or connect from your app

Creating a plug-in for Keepass2Android or enabling your app to query credentials from Keepass2Android is pretty simple. Please follow the steps below to get started. In case you have any questions, please contact me.

## Preparations 
First check out the source code and import the Keepass2AndroidPluginSDK from [https://keepass2android.codeplex.com/SourceControl/latest#src/java/Keepass2AndroidPluginSDK/](https://keepass2android.codeplex.com/SourceControl/latest#src/java/Keepass2AndroidPluginSDK/) into your workspace. You should be able to build this library project.

Now add a reference to the PluginSDK library from your existing app or add a new plug-in app and then add the reference. 

## Authorization

Keepass2Android stores very sensitive user data and therefore implements a plug-in authorization scheme based on broadcasts sent between the plug-in and the host app (=Keepass2Android or Keepass2Android Offline). Before your app/plug-in gets any information from KP2A, the user will have to grant your app/plug-in access to KP2A. As not every app/plug-in requires access to all information, you must specify which scopes are required by your app. The implemented scopes can be found in [https://keepass2android.codeplex.com/SourceControl/latest#src/java/Keepass2AndroidPluginSDK/src/keepass2android/pluginsdk/Strings.java](https://keepass2android.codeplex.com/SourceControl/latest#src/java/Keepass2AndroidPluginSDK/src/keepass2android/pluginsdk/Strings.java).

To tell Kp2a that you're a plug-in, you need to add a simple BroadcastReceiver like this:

```java

public class PluginAAccessReceiver extends keepass2android.pluginsdk.PluginAccessBroadcastReceiver
{

	@Override
	public ArrayList<String> getScopes() {
		ArrayList<String> scopes = new ArrayList<String>();
		scopes.add(Strings.SCOPE_DATABASE_ACTIONS);
		scopes.add(Strings.SCOPE_CURRENT_ENTRY);
		return scopes;
		
	}

}
```

Here, you define the method getScopes where the list of scopes is created which must be granted by the user. The actual logic of the authorization process is implemented by the base class in the sdk.

In order to make this broadcast receiver visible to KP2A, add the following lines (probably with the name adapted to your class name) in the AndroidManifest.xml:

```xml
<receiver android:name="PluginAAccessReceiver" android:exported="true">
	<intent-filter>
		<action android:name="keepass2android.ACTION_TRIGGER_REQUEST_ACCESS" />
                <action android:name="keepass2android.ACTION_RECEIVE_ACCESS" />
                <action android:name="keepass2android.ACTION_REVOKE_ACCESS" />
	</intent-filter>
</receiver>
```

Please also add a few strings in your resource files (e.g. strings.xml) with the following keys:

```xml
<string name="kp2aplugin_title">The Great PluginA</string>
<string name="kp2aplugin_shortdesc">Test plugin to demonstrate how plugins work</string>
<string name="kp2aplugin_author">[your name here](your-name-here)</string>
```
These strings will be displayed to the user when KP2A asks if access should be granted.

## Modifying the entry view
You can add menu options for the full entry or for individual fields of the entry when displayed to the user. This is done, for example, by the QR plugin ([https://play.google.com/store/apps/details?id=keepass2android.plugin.qr](https___play.google.com_store_apps_details_id=keepass2android.plugin.qr)). 
In addition, it is even possible to add new fields or modify existing fields. Please see the sample plugin "PluginA" in the KP2A repository for a simple example on how to do this:
[https://keepass2android.codeplex.com/SourceControl/latest#src/java/PluginA/src/keepass2android/plugina/PluginAActionReceiver.java](https://keepass2android.codeplex.com/SourceControl/latest#src/java/PluginA/src/keepass2android/plugina/PluginAActionReceiver.java)

## Querying credentials
KP2A 0.9.4 adds a great opportunity for third party apps: Instead of prompting the user to enter credentials or a passphrase, the app should try to get the data from KP2A if it is installed: If the user grants (or previously granted) access for the app,  KP2A will automatically retrieve the matching entry. User action is only required if the KP2A database is locked (user will usually unlock it with the short QuickUnlock code) or if no matching entry is found (user can then create a new entry or select an existing one. in the latter case KP2A will offer to add entry information so that the entry will be found automatically next time).

To implement this, simply follow the steps descrIbed above in the sections Preparation and Authorization. Then, wherever appropriate in your app, do something like this: 

```java
	try
	{
		PlaceholderFragment.this.startActivityForResult(
				Kp2aControl.getQueryEntryIntentForOwnPackage(),
				1);
	}
	catch (ActivityNotFoundException e)
	{
		Toast.makeText(
			PlaceholderFragment.this.getActivity(), 
			"no KP2A host app found", 
			Toast.LENGTH_SHORT).show();
	} 

```

(of course you can use `PacketManager` to check if the intent can be started instead of catching the `Exception`).

Instead of querying credentials associated with your own app, you might want to query other credentials as well. instead of `KpControl.getQueryEntryIntentForOwnPackage()` use
`Kp2aControl.getQueryEntryIntent("google.com")`
This requires \{"SCOPE_QUERY_CREDENTIALS (whereas getQueryEntryIntentForOwnPackage() requires SCOPE_QUERY_CREDENTIALS_FOR_OWN_PACKAGE)"\}.

The credential data can be retrieved in onActivityResult():

```java
if ((requestCode == 1) //queryEntry for own package
	&& (resultCode == RESULT_OK)) // ensure user granted access and selected something
{
	HashMap<String, String> credentials = Kp2aControl.getEntryFieldsFromIntent(data);
	if (!credentials.isEmpty())
	{
		//here we go!
		Toast.makeText(
			getActivity(), 
			"retrieved credenitals! Username="+credentials.get(KeepassDefs.UserNameField),
			Toast.LENGTH_LONG).show();
	}
}
```

Note that you get access to all strings (Title, Username, Password, URL, Notes + any user defined strings) in the entry. This may be in intersting in combination with the following section:

## Storing data in KP2A 
If you allow the user to set up an account in your app or create a password, e.g. for encryption, please add an option to store this data in the Keepass2Android database, as this will lead to great workflows for the user. It's as simple as 

```java
try {
	HashMap<String, String> fields = new HashMap<String, String>();
	//standard fields
	fields.put(KeepassDefs.TitleField, "plugin A");
	fields.put(KeepassDefs.UserNameField, "John Doe");
	fields.put(KeepassDefs.PasswordField, "top secret");
	//associate entry with our app. If we would require the URL field for a web URL,
	//this string could be added in any other (e.g. a custom) field 
	fields.put(KeepassDefs.UrlField, "androidapp://"+getActivity().getPackageName()); 
	//custom field:
	fields.put(PLUGIN_A_PASSPHRASE, "some long text");
	//mark custom field as protected (i.e. display masked, enable memory protection in Keepass2)
	ArrayList<String> protectedFields = new ArrayList<String>();
	protectedFields.add(PLUGIN_A_PASSPHRASE);
	
	//add to KP2A
	PlaceholderFragment.this.startActivityForResult(
		Kp2aControl.getAddEntryIntent(fields, protectedFields),
		2);
} catch (ActivityNotFoundException e) {
	Toast.makeText(
		PlaceholderFragment.this.getActivity(),
		"no KP2A host app found",
		Toast.LENGTH_SHORT).show();
}
```

Note that this does not even require access authorization because the user will actively save the entry anyways (after selecting the group where to create it.)

## Get information about database actions
With {"SCOPE_DATABASE_ACTIONS"}, you will be informed when the user opens, closes, locks or unlocks the database including the file name information.

PluginA uses this to simply display a toast message in its ActionReceiver:

```java
@Override
	protected void dbAction(DatabaseAction db) {
		
		Log.d("PluginA", db.getAction() + " in file " + db.getFileDisplayName() + " ("+db.getFilePath()+")");
	}
```
 

## Sample plugin
Most example code from above is taken from the simple sample plugin "PluginA" as can be found on [https://keepass2android.codeplex.com/SourceControl/latest#src/java/PluginA/](https://keepass2android.codeplex.com/SourceControl/latest#src/java/PluginA/)
