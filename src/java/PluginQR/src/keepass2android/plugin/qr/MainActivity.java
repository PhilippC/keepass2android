package keepass2android.plugin.qr;

import keepass2android.pluginsdk.AccessManager;

import com.google.zxing.client.android.CaptureActivity;

import android.app.Activity;
import android.app.ActionBar;
import android.app.Fragment;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;
import android.view.LayoutInflater;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.view.View.OnClickListener;
import android.view.ViewGroup;
import android.widget.TextView;
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
	

	@Override
	protected void onActivityResult(int requestCode, int resultCode, Intent intent) {
	
	    if (requestCode == 0) {
	        if (resultCode == RESULT_OK) {
	            String contents = intent.getStringExtra("SCAN_RESULT");
	            Log.d("QR", contents);
	
	           // Toast.makeText(this, contents, Toast.LENGTH_SHORT).show();
	           } else if (resultCode == RESULT_CANCELED) {
	            // Handle cancel
	
	        }
	        return;
	
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
					//todo
					Log.d("QR", "ShowqR");
				}
			});
			
			mRootView.findViewById(R.id.btnConfigPlugins).setOnClickListener(new OnClickListener() {
				
				@Override
				public void onClick(View v) {
					try
					{
								
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
