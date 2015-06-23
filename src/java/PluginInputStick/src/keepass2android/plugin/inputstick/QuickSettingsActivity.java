package keepass2android.plugin.inputstick;

import android.app.Activity;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.preference.PreferenceManager;
import android.widget.CheckBox;
import android.widget.RadioButton;

public class QuickSettingsActivity extends Activity {

	private CheckBox checkBoxAutoConnect;
	private RadioButton radioButtonPrimary;
	private RadioButton radioButtonSecondary;
	
	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.HONEYCOMB){
			super.setTheme( android.R.style.Theme_Holo_Dialog);
		}
		setContentView(R.layout.activity_quick_settings);
		
		checkBoxAutoConnect = (CheckBox)findViewById(R.id.checkBoxAutoConnect);
		radioButtonPrimary = (RadioButton)findViewById(R.id.radioButtonPrimary);
		radioButtonSecondary = (RadioButton)findViewById(R.id.radioButtonSecondary);
	}
	
	@Override
	protected void onResume() {
		super.onResume();
		
		SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(this);
		radioButtonPrimary.setText("Primary layout: " + prefs.getString("kbd_layout", "en-US"));
		radioButtonSecondary.setText("Secondary layout: " + prefs.getString("secondary_kbd_layout", "en-US"));
		if ("PRIMARY".equals(prefs.getString("active_layout", "PRIMARY"))) {
			radioButtonPrimary.setChecked(true);
		} else {
			radioButtonSecondary.setChecked(true);
		}
		
		checkBoxAutoConnect.setChecked(prefs.getBoolean("autoconnect", true));
	}	
	
	@Override
	protected void onPause() {	
		// TODO if modified
		SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(this);
		SharedPreferences.Editor editor = prefs.edit();
		
		if (radioButtonPrimary.isChecked()) {
			editor.putString("active_layout", "PRIMARY");
		} else {
			editor.putString("active_layout", "SECONDARY");
		}
		editor.putBoolean("autoconnect", checkBoxAutoConnect.isChecked());
		editor.apply();
		super.onPause();	
	}
	
}
