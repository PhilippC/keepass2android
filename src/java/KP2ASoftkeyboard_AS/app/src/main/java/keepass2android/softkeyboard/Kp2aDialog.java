package keepass2android.softkeyboard;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;
import android.util.Printer;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.view.inputmethod.EditorInfo;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.ListView;

import java.util.ArrayList;
import java.util.List;

import keepass2android.kbbridge.StringForTyping;

public class Kp2aDialog extends Activity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        getWindow().addFlags(WindowManager.LayoutParams.FLAG_ALT_FOCUSABLE_IM);


        setContentView(R.layout.activity_kp2a_dialog);
        ListView listview = ((ListView)findViewById(R.id.mylist));
        final String clientPackageName = getIntent().getStringExtra("clientPackageName");

        final ArrayList<StringForTyping> items = new ArrayList<StringForTyping>();




        StringForTyping openOrChangeEntry = new StringForTyping();
        if (keepass2android.kbbridge.KeyboardData.entryName == null)
        {
            openOrChangeEntry.displayName = openOrChangeEntry.key = getString(R.string.open_entry);
        }
        else
        {
            openOrChangeEntry.displayName = openOrChangeEntry.key = getString(R.string.change_entry);
        }
        openOrChangeEntry.value = "KP2ASPECIAL_SelectEntryTask";
        items.add(openOrChangeEntry);




        if ((clientPackageName != null) && (clientPackageName != ""))
        {
            StringForTyping searchEntry = new StringForTyping();
            try
            {
                searchEntry.key = searchEntry.displayName
                        = getString(R.string.open_entry_for_app, new Object[]{clientPackageName});
            }
            catch (java.util.FormatFlagsConversionMismatchException e) //buggy crowdin support for Arabic?
            {
                android.util.Log.e("KP2A", "Please report this error to crocoapps@gmail.com");
                android.util.Log.e("KP2A", e.toString());

                searchEntry.key = searchEntry.displayName
                        = "Search entry for app";
            }

            searchEntry.value = "KP2ASPECIAL_SearchUrlTask";
            items.add(searchEntry);
        }



        String[] itemNames = new String[items.size()];
        int i=0;
        for (StringForTyping sft: items)
            itemNames[i++] = sft.displayName;

        listview.setAdapter(new ArrayAdapter<String>(this,
                R.layout.kp2a_textview,
                itemNames));
        listview.setClickable(true);
        listview.setOnItemClickListener(new AdapterView.OnItemClickListener() {
            @Override
            public void onItemClick(AdapterView<?> adapterView, View view, int item, long l) {
                Log.d("KP2AK", "clicked item: " + items.get(item).key);

                if (items.get(item).value.startsWith("KP2ASPECIAL")) {
                    //change entry
                    Log.d("KP2AK", "clicked item: " + items.get(item).value);

                    String packageName = getApplicationContext().getPackageName();
                    Intent startKp2aIntent = getPackageManager().getLaunchIntentForPackage(packageName);
                    if (startKp2aIntent != null)
                    {
                        startKp2aIntent.addCategory(Intent.CATEGORY_LAUNCHER);
                        startKp2aIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK |  Intent.FLAG_ACTIVITY_CLEAR_TASK);
                        String value = items.get(item).value;
                        String taskName = value.substring("KP2ASPECIAL_".length());
                        startKp2aIntent.putExtra("KP2A_APPTASK", taskName);
                        if (taskName.equals("SearchUrlTask"))
                        {
                            startKp2aIntent.putExtra("UrlToSearch", "androidapp://"+clientPackageName);
                        }
                        startActivity(startKp2aIntent);
                    } else Log.w("KP2AK", "didn't find intent for "+packageName);
                } else {

                    StringForTyping theItem = items.get(item);

                    KP2AKeyboard.CurrentlyRunningService.commitStringForTyping(theItem);

                }
                Kp2aDialog.this.finish();

            }
        });

        findViewById(R.id.button_cancel).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                Kp2aDialog.this.finish();
            }
        });
    }
}
