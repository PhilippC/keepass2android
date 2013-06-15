/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System;
using System.Collections.Generic;
using Android.Content;
using Android.Views;
using Android.Widget;
using Android.Preferences;
using KeePassLib;
using keepass2android.view;

namespace keepass2android
{
	
	public class PwGroupListAdapter : BaseAdapter 
	{
		
		private readonly GroupBaseActivity _act;
		private readonly PwGroup _group;
		private List<PwGroup> _groupsForViewing;
		private List<PwEntry> _entriesForViewing;

		private readonly ISharedPreferences _prefs;
		
		public PwGroupListAdapter(GroupBaseActivity act, PwGroup group) {
			_act = act;
			_group = group;
			_prefs = PreferenceManager.GetDefaultSharedPreferences(act);
			
			FilterAndSort();
			
		}
		
		
		public override void NotifyDataSetChanged() {
			base.NotifyDataSetChanged();
			
			FilterAndSort();
		}
		
		
		public override void NotifyDataSetInvalidated() {
			base.NotifyDataSetInvalidated();
			
			FilterAndSort();
		}
		
		private void FilterAndSort() {
			_entriesForViewing = new List<PwEntry>();

			foreach (PwEntry entry in _group.Entries)
			{
				_entriesForViewing.Add(entry);
			}
			
			bool sortLists = _prefs.GetBoolean(_act.GetString(Resource.String.sort_key),	_act.Resources.GetBoolean(Resource.Boolean.sort_default)); 
			if ( sortLists ) 
			{
				_groupsForViewing = new List<PwGroup>(_group.Groups);
				_groupsForViewing.Sort( (x, y) => { return String.Compare (x.Name, y.Name, true); });
				_entriesForViewing.Sort( (x, y) => 
				                       { 
					String nameX = x.Strings.ReadSafe(PwDefs.TitleField);
					String nameY = y.Strings.ReadSafe(PwDefs.TitleField);
					if (nameX.ToLower() != nameY.ToLower())
						return String.Compare(nameX, nameY, StringComparison.OrdinalIgnoreCase); 
					else
						return x.CreationTime.CompareTo(y.CreationTime);
				}
				);
			} else {
				_groupsForViewing =  new List<PwGroup>(_group.Groups);
			}
		}
		
		public override int Count 
		{
			get{
			
				return _groupsForViewing.Count + _entriesForViewing.Count;
			}
		}
		
		public override Java.Lang.Object GetItem(int position) {
			return position;
		}
		
		public override long GetItemId(int position) {
			return position;
		}
		
		public override View GetView(int position, View convertView, ViewGroup parent) {
			int size = _groupsForViewing.Count;
			
			if ( position < size ) { 
				return CreateGroupView(position, convertView);
			} else {
				return CreateEntryView(position - size, convertView);
			}
		}
		
		private View CreateGroupView(int position, View convertView) {
			PwGroup g = _groupsForViewing[position];
			PwGroupView gv;
			
			if (convertView == null || !(convertView is PwGroupView)) {
				
				gv = PwGroupView.GetInstance(_act, g);
			} 
			else {
				gv = (PwGroupView) convertView;
				gv.ConvertView(g);
				
			}
			
			return gv;
		}
	
		private PwEntryView CreateEntryView(int position, View convertView) {
			PwEntry entry = _entriesForViewing[position];
			PwEntryView ev;
			
			if (convertView == null || !(convertView is PwEntryView)) {
				ev = PwEntryView.GetInstance(_act, entry, position);
			}
			else {
				ev = (PwEntryView) convertView;
				ev.ConvertView(entry, position);
			}
			
			return ev;
		}
		
	}

}

