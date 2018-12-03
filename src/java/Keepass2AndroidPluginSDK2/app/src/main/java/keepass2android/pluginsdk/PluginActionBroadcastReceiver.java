package keepass2android.pluginsdk;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.Iterator;

import org.json.JSONException;
import org.json.JSONObject;
import org.json.JSONArray;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;

public abstract class PluginActionBroadcastReceiver extends BroadcastReceiver {
	
	protected abstract class PluginActionBase
	{
		protected Context _context;
		protected Intent _intent;
		

		public PluginActionBase(Context context, Intent intent) 
		{
			_context = context;
			_intent = intent;
		}

		public String getHostPackage() {
			return _intent.getStringExtra(Strings.EXTRA_SENDER);
		}
		
		public Context getContext()
		{
			return _context;
		}

	}
	
	protected abstract class PluginEntryActionBase extends PluginActionBase
	{
		
		public PluginEntryActionBase(Context context, Intent intent)
		{
			super(context, intent);
		}
		
		protected HashMap<String, String> getEntryFieldsFromIntent()  
		{
			HashMap<String, String> res = new HashMap<String, String>();
			try {
				JSONObject json = new JSONObject(_intent.getStringExtra(Strings.EXTRA_ENTRY_OUTPUT_DATA));
				for(Iterator<String> iter = json.keys();iter.hasNext();) {
				    String key = iter.next();
				    String value = json.get(key).toString();
				    res.put(key, value);
				}
				
			} catch (JSONException e) {
				e.printStackTrace();
			} 
			return res;
		}
		
		protected String[] getProtectedFieldsListFromIntent()
		{
			try {
				JSONArray json = new JSONArray(_intent.getStringExtra(Strings.EXTRA_PROTECTED_FIELDS_LIST));
				String[] res = new String[json.length()];
				for(int i = 0; i < json.length(); i++)
					res[i] = json.getString(i);
				return res;
			} catch (JSONException e) {
				e.printStackTrace();
				return null;
			}
		}
		

		public String getEntryId()
		{
			return _intent.getStringExtra(Strings.EXTRA_ENTRY_ID);
		}
		

		public void setEntryField(String fieldId, String fieldValue, boolean isProtected) throws PluginAccessException
		{
			Intent i = new Intent(Strings.ACTION_SET_ENTRY_FIELD);
			ArrayList<String> scope = new ArrayList<String>();
			scope.add(Strings.SCOPE_CURRENT_ENTRY);
			i.putExtra(Strings.EXTRA_ACCESS_TOKEN, AccessManager.getAccessToken(_context, getHostPackage(), scope));
			i.setPackage(getHostPackage());
			i.putExtra(Strings.EXTRA_SENDER, _context.getPackageName());
			i.putExtra(Strings.EXTRA_FIELD_VALUE, fieldValue);
			i.putExtra(Strings.EXTRA_ENTRY_ID, getEntryId());
			i.putExtra(Strings.EXTRA_FIELD_ID, fieldId);
			i.putExtra(Strings.EXTRA_FIELD_PROTECTED, isProtected);
			
			_context.sendBroadcast(i);
		}
	
	}
	
	protected class ActionSelectedAction extends PluginEntryActionBase
	{
		public ActionSelectedAction(Context ctx, Intent intent) {
			super(ctx, intent);

		}
		

		
		
		/**
		 * 
		 * @return the Bundle associated with the action. This bundle can be set in OpenEntry.add(Entry)FieldAction
		 */
		public Bundle getActionData()
		{
			return _intent.getBundleExtra(Strings.EXTRA_ACTION_DATA);
		}

		/**
		 * 
		 * @return the field id which was selected. null if an entry action (in the options menu) was selected.
		 */
		public String getFieldId()
		{
			return _intent.getStringExtra(Strings.EXTRA_FIELD_ID);
		}
		
		/**
		 * 
		 * @return true if an entry action, i.e. an option from the options menu, was selected. False if an option
		 * in a popup menu for a certain field was selected. 
		 */
		public boolean isEntryAction()
		{
			return getFieldId() == null;
		}

		/**
		 * 
		 * @return a hashmap containing the entry fields in key/value form
		 */
		public HashMap<String, String> getEntryFields() 
		{
			return getEntryFieldsFromIntent();
		}
		
		/**
		 * 
		 * @return an array with the keys of all protected fields in the entry
		 */
		public String[] getProtectedFieldsList()
		{
			return getProtectedFieldsListFromIntent();
		}
	}
	
	protected class CloseEntryViewAction extends PluginEntryActionBase
	{
		public CloseEntryViewAction(Context context, Intent intent) {
			super(context, intent);
		}

		public String getEntryId()
		{
			return _intent.getStringExtra(Strings.EXTRA_ENTRY_ID);
		}
	}

