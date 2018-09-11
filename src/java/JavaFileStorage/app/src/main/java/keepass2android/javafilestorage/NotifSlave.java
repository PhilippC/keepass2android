package keepass2android.javafilestorage;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.Dialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.os.Bundle;
import android.os.Message;
import android.os.Messenger;
import android.util.Log;
import android.view.View;
import android.widget.Button;
import android.widget.TextView;
import android.widget.Toast;

//based on https://github.com/jwise/dumload/blob/master/src/com/joshuawise/dumload/NotifSlave.java
public class NotifSlave extends Activity {

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
    }

    private void say(String s) {
        Toast.makeText(getApplicationContext(), s, Toast.LENGTH_SHORT).show();
    }

    private int _nextdialog = 0;
    private Dialog dialog = null;

    @Override
    protected Dialog onCreateDialog(int id)
    {
        Log.e("KP2AJ.NotifSlave", "Create for dialog "+(Integer.toString(id)));
        if (id != _nextdialog)
            return null;
        return dialog;
    }

    private void showDialog(Dialog d)
    {
        _nextdialog++;
        dialog = d;
        Log.e("KP2AJ.NotifSlave", "Attempting to show dialog "+(Integer.toString(_nextdialog)));
        showDialog(_nextdialog);
    }

    public void onStart() {
        super.onStart();

        Intent i = getIntent(); /* i *am* not an intent! */
        final Activity thisact = this;

        final Messenger m = (Messenger)i.getParcelableExtra("keepass2android.sftp.returnmessenger");
        String reqtype = i.getStringExtra("keepass2android.sftp.reqtype");
        String prompt = i.getStringExtra("keepass2android.sftp.prompt");

        if (prompt == null || reqtype == null || m == null)	/* i.e., we got called by a dummy notification */
        {
            this.finish();
            return;
        }

        if (reqtype.equals("yesno")) {
            AlertDialog.Builder builder = new AlertDialog.Builder(this);
            builder.setTitle("Keepass2Android");
            builder.setMessage(prompt);
            builder.setCancelable(false);
            builder.setPositiveButton("Yes", new DialogInterface.OnClickListener() {
                public void onClick(DialogInterface dialog, int id) {
                    Log.e("KP2AJ.NotifSlave", "Responding with a 1.");
                    try {
                        Message me = Message.obtain();
                        me.arg1 = 1;
                        m.send(me);
                    } catch (Exception e) {
                        Log.e("KP2AJ.NotifSlave", "Failed to send a message back to my buddy.");
                    }
                    dialog.cancel();
                    thisact.finish();
                }
            });
            builder.setNegativeButton("No", new DialogInterface.OnClickListener() {
                public void onClick(DialogInterface dialog, int id) {
                    Log.e("KP2AJ.NotifSlave", "Responding with a 1.");
                    try {
                        Message me = Message.obtain();
                        me.arg1 = 0;
                        m.send(me);
                    } catch (Exception e) {
                        Log.e("KP2AJ.NotifSlave", "Failed to send a message back to my buddy.");
                    }
                    dialog.cancel();
                    thisact.finish();
                }
            });
            AlertDialog alert = builder.create();
            showDialog(alert);
        } else if (reqtype.equals("message")) {
            AlertDialog.Builder builder = new AlertDialog.Builder(this);
            builder.setTitle("Keepass2Android");
            builder.setMessage(prompt);
            builder.setCancelable(false);
            builder.setNeutralButton("OK", new DialogInterface.OnClickListener() {
                public void onClick(DialogInterface dialog, int id) {
                    try {
                        Message me = Message.obtain();
                        m.send(me);
                    } catch (Exception e) {
                        Log.e("KP2AJ.NotifSlave", "Failed to send a message back to my buddy.");
                    }
                    dialog.cancel();
                    thisact.finish();
                }
            });
            AlertDialog alert = builder.create();
            showDialog(alert);
        } /*else if (reqtype.equals("password")) {
            final Dialog d = new Dialog(this);

            d.setContentView(R.layout.notfif_slave);
            d.setTitle("Keepass2Android");
            d.setCancelable(false);

            TextView text = (TextView) d.findViewById(R.id.prompt);
            text.setText(prompt);

            Button ok = (Button) d.findViewById(R.id.ok);
            ok.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) {
                    try {
                        Message me = Message.obtain();
                        me.arg1 = 1;
                        TextView entry = (TextView) d.findViewById(R.id.entry);
                        Bundle b = new Bundle(1);
                        b.putString("response", entry.getText().toString());
                        me.setData(b);
                        m.send(me);
                    } catch (Exception e) {
                        Log.e("KP2AJ.NotifSlave", "Failed to send a message back to my buddy.");
                    }
                    d.cancel();
                    thisact.finish();
                }
            });

            Button cancel = (Button) d.findViewById(R.id.cancel);
            cancel.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) {
                    try {
                        Message me = Message.obtain();
                        me.arg1 = 0;
                        m.send(me);
                    } catch (Exception e) {
                        Log.e("KP2AJ.NotifSlave", "Failed to send a message back to my buddy.");
                    }
                    d.cancel();
                    thisact.finish();
                }
            });


            showDialog(d);
        } */else {
            Log.e("KP2AJ.NotifSlave", "What's a "+reqtype+"?");
        }
    }
}