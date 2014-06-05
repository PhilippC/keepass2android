package keepass2android.plugin.qr;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashMap;
import java.util.Iterator;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import keepass2android.pluginsdk.AccessManager;
import keepass2android.pluginsdk.KeepassDefs;
import keepass2android.pluginsdk.Kp2aControl;
import keepass2android.pluginsdk.Strings;

import com.google.zxing.BarcodeFormat;
import com.google.zxing.WriterException;

import android.animation.Animator;
import android.animation.AnimatorListenerAdapter;
import android.animation.AnimatorSet;
import android.animation.ObjectAnimator;
import android.app.Activity;
import android.app.ActionBar;
import android.app.Fragment;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager.NameNotFoundException;
import android.content.res.Resources;
import android.graphics.Bitmap;
import android.graphics.Point;
import android.graphics.Rect;
import android.os.Bundle;
import android.text.TextUtils;
import android.util.DisplayMetrics;
import android.util.Log;
import android.util.TypedValue;
import android.view.Display;
import android.view.LayoutInflater;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.view.WindowManager;
import android.view.View.OnClickListener;
import android.view.ViewGroup;
import android.view.animation.DecelerateInterpolator;
import android.widget.Adapter;
import android.widget.AdapterView;
import android.widget.AdapterView.OnItemSelectedListener;
import android.widget.ArrayAdapter;
import android.widget.CheckBox;
import android.widget.CompoundButton;
import android.widget.CompoundButton.OnCheckedChangeListener;
import android.widget.ImageView;
import android.widget.Spinner;
import android.widget.TextView;
import android.os.Build;
import android.preference.Preference;
import android.preference.PreferenceManager;

public class QRActivity extends Activity {

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		if ((getIntent() != null) && (getIntent().getStringExtra(Strings.EXTRA_ENTRY_OUTPUT_DATA)!= null))
			setContentView(R.layout.activity_qr);

