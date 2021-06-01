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
	public class LockingActivity : LifecycleAwareActivity {
	
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

	        var xcKey = App.Kp2a.CurrentDb?.KpDatabase.MasterKey.GetUserKey<ChallengeXCKey>();
	        if (CurrentlyWaitingKey == null && xcKey != null)
	        {
	            CurrentlyWaitingKey = xcKey;
	        }
	        if (CurrentlyWaitingKey != null)
	        {
	            CurrentlyWaitingKey.Activity = this;
            }



	    }

	    protected override void OnStop()
	    {
	        base.OnStop();
	        var xcKey = App.Kp2a.CurrentDb?.KpDatabase.MasterKey.GetUserKey<ChallengeXCKey>();
	        if (xcKey != null)
	        {
	            //don't store a pointer to this activity in the static database object to avoid memory leak
	            if (xcKey.Activity == this) //don't reset if another activity has come to foreground already
	                xcKey.Activity = null;
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

        //need to store this in the App object to make sure it survives activity recreation
	    protected ChallengeXCKey CurrentlyWaitingKey
	    {
	        get { return App.Kp2a._currentlyWaitingXcKey; }
	        set { App.Kp2a._currentlyWaitingXcKey = value; }
	    }


        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            Kp2aLog.Log("LockingActivity: OnActivityResult " + (requestCode == RequestCodeChallengeYubikey ? "yubichall" : ""));
	        base.OnActivityResult(requestCode, resultCode, data);
	        if ((requestCode == RequestCodeChallengeYubikey) && (CurrentlyWaitingKey != null))
	        {
	            if (resultCode == Result.Ok)
	            {
	                byte[] challengeResponse = data.GetByteArrayExtra("response");
	                if ((challengeResponse != null) && (challengeResponse.Length > 0))
	                {
	                    CurrentlyWaitingKey.Response = challengeResponse;
	                }
	                else
	                    CurrentlyWaitingKey.Error = "Did not receive a valid response.";
	            }
	            else
                {
                    CurrentlyWaitingKey.Error = "Cancelled Yubichallenge.";
                }
	            
            }

	    }



	    public Intent TryGetYubichallengeIntentOrPrompt(byte[] challenge, bool promptToInstall)
	    {
	        Intent chalIntent = new Intent("net.pp3345.ykdroid.intent.action.CHALLENGE_RESPONSE");
	        chalIntent.PutExtra("challenge", challenge);

            IList<ResolveInfo> activities = PackageManager.QueryIntentActivities(chalIntent, 0);
	        bool isIntentSafe = activities.Count > 0;
	        if (isIntentSafe)
	        {
	            return chalIntent;
	        }
	        if (promptToInstall)
	        {
	            AlertDialog.Builder b = new AlertDialog.Builder(this);
	            string message = GetString(Resource.String.NoChallengeApp) + " " + GetString(Resource.String.PleaseInstallApp, new Java.Lang.Object[]{"ykDroid"});

	            Intent yubichalIntent = new Intent("com.yubichallenge.NFCActivity.CHALLENGE");
	            IList<ResolveInfo> yubichallengeactivities = PackageManager.QueryIntentActivities(yubichalIntent, 0);
	            bool hasYubichallenge = yubichallengeactivities.Count > 0;
	            if (hasYubichallenge)
	                message += " " + GetString(Resource.String.AppOutdated, new Java.Lang.Object[] {"YubiChallenge"});

                b.SetMessage(message);
	            b.SetPositiveButton(Android.Resource.String.Ok,
	                delegate { Util.GotoUrl(this, GetString(Resource.String.MarketURL) + "net.pp3345.ykdroid"); });
	            b.SetNegativeButton(Resource.String.cancel, delegate { });
	            b.Create().Show();
	        }
	        return null;
	    }
	}
}

