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
using Android.OS;

namespace keepass2android
{
	public class ActionOnFinish: OnFinish
	{
		public delegate void ActionToPerformOnFinsh(bool success, String message);

		readonly ActionToPerformOnFinsh _actionToPerform;

		public ActionOnFinish(ActionToPerformOnFinsh actionToPerform) : base(null, null)
		{
			_actionToPerform = actionToPerform;
		}

		public ActionOnFinish(ActionToPerformOnFinsh actionToPerform, OnFinish finish) : base(finish)
		{
			_actionToPerform = actionToPerform;
		}

		public ActionOnFinish(ActionToPerformOnFinsh actionToPerform, Handler handler) : base(handler)
		{
			_actionToPerform = actionToPerform;
		}

		public override void Run()
		{
			if (Message == null)
				Message = "";
			if (Handler != null)
			{
				Handler.Post(() => {_actionToPerform(Success, Message);});
			}
			else
				_actionToPerform(Success, Message);
			base.Run();
		}
	}
}

