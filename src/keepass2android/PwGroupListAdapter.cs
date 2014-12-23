/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
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
using System.Globalization;
using Android.Content;
using Android.Views;
using Android.Widget;
using Android.Preferences;
using KeePassLib;
using keepass2android.view;

namespace keepass2android
{
	public interface IGroupViewSortOrder
	{
		int ResourceId { get; }
		bool RequiresSort { get; }
		int CompareEntries(PwEntry a, PwEntry b);
		int CompareGroups(PwGroup a, PwGroup b);
	}

	class ModDateSortOrder : IGroupViewSortOrder
	{
		public int ResourceId
		{
			get { return Resource.String.sort_moddate; }
		}

		public bool RequiresSort
		{
			get { return true; }
		}

		public int CompareEntries(PwEntry a, PwEntry b)
		{
			return a.LastModificationTime.CompareTo(b.LastModificationTime);
		}

		public int CompareGroups(PwGroup a, PwGroup b)
		{
			return a.LastModificationTime.CompareTo(b.LastModificationTime);
		}
	}
	class CreationDateSortOrder : IGroupViewSortOrder
	{
		public int ResourceId
		{
			get { return Resource.String.sort_db; }
		}

		public bool RequiresSort
		{
			get { return true; }
		}

		public int CompareEntries(PwEntry a, PwEntry b)
		{
			return a.CreationTime.CompareTo(b.CreationTime);
		}

		public int CompareGroups(PwGroup a, PwGroup b)
		{
			return a.CreationTime.CompareTo(b.CreationTime);
		}
	}

	public class DefaultSortOrder: IGroupViewSortOrder
	{
		public int ResourceId
		{
			get { return Resource.String.sort_default; }
		}

		public bool RequiresSort
		{
			get { return false; }
		}

		public int CompareEntries(PwEntry a, PwEntry b)
		{
			return 0;
		}

		public int CompareGroups(PwGroup a, PwGroup b)
		{
			return 0;
		}
	}
	public class NameSortOrder: IGroupViewSortOrder
	{
		public int ResourceId
		{
			get { return Resource.String.sort_name; }
		}

		public bool RequiresSort
		{
			get { return true; }
		}

		public int CompareEntries(PwEntry x, PwEntry y)
		{
			String nameX = x.Strings.ReadSafe(PwDefs.TitleField);
											String nameY = y.Strings.ReadSafe(PwDefs.TitleField);
			if (nameX.ToLower() != nameY.ToLower())
				return String.Compare(nameX, nameY, StringComparison.OrdinalIgnoreCase);
			else
			{
				if (PwDefs.IsTanEntry(x) && PwDefs.IsTanEntry(y))
				{
					//compare the user name fields (=TAN index)
					String userX = x.Strings.ReadSafe(PwDefs.UserNameField);
					String userY = y.Strings.ReadSafe(PwDefs.UserNameField);
					if (userX != userY)
					{
						try
						{
							return int.Parse(userX).CompareTo(int.Parse(userY));
						}
						catch (Exception)
						{
							//ignore
						}
						return String.Compare(userX, userY, StringComparison.OrdinalIgnoreCase);
					}
				}

				//use creation time for non-tan entries:

				return x.CreationTime.CompareTo(y.CreationTime);
			}
		}

		public int CompareGroups(PwGroup a, PwGroup b)
		{
			return String.CompareOrdinal(a.Name, b.Name);
		}
	}

	public class GroupViewSortOrderManager
	{
		private readonly Context _context;
		private readonly IGroupViewSortOrder[] _orders = new IGroupViewSortOrder[] { new DefaultSortOrder(), new NameSortOrder(), new ModDateSortOrder(), new CreationDateSortOrder()};
		private readonly ISharedPreferences _prefs;

		public GroupViewSortOrderManager(Context context)
		{
			_context = context;
			_prefs = PreferenceManager.GetDefaultSharedPreferences(_context);
		}

		public IGroupViewSortOrder[] SortOrders	
		{
			get { return _orders; }
		}

		public bool SortGroups
		{
			get { return true; }
			//_prefs.GetBoolean(_context.GetString(Resource.String.sortgroups_key), false); }
		}

		public IGroupViewSortOrder GetCurrentSortOrder()
		{
			return SortOrders[GetCurrentSortOrderIndex()];
		}

		public int GetCurrentSortOrderIndex()
		{
			String sortKeyOld = _context.GetString(Resource.String.sort_key_old);
			String sortKey = _context.GetString(Resource.String.sort_key);

			int sortId = _prefs.GetInt(sortKey, -1);
			if (sortId == -1)
			{
				sortId = _prefs.GetBoolean(sortKeyOld, true) ? 1 : 0;
			}
			return sortId;
		}

		public void SetNewSortOrder(int selectedAfter)
		{
			String sortKey = _context.GetString(Resource.String.sort_key);
			ISharedPreferencesEditor editor = _prefs.Edit();
			editor.PutInt(sortKey, selectedAfter);
			//editor.PutBoolean(_context.GetString(Resource.String.sortgroups_key), false);
			EditorCompat.Apply(editor);
		}
	}
	
	public class PwGroupListAdapter : BaseAdapter 
	{
		
		private readonly GroupBaseActivity _act;
		private readonly PwGroup _group;
		private List<PwGroup> _groupsForViewing;
		private List<PwEntry> _entriesForViewing;

		
		
		public PwGroupListAdapter(GroupBaseActivity act, PwGroup group) {
			_act = act;
			_group = group;
			
			
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
			GroupViewSortOrderManager sortOrderManager = new GroupViewSortOrderManager(_act);
			var sortOrder = sortOrderManager.GetCurrentSortOrder();

			if ( sortOrder.RequiresSort )
			{
				var sortGroups = sortOrderManager.SortGroups;
				_groupsForViewing = new List<PwGroup>(_group.Groups);
				_groupsForViewing.Sort( (x, y) =>
					{
						if (sortGroups)
							return sortOrder.CompareGroups(x, y);
						else
							return String.Compare (x.Name, y.Name, true);
					});
				_entriesForViewing.Sort(sortOrder.CompareEntries);
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

