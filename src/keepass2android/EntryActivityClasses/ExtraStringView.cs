using System;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
	internal class ExtraStringView : IStringView
	{
		private readonly View _container;
		private readonly TextView _valueView;
		private readonly TextView _keyView;

		public ExtraStringView(LinearLayout container, TextView valueView, TextView keyView)
		{
			_container = container;
			_valueView = valueView;
			_keyView = keyView;
		}

		public View View
		{
			get { return _container; }
		}

		public string Text
		{
			get { return _valueView.Text; }
			set
			{
				if (String.IsNullOrEmpty(value))
				{
					_valueView.Visibility = ViewStates.Gone;
					_keyView.Visibility = ViewStates.Gone;
				}
				else
				{
					_valueView.Visibility = ViewStates.Visible;
					_keyView.Visibility = ViewStates.Visible;
					_valueView.Text = value;
				}
			}
		}
	}
}