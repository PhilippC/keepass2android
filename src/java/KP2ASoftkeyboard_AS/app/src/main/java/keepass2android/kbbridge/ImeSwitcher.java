package keepass2android.kbbridge;

import java.util.List;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.SharedPreferences.Editor;
import android.content.pm.ActivityInfo;
import android.content.pm.ResolveInfo;
import android.inputmethodservice.InputMethodService;
import android.os.Bundle;
import android.os.IBinder;
import android.preference.PreferenceManager;
import android.util.Log;
import android.view.inputmethod.InputMethod;
import android.view.inputmethod.InputMethodManager;
import android.widget.Toast;

public class ImeSwitcher {
	private static final String SECURE_SETTINGS_PACKAGE_NAME = "com.intangibleobject.securesettings.plugin";
	private static final String PREVIOUS_KEYBOARD = "previous_keyboard";
	private static final String KP2A_SWITCHER = "KP2A_Switcher";
	private static final String Tag = "KP2A_SWITCHER";
	
	public static void switchToPreviousKeyboard(InputMethodService ims, boolean silent)
	{
		try {
		    InputMethodManager imm = (InputMethodManager) ims.getSystemService(Context.INPUT_METHOD_SERVICE);
		    final IBinder token = ims.getWindow().getWindow().getAttributes().token;
		    //imm.setInputMethod(token, LATIN);
		    imm.switchToLastInputMethod(token);
		} catch (Throwable t) { // java.lang.NoSuchMethodError if API_level<11
		    Log.e("KP2A","cannot set the previous input method:");
		    t.printStackTrace();
		    SharedPreferences prefs = ims.getSharedPreferences(KP2A_SWITCHER, Context.MODE_PRIVATE);
			switchToKeyboard(ims, prefs.getString(PREVIOUS_KEYBOARD, null), silent);
		}
		
	}

	//silent: if true, do not show picker, only switch in background. Don't do anything if switching fails.
	public static void switchToKeyboard(Context ctx, String newImeName, boolean silent)
	{
		Log.d(Tag,"silent: "+silent);
		if ((newImeName == null) || (!autoSwitchEnabled(ctx)))
		{
			Log.d(Tag, "(newImeName == null): "+(newImeName == null));
			Log.d(Tag, "autoSwitchEnabled(ctx)"+autoSwitchEnabled(ctx));
			if (!silent)
			{
				showPicker(ctx);
			} 
			return;			
		}
		Intent swapPluginIntent = getLaunchIntentForKeyboardSwap(ctx);

		if ((swapPluginIntent != null) && (newImeName != null))
		{
			swapPluginIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
			swapPluginIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
			swapPluginIntent.putExtra("ImeName", newImeName);
		}


		boolean sentBroadcast = false;

		if ((swapPluginIntent != null) && (!ctx.getPackageManager().queryIntentActivities(swapPluginIntent,0).isEmpty()))
		{
			Log.d(Tag, "Found keyboard swap plugin.");
			ctx.startActivity(swapPluginIntent);
			sentBroadcast = true;
		}
		else
		{
			Log.d(Tag, "Did not find keyboard swap plugin. Trying secure settings.");
			Intent qi = new Intent("com.twofortyfouram.locale.intent.action.FIRE_SETTING");
			List<ResolveInfo> pkgAppsList = ctx.getPackageManager().queryBroadcastReceivers(qi, 0);


			for (ResolveInfo ri : pkgAppsList) {
				if (ri.activityInfo.packageName.equals(SECURE_SETTINGS_PACKAGE_NAME)) {

					String currentIme = android.provider.Settings.Secure.getString(
							ctx.getContentResolver(),
							android.provider.Settings.Secure.DEFAULT_INPUT_METHOD);
					currentIme += ";" + String.valueOf(
							android.provider.Settings.Secure.getInt(
									ctx.getContentResolver(),
									android.provider.Settings.Secure.SELECTED_INPUT_METHOD_SUBTYPE,
									-1)
					);
					SharedPreferences prefs = ctx.getSharedPreferences(KP2A_SWITCHER, Context.MODE_PRIVATE);
					Editor edit = prefs.edit();

					edit.putString(PREVIOUS_KEYBOARD, currentIme);
					edit.commit();

					Intent i = new Intent("com.twofortyfouram.locale.intent.action.FIRE_SETTING");
					Bundle b = new Bundle();

					b.putString("com.intangibleobject.securesettings.plugin.extra.BLURB", "Input Method/SwitchIME");
					b.putString("com.intangibleobject.securesettings.plugin.extra.INPUT_METHOD", newImeName);
					b.putString("com.intangibleobject.securesettings.plugin.extra.SETTING", "default_input_method");
					i.putExtra("com.twofortyfouram.locale.intent.extra.BUNDLE", b);
					i.setPackage(SECURE_SETTINGS_PACKAGE_NAME);
					Log.d(Tag, "trying to switch by broadcast to SecureSettings");
					ctx.sendBroadcast(i);
					sentBroadcast = true;
					break;
				}
			}
		}
		if ((!sentBroadcast) && (!silent))
		{
			showPicker(ctx);	
		}

	}

	public static Intent getLaunchIntentForKeyboardSwap(Context ctx) {
		return ctx.getPackageManager().getLaunchIntentForPackage("keepass2android.plugin.keyboardswap2");
	}

	private static boolean autoSwitchEnabled(Context ctx) {
		SharedPreferences sp = PreferenceManager.getDefaultSharedPreferences(ctx);
		return sp.getBoolean("kp2a_switch_rooted", false);
	}

	private static void showPicker(Context ctx) {
		((InputMethodManager) ctx.getSystemService(InputMethodService.INPUT_METHOD_SERVICE))
			.showInputMethodPicker();
	}
}
