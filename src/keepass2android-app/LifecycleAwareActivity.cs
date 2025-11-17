/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

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
using System.Runtime.InteropServices;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;

namespace keepass2android
{

  public abstract class LifecycleAwareActivity : AndroidX.AppCompat.App.AppCompatActivity, IContextInstanceIdProvider
  {
    protected override void AttachBaseContext(Context baseContext)
    {
      base.AttachBaseContext(LocaleManager.setLocale(baseContext));
    }

    protected LifecycleAwareActivity(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {

    }

    protected LifecycleAwareActivity()
    {
    }


    string _className;

    string ClassName
    {
      get
      {
        if (_className == null)
          _className = GetType().Name;
        return _className;
      }
    }

    public string MyDebugName
    {
      get { return ClassName + " " + ID; }
    }

    private static int staticCount = 0;

    private int ID = staticCount++;

    protected override void OnNewIntent(Intent intent)
    {
      base.OnNewIntent(intent);
      Kp2aLog.Log(ClassName + ".OnNewIntent " + ID);
    }

    public Action<Bundle>? OnCreateListener { get; set; }
    public Action<Bundle>? OnSaveInstanceStateListener { get; set; }

    public Func<bool?>? OnSupportNavigateUpListener { get; set; }

    public override bool OnSupportNavigateUp()
    {
      bool baseRes = base.OnSupportNavigateUp();
      bool? res = OnSupportNavigateUpListener?.Invoke();
      if (res != null)
        return res.Value;
      return baseRes;
    }


    protected override void OnResume()
    {
      base.OnResume();
      App.Kp2a.PerformPendingActions(_instanceId);

      Kp2aLog.Log(ClassName + ".OnResume " + ID);
      if (App.Kp2a.CurrentDb == null)
      {
        Kp2aLog.Log(" DB null" + " " + ID);
      }
      else
      {
        Kp2aLog.Log(" DatabaseIsUnlocked=" + App.Kp2a.DatabaseIsUnlocked + " " + ID);
      }
    }

    protected override void OnStart()
    {
      App.Kp2a.ActiveContext = this;
      base.OnStart();
      Kp2aLog.Log(ClassName + ".OnStart" + " " + ID);
    }

    const string ID_KEY = "kp2a_context_instance_id";
    const int InvalidId = -1;

    private int _instanceId;

    protected override void OnCreate(Bundle bundle)
    {

      base.OnCreate(bundle);

      _instanceId = bundle?.GetInt(ID_KEY, InvalidId) ?? InvalidId;
      if (_instanceId == InvalidId)
      {
        _instanceId = _nextContextInstanceId++;
      }

      OnCreateListener?.Invoke(bundle);

      Kp2aLog.Log(ClassName + ".OnCreate" + " " + ID + " (instance=" + _instanceId + ")");
      Kp2aLog.Log(ClassName + ":apptask=" + Intent.GetStringExtra("KP2A_APP_TASK_TYPE") + " " + ID);
    }


    protected override void OnDestroy()
    {
      base.OnDestroy();
      Kp2aLog.Log(ClassName + ".OnDestroy " + IsFinishing.ToString() + " " + ID);
    }

    protected override void OnPause()
    {
      base.OnPause();
      Kp2aLog.Log(ClassName + ".OnPause" + " " + ID);
    }

    protected override void OnStop()
    {
      base.OnStop();
      Kp2aLog.Log(ClassName + ".OnStop" + " " + ID);
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
      base.OnSaveInstanceState(outState);
      outState.PutInt(ID_KEY, _instanceId);
      OnSaveInstanceStateListener?.Invoke(outState);
    }

    static int _nextContextInstanceId = 0;

    public int ContextInstanceId => _instanceId;
  }
}

