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
using Google.Android.Material.Dialog;
using keepass2android;
using KeePassLib.Interfaces;

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

		public string TitleText { get; set; }

		private void UpdateView()
		{
			if (!String.IsNullOrEmpty(_helpText))
			{
				Text = Context.GetString(Resource.String.icon_info);
				Clickable = true;


				if (_iconFont == null)
					_iconFont = Typeface.CreateFromAsset(Context.Assets, "fontawesome-webfont.ttf");

				System.Diagnostics.Debug.Assert(_iconFont != null, "_iconFont != null");

				SetTypeface(_iconFont, TypefaceStyle.Normal);
				//TextFormatted = Html.FromHtml("<a>" + Text + "</a>");
				//var spannableString = new SpannableString(Text);
				//spannableString.SetSpan(new UnderlineSpan(), 0, Text.Length, SpanTypes.ExclusiveExclusive);
				//TextFormatted = spannableString;

				MovementMethod = LinkMovementMethod.Instance;
				Click += (sender, args) =>
				{
					string title = Context.GetString(AppNames.AppNameResource);
					if (!string.IsNullOrEmpty(TitleText))
						title = TitleText;
					new MaterialAlertDialogBuilder(Context)
						.SetTitle(title)
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
			TitleText = a.GetString(Resource.Styleable.Kp2aShortHelpView_title_text);
			HelpText = a.GetString(Resource.Styleable.Kp2aShortHelpView_help_text);
		
		}
	}


    public class BackgroundOperationContainer : LinearLayout, IProgressUi
    {
        protected BackgroundOperationContainer(IntPtr javaReference, JniHandleOwnership transfer) : base(
            javaReference, transfer)
        {
        }

        public BackgroundOperationContainer(Context context) : base(context)
        {
        }

        public BackgroundOperationContainer(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            Initialize(attrs);
        }

        public BackgroundOperationContainer(Context context, IAttributeSet attrs, int defStyle) : base(context,
            attrs, defStyle)
        {
            Initialize(attrs);
        }

        private void Initialize(IAttributeSet attrs)
        {

            LayoutInflater inflater = (LayoutInflater)Context.GetSystemService(Context.LayoutInflaterService);
            inflater.Inflate(Resource.Layout.background_operation_container, this);


        }

        public void Show()
        {
            new Handler(Looper.MainLooper).Post(() =>
            {
                Visibility = ViewStates.Visible;
                FindViewById<TextView>(Resource.Id.background_ops_message)!.Visibility = ViewStates.Gone;
                FindViewById<TextView>(Resource.Id.background_ops_submessage)!.Visibility = ViewStates.Gone;
            });

        }

        public void Hide()
        {
            new Handler(Looper.MainLooper).Post(() =>
            {
                String activityType = Context.GetType().FullName;
                Kp2aLog.Log("Hiding background ops container in" + activityType);
                Visibility = ViewStates.Gone;
            });
        }

        public void UpdateMessage(string message)
        {
            new Handler(Looper.MainLooper).Post(() =>
            {
                TextView messageTextView = FindViewById<TextView>(Resource.Id.background_ops_message)!;
                if (string.IsNullOrEmpty(message))
                {
                    messageTextView.Visibility = ViewStates.Gone;
                }
                else
                {
                    messageTextView.Visibility = ViewStates.Visible;
                    messageTextView.Text = message;
                }
            });
        }

        public void UpdateSubMessage(string submessage)
        {
            new Handler(Looper.MainLooper).Post(() =>
            {
                TextView subMessageTextView = FindViewById<TextView>(Resource.Id.background_ops_submessage)!;
                if (string.IsNullOrEmpty(submessage))
                {
                    subMessageTextView.Visibility = ViewStates.Gone;
                }
                else
                {
                    subMessageTextView.Visibility = ViewStates.Visible;
                    subMessageTextView.Text = submessage;
                }
            });
        }
    }
}