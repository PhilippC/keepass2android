/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System;
using Android;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Google.Android.Material.Dialog;
using KeePassLib.Interfaces;

namespace keepass2android
{
  public interface IActiveContextProvider
  {
    Context ActiveContext { get; }
  }

  public abstract class OnOperationFinishedHandler
  {
    protected bool Success;
    protected String Message;
    protected Exception Exception;

    protected bool ImportantMessage
    {
      get;
      set;
    }

    protected Context ActiveContext
    {
      get
      {
        return _activeContextProvider?.ActiveContext;
      }
    }

    protected OnOperationFinishedHandler NextOnOperationFinishedHandler;
    protected Handler Handler;
    private IKp2aStatusLogger _statusLogger = new Kp2aNullStatusLogger(); //default: no logging but not null -> can be used whenever desired
    private readonly IActiveContextProvider _activeContextProvider;

    public IKp2aStatusLogger StatusLogger
    {
      get { return _statusLogger; }
      set { _statusLogger = value; }
    }
    protected OnOperationFinishedHandler(IActiveContextProvider activeContextProvider, Handler handler)
    {
      _activeContextProvider = activeContextProvider;
      NextOnOperationFinishedHandler = null;
      Handler = handler;
    }

    protected OnOperationFinishedHandler(IActiveContextProvider activeContextProvider, OnOperationFinishedHandler operationFinishedHandler, Handler handler)
    {
      _activeContextProvider = activeContextProvider;
      NextOnOperationFinishedHandler = operationFinishedHandler;
      Handler = handler;
    }

    protected OnOperationFinishedHandler(IActiveContextProvider activeContextProvider, OnOperationFinishedHandler operationFinishedHandler)
    {
      _activeContextProvider = activeContextProvider;
      NextOnOperationFinishedHandler = operationFinishedHandler;
      Handler = null;
    }

    public void SetResult(bool success, string message, bool importantMessage, Exception exception)
    {
      Success = success;
      Message = message;
      ImportantMessage = importantMessage;
      Exception = exception;
    }


    public void SetResult(bool success)
    {
      Success = success;
    }

    public virtual void Run()
    {
      if (NextOnOperationFinishedHandler == null) return;
      // Pass on result on call finish
      NextOnOperationFinishedHandler.SetResult(Success, Message, ImportantMessage, Exception);

      var handler = Handler ?? NextOnOperationFinishedHandler.Handler ?? null;

      if (handler != null)
      {
        handler.Post(() =>
        {
          NextOnOperationFinishedHandler.Run();
        });
      }
      else
      {
        NextOnOperationFinishedHandler.Run();
      }
    }

    protected void DisplayMessage(Context ctx)
    {
      DisplayMessage(ctx, Message, ImportantMessage);
    }

    public static void DisplayMessage(Context ctx, string message, bool makeDialog)
    {
      if (!String.IsNullOrEmpty(message))
      {
        Kp2aLog.Log("OnOperationFinishedHandler message: " + message);
        if (makeDialog && ctx != null)
        {
          try
          {
            MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(ctx);

            builder.SetMessage(message)
                .SetPositiveButton(Android.Resource.String.Ok, (sender, args) => ((Dialog)sender).Dismiss())
                .Show();

          }
          catch (Exception)
          {
            Toast.MakeText(ctx, message, ToastLength.Long).Show();
          }
        }
        else
          Toast.MakeText(ctx ?? Application.Context, message, ToastLength.Long).Show();
      }
    }
  }
}