	protected class OpenEntryAction extends PluginEntryActionBase
	{
	
		public OpenEntryAction(Context context, Intent intent)
		{
			super(context, intent);
		}
		
		
		public HashMap<String, String> getEntryFields() 
		{
			return getEntryFieldsFromIntent();
		}
		
		/**
		 * 
		 * @return an array with the keys of all protected fields in the entry
		 */
		public String[] getProtectedFieldsList()
		{
			return getProtectedFieldsListFromIntent();
		}
		
		public void addEntryAction(String actionDisplayText, int actionIconResourceId, Bundle actionData) throws PluginAccessException
		{
			addEntryFieldAction(null, null, actionDisplayText, actionIconResourceId, actionData);
		}

		public void addEntryFieldAction(String actionId, String fieldId, String actionDisplayText, int actionIconResourceId, Bundle actionData) throws PluginAccessException
		{
			Intent i = new Intent(Strings.ACTION_ADD_ENTRY_ACTION);
			ArrayList<String> scope = new ArrayList<String>();
			scope.add(Strings.SCOPE_CURRENT_ENTRY);
			i.putExtra(Strings.EXTRA_ACCESS_TOKEN, AccessManager.getAccessToken(_context, getHostPackage(), scope));
			i.setPackage(getHostPackage());
			i.putExtra(Strings.EXTRA_SENDER, _context.getPackageName());
			i.putExtra(Strings.EXTRA_ACTION_DATA, actionData);
			i.putExtra(Strings.EXTRA_ACTION_DISPLAY_TEXT, actionDisplayText);
			i.putExtra(Strings.EXTRA_ACTION_ICON_RES_ID, actionIconResourceId);
			i.putExtra(Strings.EXTRA_ENTRY_ID, getEntryId());
			i.putExtra(Strings.EXTRA_FIELD_ID, fieldId);
			i.putExtra(Strings.EXTRA_ACTION_ID, actionId);
			
			_context.sendBroadcast(i);
		}

		
		
		
	}
	
	protected class DatabaseAction extends PluginActionBase
	{

		public DatabaseAction(Context context, Intent intent) {
			super(context, intent);
		}
		
		public String getFileDisplayName()
		{
			return _intent.getStringExtra(Strings.EXTRA_DATABASE_FILE_DISPLAYNAME);
		}
		
		public String getFilePath()
		{
			return _intent.getStringExtra(Strings.EXTRA_DATABASE_FILEPATH);
		}
		
		public String getAction()
		{
			return _intent.getAction();
		}
		
	}
	//EntryOutputModified is very similar to OpenEntry because it receives the same 
	//data (+ the field id which was modified)
	protected class EntryOutputModifiedAction extends OpenEntryAction
	{
	
		public EntryOutputModifiedAction(Context context, Intent intent)
		{
			super(context, intent);
		}
		
		public String getModifiedFieldId()
		{
			return _intent.getStringExtra(Strings.EXTRA_FIELD_ID);
		}
	}

	@Override
	public void onReceive(Context ctx, Intent intent) {
		String action = intent.getAction();
		android.util.Log.d("KP2A.pluginsdk", "received broadcast in PluginActionBroadcastReceiver with action="+action);
		if (action == null)
			return;
		if (action.equals(Strings.ACTION_OPEN_ENTRY))
		{
			openEntry(new OpenEntryAction(ctx, intent));	
		}
		else if (action.equals(Strings.ACTION_CLOSE_ENTRY_VIEW))
		{
			closeEntryView(new CloseEntryViewAction(ctx, intent));	
		}		
		else if (action.equals(Strings.ACTION_ENTRY_ACTION_SELECTED))
		{
			actionSelected(new ActionSelectedAction(ctx, intent));
		}
		else if (action.equals(Strings.ACTION_ENTRY_OUTPUT_MODIFIED))
		{
			entryOutputModified(new EntryOutputModifiedAction(ctx, intent));
		}
		else if (action.equals(Strings.ACTION_LOCK_DATABASE)
				|| action.equals(Strings.ACTION_UNLOCK_DATABASE)
				|| action.equals(Strings.ACTION_OPEN_DATABASE)
				|| action.equals(Strings.ACTION_CLOSE_DATABASE))
		{
			dbAction(new DatabaseAction(ctx,  intent));
		}
		else
		{
			//TODO handle unexpected action
		}		

		
	}

	protected void closeEntryView(CloseEntryViewAction closeEntryView) {}

	protected void actionSelected(ActionSelectedAction actionSelected) {}

	protected void openEntry(OpenEntryAction oe) {}
	
	protected void entryOutputModified(EntryOutputModifiedAction eom) {}
	
	protected void dbAction(DatabaseAction db) {}

}
