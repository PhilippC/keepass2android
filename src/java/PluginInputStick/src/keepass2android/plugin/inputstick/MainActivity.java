package keepass2android.plugin.inputstick;

import com.inputstick.api.ConnectionManager;
import com.inputstick.api.basic.InputStickHID;
import com.inputstick.api.basic.InputStickKeyboard;

import android.app.Activity;
import android.app.ActionBar;
import android.app.Fragment;
import android.content.Intent;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.view.ViewGroup;
import android.os.Build;

public class MainActivity extends Activity {

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		setContentView(R.layout.activity_main);

		if (savedInstanceState == null) {
			getFragmentManager().beginTransaction()
					.add(R.id.container, new PlaceholderFragment()).commit();
		}
	}

	@Override
	public boolean onCreateOptionsMenu(Menu menu) {

		// Inflate the menu; this adds items to the action bar if it is present.
		getMenuInflater().inflate(R.menu.main, menu);
		return true;
	}

	@Override
	public boolean onOptionsItemSelected(MenuItem item) {
		// Handle action bar item clicks here. The action bar will
		// automatically handle clicks on the Home/Up button, so long
		// as you specify a parent activity in AndroidManifest.xml.
		int id = item.getItemId();
		if (id == R.id.action_settings) {
			return true;
		}
		return super.onOptionsItemSelected(item);
	}

	/**
	 * A placeholder fragment containing a simple view.
	 */
	public static class PlaceholderFragment extends Fragment {

		public PlaceholderFragment() {
		}

		@Override
		public View onCreateView(LayoutInflater inflater, ViewGroup container,
				Bundle savedInstanceState) {
			View rootView = inflater.inflate(R.layout.fragment_main, container,
					false);
			View btn1 = rootView.findViewById(R.id.button1);
			btn1.setOnClickListener(new View.OnClickListener() {
				
				@Override
				public void onClick(View v) {
					
					String textToType = "some text to transfer";
					triggerTypeService(textToType);
					
					
				}

				
			});
			
			rootView.findViewById(R.id.button2).setOnClickListener(new View.OnClickListener() {
				
				@Override
				public void onClick(View v) {
					triggerTypeService("");
					
				}
			});
			
			return rootView;
		}
		private void triggerTypeService(String textToType) {
			Intent serviceIntent = new Intent(getActivity(), InputStickService.class);
			serviceIntent.setAction(InputStickService.TYPE);
		
			serviceIntent.putExtra(Intent.EXTRA_TEXT, textToType);
			getActivity().startService(serviceIntent);
		}
	}

}
