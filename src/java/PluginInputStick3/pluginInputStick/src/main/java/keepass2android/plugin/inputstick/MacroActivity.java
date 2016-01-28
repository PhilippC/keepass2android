package keepass2android.plugin.inputstick;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.preference.PreferenceManager;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.View;
import android.view.View.OnClickListener;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.CompoundButton;
import android.widget.CompoundButton.OnCheckedChangeListener;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.RadioButton;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

public class MacroActivity extends Activity {
	
	private SharedPreferences prefs;
	
	private String macro;
	private String id;
	
	private EditText editTextMacro;
	private EditText editTextString;
	private Spinner spinnerDelay;
	private Button buttonDelete;
	private Button buttonSave;
	private RadioButton radioButtonBackground;
	private RadioButton radioButtonShowControls;
	

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		setContentView(R.layout.activity_macro);
		
		prefs = PreferenceManager.getDefaultSharedPreferences(this);
		
		editTextMacro = (EditText)findViewById(R.id.editTextMacro);
		editTextString = (EditText)findViewById(R.id.editTextString);
		spinnerDelay = (Spinner)findViewById(R.id.spinnerDelay);		
		
		
		radioButtonBackground = (RadioButton)findViewById(R.id.radioButtonBackground);
		radioButtonBackground.setOnCheckedChangeListener(new OnCheckedChangeListener() {
			@Override
			public void onCheckedChanged(CompoundButton arg0, boolean isChecked) {
				if (isChecked) {
					setExecutionMode(true);
				}				
			}			
		});
		radioButtonShowControls = (RadioButton)findViewById(R.id.radioButtonShowControls);
		radioButtonShowControls.setOnCheckedChangeListener(new OnCheckedChangeListener() {
			@Override
			public void onCheckedChanged(CompoundButton arg0, boolean isChecked) {
				if (isChecked) {
					setExecutionMode(false);
				}				
			}			
		});	
		
