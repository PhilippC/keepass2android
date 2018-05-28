/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
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
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;

namespace keepass2android
{
	/// <summary>
	/// Base class for activities. Notifies the TimeoutHelper whether the app is active or not.
	/// </summary>
	public class LockingActivity : LifecycleDebugActivity {
	
		public LockingActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		public LockingActivity()
		{
		}

	    protected override void OnStart()
	    {
	        base.OnStart();

	        if (App.Kp2a.GetDb().Loaded)
	        {
	            var xcKey = App.Kp2a.GetDb().KpDatabase.MasterKey.GetUserKey<ChallengeXCKey>();
	            if (xcKey != null)
	            {
	                xcKey.Activity = this;
	                _currentlyWaitingKey = xcKey;

	            }

	        }

        }

	    protected override void OnStop()
	    {
	        base.OnStop();
	        if (App.Kp2a.GetDb().Loaded)
	        {
	            var xcKey = App.Kp2a.GetDb().KpDatabase.MasterKey.GetUserKey<ChallengeXCKey>();
	            if (xcKey != null)
	            {
                    //don't store a pointer to this activity in the static database object to avoid memory leak
                    if (xcKey.Activity == this) //don't reset if another activity has come to foreground already
	                    xcKey.Activity = null;
	            }

	        }
        }

	    protected override void OnPause() {
			base.OnPause();
			
			TimeoutHelper.Pause(this);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			GC.Collect();
		}
		
		protected override void OnResume() {
			base.OnResume();
			
			TimeoutHelper.Resume(this);
		}

	    public const int RequestCodeChallengeYubikey = 793;

	    protected ChallengeXCKey _currentlyWaitingKey;


        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
	    {
	        base.OnActivityResult(requestCode, resultCode, data);
	        if ((requestCode == RequestCodeChallengeYubikey) && (_currentlyWaitingKey != null))
	        {
	            if (resultCode == Result.Ok)
	            {
	                byte[] challengeResponse = data.GetByteArrayExtra("response");
	                if ((challengeResponse != null) && (challengeResponse.Length > 0))
	                {
	                    _currentlyWaitingKey.Response = challengeResponse;
	                }
	                else
	                    _currentlyWaitingKey.Error = "Did not receive a valid response.";
	                    

	            }
	            else
                {
                    _currentlyWaitingKey.Error = "Cancelled Yubichallenge.";
                }
	            
            }

	    }


	    public Intent GetYubichallengeIntent(byte[] challenge)
	    {
	        Intent chalIntent = new Intent(this, typeof(YubiChallengeActivity));
	        chalIntent.PutExtra("challenge", challenge);
	        return chalIntent;
	        
	    }
    }
}

