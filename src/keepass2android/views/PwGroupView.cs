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
using KeePassLib;

namespace keepass2android.view
{
	
	public class PwGroupView : ClickView 
	{
		
		protected PwGroup mPw;
		protected GroupBaseActivity mAct;
		protected TextView mTv;

		protected const int MENU_OPEN = Menu.First;
		private const int MENU_DELETE = MENU_OPEN + 1;
		
		public static PwGroupView getInstance(GroupBaseActivity act, PwGroup pw) {

			return new PwGroupView(act, pw);

		}
		public PwGroupView (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}
		protected PwGroupView(GroupBaseActivity act, PwGroup pw)
		: base(act){
			mAct = act;
			
			View gv = View.Inflate(act, Resource.Layout.group_list_entry, null);
			
			mTv = (TextView) gv.FindViewById(Resource.Id.group_text);
			float size = PrefsUtil.getListTextSize(act); 
			mTv.TextSize = size;
			
			TextView label = (TextView) gv.FindViewById(Resource.Id.group_label);
			label.TextSize = size-8;
			
			populateView(gv, pw);
			
			LayoutParams lp = new LayoutParams(LayoutParams.FillParent, LayoutParams.WrapContent);
			
			AddView(gv, lp);
		}
		
		private void populateView(View gv, PwGroup pw) {
			mPw = pw;
			
			ImageView iv = (ImageView) gv.FindViewById(Resource.Id.group_icon);
			App.getDB().drawFactory.assignDrawableTo(iv, Resources, App.getDB().pm, pw.IconId, pw.CustomIconUuid);
			
			mTv.Text = pw.Name;
		}
		
		public void convertView(PwGroup pw) {
			populateView(this, pw);
		}
		
		public override void OnClick() {
			launchGroup();
		}
		
		private void launchGroup() {
			GroupActivity.Launch(mAct, mPw);
			mAct.OverridePendingTransition(Resource.Animation.anim_enter, Resource.Animation.anim_leave);

		}
		
		public override void OnCreateMenu(IContextMenu menu, IContextMenuContextMenuInfo menuInfo) {
			menu.Add(0, MENU_OPEN, 0, Resource.String.menu_open);
			menu.Add(0, MENU_DELETE, 0, Resource.String.menu_delete);
		}
		
		public override bool OnContextItemSelected(IMenuItem item) 
		{
			switch ( item.ItemId ) {
				
			case MENU_OPEN:
				launchGroup();
				return true;
			
			case MENU_DELETE:
				Handler handler = new Handler();
				DeleteGroup task = new DeleteGroup(Context, App.getDB(), mPw, mAct, new GroupBaseActivity.AfterDeleteGroup(handler, mAct));
				task.start();
				return true;
			default:
				return false;
			}
		}
		
	}
}

