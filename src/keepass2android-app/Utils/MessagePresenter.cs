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

using Android.OS;
using Android.Views;
using Google.Android.Material.Snackbar;

namespace keepass2android.Utils
{
  public struct Message
  {
    public Message()
    {
      Text = null;
      Severity = MessageSeverity.Info;
    }

    public string Text { get; set; }
    public MessageSeverity Severity { get; set; }
    public bool ShowOnSubsequentScreens { get; set; } = true;
  }
  public interface IMessagePresenter
  {
    void ShowMessage(Message message);

    List<Message> PendingMessages
    {
      get;
    }

  }

  internal class NonePresenter : IMessagePresenter
  {
    public void ShowMessage(Message message)
    {
      PendingMessages.Add(message);
    }

    public List<Message> PendingMessages
    {
      get;
      set;
    } = new List<Message>();
  }


  internal class ToastPresenter : IMessagePresenter
  {
    public void ShowMessage(Message message)
    {
      new Handler(Looper.MainLooper).Post(() =>
      {
        Toast.MakeText(App.Context, message.Text, ToastLength.Long).Show();
      });
    }

    public List<Message> PendingMessages => new();
  }

  internal class ChainedSnackbarPresenter : IMessagePresenter
  {
    internal ChainedSnackbarPresenter(View anchorView)
    {
      this.AnchorView = anchorView;

    }

    private DateTime nextSnackbarShowTime = DateTime.Now;
    private List<Message> queuedMessages = new List<Message>();

    public View AnchorView { get; set; }

    private TimeSpan chainingTime = TimeSpan.FromSeconds(1.5);
    private Snackbar snackbar;
    private Message lastMessage;

    public void ShowMessage(Message message)
    {
      if (DateTime.Now <= nextSnackbarShowTime)
      {
        var waitDuration = nextSnackbarShowTime - DateTime.Now;
        nextSnackbarShowTime = nextSnackbarShowTime.Add(chainingTime);

        if (!queuedMessages.Any())
        {
          if (Looper.MainLooper != null)
          {
            new Handler(Looper.MainLooper).PostDelayed(ShowNextSnackbar,
                (long)waitDuration.TotalMilliseconds);
          }
          else
          {
            Kp2aLog.Log("Currently cannot show message");
          }
        }

        queuedMessages.Add(message);

        return;
      }
      ShowSnackbarNow(message);
      nextSnackbarShowTime = DateTime.Now.Add(chainingTime);

    }

    public List<Message> PendingMessages
    {
      get
      {
        List<Message> pendingMessages = new List<Message>();
        if (snackbar?.IsShown == true)
        {
          pendingMessages.Add(lastMessage);
        }

        pendingMessages.AddRange(queuedMessages);
        return pendingMessages;
      }
    }

    private void ShowNextSnackbar()
    {
      if (!queuedMessages.Any())
      {
        return;
      }

      ShowSnackbarNow(queuedMessages.First());
      queuedMessages.RemoveAt(0);

      if (!queuedMessages.Any())
      {
        new Handler().PostDelayed(() => { ShowNextSnackbar(); }, (long)chainingTime.TotalMilliseconds);
      }
    }

    private void ShowSnackbarNow(Message message)
    {
      snackbar = Snackbar
          .Make(AnchorView, message.Text,
              Snackbar.LengthLong);
      snackbar.SetTextMaxLines(10);
      if ((int)Build.VERSION.SdkInt >= 23)
      {
        if (message.Severity == MessageSeverity.Error)
        {
          snackbar.SetBackgroundTint(App.Context.GetColor(Resource.Color.md_theme_errorContainer));
          snackbar.SetTextColor(App.Context.GetColor(Resource.Color.md_theme_onErrorContainer));
        }
        else if (message.Severity == MessageSeverity.Warning)
        {
          snackbar.SetBackgroundTint(App.Context.GetColor(Resource.Color.md_theme_inverseSurface));
          snackbar.SetTextColor(App.Context.GetColor(Resource.Color.md_theme_inverseOnSurface));
        }
        else
        {
          snackbar.SetBackgroundTint(App.Context.GetColor(Resource.Color.md_theme_secondaryContainer));
          snackbar.SetTextColor(App.Context.GetColor(Resource.Color.md_theme_onSecondaryContainer));
        }
      }

      snackbar.Show();
      lastMessage = message;

    }
  }
}
