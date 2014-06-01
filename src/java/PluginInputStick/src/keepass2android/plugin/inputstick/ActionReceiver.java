package keepass2android.plugin.inputstick;

import keepass2android.pluginsdk.PluginAccessException;
import keepass2android.pluginsdk.Strings;

import org.json.JSONObject;

import android.content.Context;
import android.content.Intent;
import android.os.Bundle;

public class ActionReceiver extends keepass2android.pluginsdk.PluginActionBroadcastReceiver {
	private static final String EXTRA_TEXT = "text";

	@Override
	protected void openEntry(OpenEntryAction oe) {
		try {
			for (String field: oe.getEntryFields().keySet())
			{
				oe.addEntryFieldAction("keepass2android.plugin.inputstick.type", Strings.PREFIX_STRING+field, oe.getContext().getString(R.string.action_input_stick),
					R.drawable.ic_launcher, null);
			}
			Bundle b1 = new Bundle();
			b1.putString(EXTRA_TEXT, "\t");
			oe.addEntryAction(oe.getContext().getString(R.string.action_type_tab), R.drawable.ic_launcher, b1);
			Bundle b2 = new Bundle();
			b2.putString(EXTRA_TEXT, "\n");
			oe.addEntryAction(oe.getContext().getString(R.string.action_type_enter), R.drawable.ic_launcher, b2);
		} catch (PluginAccessException e) {
			e.printStackTrace();
		}
		typeText(oe.getContext(), "");
	}
	
	@Override 
	protected void closeEntryView(CloseEntryViewAction closeEntryView) 
	{
		Intent serviceIntent = new Intent(closeEntryView.getContext(), InputStickService.class);
		serviceIntent.setAction(InputStickService.DISCONNECT);
		closeEntryView.getContext().startService(serviceIntent);
		
	};
	
	@Override
	protected void actionSelected(ActionSelectedAction actionSelected) {
		if (actionSelected.isEntryAction())
		{
			typeText(actionSelected.getContext(), actionSelected.getActionData().getString(EXTRA_TEXT));
		}
		else
		{
			String fieldKey =actionSelected.getFieldId().substring(Strings.PREFIX_STRING.length());
			String text = actionSelected.getEntryFields().get(fieldKey);
			typeText(actionSelected.getContext(), text);
		}
	}

	
	private void typeText(Context ctx, String text) {
		Intent serviceIntent = new Intent(ctx, InputStickService.class);
		serviceIntent.setAction(InputStickService.TYPE);
	
		serviceIntent.putExtra(Intent.EXTRA_TEXT, text);
		ctx.startService(serviceIntent);
		
	}

	@Override
	protected void entryOutputModified(EntryOutputModifiedAction eom) {
		try {
			eom.addEntryFieldAction("keepass2android.plugin.inputstick.type", eom.getModifiedFieldId(), eom.getContext().getString(R.string.action_input_stick),
					R.drawable.ic_launcher, null);
		} catch (PluginAccessException e) {
			e.printStackTrace();
		}
	}

}
