using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Text;
using Android.Text.Method;
using Android.Text.Style;
using Android.Text.Util;
using Android.Util;
using Android.Views;
using Android.Widget;
using PluginHostTest;

namespace keepass2android.views
{
	public class Kp2aShortHelpView: TextView
	{
		private string _helpText;
		private static Typeface _iconFont;

		protected Kp2aShortHelpView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
		{
		}

		public Kp2aShortHelpView(Context context) : base(context)
		{
		}

		public Kp2aShortHelpView(Context context, IAttributeSet attrs) : base(context, attrs)
		{
			Initialize(attrs);
		}

		public Kp2aShortHelpView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
		{
			Initialize(attrs);
		}

		public string HelpText
		{
			get { return _helpText; }
			set { _helpText = value;
				UpdateView();
			}
		}

		private void UpdateView()
		{
			if (!String.IsNullOrEmpty(_helpText))
			{
				Text = "i";
				Clickable = true;


				MovementMethod = LinkMovementMethod.Instance;
				Click += (sender, args) =>
				{
					new AlertDialog.Builder(Context)
						.SetTitle("PluginHostTest")
						.SetMessage(_helpText)
						.SetPositiveButton(Android.Resource.String.Ok, (o, eventArgs) => { })
					.Show();
				};
				Visibility = ViewStates.Visible;
			}
			else
			{
				Visibility = ViewStates.Gone;
			}
		}

		void Initialize(IAttributeSet attrs)
		{
			TypedArray a = Context.ObtainStyledAttributes(
		 attrs,
		 Resource.Styleable.Kp2aShortHelpView);
			HelpText = a.GetString(Resource.Styleable.Kp2aShortHelpView_help_text);
		
		}
	}
}