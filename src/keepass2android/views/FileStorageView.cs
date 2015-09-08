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
using Android.App;
using Android.Graphics.Drawables;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Text;

namespace keepass2android.view
{/*
	public sealed class FileStorageView : ClickView
	{
		private readonly TextView _textView;

		public FileStorageView(IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		public FileStorageView(Activity activity, string protocolId, int pos)
			: base(activity)
		{
			View ev = Inflate(activity, Resource.Layout.filestorage_selection_listitem, null);
			_textView = (TextView)ev.FindViewById(Resource.Id.filestorage_label);
			_textView.TextSize = PrefsUtil.GetListTextSize(activity);

			PopulateView(ev, protocolId, pos);
			
			LayoutParams lp = new LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
			
			AddView(ev, lp);
			
		}
		
		private void PopulateView(View ev, string protocolId, int pos)
		{
			ImageView iv = (ImageView)ev.FindViewById(Resource.Id.filestorage_logo);

			Drawable drawable = App.Kp2a.GetResourceDrawable("ic_storage_" + protocolId);
			iv.SetImageDrawable(drawable);

			String title = App.Kp2a.GetResourceString("filestoragename_" + protocolId);
			var str = new SpannableString(title);
			_textView.TextFormatted = str;


		}
		
		
		public override void OnClick()
		{
		
		}
		
		
		public override void OnCreateMenu(IContextMenu menu, IContextMenuContextMenuInfo menuInfo)
		{
		}

		public override bool OnContextItemSelected(IMenuItem item)
		{
			return false;
		}
	}
}

