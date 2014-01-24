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
import android.util.Log;
import android.view.inputmethod.InputMethod;
import android.view.inputmethod.InputMethodManager;
import android.widget.Toast;

public class ImeSwitcher {
	private static final String PREVIOUS_KEYBOARD = "previous_keyboard";
	private static final String KP2A_SWITCHER = "KP2A_Switcher";
	private static final String Tag = "KP2A_SWITCHER";
	
	public static void switchToPreviousKeyboard(Context ctx)
	{
		SharedPreferences prefs = ctx.getSharedPreferences(KP2A_SWITCHER, Context.MODE_PRIVATE);
		switchToKeyboard(ctx, prefs.getString(PREVIOUS_KEYBOARD, null));
	}

	public static void switchToKeyboard(Context ctx, String newImeName)
	{
		if (newImeName == null)
		{
			showPicker(ctx);
			return;
			
		}
		Intent qi = new Intent("com.twofortyfouram.locale.intent.action.FIRE_SETTING");
		List<ResolveInfo> pkgAppsList = ctx.getPackageManager().queryBroadcastReceivers(qi, 0);
		boolean sentBroadcast = false;
		for (ResolveInfo ri: pkgAppsList)
		{
			
			if (ri.activityInfo.packageName.equals("com.intangibleobject.securesettings.plugin"))
			{
				
				String currentIme = android.provider.Settings.Secure.getString(
                        ctx.getContentResolver(),
                        android.provider.Settings.Secure.DEFAULT_INPUT_METHOD);
								
				SharedPreferences prefs = ctx.getSharedPreferences(KP2A_SWITCHER, Context.MODE_PRIVATE);
				Editor edit = prefs.edit();
				
				edit.putString(PREVIOUS_KEYBOARD, currentIme);
				edit.commit();
				
				Intent i=new Intent("com.twofortyfouram.locale.intent.action.FIRE_SETTING");
				Bundle b = new Bundle();

				b.putString("com.intangibleobject.securesettings.plugin.extra.BLURB", "Input Method/Switch IME");
				b.putString("com.intangibleobject.securesettings.plugin.extra.INPUT_METHOD", newImeName);
				b.putString("com.intangibleobject.securesettings.plugin.extra.SETTING","default_input_method");
				i.putExtra("com.twofortyfouram.locale.intent.extra.BUNDLE", b);
				ctx.sendBroadcast(i);
				sentBroadcast = true;
				break;
			}
		}
		if (!sentBroadcast)
		{
			//report that switch failed:
			try
			{
				Toast.makeText(ctx, "SecureSettings not found on system!", Toast.LENGTH_LONG).show();	
			}
			catch (Exception e)
			{
				Log.e(Tag, e.toString());
			}
				
			showPicker(ctx);	
		}

	}

	private static void showPicker(Context ctx) {
		((InputMethodManager) ctx.getSystemService(InputMethodService.INPUT_METHOD_SERVICE))
			.showInputMethodPicker();
	}
}
