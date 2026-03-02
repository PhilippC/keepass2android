/*
 * This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
 *
 *   Keepass2Android is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   Keepass2Android is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
 */

package keepass2android.plugin.qr;

import java.util.ArrayList;

import keepass2android.pluginsdk.PluginAccessBroadcastReceiver;
import keepass2android.pluginsdk.Strings;

public class AccessReceiver extends PluginAccessBroadcastReceiver {

	@Override
	public ArrayList<String> getScopes() {
		ArrayList<String> scopes = new ArrayList<String>();
		scopes.add(Strings.SCOPE_CURRENT_ENTRY);
		scopes.add(Strings.SCOPE_QUERY_CREDENTIALS);
		return scopes;
	}

}
