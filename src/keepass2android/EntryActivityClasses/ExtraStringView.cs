using System;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
	internal class ExtraStringView : IStringView
	{
		private readonly View _container;
		private readonly TextView _valueView;
	    private readonly TextView _visibleValueView;
        private readonly TextView _keyView;

		public ExtraStringView(LinearLayout container, TextView valueView, TextView visibleValueView, TextView keyView)
		{
			_container = container;
			_valueView = valueView;
		    _visibleValueView = visibleValueView;
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
					_container.Visibility = ViewStates.Gone;
				}
				else
				{
					_container.Visibility = ViewStates.Visible;
					_valueView.Text = value;
				    if (_visibleValueView != null)
				        _visibleValueView.Text = value;

				}
			}
		}
	}
}