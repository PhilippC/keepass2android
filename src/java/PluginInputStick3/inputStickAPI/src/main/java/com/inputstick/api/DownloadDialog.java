package com.inputstick.api;

import android.app.AlertDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.net.Uri;

public class DownloadDialog {
	
	public static final int NOT_INSTALLED = 0;
	public static final int NOT_UPDATED = 1;
	
	
	public static AlertDialog getDialog(final Context ctx, final int messageCode) {
		AlertDialog.Builder downloadDialog = new AlertDialog.Builder(ctx);
		
		if (messageCode == NOT_UPDATED) {
			downloadDialog.setTitle("InputStickUtility app must be updated");
			downloadDialog.setMessage("It appears that you are using older version of InputStickUtility application. Update now (GoolePlay)?");
		} else {		
			downloadDialog.setTitle("InputStickUtility app NOT installed");
			downloadDialog.setMessage("InputStickUtility is required to complete this action. Download now (GoolePlay)?\nNote: InputStick USB receiver (HARDWARE!) is also required.");
		}
		downloadDialog.setPositiveButton("Yes",
				new DialogInterface.OnClickListener() {
					@Override
					public void onClick(DialogInterface dialogInterface, int i) {
						final String appPackageName = "com.inputstick.apps.inputstickutility";
						try {
							ctx.startActivity(new Intent(Intent.ACTION_VIEW,
									Uri.parse("market://details?id="
											+ appPackageName)));
						} catch (android.content.ActivityNotFoundException anfe) {
							ctx.startActivity(new Intent(
									Intent.ACTION_VIEW,
									Uri.parse("http://play.google.com/store/apps/details?id="
											+ appPackageName)));
						}
					}
				});
		downloadDialog.setNegativeButton("No",
				new DialogInterface.OnClickListener() {
					@Override
					public void onClick(DialogInterface dialogInterface, int i) {
					}
				});
		return downloadDialog.show();		
	}
	
	

}
