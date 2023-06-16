package keepass2android.javafilestorage;

import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.Message;
import android.os.Messenger;
import android.os.SystemClock;
import androidx.core.app.NotificationCompat;
import android.util.Log;

import com.jcraft.jsch.UserInfo;

public class SftpUserInfo implements UserInfo {

	private Object /* pick one type, and fixate on it */ dance(final String type, final String text)	/* for inside the thread */
	{

		final Message msg = Message.obtain();

		/* t(*A*t) */
		Thread t = new Thread() {
			public void run() {
				Looper.prepare();
				int bogon = (int)SystemClock.elapsedRealtime();

				NotificationManager mNotificationManager = (NotificationManager)_appContext.getSystemService(Context.NOTIFICATION_SERVICE);
				NotificationCompat.Builder builder = new NotificationCompat.Builder(_appContext);
				builder.setContentText("SFTP prompt");
				builder.setSmallIcon(R.drawable.ic_logo_green_foreground);

				Handler h = new Handler() {
					public void handleMessage(Message M) {
						msg.copyFrom(M);
						Looper.myLooper().quit();
					}
				};
				Messenger m = new Messenger(h);

				Intent intent = new Intent(_appContext, NotifSlave.class);

				intent.setAction("keepass2android.sftp.NotifSlave");
				intent.putExtra("keepass2android.sftp.returnmessenger", m);
				intent.putExtra("keepass2android.sftp.reqtype", type);
				intent.putExtra("keepass2android.sftp.prompt", text);
				intent.setData((Uri.parse("suckit://"+SystemClock.elapsedRealtime())));


				Log.e("KP2AJFS[thread]", "built after 2023-03-14");

				int flags = 0;
				if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.S) {
					Log.e("KP2AJFS[thread]", "Setting mutable flag...");
					flags |= PendingIntent.FLAG_MUTABLE;
				}
				PendingIntent contentIntent = PendingIntent.getActivity(_appContext, 0, intent, flags);

				builder.setContentIntent(contentIntent);

				{
					Intent directIntent = new Intent(_appContext, NotifSlave.class);
					directIntent.setAction("keepass2android.sftp.NotifSlave");
					directIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);

					directIntent.putExtra("keepass2android.sftp.returnmessenger", m);
					directIntent.putExtra("keepass2android.sftp.reqtype", type);
					directIntent.putExtra("keepass2android.sftp.prompt", text);
					directIntent.setData((Uri.parse("suckit://" + SystemClock.elapsedRealtime())));
					_appContext.startActivity(directIntent);
				}



				Log.e("KP2AJFS[thread]", "Notifying...");

				mNotificationManager.notify(bogon, builder.build());



				Log.e("KP2AJFS[thread]", "About to go to 'sleep'...");
				Looper.loop();
				Log.e("KP2AJFS[thread]", "And we're alive!");

				Log.e("KP2AJFS[thread]", "result was: "+(Integer.toString(msg.arg1)));

				mNotificationManager.cancel(bogon);
			}
		};

		t.start();
		try {
			t.join();
		} catch (Exception e) {
			return null;
		}

		if (type.equals("yesno"))
			return new Boolean(msg.arg1 == 1);
		else if (type.equals("message"))
			return null;
		else if (type.equals("password")) {
			if (msg.arg1 == 0)
				return null;
			Bundle b = msg.getData();
			return b.getString("response");
		} else
			return null;
	}


	Context _appContext;

	String _password;
	String _passphrase;
	
	public SftpUserInfo(String password, String passphrase, Context appContext)
	{
		_password = password;
		_passphrase = passphrase;
		_appContext = appContext;
	}

	@Override
	public String getPassphrase() {

		return _passphrase;
	}

	@Override
	public String getPassword() {
		
		return _password;
	}

	@Override
	public boolean promptPassword(String message) {
		return _password != null;
	}

	@Override
	public boolean promptPassphrase(String message) {
		return _passphrase != null;
	}

	@Override
	public boolean promptYesNo(String message) {
		return (Boolean)dance("yesno", message);

		//test with https://www.sftp.net/public-online-sftp-servers?
	}

	@Override
	public void showMessage(String message)
	{
		dance("message", message);
	}

}
