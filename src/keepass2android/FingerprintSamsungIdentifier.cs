using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Com.Samsung.Android.Sdk;
using Com.Samsung.Android.Sdk.Pass;
using Java.Lang;

namespace keepass2android
{
	class FingerprintSamsungIdentifier: IFingerprintIdentifier
	{
		SpassFingerprint _spassFingerprint;
		Spass _spass;
		public FingerprintSamsungIdentifier(Context context)
		{
			_spass = new Spass();

			try
			{
				_spass.Initialize(context);
			}
			catch (SecurityException)
			{
				//"Did you add the permission to the AndroidManifest.xml?");
				throw;
			}

			if (_spass.IsFeatureEnabled(Spass.DeviceFingerprint))
			{
				_spassFingerprint = new SpassFingerprint(context);
			}
			else
			{
				throw new RuntimeException("Fingerprint Featue not available.");
			}


		}

		public bool Init()
		{
			try
			{
				return _spassFingerprint.HasRegisteredFinger;
			}
			catch (UnsupportedOperationException)
			{
				return false;
			}
			
			
		}
		class IdentifyListener : Java.Lang.Object, IIdentifyListener
		{
			private readonly IFingerprintAuthCallback _callback;
			private readonly Context _context;
			private readonly FingerprintSamsungIdentifier _id;


			public IdentifyListener(IFingerprintAuthCallback callback, Context context, FingerprintSamsungIdentifier id)
			{
				_callback = callback;
				_context = context;
				_id = id;
			}

			

			public void OnFinished (int responseCode)
			{
				_id.Listening = false;
				if (responseCode == SpassFingerprint.StatusAuthentificationSuccess) 
				{
					_callback.OnFingerprintAuthSucceeded();
				} 
				else if (responseCode == SpassFingerprint.StatusAuthentificationPasswordSuccess) 
				{
					_callback.OnFingerprintAuthSucceeded();
				} 
				
			}

			public void OnReady ()
			{
				
			}

			public void OnStarted ()
			{
				
			}
		}

		internal bool Listening
		{
			get; set;
		}


		public void StartListening(Context ctx, IFingerprintAuthCallback callback)
		{
			if (Listening) return;

			try 
			{
				_spassFingerprint.StartIdentifyWithDialog(ctx, new IdentifyListener(callback, ctx, this), false);
				Listening = true;
			} 
			catch (SpassInvalidStateException m)
			{
				callback.OnFingerprintError(m.Message);
			} 
			catch (IllegalStateException ex) 
			{
				callback.OnFingerprintError(ex.Message);
			}
		}

		public void StopListening()
		{
			try
			{
				_spassFingerprint.CancelIdentify();
				Listening = false;
			}
			catch (IllegalStateException ise)
			{
				Kp2aLog.Log(ise.Message);
			}
		}
	}
}