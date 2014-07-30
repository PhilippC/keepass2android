package keepass2android.plugin.inputstick;

import keepass2android.pluginsdk.KeepassDefs;
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
			Bundle b3 = new Bundle();
			b3.putString(EXTRA_TEXT, "user_pass");
			oe.addEntryAction(oe.getContext().getString(R.string.action_type_user_tab_pass_enter), R.drawable.ic_launcher, b3);
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
			String text = actionSelected.getActionData().getString(EXTRA_TEXT);
			if ("user_pass".equals(text))
			{
				typeText(actionSelected.getContext(), 
						actionSelected.getEntryFields().get(KeepassDefs.UserNameField));
				typeText(actionSelected.getContext(), "\t");
				typeText(actionSelected.getContext(), actionSelected.getEntryFields().get(KeepassDefs.PasswordField));
				typeText(actionSelected.getContext(), "\n");
				
			}
			else
			{
				typeText(actionSelected.getContext(), text);
			}
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
