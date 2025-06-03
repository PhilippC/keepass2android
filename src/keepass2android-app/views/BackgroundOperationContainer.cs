using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;

namespace keepass2android.views;

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

        FindViewById(Resource.Id.cancel_background).Click += (obj,args) =>
        {
            App.Kp2a.CancelBackgroundOperations();
        };
    }

    public void Show()
    {
        App.Kp2a.UiThreadHandler.Post(() =>
        {
            Kp2aLog.Log("OPR: Starting posted Show ");
            Visibility = ViewStates.Visible;
            FindViewById<TextView>(Resource.Id.background_ops_message)!.Visibility = ViewStates.Gone;
            FindViewById<TextView>(Resource.Id.background_ops_submessage)!.Visibility = ViewStates.Gone;
            Kp2aLog.Log("OPR: Finished posted Show ");
        });

    }

    public void Hide()
    {
        App.Kp2a.UiThreadHandler.Post(() =>
        {
            Kp2aLog.Log("OPR: Starting posted Hide ");
            String activityType = Context.GetType().FullName;
            Kp2aLog.Log("Hiding background ops container in" + activityType);
            Visibility = ViewStates.Gone;
            Kp2aLog.Log("OPR: Finished posted Hide ");
        });
    }

    public void UpdateMessage(string message)
    {
        App.Kp2a.UiThreadHandler.Post(() =>
        {
            Kp2aLog.Log("OPR: Starting posted UpdateMessage ");
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
            Kp2aLog.Log("OPR: Finished posted UpdateMessage ");
        });
    }

    public void UpdateSubMessage(string submessage)
    {
        App.Kp2a.UiThreadHandler.Post(() =>
        {
            Kp2aLog.Log("OPR: Starting posted UpdateSubMessage ");
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
            Kp2aLog.Log("OPR: Finished posted UpdateSubMessage ");
        });
    }
}