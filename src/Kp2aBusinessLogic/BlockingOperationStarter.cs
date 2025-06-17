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

using Android.App;
using Android.Content;
using Android.OS;
using Java.Lang;
using Java.Security;
using System.Threading.Tasks;

namespace keepass2android
{
    /// <summary>
    /// Class to run a task while a progress dialog is shown
    /// </summary>
    public class BlockingOperationStarter
	{

		private readonly OperationWithFinishHandler _task;
        private readonly IKp2aApp _app;

	    public BlockingOperationStarter(IKp2aApp app, OperationWithFinishHandler task)
		{
			_task = task;
            _app = app;
        }

	    public void Run()
        {
            _app.CancelBackgroundOperations();
            OperationRunner.Instance.Run(_app, _task, true);


        }

	    
	}
}

