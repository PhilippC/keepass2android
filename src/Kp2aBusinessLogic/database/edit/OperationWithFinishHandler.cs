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
using Android.App;
using Android.Content;
using KeePassLib.Interfaces;

namespace keepass2android
{

    public abstract class OperationWithFinishHandler
    {

        protected OnOperationFinishedHandler _operationFinishedHandler;
        public IKp2aStatusLogger StatusLogger = new Kp2aNullStatusLogger(); //default: empty but not null
        private IActiveContextProvider _activeContextProvider;

        protected OperationWithFinishHandler(IActiveContextProvider activeContextProvider, OnOperationFinishedHandler operationFinishedHandler)
        {
            _activeContextProvider = activeContextProvider;
            _operationFinishedHandler = operationFinishedHandler;
        }

        public OnOperationFinishedHandler operationFinishedHandler
        {
            get { return _operationFinishedHandler; }
            set { _operationFinishedHandler = value; }
        }


        protected void Finish(bool result, String message, bool importantMessage = false, Exception exception = null)
        {
            if (operationFinishedHandler != null)
            {
                operationFinishedHandler.SetResult(result, message, importantMessage, exception);
                operationFinishedHandler.Run();
            }
        }

        protected void Finish(bool result)
        {
            if (operationFinishedHandler != null)
            {
                operationFinishedHandler.SetResult(result);
                operationFinishedHandler.Run();
            }
        }

        public void SetStatusLogger(IKp2aStatusLogger statusLogger)
        {
            if (operationFinishedHandler != null)
            {
                operationFinishedHandler.StatusLogger = statusLogger;
            }
            StatusLogger = statusLogger;
        }

        public abstract void Run();
    }
}

