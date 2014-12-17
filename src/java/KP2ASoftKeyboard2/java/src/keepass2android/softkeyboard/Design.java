/*
 * Copyright (C) 2014 Wiktor Lawski <wiktor.lawski@gmail.com>
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not
 * use this file except in compliance with the License. You may obtain a copy of
 * the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 */

package keepass2android.softkeyboard;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.SharedPreferences;

public class Design {
	@SuppressLint("InlinedApi")
	public static void updateTheme(Activity activity, SharedPreferences prefs) {
		
		if (android.os.Build.VERSION.SDK_INT >= 11
            /* android.os.Build.VERSION_CODES.HONEYCOMB */) {
            String design = prefs.getString("design_key", "Light");
            
            if (design.equals("Light")) {
            	activity.setTheme(android.R.style.Theme_Holo_Light);
            } else {
            	activity.setTheme(android.R.style.Theme_Holo);
            }
        }
    }
}
