using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace keepass2android.views
{
	public class TextWithHelp : RelativeLayout
	{
		public TextWithHelp(Context context, IAttributeSet attrs) :
			base(context, attrs)
		{
			Initialize(attrs);
		}

		public TextWithHelp(Context context, IAttributeSet attrs, int defStyle) :
			base(context, attrs, defStyle)
		{
			Initialize(attrs);
		}

		private void Initialize(IAttributeSet attrs)
		{
			LayoutInflater inflater = (LayoutInflater)Context.GetSystemService(Context.LayoutInflaterService);
			inflater.Inflate(Resource.Layout.text_with_help, this);

			TypedArray a = Context.ObtainStyledAttributes(
	 attrs,
	 Resource.Styleable.TextWithHelp);
			((Kp2aShortHelpView)FindViewById(Resource.Id.help)).HelpText = a.GetString(Resource.Styleable.TextWithHelp_help_text);

			const string xmlns = "http://schemas.android.com/apk/res/android";
			((TextView)FindViewById(Resource.Id.text)).Text = Context.GetString(attrs.GetAttributeResourceValue(xmlns, "text",Resource.String.ellipsis));

		}
	}
}