using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace keepass2android.EntryActivityClasses
{
	internal class ViewImagePopupItem:IPopupMenuItem
	{
			private readonly string _key;
		private readonly EntryActivity _entryActivity;

		public ViewImagePopupItem(string key, EntryActivity entryActivity)
		{
			_key = key;
			_entryActivity = entryActivity;
		}
		public Drawable Icon
		{
			get
			{
				 return _entryActivity.Resources.GetDrawable(Resource.Drawable.ic_picture); 
			}
		}

		public string Text
		{
			get
			{
			 return _entryActivity.Resources.GetString(Resource.String.ShowAttachedImage); 
			}
		}

		public void HandleClick()
		{
			_entryActivity.ShowAttachedImage(_key);

		}
	}
}