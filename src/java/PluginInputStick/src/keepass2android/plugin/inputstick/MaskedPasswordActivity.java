package keepass2android.plugin.inputstick;

import java.util.Timer;
import java.util.TimerTask;

import android.app.Activity;
import android.graphics.Color;
import android.graphics.PorterDuff;
import android.os.Bundle;
import android.os.Handler;
import android.os.Message;
import android.view.View;
import android.view.View.OnClickListener;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.Toast;

import com.inputstick.api.basic.InputStickHID;
import com.inputstick.api.layout.KeyboardLayout;

public class MaskedPasswordActivity extends Activity /*implements InputStickStateListener*/ {
	
	private static final int TIME = 120;  //activity will be available for max TIME [s] 
	private static final int BUTTONS_CNT = 16;
	
	private boolean[] wasClicked;
	
	private MyButtonOnClickListener listener = new MyButtonOnClickListener();
	
	private static String password;
	private static KeyboardLayout layout;	
	private static int offset;
	
	
	
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
	
	private static int remainingTime;
	private static MaskedPasswordActivity me;
	private static Timer timer;
	
	protected static void startTimer() {
		remainingTime = TIME;
		if (timer == null) {		
		    timer = new Timer();		    
		    timer.scheduleAtFixedRate(new TimerTask() {
		        public void run() {
		            if (remainingTime > 0) {
		            	remainingTime--;
		            	mHandler.obtainMessage(1).sendToTarget();
		            } else {
		            	if (timer != null) {		            
		            		timer.cancel();
		            		timer.purge();		            		
		            		timer = null;
		            	}
		            }		            	
		        }
		    }, 1000, 1000);
		} 
	};

	private static final Handler mHandler = new Handler() {
	    public void handleMessage(Message msg) {
	    	if (me != null) {
		    	me.setTitle("Time left: " + remainingTime);
		    	if (remainingTime == 0) {
		    		password = " ";		    	
	    			me.finish();
		    	}
	    	}
	    }
	};

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);		
		
		if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.HONEYCOMB){
			super.setTheme( android.R.style.Theme_Holo_Dialog);
		}
		setContentView(R.layout.activity_masked_password);		
		
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
			
		me = this;		

		Bundle b = getIntent().getExtras();
		if (b != null) {				
			password = b.getString(Const.EXTRA_TEXT, " ");
			layout = KeyboardLayout.getLayout(b.getString(Const.EXTRA_LAYOUT, "en-US"));
			wasClicked = new boolean[password.length()];			
		}
	
		if (savedInstanceState == null) {			
			setTitle("Time left: " + TIME);
			startTimer();
		} else {	
			offset = savedInstanceState.getInt("offset");	
			wasClicked = savedInstanceState.getBooleanArray("clicked");
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
		//InputStickHID.addStateListener(this);
		refreshButtons();
	}
	
	@Override
	protected void onPause() {	
		//InputStickHID.removeStateListener(this);	
	    super.onPause();	    	    
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
				b.setText(String.valueOf(index));
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
				//System.out.println("TYPE: "+toType);
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

	/*@Override
	public void onStateChanged(int state) {
		refreshButtons();
	}*/	
}
