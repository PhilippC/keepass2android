using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Hardware.Biometrics;
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
	class BiometrySamsungIdentifier: IBiometricIdentifier
	{
        private readonly Context _context;
        SpassFingerprint _spassFingerprint;
		Spass _spass;
		public BiometrySamsungIdentifier(Context context)
		{
            _context = context;
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
			private readonly IBiometricAuthCallback _callback;
			private readonly Context _context;
			private readonly BiometrySamsungIdentifier _id;


			public IdentifyListener(IBiometricAuthCallback callback, Context context, BiometrySamsungIdentifier id)
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
					_callback.OnBiometricAuthSucceeded();
				} 
				else if (responseCode == SpassFingerprint.StatusAuthentificationPasswordSuccess) 
				{
					_callback.OnBiometricAuthSucceeded();
				} 
				
			}

			public void OnReady ()
			{
				
			}
			public void OnCompleted()
			{
				// TODO
			}

			public void OnStarted ()
			{
				
			}
		}

		internal bool Listening
		{
			get; set;
		}


		public void StartListening(IBiometricAuthCallback callback)
		{
			if (Listening) return;

			try 
			{
				_spassFingerprint.StartIdentifyWithDialog(_context, new IdentifyListener(callback, _context, this), false);
				Listening = true;
			} 
			catch (SpassInvalidStateException m)
			{
				callback.OnBiometricError(m.Message);
			} 
			catch (IllegalStateException ex) 
			{
				callback.OnBiometricError(ex.Message);
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
				Kp2aLog.Log(ise.ToString());
			}
			catch (System.Exception e)
			{
				Kp2aLog.LogUnexpectedError(e);
			}
		}

        public bool HasUserInterface
        {
            get { return false; }
        }
    }
}