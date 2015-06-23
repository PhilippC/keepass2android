package keepass2android.plugin.inputstick;

import com.inputstick.api.ConnectionManager;
import com.inputstick.api.basic.InputStickHID;
import com.inputstick.api.layout.KeyboardLayout;

import android.content.Context;
import android.content.SharedPreferences;
import android.preference.PreferenceManager;

public class PluginUtils {
	
	public static void type(Context ctx, String toType) {
		if (InputStickHID.getState() == ConnectionManager.STATE_READY) {			
			SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(ctx);
			String layoutKey;
			if ("PRIMARY".equals(prefs.getString("active_layout", "PRIMARY"))) {
				layoutKey = "kbd_layout";
			} else {
				layoutKey = "secondary_kbd_layout";
			}
			
			KeyboardLayout layout = KeyboardLayout.getLayout(prefs.getString(layoutKey, "en-US"));
			layout.type(toType);
		}
	}

}
