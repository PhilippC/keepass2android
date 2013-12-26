using System;
using Android.App;
using Android.Runtime;
using Android.Views;

namespace keepass2android.view
{
	public sealed class FileStorageViewKp2a: ClickView
	{
		public FileStorageViewKp2a(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
		{
		}

		public FileStorageViewKp2a(Activity activity)
			: base(activity)
		{
			View ev = Inflate(activity, Resource.Layout.filestorage_selection_listitem_kp2a, null);
			LayoutParams lp = new LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
			
			AddView(ev, lp);
			
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