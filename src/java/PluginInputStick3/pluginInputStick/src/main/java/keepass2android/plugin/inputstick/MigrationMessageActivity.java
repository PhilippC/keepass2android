package keepass2android.plugin.inputstick;

import android.app.Activity;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.net.Uri;
import android.os.Bundle;
import android.preference.PreferenceManager;
import android.support.v4.app.NotificationCompat;
import android.view.View;
import android.view.View.OnClickListener;
import android.widget.Button;
import android.widget.Toast;

public class MigrationMessageActivity extends Activity {
	
	public static long lastToast;

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		setContentView(R.layout.activity_migration_message);
		
		Button button = (Button)findViewById(R.id.buttonMigrationAction);		
		button.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				try {
					startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=com.inputstick.apps.kp2aplugin")));
				} catch (android.content.ActivityNotFoundException anfe) {
					startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("http://play.google.com/store/apps/details?id=com.inputstick.apps.kp2aplugin")));
				}
			}
		});
	}	
	
	@Override
	protected void onPause() {
		SharedPreferences.Editor edit = PreferenceManager.getDefaultSharedPreferences(MigrationMessageActivity.this).edit();
		edit.putBoolean("show_migration_message_activity", false);
		edit.apply();		
		super.onPause();				
	}	
	
	public static void displayNotification(Context ctx) {
		NotificationManager mNotificationManager = (NotificationManager) ctx.getSystemService(Context.NOTIFICATION_SERVICE);
		NotificationCompat.Builder mBuilder = new NotificationCompat.Builder(ctx);

		Intent intent = new Intent(ctx, MigrationMessageActivity.class);
		PendingIntent pendingIntent = PendingIntent.getActivity(ctx, 0, intent, 0);
		
		mBuilder.setContentTitle(ctx.getString(R.string.app_name));
		mBuilder.setContentText(ctx.getString(R.string.update_required));
		mBuilder.setSmallIcon(R.drawable.ic_notification);
		mBuilder.setAutoCancel(true);
		mBuilder.setOngoing(true);
		mBuilder.setContentIntent(pendingIntent);
		mNotificationManager.notify(0, mBuilder.build());
		
		long now = System.currentTimeMillis();
		if (now > lastToast + 86400000) {
			lastToast = now;
			Toast.makeText(ctx, ctx.getString(R.string.app_name) + ": " + ctx.getString(R.string.update_required), Toast.LENGTH_SHORT).show();
		}
	}
	
	public static void hideNotification(Context ctx) {
		NotificationManager mNotificationManager = (NotificationManager) ctx.getSystemService(Context.NOTIFICATION_SERVICE);
		mNotificationManager.cancel(0);  
	}		
	
	public static boolean shouldShowActivity(SharedPreferences sharedPref) {			
		return sharedPref.getBoolean("show_migration_message_activity", true);
	}		
	
}
