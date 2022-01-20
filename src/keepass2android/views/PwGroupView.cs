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
using Object = Java.Lang.Object;

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

		    Database db = App.Kp2a.FindDatabaseForElement(pw);

            gv.FindViewById(Resource.Id.group_icon_bkg).Visibility = db.DrawableFactory.IsWhiteIconSet ? ViewStates.Visible : ViewStates.Gone;

		    gv.FindViewById(Resource.Id.icon).Visibility = ViewStates.Visible;
		    gv.FindViewById(Resource.Id.check_mark).Visibility = ViewStates.Invisible;

            PopulateView(gv, pw);
			
			LayoutParams lp = new LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
			
			AddView(gv, lp);
		}
		
		private void PopulateView(View gv, PwGroup pw) {
			_pwGroup = pw;
			
			ImageView iv = (ImageView) gv.FindViewById(Resource.Id.icon);
		    
            Database db;
            try
            {
                db = App.Kp2a.FindDatabaseForElement(pw);
            }
            catch (Exception e)
            {
				//for some reason, since Android 12 we get here when the database is reloaded (after making remote changes and selecting sync)
				//we can just ignore this.
                Console.WriteLine(e);
                return;

            }

			db.DrawableFactory.AssignDrawableTo(iv, _groupBaseActivity, db.KpDatabase, pw.IconId, pw.CustomIconUuid, true);
		    gv.FindViewById(Resource.Id.icon).Visibility = ViewStates.Visible;
		    gv.FindViewById(Resource.Id.check_mark).Visibility = ViewStates.Invisible;


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
		        _label.Text += Context.GetString(Resource.String.Entry_singular);
		    else
		    {
		        Java.Lang.Object obj = (int)numEntries;
		        _label.Text += Context.GetString(Resource.String.Entry_plural, obj);
            }
				
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
			GroupActivity.Launch(_groupBaseActivity, _pwGroup, _groupBaseActivity.AppTask, new ActivityLaunchModeRequestCode(0));
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

    
    internal class JavaObjectAdapter : Java.Lang.Object
    {
        private readonly uint _value;
        public JavaObjectAdapter(uint value)
        {
            _value = value;
        }

        public JavaObjectAdapter(System.IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
            : base(handle, transfer)
        {
            
        }

        public override string ToString()
        {
            return _value.ToString();
        }
    }
}

