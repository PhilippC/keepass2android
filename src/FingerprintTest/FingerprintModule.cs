using System;
using Android.Content;
using Javax.Crypto;
using Java.Security;
using Java.Lang;
using Android.Views.InputMethods;
using Android.App;
using Android.Hardware.Fingerprints;
using Android.OS;
using Android.Security.Keystore;
using Android.Preferences;
using Java.IO;
using Java.Security.Cert;

namespace keepass2android
{
	public class FingerprintModule
	{
		public Context Context { get; set; }

		public FingerprintModule (Context context)
		{
			Context = context;
		}

		public FingerprintManager FingerprintManager 
		{
			get { return (FingerprintManager) Context.GetSystemService("FingerprintManager"); }
		}

		public KeyguardManager KeyguardManager 
		{
			get
			{
				return (KeyguardManager) Context.GetSystemService("keyguard");
			}
		}
	

		public KeyStore Keystore
		{
			get
			{
				try
				{
					return KeyStore.GetInstance("AndroidKeyStore");
				}
				catch (KeyStoreException e)
				{
					throw new RuntimeException("Failed to get an instance of KeyStore", e);
				}
			}
		}

		public KeyGenerator KeyGenerator
		{
			get
			{
				try
				{
					return KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore");
				}
				catch (NoSuchAlgorithmException e)
				{
					throw new RuntimeException("Failed to get an instance of KeyGenerator", e);
				}
				catch (NoSuchProviderException e)
				{
					throw new RuntimeException("Failed to get an instance of KeyGenerator", e);
				}
			}
		}

		public Cipher Cipher
		{
			get
			{
				try
				{
					return Cipher.GetInstance(KeyProperties.KeyAlgorithmAes + "/"
					                          + KeyProperties.BlockModeCbc + "/"
					                          + KeyProperties.EncryptionPaddingPkcs7);
				}
				catch (NoSuchAlgorithmException e)
				{
					throw new RuntimeException("Failed to get an instance of Cipher", e);
				}
				catch (NoSuchPaddingException e)
				{
					throw new RuntimeException("Failed to get an instance of Cipher", e);
				}
			}
		}

		public InputMethodManager InputMethodManager 
		{
			get { return (InputMethodManager) Context.GetSystemService(Context.InputMethodService); }
		}

		public ISharedPreferences SharedPreferences
		{
			get { return PreferenceManager.GetDefaultSharedPreferences(Context); }
		}
	}

	public class FingerprintEncryptionModule: FingerprintManager.AuthenticationCallback
	{
		public override void OnAuthenticationError(FingerprintState errorCode, ICharSequence errString)
		{
			_callback.OnAuthenticationError(errorCode, errString);
		}

		public override void OnAuthenticationFailed()
		{
			if (!_selfCancelled)
				_callback.OnAuthenticationFailed();
		}

		public override void OnAuthenticationHelp(FingerprintState helpCode, ICharSequence helpString)
		{
			_callback.OnAuthenticationHelp(helpCode, helpString);
		}

		public override void OnAuthenticationSucceeded(FingerprintManager.AuthenticationResult result)
		{
			_callback.OnAuthenticationSucceeded(result);
		}

		private readonly FingerprintModule _fingerprint;
		private readonly string _keyId;
		private Cipher _cipher;
		private bool _selfCancelled;
		private CancellationSignal _cancellationSignal;
		private FingerprintManager.CryptoObject _cryptoObject;
		private FingerprintManager.AuthenticationCallback _callback;

		public FingerprintEncryptionModule(FingerprintModule fingerprint, string keyId)
		{
			_fingerprint = fingerprint;
			_keyId = keyId;
			_cipher = fingerprint.Cipher;
			CreateKey();
		}

		/// <summary>
		/// Creates a symmetric key in the Android Key Store which can only be used after the user 
		/// has authenticated with fingerprint.
		/// </summary>
		private void CreateKey()
		{
			// The enrolling flow for fingerprint. This is where you ask the user to set up fingerprint
			// for your flow. Use of keys is necessary if you need to know if the set of
			// enrolled fingerprints has changed.
			try
			{
				_fingerprint.Keystore.Load(null);
				// Set the alias of the entry in Android KeyStore where the key will appear
				// and the constrains (purposes) in the constructor of the Builder
				_fingerprint.KeyGenerator.Init(new KeyGenParameterSpec.Builder(GetAlias(_keyId),
					KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
					.SetBlockModes(KeyProperties.BlockModeCbc)
					// Require the user to authenticate with a fingerprint to authorize every use
					// of the key
					.SetUserAuthenticationRequired(true)
					.SetEncryptionPaddings(KeyProperties.EncryptionPaddingPkcs7)
					.Build());
				_fingerprint.KeyGenerator.GenerateKey();
			}
			catch (NoSuchAlgorithmException e)
			{
				throw new RuntimeException(e);
			}
			catch (InvalidAlgorithmParameterException e)
			{
				throw new RuntimeException(e);
			}
			catch (CertificateException e)
			{
				throw new RuntimeException(e);
			}
			catch (IOException e)
			{
				throw new RuntimeException(e);
			}
		}

		public bool InitCipher()
		{
			try
			{
				_fingerprint.Keystore.Load(null);
				var key = _fingerprint.Keystore.GetKey(GetAlias(_keyId), null);
				_cipher.Init(CipherMode.EncryptMode, key);
				_cryptoObject = new FingerprintManager.CryptoObject(_cipher);
				return true;
			}
			catch (KeyPermanentlyInvalidatedException)
			{
				return false;
			}
			catch (KeyStoreException e)
			{
				throw new RuntimeException("Failed to init Cipher", e);
			}
			catch (CertificateException e)
			{
				throw new RuntimeException("Failed to init Cipher", e);
			}
			catch (UnrecoverableKeyException e)
			{
				throw new RuntimeException("Failed to init Cipher", e);
			}
			catch (IOException e)
			{
				throw new RuntimeException("Failed to init Cipher", e);
			}
			catch (NoSuchAlgorithmException e)
			{
				throw new RuntimeException("Failed to init Cipher", e);
			}
			catch (InvalidKeyException e)
			{
				throw new RuntimeException("Failed to init Cipher", e);
			}
		}

		private string GetAlias(string keyId)
		{
			return "keepass2android." + keyId;
		}

		public bool IsFingerprintAuthAvailable
		{
			get
			{
				return _fingerprint.FingerprintManager.IsHardwareDetected
					&& _fingerprint.FingerprintManager.HasEnrolledFingerprints;
			}
		}

		public void StartListening(FingerprintManager.AuthenticationCallback callback)
		{
			if (!IsFingerprintAuthAvailable)
				return;

			_cancellationSignal = new CancellationSignal();
			_selfCancelled = false;
			_callback = callback;
			_fingerprint.FingerprintManager.Authenticate(_cryptoObject, _cancellationSignal, 0 /* flags */, this, null);
			
		}

		public void StopListening()
		{
			if (_cancellationSignal != null)
			{
				_selfCancelled = true;
				_cancellationSignal.Cancel();
				_cancellationSignal = null;
			}
		}

		public void Encrypt(string textToEncrypt)
		{
			_cipher.DoFinal(MemUtil)
		}
	}
}