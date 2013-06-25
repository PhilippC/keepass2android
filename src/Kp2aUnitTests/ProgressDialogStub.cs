using System;
using System.Collections.Generic;
using System.Text;
using keepass2android;

namespace Kp2aUnitTests
{
	class ProgressDialogStub : IProgressDialog
	{
		public void SetTitle(string title)
		{
			
		}

		public void SetMessage(string getResourceString)
		{
			
		}

		public void Dismiss()
		{
			Dismissed = true;
		}

		public void Show()
		{
			Showed = true;
		}

		protected bool Showed { get; set; }

		public bool Dismissed { get; set; }
	}
}
