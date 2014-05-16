using System;
using Android.App;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
	internal class StandardStringView : IStringView
	{
		private readonly int _viewId;
		private readonly int _containerViewId;
		private readonly Activity _activity;

		public StandardStringView(int viewId, int containerViewId, Activity activity)
		{
			_viewId = viewId;
			_containerViewId = containerViewId;
			_activity = activity;
		}

		public string Text
		{
			set
			{
				View container = _activity.FindViewById(_containerViewId);
				TextView tv = (TextView) _activity.FindViewById(_viewId);
				if (String.IsNullOrEmpty(value))
				{
					container.Visibility = tv.Visibility = ViewStates.Gone;
				}
				else
				{
					container.Visibility = tv.Visibility = ViewStates.Visible;
					tv.Text = value;
				}
			}
			get
			{
				TextView tv = (TextView) _activity.FindViewById(_viewId);
				return tv.Text;
			}
		}
	}
}