		if (savedInstanceState == null) {
			getFragmentManager().beginTransaction()
					.add(R.id.container, new PlaceholderFragment()).commit();
		}
		
		
	}

	@Override
	public boolean onCreateOptionsMenu(Menu menu) {

		// Inflate the menu; this adds items to the action bar if it is present.
		getMenuInflater().inflate(R.menu.qr, menu);
		return true;
	}

	/**
	 * A placeholder fragment containing a simple view.
	 */
	public static class PlaceholderFragment extends Fragment {
		
		// Hold a reference to the current animator,
	    // so that it can be canceled mid-way.
	    private Animator mCurrentAnimator;


		private int mShortAnimationDuration;
		
		Bitmap mBitmap;
		ImageView mImageView;
		TextView mErrorView;
		HashMap<String, String> mEntryOutput;
		
		//JSON-Array with field keys of the protected strings.
		//We don't need that list (so don't deserialize) other than for 
		//forwarding to KP2A
		String mProtectedFieldsList;
		
		ArrayList<String> mFieldList = new ArrayList<String>();
		Spinner mSpinner;
		String mHostname;

		private CheckBox mCbIncludeLabel;


		private Resources kp2aRes;

		public PlaceholderFragment() {
		}
		
		
		
		@Override
		public View onCreateView(LayoutInflater inflater, ViewGroup container,
				Bundle savedInstanceState) {
			View rootView = inflater.inflate(R.layout.fragment_qr, container,
					false);
			
			mSpinner = (Spinner) rootView.findViewById(R.id.spinner);
			
			mEntryOutput = Kp2aControl.getEntryFieldsFromIntent(getActivity().getIntent());
			mProtectedFieldsList = getProtectedFieldsList(getActivity().getIntent());
			
			ArrayList<String> spinnerItems = new ArrayList<String>();
			spinnerItems.add(getActivity().getString(R.string.all_fields));
			mFieldList.add(null); //all fields
			
			try {
				mHostname = getActivity().getIntent().getStringExtra(Strings.EXTRA_SENDER);
				kp2aRes = getActivity().getPackageManager().getResourcesForApplication(mHostname);
			} catch (NameNotFoundException e) {
				// TODO Auto-generated catch block
				e.printStackTrace();
			}
			
			addIfExists(KeepassDefs.UserNameField, "entry_user_name", spinnerItems);
			addIfExists(KeepassDefs.UrlField, "entry_url", spinnerItems);
			addIfExists(KeepassDefs.PasswordField, "entry_password", spinnerItems);
			addIfExists(KeepassDefs.TitleField, "entry_title", spinnerItems);
			addIfExists(KeepassDefs.NotesField, "entry_comment", spinnerItems);
			
			//add non-standard fields:
			ArrayList<String> allKeys = new ArrayList<String>(mEntryOutput.keySet());
			Collections.sort(allKeys);
			
			for (String k: allKeys)
			{
				if (!KeepassDefs.IsStandardField(k))
				{
					if (!TextUtils.isEmpty(mEntryOutput.get(k)))
					mFieldList.add(k);
					spinnerItems.add(k);
				}
			}
			
			mCbIncludeLabel = (CheckBox)rootView.findViewById(R.id.cbIncludeLabel);
			
			boolean includeLabel = PreferenceManager.getDefaultSharedPreferences(getActivity()).getBoolean("includeLabels", false);
			mCbIncludeLabel.setChecked(includeLabel);
			mCbIncludeLabel.setOnCheckedChangeListener(new OnCheckedChangeListener() {
				
				@Override
				public void onCheckedChanged(CompoundButton buttonView, boolean isChecked) {
					PreferenceManager.getDefaultSharedPreferences(getActivity()).edit().putBoolean("includeLabels", isChecked);
					updateQrCode(buildQrData(mFieldList.get( mSpinner.getSelectedItemPosition() )));
				}
			});
			
			ArrayAdapter<String> adapter = new ArrayAdapter<String>(getActivity(), android.R.layout.simple_spinner_item, spinnerItems);
			adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
			mSpinner.setAdapter(adapter);
			
			mImageView = ((ImageView)rootView.findViewById(R.id.qrView));
			mErrorView = ((TextView)rootView.findViewById(R.id.tvError));
			String fieldId = null;
			
			if (getActivity().getIntent() != null)
			{
				fieldId = getActivity().getIntent().getStringExtra(Strings.EXTRA_FIELD_ID);
				if (fieldId != null)
				{
					fieldId = fieldId.substring(Strings.PREFIX_STRING.length());
				}
			}
			updateQrCode(buildQrData(fieldId));
			
			mImageView.setOnClickListener(new OnClickListener() {
				
				@Override
				public void onClick(View v) {
					zoomImageFromThumb();
				}
			});

			mSpinner.setOnItemSelectedListener(new OnItemSelectedListener() {

				@Override
				public void onItemSelected(AdapterView<?> arg0, View arg1,
						int arg2, long arg3) {
					if  (arg2 != 0)
						mCbIncludeLabel.setVisibility(View.VISIBLE);
					else
						mCbIncludeLabel.setVisibility(View.GONE);
					updateQrCode(buildQrData(mFieldList.get(arg2)));
				}

				@Override
				public void onNothingSelected(AdapterView<?> arg0) {
					
				}
			});
			
			mSpinner.setSelection(mFieldList.indexOf(fieldId));
			
			mShortAnimationDuration = getResources().getInteger(
	                android.R.integer.config_shortAnimTime);
			
		

			return rootView;
		}

		private String getProtectedFieldsList(Intent intent) {
			if (intent == null)
				return null;
			return intent.getStringExtra(Strings.EXTRA_PROTECTED_FIELDS_LIST);
		}

		private void addIfExists(String fieldKey, String resKey,
				ArrayList<String> spinnerItems) {
			if (!TextUtils.isEmpty(mEntryOutput.get(fieldKey)))
			{
				mFieldList.add(fieldKey);
				String displayString = fieldKey;
				try
				{
					displayString = kp2aRes.getString(kp2aRes.getIdentifier(resKey, "string", mHostname));
				}
				catch (Exception e)
				{
					e.printStackTrace();
				}
				spinnerItems.add(displayString);
			}
			

		}

		private String buildQrData(String fieldId) {
			String res = "";

			if (fieldId == null)
			{
				
				try {
					JSONObject json = new JSONObject();
					json.put("fields", new JSONObject(mEntryOutput));
					if (!TextUtils.isEmpty(mProtectedFieldsList))
					{
						json.put("p", new JSONArray(mProtectedFieldsList));
					}
					res = "kp2a:"+json.toString();
				} catch (JSONException e) {
					res = "error: " + e.toString();
				}
				
			}
			else
			{
				if ((mCbIncludeLabel.isChecked()))
				{
					res = fieldId+": ";
				}
				res += mEntryOutput.get(fieldId);
			}
			
			return res;
		}

		private void updateQrCode(String qrData) {
			DisplayMetrics displayMetrics = new DisplayMetrics();
			WindowManager wm = (WindowManager) getActivity().getSystemService(Context.WINDOW_SERVICE); // the results will be higher than using the activity context object or the getWindowManager() shortcut
			wm.getDefaultDisplay().getMetrics(displayMetrics);
			int screenWidth = displayMetrics.widthPixels;
			int screenHeight = displayMetrics.heightPixels;
			
			int qrCodeDimension = screenWidth > screenHeight ? screenHeight : screenWidth;
			QRCodeEncoder qrCodeEncoder = new QRCodeEncoder(qrData, null,
			        Contents.Type.TEXT, BarcodeFormat.QR_CODE.toString(), qrCodeDimension);

			

			try {
			    mBitmap = qrCodeEncoder.encodeAsBitmap();
			    mImageView.setImageBitmap(mBitmap);
			    mImageView.setVisibility(View.VISIBLE);
			    mErrorView.setVisibility(View.GONE);
			} catch (WriterException e) {
			    e.printStackTrace();
			    mErrorView.setText("Error: "+e.getMessage());
			    mErrorView.setVisibility(View.VISIBLE);
			    mImageView.setVisibility(View.GONE);
			}
		}
		
		private void zoomImageFromThumb() {
		    // If there's an animation in progress, cancel it
		    // immediately and proceed with this one.
		    if (mCurrentAnimator != null) {
		        mCurrentAnimator.cancel();
		    }

		    // Load the high-resolution "zoomed-in" image.
		    final ImageView expandedImageView = (ImageView) getActivity().findViewById(
		            R.id.expanded_image);
		    expandedImageView.setImageBitmap(mBitmap);

		    // Calculate the starting and ending bounds for the zoomed-in image.
		    // This step involves lots of math. Yay, math.
		    final Rect startBounds = new Rect();
		    final Rect finalBounds = new Rect();
		    final Point globalOffset = new Point();

		    // The start bounds are the global visible rectangle of the thumbnail,
		    // and the final bounds are the global visible rectangle of the container
		    // view. Also set the container view's offset as the origin for the
		    // bounds, since that's the origin for the positioning animation
		    // properties (X, Y).
		    mImageView.getGlobalVisibleRect(startBounds);
		    getActivity().findViewById(R.id.container)
		            .getGlobalVisibleRect(finalBounds, globalOffset);
		    startBounds.offset(-globalOffset.x, -globalOffset.y);
		    finalBounds.offset(-globalOffset.x, -globalOffset.y);

		    // Adjust the start bounds to be the same aspect ratio as the final
		    // bounds using the "center crop" technique. This prevents undesirable
		    // stretching during the animation. Also calculate the start scaling
		    // factor (the end scaling factor is always 1.0).
		    float startScale;
		    if ((float) finalBounds.width() / finalBounds.height()
		            > (float) startBounds.width() / startBounds.height()) {
		        // Extend start bounds horizontally
		        startScale = (float) startBounds.height() / finalBounds.height();
		        float startWidth = startScale * finalBounds.width();
		        float deltaWidth = (startWidth - startBounds.width()) / 2;
		        startBounds.left -= deltaWidth;
		        startBounds.right += deltaWidth;
		    } else {
		        // Extend start bounds vertically
		        startScale = (float) startBounds.width() / finalBounds.width();
		        float startHeight = startScale * finalBounds.height();
		        float deltaHeight = (startHeight - startBounds.height()) / 2;
		        startBounds.top -= deltaHeight;
		        startBounds.bottom += deltaHeight;
		    }

		    // Hide the thumbnail and show the zoomed-in view. When the animation
		    // begins, it will position the zoomed-in view in the place of the
		    // thumbnail.
		    mImageView.setAlpha(0f);
		    expandedImageView.setVisibility(View.VISIBLE);

		    // Set the pivot point for SCALE_X and SCALE_Y transformations
		    // to the top-left corner of the zoomed-in view (the default
		    // is the center of the view).
		    expandedImageView.setPivotX(0f);
		    expandedImageView.setPivotY(0f);

		    // Construct and run the parallel animation of the four translation and
		    // scale properties (X, Y, SCALE_X, and SCALE_Y).
		    AnimatorSet set = new AnimatorSet();
		    set
		            .play(ObjectAnimator.ofFloat(expandedImageView, View.X,
		                    startBounds.left, finalBounds.left))
		            .with(ObjectAnimator.ofFloat(expandedImageView, View.Y,
		                    startBounds.top, finalBounds.top))
		            .with(ObjectAnimator.ofFloat(expandedImageView, View.SCALE_X,
		            startScale, 1f)).with(ObjectAnimator.ofFloat(expandedImageView,
		                    View.SCALE_Y, startScale, 1f));
		    set.setDuration(mShortAnimationDuration);
		    set.setInterpolator(new DecelerateInterpolator());
		    set.addListener(new AnimatorListenerAdapter() {
		        @Override
		        public void onAnimationEnd(Animator animation) {
		            mCurrentAnimator = null;
		        }

		        @Override
		        public void onAnimationCancel(Animator animation) {
		            mCurrentAnimator = null;
		        }
		    });
		    set.start();
		    mCurrentAnimator = set;

		    // Upon clicking the zoomed-in image, it should zoom back down
		    // to the original bounds and show the thumbnail instead of
		    // the expanded image.
		    final float startScaleFinal = startScale;
		    expandedImageView.setOnClickListener(new View.OnClickListener() {
		        @Override
		        public void onClick(View view) {
		            if (mCurrentAnimator != null) {
		                mCurrentAnimator.cancel();
		            }

		            // Animate the four positioning/sizing properties in parallel,
		            // back to their original values.
		            AnimatorSet set = new AnimatorSet();
		            set.play(ObjectAnimator
		                        .ofFloat(expandedImageView, View.X, startBounds.left))
		                        .with(ObjectAnimator
		                                .ofFloat(expandedImageView, 
		                                        View.Y,startBounds.top))
		                        .with(ObjectAnimator
		                                .ofFloat(expandedImageView, 
		                                        View.SCALE_X, startScaleFinal))
		                        .with(ObjectAnimator
		                                .ofFloat(expandedImageView, 
		                                        View.SCALE_Y, startScaleFinal));
		            set.setDuration(mShortAnimationDuration);
		            set.setInterpolator(new DecelerateInterpolator());
		            set.addListener(new AnimatorListenerAdapter() {
		                @Override
		                public void onAnimationEnd(Animator animation) {
		                	mImageView.setAlpha(1f);
		                    expandedImageView.setVisibility(View.GONE);
		                    mCurrentAnimator = null;
		                }

		                @Override
		                public void onAnimationCancel(Animator animation) {
		                	mImageView.setAlpha(1f);
		                    expandedImageView.setVisibility(View.GONE);
		                    mCurrentAnimator = null;
		                }
		            });
		            set.start();
		            mCurrentAnimator = set;
		        }
		    });
		}
	}

}
