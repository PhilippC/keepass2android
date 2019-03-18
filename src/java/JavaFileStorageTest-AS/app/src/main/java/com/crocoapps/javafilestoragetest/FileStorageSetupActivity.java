package com.crocoapps.javafilestoragetest;

import keepass2android.javafilestorage.JavaFileStorage;
import android.os.Bundle;
import android.app.Activity;
import android.content.Intent;
import android.util.Log;
import android.view.Menu;

public class FileStorageSetupActivity 
extends Activity implements JavaFileStorage.FileStorageSetupActivity {

	Bundle state = new Bundle();

	
	boolean isRecreated = false;
	
	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		setContentView(R.layout.activity_file_storage_setup);
		Log.d("FSSA", "onCreate");

		if (savedInstanceState != null)
		{
			isRecreated = true;
			state = (Bundle) savedInstanceState.clone();
			Log.d("FSSA", "recreating state");
			for (String key: state.keySet())
			{
				Log.d("FSSA", "state " + key + ":" +state.get(key));
			}
		}
		if (!isRecreated)
		{
			if (MainActivity.storageToTest == null)
				MainActivity.createStorageToTest(this, getApplicationContext(), false);
			MainActivity.storageToTest.onCreate(this, savedInstanceState);
		}
	}
	
	@Override
	protected void onSaveInstanceState(Bundle outState) {
		super.onSaveInstanceState(outState);
		
		outState.putAll(state);
		Log.d("FSSA", "storing state");
		for (String key: state.keySet())
		{
			Log.d("FSSA", "state " + key + ":" +state.get(key));
		}
	}
	
	@Override
	protected void onResume() {
		super.onResume();
		if (MainActivity.storageToTest == null)
		{
			Log.d("FSSA", "MainActivity.storageToTest==null!");
			MainActivity.createStorageToTest(getApplicationContext(), getApplicationContext(), false);
		}
		else
			Log.d("FSSA", "MainActivity.storageToTest is safe!");
		MainActivity.storageToTest.onResume(this);
	}
	
	@Override
	protected void onStart() {
		super.onStart();
		if (!isRecreated)
			MainActivity.storageToTest.onStart(this);
	}
	
	@Override
	protected void onActivityResult(int requestCode, int resultCode, Intent data) {
		// TODO Auto-generated method stub
		super.onActivityResult(requestCode, resultCode, data);
		
		MainActivity.storageToTest.onActivityResult(this, requestCode, resultCode, data);
	}

	@Override
	public boolean onCreateOptionsMenu(Menu menu) {
		// Inflate the menu; this adds items to the action bar if it is present.
		getMenuInflater().inflate(R.menu.file_storage_setup, menu);
		return true;
	}

	@Override
	public String getPath() {
		// TODO Auto-generated method stub
		if (getState().containsKey(JavaFileStorage.EXTRA_PATH))
			return getState().getString(JavaFileStorage.EXTRA_PATH);
		return getIntent().getStringExtra(JavaFileStorage.EXTRA_PATH);
	}

	@Override
	public String getProcessName() {
		return getIntent().getStringExtra(JavaFileStorage.EXTRA_PROCESS_NAME);
	}

	@Override
	public boolean isForSave() {
		return getIntent().getBooleanExtra(JavaFileStorage.EXTRA_IS_FOR_SAVE, false);
	}
	@Override
	public Bundle getState() {
		Log.d("FSSA", "returning state");
		for (String key: state.keySet())
		{
			Log.d("FSSA", "state " + key + ":" +state.get(key));
		}
		return state;
	}

}
