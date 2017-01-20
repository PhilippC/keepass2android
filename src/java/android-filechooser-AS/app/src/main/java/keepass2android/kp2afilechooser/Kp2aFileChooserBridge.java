package keepass2android.kp2afilechooser;

import group.pals.android.lib.ui.filechooser.FileChooserActivity;
//import group.pals.android.lib.ui.filechooser.FileChooserActivity_v7;
import group.pals.android.lib.ui.filechooser.providers.basefile.BaseFileContract.BaseFile;
import android.content.Context;
import android.content.Intent;

public class Kp2aFileChooserBridge {
	public static Intent getLaunchFileChooserIntent(Context ctx, String authority, String defaultPath)
	{
		android.util.Log.d("KP2A_FC", "getLaunchFileChooserIntent");
		//Always use FileChooserActivity. _v7 was removed due to problems with Mono for Android binding.
		Class<?> cls = FileChooserActivity.class;

		Intent intent = new Intent(ctx, cls);
		intent.putExtra(FileChooserActivity.EXTRA_FILE_PROVIDER_AUTHORITY, authority);
		intent.putExtra(FileChooserActivity.EXTRA_ROOTPATH,
		BaseFile.genContentIdUriBase(authority) 
		.buildUpon()
		.appendPath(defaultPath)
		.build());
		return intent;
	}
}
