package keepass2android.plugin.inputstick;

import java.util.ArrayList;

import keepass2android.pluginsdk.Strings;

public class AccessReceiver extends keepass2android.pluginsdk.PluginAccessBroadcastReceiver {

	@Override
	public ArrayList<String> getScopes() {
		ArrayList<String> scopes = new ArrayList<String>();
		scopes.add(Strings.SCOPE_CURRENT_ENTRY);
		return scopes;
	}

}
