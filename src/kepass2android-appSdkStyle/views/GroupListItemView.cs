using System;
using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace keepass2android.view
{
    public abstract class GroupListItemView : LinearLayout
    {
        protected readonly GroupBaseActivity _groupBaseActivity;

        protected GroupListItemView(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        public GroupListItemView(GroupBaseActivity context)
            : base(context)
        {
            _groupBaseActivity = context;

        }

        public GroupListItemView(Context context, IAttributeSet attrs)
            : base(context, attrs)
        {
        }

        public GroupListItemView(Context context, IAttributeSet attrs, int defStyleAttr)
            : base(context, attrs, defStyleAttr)
        {
        }

        public GroupListItemView(Context context, IAttributeSet attrs, int defStyleAttr, int defStyleRes)
            : base(context, attrs, defStyleAttr, defStyleRes)
        {
        }


        public override bool Activated
        {
            get { return base.Activated; }
            set
            {
                if (value)
                {
                    FindViewById(Resource.Id.icon).Visibility = ViewStates.Invisible;
                    FindViewById(Resource.Id.check_mark).Visibility = ViewStates.Visible;
                }
                else
                {
                    FindViewById(Resource.Id.icon).Visibility = ViewStates.Visible;
                    FindViewById(Resource.Id.check_mark).Visibility = ViewStates.Invisible;
                }

                base.Activated = value;
            }
        }

	    public void SetRightArrowVisibility(bool visible)
	    {
			FindViewById(Resource.Id.right_arrow).Visibility = visible ? ViewStates.Visible : ViewStates.Invisible;
	    }

        public abstract void OnClick();

    }
}