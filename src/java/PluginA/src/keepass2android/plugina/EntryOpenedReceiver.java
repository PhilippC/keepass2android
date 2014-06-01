package keepass2android.plugina;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;



public class EntryOpenedReceiver extends BroadcastReceiver {
	@Override
	  public void onReceive(Context context, Intent intent) {
	    Bundle extras = intent.getExtras();
	    if (extras != null) {
	      Log.w("PluginA", "received broadcast!");
	      
	    }
	  }
}
