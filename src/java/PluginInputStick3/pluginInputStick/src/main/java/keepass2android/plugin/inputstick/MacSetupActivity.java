package keepass2android.plugin.inputstick;

import android.app.Activity;
import android.os.Bundle;
import android.view.View;
import android.view.View.OnClickListener;
import android.widget.Button;
import android.widget.TextView;
import android.widget.Toast;

import com.inputstick.api.ConnectionManager;
import com.inputstick.api.basic.InputStickHID;
import com.inputstick.api.basic.InputStickKeyboard;
import com.inputstick.api.hid.HIDKeycodes;
import com.inputstick.api.layout.KeyboardLayout;

public class MacSetupActivity extends Activity {
	
	private boolean nonUS;
	
	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		super.setTheme( android.R.style.Theme_Holo_Dialog);
		setContentView(R.layout.activity_mac_setup);

		String layoutName = ActionManager.getUserPrefs().getLayoutPrimary();				
		KeyboardLayout layout = KeyboardLayout.getLayout(layoutName);
		
		TextView textViewLayoutInfo = (TextView)findViewById(R.id.textViewLayoutInfo);
		textViewLayoutInfo.append(" " + layoutName);
		
		Button buttonNextToShift = (Button)findViewById(R.id.buttonNextToShift);		
		buttonNextToShift.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				if (InputStickHID.getState() == ConnectionManager.STATE_READY) {
					if (nonUS) {
						InputStickKeyboard.pressAndRelease(HIDKeycodes.NONE, HIDKeycodes.KEY_BACKSLASH_NON_US);
					} else {
						InputStickKeyboard.pressAndRelease(HIDKeycodes.NONE, HIDKeycodes.KEY_Z);
					}
				} else {				
					Toast.makeText(MacSetupActivity.this, R.string.not_ready, Toast.LENGTH_SHORT).show();
				}
			}
		});
		
		
		//check if non-us backslash key is used by this layout:				
		int[][] lut = layout.getLUT();
		int tmp = lut[0x56][1];
		nonUS = true;
		for (int i = 0; i < 0x40; i++) {
			for (int j = 1; j < 6; j++) {
				if (lut[i][j] == tmp) {
					nonUS = false;
					break;
				}
			}
		}
		if (nonUS) {
			//non-US ISO
			buttonNextToShift.setText(String.valueOf(layout.getChar(KeyboardLayout.hidToScanCode(HIDKeycodes.KEY_BACKSLASH_NON_US), false, false, false)));
		} else {
			//US ANSI
			buttonNextToShift.setText(String.valueOf(layout.getChar(KeyboardLayout.hidToScanCode(HIDKeycodes.KEY_Z), false, false, false)));
		}
	}


}
