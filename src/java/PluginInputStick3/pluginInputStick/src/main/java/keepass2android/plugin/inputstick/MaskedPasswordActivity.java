package keepass2android.plugin.inputstick;

import android.app.Activity;
import android.graphics.Color;
import android.graphics.PorterDuff;
import android.os.Bundle;
import android.os.Handler;
import android.view.View;
import android.view.View.OnClickListener;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.Toast;

import com.inputstick.api.basic.InputStickHID;
import com.inputstick.api.layout.KeyboardLayout;

public class MaskedPasswordActivity extends Activity {
		
	private static final int BUTTONS_CNT = 16;
	
	private boolean[] wasClicked;
	private int offset;
	
	private MyButtonOnClickListener listener = new MyButtonOnClickListener();
	
	private String password;
	private KeyboardLayout layout;	
			
	private Button buttonPrev;
	private Button buttonNext;
	
	private CheckBox checkBoxShowPassword;
	private Button[] buttons;
	private int[] buttonIds = {
			R.id.buttonChar1,
			R.id.buttonChar2,
			R.id.buttonChar3,
			R.id.buttonChar4,
			R.id.buttonChar5,
			R.id.buttonChar6,
			R.id.buttonChar7,
			R.id.buttonChar8,
			R.id.buttonChar9,
			R.id.buttonChar10,
			R.id.buttonChar11,
			R.id.buttonChar12,
			R.id.buttonChar13,
			R.id.buttonChar14,
			R.id.buttonChar15,
			R.id.buttonChar16,
	};	
	
	private String timeLeftMessage;
	private static int remainingTime;
	
	private final Handler mHandler = new Handler();
	private final Runnable tick = new Runnable(){
	    public void run(){
			setTitle(timeLeftMessage + " " + (remainingTime/1000));
			if (remainingTime <= 0) {
				password = " ";
				finish();
			} else {
				remainingTime -= 1000;
				mHandler.postDelayed(this, 1000);    
			}
	    }
	};

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);		
		super.setTheme( android.R.style.Theme_Holo_Dialog);
		setContentView(R.layout.activity_masked_password);		
		
		timeLeftMessage = getString(R.string.time_left);
		
		checkBoxShowPassword = (CheckBox)findViewById(R.id.checkBoxShowPassword);
		buttonPrev = (Button)findViewById(R.id.buttonPrev);
		buttonNext = (Button)findViewById(R.id.buttonNext);
		
		checkBoxShowPassword.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				refreshButtons();
			}
		});

		buttonPrev.setOnClickListener(new OnClickListener() {
			public void onClick(View v) {
				if (offset >= BUTTONS_CNT) {
					offset -= BUTTONS_CNT;
					refreshButtons();
				}
			}
		});
		
		buttonNext.setOnClickListener(new OnClickListener() {
			public void onClick(View v) {
				offset += BUTTONS_CNT;
				refreshButtons();				
			}
		});		
		
		buttons = new Button[16];
		for (int i = 0; i < 16; i++) {
			buttons[i] = (Button)findViewById(buttonIds[i]);
			buttons[i].setOnClickListener(listener);
		}

		Bundle b = getIntent().getExtras();
		if (b != null) {				
			password = b.getString(Const.EXTRA_TEXT, " ");
			layout = KeyboardLayout.getLayout(b.getString(Const.EXTRA_LAYOUT, "en-US"));
			wasClicked = new boolean[password.length()];			
		}
	
		if (savedInstanceState == null) {			
			remainingTime = Const.MASKED_PASSWORD_TIMEOUT_MS;
			mHandler.post(tick);
		} else {	
			offset = savedInstanceState.getInt("offset");	
			wasClicked = savedInstanceState.getBooleanArray("clicked");
			mHandler.post(tick);
		}
		
		
		
	}
	
	@Override
	public void onSaveInstanceState(Bundle savedInstanceState) {
	    savedInstanceState.putInt("offset", offset);
	    savedInstanceState.putBooleanArray("clicked", wasClicked);
	    super.onSaveInstanceState(savedInstanceState);
	}
	
	@Override
	protected void onResume() {
		super.onResume();		
		refreshButtons();
	}
	
	@Override
	protected void onDestroy() {
	      super.onDestroy();
	      mHandler.removeCallbacks(tick);
	}	
	
	private void drawButton(int i) {
		Button b = buttons[i];
		int index = i + offset;
		boolean enabled = true;
		int color = Color.WHITE;
		
		if (index >= password.length()) {
			b.setText("");
			enabled = false;			
		} else {
			if (checkBoxShowPassword.isChecked()) {
				b.setText(String.valueOf(password.charAt(index)));
			} else {
				b.setText(String.valueOf(index + 1));
			}
			
			if (wasClicked[index]) {
				color = Color.GREEN;
			} 
		}
		
		b.getBackground().setColorFilter(color, PorterDuff.Mode.MULTIPLY );
		b.setEnabled(enabled);
	}
	
	private void refreshButtons() {
		if ((offset + BUTTONS_CNT) >= password.length()) {
			buttonNext.setEnabled(false);
		} else {
			buttonNext.setEnabled(true);
		}
		if (offset == 0) {
			buttonPrev.setEnabled(false);
		} else {
			buttonPrev.setEnabled(true);
		}		
		
		for (int i = 0; i < BUTTONS_CNT; i++) {
			drawButton(i);
		}
	}
	
	private void type(int n) {
		if (password != null) {
			if (password.length() >= n) {
				int index = n;
				if (index < 0) return;
				char c = password.charAt(index);
				String toType = String.valueOf(c);
				if ((InputStickHID.isReady()) && (layout != null)) {
					layout.type(toType);
				} else {
					Toast.makeText(this, R.string.not_ready, Toast.LENGTH_SHORT).show();
				}
			}
		}
	}
	
	private class MyButtonOnClickListener implements OnClickListener {
		@Override
		public void onClick(View v) {
			for (int i = 0; i < 16; i++) {
				if (buttons[i].equals(v)) {
					type(i + offset);
					wasClicked[i + offset] = true;
					v.getBackground().setColorFilter(Color.GREEN, PorterDuff.Mode.MULTIPLY );
				}
			}			
		}		
	}

}
