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