		editTextMacro.addTextChangedListener(new TextWatcher() {            
			@Override
			public void afterTextChanged(Editable s) {
				if (s.length() > 0) {
					buttonSave.setEnabled(true);
				} else {
					buttonSave.setEnabled(false);
				}
			}
			@Override
			public void beforeTextChanged(CharSequence s, int start, int count, int after) {

			}
			@Override
			public void onTextChanged(CharSequence s, int start, int before, int count) {
			}
		});
		
		
		buttonDelete = (Button)findViewById(R.id.buttonDelete);		
		buttonDelete.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				AlertDialog.Builder alert = new AlertDialog.Builder(MacroActivity.this);
				alert.setTitle(R.string.delete_title);
				alert.setMessage(R.string.delete_message);
				alert.setPositiveButton(R.string.ok, new DialogInterface.OnClickListener() {
					public void onClick(DialogInterface dialog, int whichButton) {
						deleteMacro();
					}
				});
				alert.setNegativeButton(R.string.cancel, null);
				alert.show();				
			}
		});
		

		buttonSave = (Button)findViewById(R.id.buttonSave);		
		buttonSave.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				saveMacro();
			}
		});
		
		Button button;
		button = (Button)findViewById(R.id.buttonHelp);		
		button.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				AlertDialog.Builder alert = new AlertDialog.Builder(MacroActivity.this);
				alert.setTitle(R.string.help);
				alert.setMessage(R.string.macro_help);	
				alert.setNeutralButton(R.string.ok, null);
				alert.show();	
			}
		});
		
		button = (Button)findViewById(R.id.buttonAddFromField);		
		button.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				CharSequence options[] = new CharSequence[] {MacroActivity.this.getString(R.string.user_name), 
															MacroActivity.this.getString(R.string.password), 
															MacroActivity.this.getString(R.string.url),
															MacroActivity.this.getString(R.string.password_masked), 															
															MacroActivity.this.getString(R.string.clipboard_authenticator)};
				

				AlertDialog.Builder builder = new AlertDialog.Builder(MacroActivity.this);
				builder.setTitle(R.string.add_from_field);
				builder.setItems(options, new DialogInterface.OnClickListener() {
				    @Override
				    public void onClick(DialogInterface dialog, int which) {
				        switch (which) { 
				        	case 0:
				        		addAction(MacroHelper.MACRO_ACTION_USER_NAME, null);
				        		break;
				        	case 1:
				        		addAction(MacroHelper.MACRO_ACTION_PASSWORD, null);
				        		break;
				        	case 2:
				        		addAction(MacroHelper.MACRO_ACTION_URL, null);				        		
				        		break;
				        	case 3:
				        		addAction(MacroHelper.MACRO_ACTION_PASSWORD_MASKED, null);
				        		break;	
				        	case 4:
				        		addAction(MacroHelper.MACRO_ACTION_CLIPBOARD, null);
				        		break;					        		
				        }
				    }
				});
				builder.show();				
			}
		});
		
		button = (Button)findViewById(R.id.buttonAddEnter);		
		button.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				addAction(MacroHelper.MACRO_ACTION_KEY, "enter");
			}
		});
		button = (Button)findViewById(R.id.buttonAddTab);		
		button.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				addAction(MacroHelper.MACRO_ACTION_KEY, "tab");
			}
		});
		button = (Button)findViewById(R.id.buttonAddCustom);		
		button.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				AlertDialog.Builder alert = new AlertDialog.Builder(MacroActivity.this);
				alert.setTitle(R.string.custom_key_title);		
				
				final LinearLayout lin= new LinearLayout(MacroActivity.this);
				lin.setOrientation(LinearLayout.VERTICAL);
				
				final TextView tvInfo = new TextView(MacroActivity.this);				
				tvInfo.setText(R.string.custom_key_message);
				
				final Spinner spinner = new Spinner(MacroActivity.this);				
				ArrayAdapter<String> adapter = new ArrayAdapter<String>(MacroActivity.this, android.R.layout.simple_spinner_item, MacroHelper.getKeyList());
				spinner.setAdapter(adapter);
				
				final CheckBox cbCtrlLeft = new CheckBox(MacroActivity.this);
				cbCtrlLeft.setText("Ctrl");
				final CheckBox cbShiftLeft = new CheckBox(MacroActivity.this);
				cbShiftLeft.setText("Shift");
				final CheckBox cbAltLeft = new CheckBox(MacroActivity.this);
				cbAltLeft.setText("Alt");
				final CheckBox cbGuiLeft = new CheckBox(MacroActivity.this);
				cbGuiLeft.setText("GUI (Win key)");
				final CheckBox cbAltRight = new CheckBox(MacroActivity.this);
				cbAltRight.setText("AltGr (right)");
				
				//cbKeyword = new CheckBox(SettingsTextActivity.this);
				//cbKeyword.setChecked(false);
				
				lin.addView(tvInfo);
				lin.addView(spinner);
				lin.addView(cbCtrlLeft);
				lin.addView(cbShiftLeft);	
				lin.addView(cbAltLeft);	
				lin.addView(cbGuiLeft);	
				lin.addView(cbAltRight);	
				alert.setView(lin);
				
				alert.setPositiveButton(R.string.ok, new DialogInterface.OnClickListener() {
					private String param;
					
					private void add(String toAdd) {
						if (param.length() > 0) {
							param += "+";
						}
						param += toAdd;
					}
					
					public void onClick(DialogInterface dialog, int whichButton) {
						param = "";
						if (cbCtrlLeft.isChecked()) add("Ctrl");
						if (cbShiftLeft.isChecked()) add("Shift");
						if (cbAltLeft.isChecked()) add("Alt");
						if (cbGuiLeft.isChecked()) add("Gui");
						if (cbAltRight.isChecked()) add("AltGr");	
						add((String)spinner.getSelectedItem());
						addAction(MacroHelper.MACRO_ACTION_KEY, param);
					}
				});
				alert.setNegativeButton(R.string.cancel, null);
				alert.show();
			}
		});
		button = (Button)findViewById(R.id.buttonAddString);		
		button.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				String s = editTextString.getText().toString();
				if ((s != null) && (s.length() > 0)) {
					if (s.contains("%")) {
						Toast.makeText(MacroActivity.this, R.string.illegal_character_toast, Toast.LENGTH_LONG).show();
					} else {
						addAction(MacroHelper.MACRO_ACTION_TYPE, s);
					}
				}								
			}
		});
		button = (Button)findViewById(R.id.buttonAddDelay);		
		button.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				addAction(MacroHelper.MACRO_ACTION_DELAY, String.valueOf(spinnerDelay.getSelectedItem()));
			}
		});		
		
		button = (Button) findViewById(R.id.buttonTemplateSave);
		button.setOnClickListener(new OnClickListener() {
			public void onClick(View v) {
				AlertDialog.Builder builder = new AlertDialog.Builder(MacroActivity.this);
				builder.setTitle(R.string.save_as);
				builder.setItems(getTemplateNames(), new DialogInterface.OnClickListener() {
					@Override
					public void onClick(DialogInterface dialog, int which) {
						getSaveTemplateDialog(which).show();
					}
				});
				builder.show();
			}
		});
		button = (Button)findViewById(R.id.buttonTemplateLoad);		
		button.setOnClickListener(new OnClickListener() {			
			public void onClick(View v) {
				AlertDialog.Builder builder = new AlertDialog.Builder(MacroActivity.this);
				builder.setTitle(R.string.load_from);
				builder.setItems(getTemplateNames(), new DialogInterface.OnClickListener() {
					@Override
					public void onClick(DialogInterface dialog, int which) {
						String tmp = prefs.getString(Const.TEMPLATE_PREF_PREFIX + which, "");						
						if ((tmp != null) && ( !tmp.equals(""))) {						
							editTextMacro.setText(tmp);
							macro = tmp;
							manageUI();
							saveMacro();
						} else {
							Toast.makeText(MacroActivity.this, R.string.empty, Toast.LENGTH_SHORT).show();
						}
					}
				});
				builder.show();
			}
		});
		
		
		Bundle b = getIntent().getExtras();
		macro = b.getString(Const.EXTRA_MACRO, null);
		id = b.getString(Const.EXTRA_ENTRY_ID, null);
		
		if (b.getBoolean(Const.EXTRA_MACRO_RUN_BUT_EMPTY, false)) {
			Toast.makeText(MacroActivity.this, R.string.no_macro_create_new, Toast.LENGTH_LONG).show();
		}
		
		if (macro == null) {
			macro = "";
			setTitle(R.string.add_macro_title);
			buttonDelete.setEnabled(false);
			buttonSave.setEnabled(false);
		} else {
			setTitle(R.string.edit_macro_title);
			editTextMacro.setText(macro);	
		}
		manageUI();
	}
	
	private void manageUI() {
		if (macro != null) {
			if (macro.startsWith(MacroHelper.MACRO_BACKGROUND_EXEC_STRING)) {
				radioButtonBackground.setChecked(true);
			}		
		}
	}
	
	private void setExecutionMode(boolean isBackground) {
		String tmp = editTextMacro.getText().toString();		
		if (isBackground) {
			if ((tmp != null) && ( !tmp.startsWith(MacroHelper.MACRO_BACKGROUND_EXEC_STRING))) {				
				editTextMacro.setText(MacroHelper.MACRO_BACKGROUND_EXEC_STRING + tmp);
			}					
		} else {
			if ((tmp != null) && (tmp.startsWith(MacroHelper.MACRO_BACKGROUND_EXEC_STRING))) {				
				editTextMacro.setText(tmp.substring(MacroHelper.MACRO_BACKGROUND_EXEC_STRING.length()));
			}	
		}
	}
	
	private void addAction(String action, String param) {
		String tmp = "%" + action;
		if (param != null) {
			tmp += "=" + param;
		}
		
		String m = editTextMacro.getText().toString();		
		m += tmp;
		Toast.makeText(this, getString(R.string.added) + " " + tmp, Toast.LENGTH_SHORT).show();
		if (radioButtonBackground.isChecked()) {
			if ( !m.startsWith(MacroHelper.MACRO_BACKGROUND_EXEC_STRING)) {
				m = MacroHelper.MACRO_BACKGROUND_EXEC_STRING + m;
			}
		}
		
		editTextMacro.setText(m);
	}
	
	private void saveMacro() {
		macro = editTextMacro.getText().toString();
		SharedPreferences.Editor editor = prefs.edit();
		if ("".equals(macro)) {
			editor.putString(Const.MACRO_PREF_PREFIX + id, null); //do not save empty macro
		} else {
			editor.putString(Const.MACRO_PREF_PREFIX + id, macro);
		}
		editor.apply();	
		buttonDelete.setEnabled(true);
		Toast.makeText(MacroActivity.this, R.string.saved_toast, Toast.LENGTH_SHORT).show();
	}
	
	private void deleteMacro() {
		macro = "";
		SharedPreferences.Editor editor = prefs.edit();
		editor.remove(Const.MACRO_PREF_PREFIX + id);
		editor.apply();	
		editTextMacro.setText("");
		buttonDelete.setEnabled(false);
		Toast.makeText(MacroActivity.this, R.string.deleted_toast, Toast.LENGTH_SHORT).show();
	}
	
	@Override
	public void onBackPressed() {
		if ( !editTextMacro.getText().toString().equals(macro)) {		
			AlertDialog.Builder alert = new AlertDialog.Builder(MacroActivity.this);
			alert.setTitle(R.string.save_title);
			alert.setMessage(R.string.save_message);
			alert.setPositiveButton(R.string.ok, new DialogInterface.OnClickListener() {
				public void onClick(DialogInterface dialog, int whichButton) {
					saveMacro();
					finish();			
				}
			});
			alert.setNegativeButton(R.string.no, new DialogInterface.OnClickListener() {
				public void onClick(DialogInterface dialog, int whichButton) {
					finish();
				}
			});		
			alert.setNeutralButton(R.string.cancel, null);
			alert.show();	
		} else {
			super.onBackPressed();
		}
	}
	
	
	
	
	public CharSequence[] getTemplateNames() {
		return new CharSequence[] {getTemplateName(0), getTemplateName(1), getTemplateName(2), getTemplateName(3), getTemplateName(4)};
	}
	
	public String getTemplateName(int id) {
		return prefs.getString(Const.TEMPLATE_NAME_PREF_PREFIX + id, getTemplateDefaultName(id));
	}
	
	public String getTemplateDefaultName(int id) {
		return Const.TEMPLATE_DEFAULT_NAME_PREF_PREFIX + id;
	}
	
	public AlertDialog getSaveTemplateDialog(final int id) {
		AlertDialog.Builder alert = new AlertDialog.Builder(this);		
		alert.setTitle(R.string.template_name); 		

		final EditText editTextName = new EditText(this);
		//display current name if exists, if not, leave empty
		if (prefs.contains(Const.TEMPLATE_NAME_PREF_PREFIX + id)) {
			editTextName.setText(getTemplateName(id));
		} 
		final LinearLayout lin= new LinearLayout(this);
		lin.setOrientation(LinearLayout.VERTICAL);
		lin.addView(editTextName);
		alert.setView(lin);
		
		alert.setPositiveButton(R.string.ok, new DialogInterface.OnClickListener() {
			public void onClick(DialogInterface dialog, int whichButton) {
				String name = editTextName.getText().toString();
				//use default name if no name was provided
				if ("".equals(name)) {
					name = getTemplateDefaultName(id);
				}				
				SharedPreferences.Editor editor = prefs.edit();
				editor.putString(Const.TEMPLATE_NAME_PREF_PREFIX + id, name);
				editor.putString(Const.TEMPLATE_PREF_PREFIX + id, editTextMacro.getText().toString());
				editor.apply();		
				Toast.makeText(MacroActivity.this, R.string.saved_toast, Toast.LENGTH_SHORT).show();				
			}
		});
		alert.setNegativeButton(R.string.cancel, null);
		return alert.show();		
	}		

}
