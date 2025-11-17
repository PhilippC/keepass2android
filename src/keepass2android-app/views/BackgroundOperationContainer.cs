// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

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

    FindViewById(Resource.Id.cancel_background).Click += (obj, args) =>
    {
      App.Kp2a.CancelBackgroundOperations();
    };
  }

  public void Show()
  {
    App.Kp2a.UiThreadHandler.Post(() =>
    {
      Visibility = ViewStates.Visible;
      FindViewById<TextView>(Resource.Id.background_ops_message)!.Visibility = ViewStates.Gone;
      FindViewById<TextView>(Resource.Id.background_ops_submessage)!.Visibility = ViewStates.Gone;
    });

  }

  public void Hide()
  {
    App.Kp2a.UiThreadHandler.Post(() =>
    {
      String activityType = Context.GetType().FullName;
      Visibility = ViewStates.Gone;
    });
  }

  public void UpdateMessage(string message)
  {
    App.Kp2a.UiThreadHandler.Post(() =>
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
    App.Kp2a.UiThreadHandler.Post(() =>
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