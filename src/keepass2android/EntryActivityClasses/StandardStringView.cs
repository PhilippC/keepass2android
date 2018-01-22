using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
	internal class StandardStringView : IStringView
	{
		private readonly List<int> _viewIds;
		private readonly int _containerViewId;
		private readonly Activity _activity;

		public StandardStringView(List<int> viewIds, int containerViewId, Activity activity)
		{
			_viewIds = viewIds;
			_containerViewId = containerViewId;
			_activity = activity;
		}

		public string Text
		{
			set
			{
				View container = _activity.FindViewById(_containerViewId);
			    foreach (int viewId in _viewIds)
			    {
			        TextView tv = (TextView) _activity.FindViewById(viewId);
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
			}
			get
			{
				TextView tv = (TextView) _activity.FindViewById(_viewIds.First());
				return tv.Text;
			}
		}
	}
}