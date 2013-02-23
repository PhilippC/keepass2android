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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
	public class ActionOnFinish: OnFinish
	{
		public delegate void ActionToPerformOnFinsh(bool success, String message);

		ActionToPerformOnFinsh actionToPerform;

		public ActionOnFinish(ActionToPerformOnFinsh actionToPerform) : base(null, null)
		{
			this.actionToPerform = actionToPerform;
		}

		public ActionOnFinish(ActionToPerformOnFinsh actionToPerform, Handler handler) : base(handler)
		{
			this.actionToPerform = actionToPerform;
		}

		public override void run()
		{
			if (this.mMessage == null)
				this.mMessage = "";
			if (this.mHandler != null)
			{
				this.mHandler.Post(() => {actionToPerform(this.mSuccess, this.mMessage);});
			}
			else
				actionToPerform(this.mSuccess, this.mMessage);
			base.run();
		}
	}
}

