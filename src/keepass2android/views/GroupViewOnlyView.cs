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
using Android.Util;
using Android.Views;
using Android.Widget;

namespace keepass2android.view
{
	public class GroupViewOnlyView : RelativeLayout 
	{
		public GroupViewOnlyView (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}
			
			public GroupViewOnlyView(Context context): base(context) {
				Inflate(context);
			}
			
			public GroupViewOnlyView(Context context, IAttributeSet attrs): base(context, attrs) {
				Inflate(context);
			}
			
			private void Inflate(Context context) {
				LayoutInflater inflater = (LayoutInflater) context.GetSystemService(Context.LayoutInflaterService);
				inflater.Inflate(Resource.Layout.group_add_entry, this);
				
				// Hide the buttons
				View addGroup = FindViewById(Resource.Id.add_group);
				addGroup.Visibility = ViewStates.Invisible;
				
				View addEntry = FindViewById(Resource.Id.add_entry);
				addEntry.Visibility = ViewStates.Invisible;
				
				View divider2 = FindViewById(Resource.Id.divider2);
				divider2.Visibility = ViewStates.Invisible;
				
				View list = FindViewById(Android.Resource.Id.List);
				LayoutParams lp = (RelativeLayout.LayoutParams) list.LayoutParameters;

				lp.AddRule(LayoutRules.AlignParentBottom, (int)LayoutRules.True);
				
				
			}
			
			
		}

}

