package keepass2android.plugin.inputstick;

import java.util.ArrayList;

import android.app.Activity;
import android.os.Bundle;
import android.view.View;
import android.widget.AdapterView;
import android.widget.AdapterView.OnItemClickListener;
import android.widget.ArrayAdapter;
import android.widget.ListView;
import android.widget.Toast;

import com.inputstick.api.hid.HIDKeycodes;

public class AllActionsActivity extends Activity {
	
	private long lastActionTime;
	private long maxTime;

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		super.setTheme( android.R.style.Theme_Holo_Dialog);
		setContentView(R.layout.activity_all_actions);
		
		final UserPreferences userPrefs = ActionManager.getUserPrefs();
		maxTime = getIntent().getLongExtra(Const.EXTRA_MAX_TIME, 0);
		lastActionTime = System.currentTimeMillis();

		ListView listViewActions = (ListView) findViewById(R.id.listViewActions);
		ArrayList<String> list = new ArrayList<String>();
		ArrayAdapter<String> listAdapter = new ArrayAdapter<String>(this, R.layout.row, list);
		
		listAdapter.add(ActionManager.getActionString(R.string.action_open_settings, false));
		listAdapter.add(ActionManager.getActionString(R.string.action_connect, false));
		listAdapter.add(ActionManager.getActionString(R.string.action_disconnect, false));
		listAdapter.add(ActionManager.getActionString(R.string.action_open_mac_setup, false));
		listAdapter.add(ActionManager.getActionString(R.string.action_type_tab, false));
		listAdapter.add(ActionManager.getActionString(R.string.action_type_enter, false));
		listAdapter.add(ActionManager.getActionString(R.string.action_macro_add_edit, false));
		
		listAdapter.add(ActionManager.getActionStringForPrimaryLayout(R.string.action_type_user_tab_pass, false));
		listAdapter.add(ActionManager.getActionStringForPrimaryLayout(R.string.action_type_user_tab_pass_enter, false));
		listAdapter.add(ActionManager.getActionStringForPrimaryLayout(R.string.action_masked_password, false));
		listAdapter.add(ActionManager.getActionStringForPrimaryLayout(R.string.action_macro_run, false));
		listAdapter.add(ActionManager.getActionStringForPrimaryLayout(R.string.action_clipboard, false));
		
		if (userPrefs.isShowSecondary()) {
			listAdapter.add(ActionManager.getActionStringForSecondaryLayout(R.string.action_type_user_tab_pass, false));
			listAdapter.add(ActionManager.getActionStringForSecondaryLayout(R.string.action_type_user_tab_pass_enter, false));
			listAdapter.add(ActionManager.getActionStringForSecondaryLayout(R.string.action_masked_password, false));
			listAdapter.add(ActionManager.getActionStringForSecondaryLayout(R.string.action_macro_run, false));
			listAdapter.add(ActionManager.getActionStringForSecondaryLayout(R.string.action_clipboard, false));
		}
		
		listViewActions.setAdapter(listAdapter);
		listViewActions.setOnItemClickListener(new OnItemClickListener() {
			@Override
			public void onItemClick(AdapterView<?> arg0, View view, int pos, long arg3) {
				long now = System.currentTimeMillis();
				if (now > maxTime) {
					Toast.makeText(AllActionsActivity.this, R.string.text_locked, Toast.LENGTH_LONG).show();
				} else {
					maxTime += (now - lastActionTime);
					lastActionTime = now;
					
					switch (pos) {
						//general:
						case 0:
							ActionManager.startSettingsActivity();
							break;
						case 1:
							ActionManager.connect();
							break;
						case 2:
							ActionManager.disconnect();
							break;						
						case 3:
							ActionManager.startMacSetupActivity();
							break;		
						case 4:
							ActionManager.queueKey(HIDKeycodes.NONE, HIDKeycodes.KEY_TAB);
							break;		
						case 5:
							ActionManager.queueKey(HIDKeycodes.NONE, HIDKeycodes.KEY_ENTER);
							break;
						case 6:
							ActionManager.addEditMacro(false);
							break;
						//entry, primary layout
						case 7:
							ActionManager.typeUsernameAndPassword(userPrefs.getLayoutPrimary(), false);
							break;				
						case 8:
							ActionManager.typeUsernameAndPassword(userPrefs.getLayoutPrimary(), true);
							break;	
						case 9:
							ActionManager.openMaskedPassword(userPrefs.getLayoutPrimary(), true);
							break;		
						case 10:
							ActionManager.runMacro(userPrefs.getLayoutPrimary());
							break;	
						case 11:
							ActionManager.clipboardTyping(userPrefs.getLayoutPrimary());
							break;
						//entry, secondary layout
						case 12:
							ActionManager.typeUsernameAndPassword(userPrefs.getLayoutSecondary(), false);
							break;				
						case 13:
							ActionManager.typeUsernameAndPassword(userPrefs.getLayoutSecondary(), true);
							break;	
						case 14:
							ActionManager.openMaskedPassword(userPrefs.getLayoutSecondary(), true);
							break;		
						case 15:
							ActionManager.runMacro(userPrefs.getLayoutSecondary());
							break;	
						case 16:
							ActionManager.clipboardTyping(userPrefs.getLayoutSecondary());
							break;							
					}
				}
			}			
		});
	}
	
	
}
