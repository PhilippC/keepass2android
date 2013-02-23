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
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Preferences;
using KeePassLib;
using keepass2android.view;

namespace keepass2android
{
	
	public class PwGroupListAdapter : BaseAdapter 
	{
		
		private GroupBaseActivity mAct;
		private PwGroup mGroup;
		private List<PwGroup> groupsForViewing;
		private List<PwEntry> entriesForViewing;

		private ISharedPreferences prefs;
		
		public PwGroupListAdapter(GroupBaseActivity act, PwGroup group) {
			mAct = act;
			mGroup = group;
			prefs = PreferenceManager.GetDefaultSharedPreferences(act);
			
			filterAndSort();
			
		}
		
		
		public override void NotifyDataSetChanged() {
			base.NotifyDataSetChanged();
			
			filterAndSort();
		}
		
		
		public override void NotifyDataSetInvalidated() {
			base.NotifyDataSetInvalidated();
			
			filterAndSort();
		}
		
		private void filterAndSort() {
			entriesForViewing = new List<PwEntry>();

			foreach (PwEntry entry in mGroup.Entries)
			{
				entriesForViewing.Add(entry);
			}
			
			bool sortLists = prefs.GetBoolean(mAct.GetString(Resource.String.sort_key),	mAct.Resources.GetBoolean(Resource.Boolean.sort_default)); 
			if ( sortLists ) 
			{
				groupsForViewing = new List<PwGroup>(mGroup.Groups);
				groupsForViewing.Sort( (PwGroup x,PwGroup y) => { return String.Compare (x.Name, y.Name, true); });
				entriesForViewing.Sort( (PwEntry x, PwEntry y) => 
				                       { 
					String nameX = x.Strings.ReadSafe(PwDefs.TitleField);
					String nameY = y.Strings.ReadSafe(PwDefs.TitleField);
					if (nameX.ToLower() != nameY.ToLower())
						return String.Compare (nameX,nameY,true); 
					else
						return x.CreationTime.CompareTo(y.CreationTime);
				}
				);
			} else {
				groupsForViewing =  new List<PwGroup>(mGroup.Groups);
			}
		}
		
		public override int Count 
		{
			get{
			
				return groupsForViewing.Count + entriesForViewing.Count;
			}
		}
		
		public override Java.Lang.Object GetItem(int position) {
			return position;
		}
		
		public override long GetItemId(int position) {
			return position;
		}
		
		public override View GetView(int position, View convertView, ViewGroup parent) {
			int size = groupsForViewing.Count;
			
			if ( position < size ) { 
				return createGroupView(position, convertView);
			} else {
				return createEntryView(position - size, convertView);
			}
		}
		
		private View createGroupView(int position, View convertView) {
			PwGroup g = groupsForViewing[position];
			PwGroupView gv;
			
			if (convertView == null || !(convertView is PwGroupView)) {
				
				gv = PwGroupView.getInstance(mAct, g);
			} 
			else {
				gv = (PwGroupView) convertView;
				gv.convertView(g);
				
			}
			
			return gv;
		}
	
		private PwEntryView createEntryView(int position, View convertView) {
			PwEntry entry = entriesForViewing[position];
			PwEntryView ev;
			
			if (convertView == null || !(convertView is PwEntryView)) {
				ev = PwEntryView.getInstance(mAct, entry, position);
			}
			else {
				ev = (PwEntryView) convertView;
				ev.convertView(entry, position);
			}
			
			return ev;
		}
		
	}

}

