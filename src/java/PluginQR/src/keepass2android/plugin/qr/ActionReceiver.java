package keepass2android.plugin.qr;

import org.json.JSONObject;

import android.content.Intent;
import android.widget.Toast;
import keepass2android.pluginsdk.PluginAccessException;
import keepass2android.pluginsdk.PluginActionBroadcastReceiver;
import keepass2android.pluginsdk.Strings;

public class ActionReceiver extends PluginActionBroadcastReceiver{
	@Override
	protected void openEntry(OpenEntryAction oe) {
		try {
			oe.addEntryAction(oe.getContext().getString(R.string.action_show_qr),
					R.drawable.qrcode, null);
			
			for (String field: oe.getEntryFields().keySet())
			{
				oe.addEntryFieldAction("keepass2android.plugin.qr.show", Strings.PREFIX_STRING+field, oe.getContext().getString(R.string.action_show_qr),
					R.drawable.qrcode, null);
			}
		} catch (PluginAccessException e) {
			e.printStackTrace();
		}
	}
	
	@Override
	protected void actionSelected(ActionSelectedAction actionSelected) {
		Intent i = new Intent(actionSelected.getContext(), QRActivity.class);
		i.putExtra(Strings.EXTRA_ENTRY_OUTPUT_DATA, new JSONObject(actionSelected.getEntryFields()).toString());
		i.putExtra(Strings.EXTRA_FIELD_ID, actionSelected.getFieldId());
		i.putExtra(Strings.EXTRA_SENDER, actionSelected.getHostPackage());
		i.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
		actionSelected.getContext().startActivity(i);
	}
	
	@Override
	protected void entryOutputModified(EntryOutputModifiedAction eom) {
		try {
			eom.addEntryFieldAction("keepass2android.plugin.qr.show", eom.getModifiedFieldId(), eom.getContext().getString(R.string.action_show_qr),
					R.drawable.qrcode, null);
		} catch (PluginAccessException e) {
			e.printStackTrace();
		}
	}
	
	
}
