package keepass2android.plugina;
import java.util.ArrayList;
import java.util.HashMap;

import keepass2android.pluginsdk.KeepassDefs;
import keepass2android.pluginsdk.Kp2aControl;
import android.app.Activity;
import android.app.Fragment;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.view.View.OnClickListener;
import android.view.ViewGroup;
import android.widget.Toast;

public class PlugInA extends Activity {

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		setContentView(R.layout.activity_plug_in);

		if (savedInstanceState == null) {
			getFragmentManager().beginTransaction()
					.add(R.id.container, new PlaceholderFragment()).commit();
		}
	}

	@Override
	public boolean onCreateOptionsMenu(Menu menu) {

		// Inflate the menu; this adds items to the action bar if it is present.
		getMenuInflater().inflate(R.menu.plug_in, menu);
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

		private static final String PLUGIN_A_PASSPHRASE = "PluginA passphrase";

		public PlaceholderFragment() {
		}

		@Override
		public View onCreateView(LayoutInflater inflater, ViewGroup container,
				Bundle savedInstanceState) {
			View rootView = inflater.inflate(R.layout.fragment_plug_in,
					container, false);

			rootView.findViewById(R.id.btnQuery).setOnClickListener(
					new OnClickListener() {

						@Override
						public void onClick(View v) {
							try {
								PlaceholderFragment.this.startActivityForResult(
										Kp2aControl
												.getQueryEntryIntentForOwnPackage(),
										1);
							} catch (ActivityNotFoundException e) {
								Toast.makeText(
										PlaceholderFragment.this.getActivity(),
										"no KP2A host app found",
										Toast.LENGTH_SHORT).show();
							}

						}
					});
			rootView.findViewById(R.id.btnAdd).setOnClickListener(
					new OnClickListener() {

						@Override
						public void onClick(View v) {
							
							
							
							try {
								HashMap<String, String> fields = new HashMap<String, String>();
								//standard fields
								fields.put(KeepassDefs.TitleField, "plugin A");
								fields.put(KeepassDefs.UserNameField, "John Doe");
								fields.put(KeepassDefs.PasswordField, "top secret");
								//associate entry with our app. If we would require the URL field for a web URL,
								//this string could be added in any other (e.g. a custom) field 
								fields.put(KeepassDefs.UrlField, "androidapp://"+getActivity().getPackageName()); 
								//custom field:
								fields.put(PLUGIN_A_PASSPHRASE, "some long text");
								//mark custom field as protected (i.e. display masked, enable memory protection in Keepass2)
								ArrayList<String> protectedFields = new ArrayList<String>();
								protectedFields.add(PLUGIN_A_PASSPHRASE);
								
								//add to KP2A
								PlaceholderFragment.this.startActivityForResult(
										Kp2aControl
												.getAddEntryIntent(fields, protectedFields),
										2);
							} catch (ActivityNotFoundException e) {
								Toast.makeText(
										PlaceholderFragment.this.getActivity(),
										"no KP2A host app found",
										Toast.LENGTH_SHORT).show();
							}

						}
					});

			return rootView;
		}
		
		@Override
		public void onActivityResult(int requestCode, int resultCode,
				Intent data) {
			super.onActivityResult(requestCode, resultCode, data);
			
			if ((requestCode == 1) //queryEntry for own package
					&& (resultCode == RESULT_OK)) // ensure user granted access and selected something
			{
				HashMap<String, String> credentials = Kp2aControl.getEntryFieldsFromIntent(data);
				if (!credentials.isEmpty())
				{
					//here we go!
					Toast.makeText(getActivity(), "retrieved credenitals! Username="+credentials.get(KeepassDefs.UserNameField), Toast.LENGTH_LONG).show();
				}
			}
		}
	}

}
