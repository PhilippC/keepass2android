package keepass2android.plugin.inputstick;

import java.util.ArrayList;
import java.util.List;

import android.app.Activity;
import android.os.Bundle;
import android.view.View;
import android.view.View.OnClickListener;
import android.widget.Button;
import android.widget.TextView;
import android.widget.Toast;

public class MacroExecuteActivity extends Activity {
	
	private long lastActionTime;
	private long maxTime;
	
	private String layoutName;
	private List<String> actions;
	private int index;
	
	private Button buttonActionExecute;
	private Button buttonActionPrev;
	private Button buttonActionNext;
	private TextView textViewActionPreview;

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		super.setTheme( android.R.style.Theme_Holo_Dialog);
		setContentView(R.layout.activity_macro_execute);
		
		maxTime = getIntent().getLongExtra(Const.EXTRA_MAX_TIME, 0);
		lastActionTime = System.currentTimeMillis();
		
		textViewActionPreview = (TextView)findViewById(R.id.textViewActionPreview);
		
		buttonActionExecute = (Button) findViewById(R.id.buttonActionExecute);
		buttonActionExecute.setOnClickListener(new OnClickListener() {
			public void onClick(View v) {
				if (index >= actions.size()) {
					finish();
				} else {
					if (checkTime()) {
						ActionManager.runMacroAction(layoutName, actions.get(index));
						goToNext();
					}
				}
			}
		});
		
		buttonActionPrev = (Button)findViewById(R.id.buttonActionPrev);
		buttonActionPrev.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				if (checkTime()) {
					goToPrev();
				}
			}
		});	
		
		buttonActionNext = (Button)findViewById(R.id.buttonActionNext);
		buttonActionNext.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				if (checkTime()) {
					goToNext();
				}
			}
		});	
		
		layoutName = getIntent().getStringExtra(Const.EXTRA_LAYOUT);
		String tmp[] = getIntent().getStringArrayExtra(Const.EXTRA_MACRO_ACTIONS);
		if (tmp == null) finish();
		
		actions = new ArrayList<String>();
		for (String s : tmp) {
			if ((s != null) && (s.length() > 0)) {			
				if (( !s.startsWith(MacroHelper.MACRO_ACTION_DELAY)) && ( !s.startsWith(MacroHelper.MACRO_BACKGROUND_EXEC_STRING))) {
					actions.add(s);
				}
			}
		}				
		index = 0;
		manageUI();		
	}
	
	private void goToPrev() {
		if (index > 0) {
			index--;
			manageUI();
		}			
	}
	
	private void goToNext() {
		if (index < actions.size()) {
			index++;
			manageUI();
		}		
	}
	
	private void manageUI() {
		if (index >= actions.size()) {
			textViewActionPreview.setText(R.string.end);
			buttonActionExecute.setText(R.string.done);
			buttonActionNext.setEnabled(false);
		} else {			
			textViewActionPreview.setText(getString(R.string.current_position) + " " + (index + 1) + "/" + actions.size());			
			textViewActionPreview.append("\n" + getString(R.string.preview) + "\n" + actions.get(index));
			buttonActionExecute.setText(R.string.execute);
			buttonActionNext.setEnabled(true);
		}
		if (index == 0) {
			buttonActionPrev.setEnabled(false);
		} else {
			buttonActionPrev.setEnabled(true);
		}
	}
	
	private boolean checkTime() {
		long now = System.currentTimeMillis();
		if (now > maxTime) {
			Toast.makeText(this, R.string.text_locked, Toast.LENGTH_LONG).show();
			return false;
		} else {
			maxTime += (now - lastActionTime);
			lastActionTime = now;
			return true;
		}
	}
	
}
