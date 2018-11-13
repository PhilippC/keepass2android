package keepass2android.plugin.qr;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import keepass2android.pluginsdk.AccessManager;
import keepass2android.pluginsdk.KeepassDefs;
import keepass2android.pluginsdk.Kp2aControl;
import keepass2android.pluginsdk.Strings;

import com.google.zxing.client.android.CaptureActivity;

import android.app.Activity;
import android.app.ActionBar;
import android.app.AlertDialog;
import android.app.AlertDialog.Builder;
import android.app.Fragment;
import android.content.ActivityNotFoundException;
import android.content.DialogInterface;
import android.content.Intent;
import android.os.Bundle;
import android.text.TextUtils;
import android.util.Log;
import android.view.LayoutInflater;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.view.View.OnClickListener;
import android.view.ViewGroup;
import android.widget.TextView;
import android.widget.Toast;
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
		
		new Builder(this)
		.setMessage(R.string.about_msg)
		.setPositiveButton(android.R.string.ok, new DialogInterface.OnClickListener() {

			@Override
			public void onClick(DialogInterface dialog, int which) {
				// TODO Auto-generated method stub
				
			}
			

		})
		.create().show();
		
		return true;
	}


	@Override
	protected void onActivityResult(int requestCode, int resultCode, Intent intent) {
	
	    if (requestCode == 0) {
	        if (resultCode == RESULT_OK) {
	            final String contents = intent.getStringExtra("SCAN_RESULT");
	            if (contents.startsWith("kp2a:"))
	            {
	            	//received a full entry
	            	String entryText = contents.substring("kp2a:".length());
	            	try
		            {
		            	JSONObject json = new JSONObject(entryText);
		            	String outputData = json.get("fields").toString();
		            
		            	String protectedFields = null;
		            	if (json.has("p"))
		            		protectedFields = json.get("p").toString();
		            	
		            	ArrayList<String> protectedFieldsList = null;
		            	if (!TextUtils.isEmpty(protectedFields))
		        		{
		        			JSONArray protectedFieldsJson = new JSONArray(protectedFields);
		        			protectedFieldsList = new ArrayList<String>();
		        			for (int i=0; i<protectedFieldsJson.length(); i++) {
		        			    protectedFieldsList.add( protectedFieldsJson.getString(i) );
		        			}
		        		}
		            	
		            	Intent startKp2aIntent = Kp2aControl.getAddEntryIntent(outputData, protectedFieldsList);
		            	startActivity(startKp2aIntent);
		            }
		            catch (JSONException e)
		            {
		            	e.printStackTrace();
		            	Toast.makeText(this, "Error reading entry", Toast.LENGTH_SHORT).show();
		            }	
	            	catch (ActivityNotFoundException e)
	            	{
	            		Toast.makeText(this, getString(R.string.no_host_app), Toast.LENGTH_SHORT).show();
	            	}
	            }
	            else
	            {
	            	//received some text
	            	AlertDialog.Builder b = new Builder(this);
	            	b.setMessage(R.string.qr_question)
	            	.setPositiveButton(R.string.create_entry, new DialogInterface.OnClickListener() {
						
						@Override
						public void onClick(DialogInterface dialog, int which) {
							try
				            {
				            	HashMap<String, String> fields = new HashMap<String, String>();
				            	if ((contents.startsWith("http://")) || (contents.startsWith("https://"))
				            			|| (contents.startsWith("ftp://")))
		            			{
				            		fields.put(KeepassDefs.UrlField, contents);
		            			}
				            	else
				            	{
				            		fields.put(KeepassDefs.PasswordField, contents);
				            	}
				            	
				            	Intent startKp2aIntent = Kp2aControl.getAddEntryIntent(fields, null);
				            	startActivity(startKp2aIntent);
				            }
				            catch (ActivityNotFoundException e)
			            	{
			            		Toast.makeText(MainActivity.this, R.string.no_host_app, Toast.LENGTH_SHORT).show();
			            	} 
							
						}
					})
					.setNegativeButton(R.string.search_entry, new DialogInterface.OnClickListener() {
						
						@Override
						public void onClick(DialogInterface dialog, int which) {
							try
				            {
				            	Intent startKp2aIntent = Kp2aControl.getOpenEntryIntent(contents, true, false);				            	
				            	startActivity(startKp2aIntent);
				            }
				            catch (ActivityNotFoundException e)
			            	{
			            		Toast.makeText(MainActivity.this, R.string.no_host_app, Toast.LENGTH_SHORT).show();
			            	} 
							
						}
					}).create().show();
	            	
	            	
	            	
	            }
	            
	           	} else if (resultCode == RESULT_CANCELED) {
	            // Handle cancel
	
	        }
	        return;
	
	    }
	    if (requestCode == 124) {
	    	if (resultCode == RESULT_OK)
	    	{
	    		if (intent != null)
	    		{
	    			Intent i = new Intent(this, QRActivity.class);
	    			i.putExtra(Strings.EXTRA_ENTRY_OUTPUT_DATA, intent.getStringExtra(Strings.EXTRA_ENTRY_OUTPUT_DATA));
	    			i.putExtra(Strings.EXTRA_PROTECTED_FIELDS_LIST, intent.getStringExtra(Strings.EXTRA_PROTECTED_FIELDS_LIST));
	    			i.putExtra(Strings.EXTRA_SENDER, intent.getStringExtra(Strings.EXTRA_SENDER));
	    			i.putExtra(Strings.EXTRA_ENTRY_ID, intent.getStringExtra(Strings.EXTRA_ENTRY_ID));
	    			startActivity(i);		
	    		}
	    		
	    	}
	    	else
	    	{
	    		Log.d("QR", "No data received :-(");
	    	}
	    	
	    }
	    super.onActivityResult(requestCode, resultCode, intent);
	
	}

	
	/**
	 * A placeholder fragment containing a simple view.
	 */
	public static class PlaceholderFragment extends Fragment {

		private View mRootView;

		public PlaceholderFragment() {
		}
		
		@Override
		public void onResume() {
			
			mRootView.findViewById(R.id.progressBar1).setVisibility(View.GONE);
			if (AccessManager.getAllHostPackages(getActivity()).isEmpty())
			{
				((TextView)mRootView.findViewById(R.id.tvHostStatus)).setText(getString(R.string.not_enabled_as_plugin));
				mRootView.findViewById(R.id.btnConfigPlugins).setVisibility(View.VISIBLE);
			}
			else
			{
				((TextView)mRootView.findViewById(R.id.tvHostStatus)).setText(getString(R.string.enabled_as_plugin));
				mRootView.findViewById(R.id.btnConfigPlugins).setVisibility(View.INVISIBLE);
			}
			
			super.onResume();
		}

		@Override
		public View onCreateView(LayoutInflater inflater, ViewGroup container,
				Bundle savedInstanceState) {
			mRootView = inflater.inflate(R.layout.fragment_main, container,
					false);
			
			mRootView.findViewById(R.id.btnScanQRCode).setOnClickListener(new OnClickListener() {
				
				@Override
				public void onClick(View v) {
					mRootView.findViewById(R.id.progressBar1).setVisibility(View.VISIBLE);
					Intent i = new Intent(getActivity(), CaptureActivity.class);
					i.setAction("com.google.zxing.client.android.SCAN");
					getActivity().startActivityForResult(i, 0);
					
				}
			});
			
			
			mRootView.findViewById(R.id.btnShowQR).setOnClickListener(new OnClickListener() {
				
				@Override
				public void onClick(View v) {
					getActivity().startActivityForResult(Kp2aControl.getQueryEntryIntent(null),124);
					Log.d("QR", "ShowqR");
				}
			});
			
			mRootView.findViewById(R.id.btnConfigPlugins).setOnClickListener(new OnClickListener() {
				
				@Override
				public void onClick(View v) {
					try
					{
						Intent i = new Intent(Strings.ACTION_EDIT_PLUGIN_SETTINGS);
						i.putExtra(Strings.EXTRA_PLUGIN_PACKAGE, getActivity().getPackageName());
						startActivityForResult(i, 123);
					}
					catch(Exception e)
					{
						e.printStackTrace();
					}

					
				}
			});
			
			return mRootView;
		}
	}

}
