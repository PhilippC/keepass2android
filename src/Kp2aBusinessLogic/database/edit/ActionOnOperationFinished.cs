/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

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
using Android.App;
using Android.Content;
using Android.OS;
using keepass2android;

namespace keepass2android
{
  public class ActionOnOperationFinished : OnOperationFinishedHandler
  {
    public delegate void ActionToPerformOnFinsh(bool success, String message, bool importantMessage, Exception exception, Context activeContext);

    readonly ActionToPerformOnFinsh _actionToPerform;

    public ActionOnOperationFinished(IKp2aApp app, ActionToPerformOnFinsh actionToPerform) : base(app, null, null)
    {
      _actionToPerform = actionToPerform;
    }

    public ActionOnOperationFinished(IKp2aApp app, ActionToPerformOnFinsh actionToPerform, OnOperationFinishedHandler operationFinishedHandler) : base(app, operationFinishedHandler)
    {
      _actionToPerform = actionToPerform;
    }

    public override void Run()
    {
      if (Message == null)
        Message = "";
      if (Handler != null)
      {
        Handler.Post(() =>
        {
          _actionToPerform(Success, Message, ImportantMessage, Exception, ActiveContext);
        });
      }
      else
      {
        _actionToPerform(Success, Message, ImportantMessage, Exception, ActiveContext);
      }
      base.Run();
    }
  }
}



//Action which runs when the contextInstanceId is the active context
// otherwise it is registered as pending action for the context instance.
public class ActionInContextInstanceOnOperationFinished : ActionOnOperationFinished
{
  private readonly int _contextInstanceId;
  private IKp2aApp _app;

  public ActionInContextInstanceOnOperationFinished(int contextInstanceId, IKp2aApp app, ActionToPerformOnFinsh actionToPerform) : base(app, actionToPerform)
  {
    _contextInstanceId = contextInstanceId;
    _app = app;
  }
  public ActionInContextInstanceOnOperationFinished(int contextInstanceId, IKp2aApp app, ActionToPerformOnFinsh actionToPerform, OnOperationFinishedHandler operationFinishedHandler) : base(app, actionToPerform, operationFinishedHandler)
  {
    _contextInstanceId = contextInstanceId;
    _app = app;
  }

  public override void Run()
  {
    if ((ActiveContext as IContextInstanceIdProvider)?.ContextInstanceId != _contextInstanceId)
    {
      _app.RegisterPendingActionForContextInstance(_contextInstanceId, this);
    }
    else _app.UiThreadHandler.Post(() => base.Run());
  }

}

