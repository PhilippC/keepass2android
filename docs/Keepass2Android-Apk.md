Keepass2Android's apk is pretty big, e.g. when comparing to Keepassdroid. The main difference is that Keepass2Android is built on Mono for Android. Mono is an open-source implementation of the Microsoft .Net Framework (installed on pretty much every Windows PC). On Windows, the .net framework requires several hundred MB (but only once, not for every application). On Android devices, Mono is not installed globally. Instead, it is packaged into every app. The more features from Mono are required, the bigger the package becomes.

Here's a list of what is contained in the Keepass2Android 0.9.1 application package:

{{
Mono for Android		
			.net dlls			5.0 MB
			Runtime				2.5 MB				
			Google libraries		0.8 MB				
			(for Drive support)

Resources		Strings, Icons..		2.1 MB				
Password Font						0.2 MB				
Java Code		including Dropbox 		1.1 MB				
				GDrive, SkyDrive
				libraries
							
Keepass library						0.2 MB				
Keepass2Android Code					0.3 MB				
Java/Mono bindings					0.5 MB				
							
rest							0.3 MB		

TOTAL							13 MB
}}