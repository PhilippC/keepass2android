package keepass2android.plugina;

import keepass2android.pluginsdk.PluginAccessException;
import keepass2android.pluginsdk.Strings;
import android.os.Bundle;
import android.util.Log;
import android.widget.Toast;

public class PluginAActionReceiver
	extends keepass2android.pluginsdk.PluginActionBroadcastReceiver
{

	@Override
	protected void actionSelected(ActionSelectedAction actionSelected) {
		if (actionSelected.isEntryAction())
		{
			Toast.makeText(actionSelected.getContext(), "PluginA rocks!", Toast.LENGTH_SHORT).show();
		}
		else
		{
			String fieldId = actionSelected.getFieldId().substring(Strings.PREFIX_STRING.length());
			//Toast.makeText(actionSelected.getContext(), actionSelected.getActionData().getString("text"), Toast.LENGTH_SHORT).show();
			Toast.makeText(actionSelected.getContext(), actionSelected.getEntryFields().get(fieldId), Toast.LENGTH_SHORT).show();
		}
		
	}

	@Override
	protected void openEntry(OpenEntryAction oe) {
		
		try {
			
			Bundle bField = new Bundle();
			bField.putString("text", oe.getEntryFields().get("Password"));
			oe.addEntryFieldAction("keepass2android.plugina.bla", Strings.PREFIX_STRING+"Password", "PluginA says hello", R.drawable.ic_launcher, bField);
			oe.addEntryFieldAction("keepass2android.plugina.bla",Strings.PREFIX_STRING+"UserName", "Be nice!", R.drawable.ic_launcher, null);
			
			Bundle bEntry = new Bundle();
			oe.addEntryAction("PluginA", R.drawable.ic_launcher, bEntry);
			
			oe.setEntryField("newFieldFromPluginA", "I love pluginA", false);
			oe.setEntryField("SecretFieldFromPluginA", "I love pluginA uiadertn", true);
			oe.setEntryField("UserName", "Batman (Overwritten by PluginA)", true);
			
		} catch (PluginAccessException e) {
			e.printStackTrace();
		}

	}

	@Override
	protected void dbAction(DatabaseAction db) {
		
		Log.d("PluginA", db.getAction() + " in file " + db.getFileDisplayName() + " ("+db.getFilePath()+")");
	}
}
