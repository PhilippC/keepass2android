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
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;

namespace keepass2android.view
{

    public sealed class PwGroupView : GroupListItemView 
	{
		private PwGroup _pwGroup;
		private readonly GroupBaseActivity _groupBaseActivity;
		private readonly TextView _textview, _label;
		private int? _defaultTextColor;

		private const int MenuOpen = Menu.First;
		private const int MenuDelete = MenuOpen + 1;
		private const int MenuMove = MenuDelete + 1;
		private const int MenuEdit = MenuDelete + 2;
		
		public static PwGroupView GetInstance(GroupBaseActivity act, PwGroup pw) {

			return new PwGroupView(act, pw);

		}
		public PwGroupView (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		private PwGroupView(GroupBaseActivity act, PwGroup pw)
		: base(act){
			_groupBaseActivity = act;
			
			View gv = Inflate(act, Resource.Layout.group_list_entry, null);
			
			_textview = (TextView) gv.FindViewById(Resource.Id.group_text);
			float size = PrefsUtil.GetListTextSize(act); 
			_textview.TextSize = size;
			
			_label = (TextView) gv.FindViewById(Resource.Id.group_label);
			_label.TextSize = size-8;

			gv.FindViewById(Resource.Id.group_icon_bkg).Visibility = App.Kp2a.GetDb().DrawableFactory.IsWhiteIconSet ? ViewStates.Visible : ViewStates.Gone;

			PopulateView(gv, pw);
			
			LayoutParams lp = new LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
			
			AddView(gv, lp);
		}
		
		private void PopulateView(View gv, PwGroup pw) {
			_pwGroup = pw;
			
			ImageView iv = (ImageView) gv.FindViewById(Resource.Id.icon);
			App.Kp2a.GetDb().DrawableFactory.AssignDrawableTo(iv, _groupBaseActivity, App.Kp2a.GetDb().KpDatabase, pw.IconId, pw.CustomIconUuid, true);
			
			_textview.Text = pw.Name;

			if (_defaultTextColor == null)
				_defaultTextColor = _textview.TextColors.DefaultColor;

			if (_groupBaseActivity.IsBeingMoved(_pwGroup.Uuid))
			{
				int elementBeingMoved = Context.Resources.GetColor(Resource.Color.element_being_moved);
				_textview.SetTextColor(new Color(elementBeingMoved));
			}
			else
				_textview.SetTextColor(new Color((int)_defaultTextColor));

			_label.Text = _groupBaseActivity.GetString (Resource.String.group)+" - ";
			uint numEntries = CountEntries (pw);
			if (numEntries == 1)
				_label.Text += Context.GetString (Resource.String.Entry_singular);
			else
				_label.Text += Context.GetString (Resource.String.Entry_plural, new Java.Lang.Object[] { numEntries });
		}

		uint CountEntries(PwGroup g)
		{
			uint n = g.Entries.UCount;

			foreach (PwGroup subgroup in g.Groups) 
			{
				n += CountEntries(subgroup);
			}

			return n;
		}
		
		public void ConvertView(PwGroup pw) {
			PopulateView(this, pw);
		}
		
		
		private void LaunchGroup() {
			GroupActivity.Launch(_groupBaseActivity, _pwGroup, _groupBaseActivity.AppTask);
			//_groupBaseActivity.OverridePendingTransition(Resource.Animation.anim_enter, Resource.Animation.anim_leave);

		}
        /*
		public override void OnCreateMenu(IContextMenu menu, IContextMenuContextMenuInfo menuInfo) {
			menu.Add(0, MenuOpen, 0, Resource.String.menu_open);
			if (App.Kp2a.GetDb().CanWrite)
			{
				menu.Add(0, MenuDelete, 0, Resource.String.menu_delete);
				menu.Add(0, MenuMove, 0, Resource.String.menu_move);
				menu.Add(0, MenuEdit, 0, Resource.String.menu_edit);
			}
		}
		
		public override bool OnContextItemSelected(IMenuItem item) 
		{
			switch ( item.ItemId ) {
				case MenuOpen:
					LaunchGroup();
					return true;
			
				case MenuDelete:
					Handler handler = new Handler();
					DeleteGroup task = new DeleteGroup(Context, App.Kp2a, _pwGroup, new GroupBaseActivity.AfterDeleteGroup(handler, _groupBaseActivity));
					task.Start();
					return true;
				case MenuMove:
					_groupBaseActivity.StartTask(new MoveElementsTask { Uuid = _pwGroup.Uuid });
					return true;
				case MenuEdit:
					_groupBaseActivity.EditGroup(_pwGroup);
					return true;
				default:
					return false;
			}
		}*/

        public override void OnClick()
        {
            LaunchGroup();
        }
	}
}

