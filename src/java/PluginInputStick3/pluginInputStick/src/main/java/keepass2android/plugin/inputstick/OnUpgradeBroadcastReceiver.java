package keepass2android.plugin.inputstick;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

public class OnUpgradeBroadcastReceiver extends BroadcastReceiver {

	@Override
	public void onReceive(Context ctx, Intent arg1) {
		MigrationMessageActivity.displayNotification(ctx);
	}

}
