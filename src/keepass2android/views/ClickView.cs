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
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace keepass2android.view
{
	public abstract class ClickView : LinearLayout {
		protected ClickView (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		protected ClickView(Context context) :base(context)
		{
		}
		
		abstract public void OnClick();
		
		abstract public void OnCreateMenu(IContextMenu menu, IContextMenuContextMenuInfo menuInfo);
		
		abstract public bool OnContextItemSelected(IMenuItem item);
	}
}

