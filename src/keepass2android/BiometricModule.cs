using System;
using Android.Content;
using Javax.Crypto;
using Java.Security;
using Java.Lang;
using Android.Views.InputMethods;
using Android.App;
using Android.OS;
using Android.Security.Keystore;
using Android.Preferences;
using Android.Util;
using Android.Widget;
using Androidx.Biometric;
using AndroidX.Fragment.App;
using Java.IO;
using Java.Security.Cert;
using Java.Util.Concurrent;
using Javax.Crypto.Spec;
using Exception = System.Exception;
using File = System.IO.File;

namespace keepass2android
{

    public static class Kp2aLog
    {
        private static bool? _logToFile;

        private static object _fileLocker = new object();

        public static void Log(string message)
        {
            if (message != null)
                Android.Util.Log.Debug("KP2A", message);
            if (LogToFile)
            {
                lock (_fileLocker)
                {
                    try
                    {
                        using (var streamWriter = System.IO.File.AppendText(LogFilename))
                        {
                            string stringToLog = DateTime.Now + ":" + DateTime.Now.Millisecond + " -- " + message;
                            streamWriter.WriteLine(stringToLog);
                        }
                    }
                    catch (Exception e)
                    {
                        Android.Util.Log.Debug("KP2A", "Couldn't write to log file. " + e);
                    }
                }

            }

        }

        private static string LogFilename
        {
            get { return Application.Context.FilesDir.CanonicalPath + "/keepass2android.log"; }
        }

        private static bool LogToFile
        {
            get
            {
                if (_logToFile == null)
                    _logToFile = System.IO.File.Exists(LogFilename);
                return (bool)_logToFile;
            }
        }
        public static event EventHandler<Exception> OnUnexpectedError;

        public static void LogUnexpectedError(Exception exception)
        {
            Log(exception.ToString());
            if (OnUnexpectedError != null)
                OnUnexpectedError(null, exception);
        }

        public static void CreateLogFile()
        {
            if (!System.IO.File.Exists(LogFilename))
            {
                System.IO.File.Create(LogFilename).Dispose();
                _logToFile = true;
            }


        }

        public static void FinishLogFile()
        {
            if (System.IO.File.Exists(LogFilename))
            {
                _logToFile = false;
                int count = 0;
                while (System.IO.File.Exists(LogFilename + "." + count))
                    count++;
                System.IO.File.Move(LogFilename, LogFilename + "." + count);

            }

        }

        public static void SendLog(Context ctx)
        {
            if (!System.IO.File.Exists(LogFilename))
                return;
            Intent sendIntent = new Intent();
            sendIntent.SetAction(Intent.ActionSend);
            sendIntent.PutExtra(Intent.ExtraText, File.ReadAllText(LogFilename));
            sendIntent.PutExtra(Intent.ExtraEmail, "crocoapps@gmail.com");
            sendIntent.PutExtra(Intent.ExtraSubject, "Keepass2Android log");
            sendIntent.SetType("text/plain");
            ctx.StartActivity(Intent.CreateChooser(sendIntent, "Send log to..."));
        }
    }
    public interface IBiometricAuthCallback
    {
        void OnBiometricAuthSucceeded();
        void OnBiometricError(string toString);
    }

    public class BiometricModule
    {
        public AndroidX.Fragment.App.FragmentActivity Activity { get; set; }

        public BiometricModule(AndroidX.Fragment.App.FragmentActivity activity)
        {
            Activity = activity;
        }


        public KeyguardManager KeyguardManager
        {
            get
            {
                return (KeyguardManager)Activity.GetSystemService("keyguard");
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

        public ISharedPreferences SharedPreferences
        {
            get { return PreferenceManager.GetDefaultSharedPreferences(Activity); }
        }

        public bool IsAvailable
        {
            get
            {
                return BiometricManager.From(Activity).CanAuthenticate() ==
                       BiometricManager.BiometricSuccess;
            }
        }

        public bool IsHardwareAvailable
        {
            get
            {
                var result = BiometricManager.From(Activity).CanAuthenticate();
                return result == BiometricManager.BiometricSuccess 
                       || result == BiometricManager.BiometricErrorNoneEnrolled;
            }
        }
    }

    public abstract class BiometricCrypt : IBiometricIdentifier
    {
        protected const string FailedToInitCipher = "Failed to init Cipher";

        protected readonly string _keyId;

        protected Cipher _cipher;
        private CancellationSignal _cancellationSignal;
        protected BiometricPrompt.CryptoObject _cryptoObject;

        protected KeyStore _keystore;

        private BiometricPrompt _biometricPrompt;
        private FragmentActivity _activity;

        public BiometricCrypt(BiometricModule biometric, string keyId)
        {
            Kp2aLog.Log("FP: Create " + this.GetType().Name);
            _keyId = keyId;
            _cipher = biometric.Cipher;
            _keystore = biometric.Keystore;
            _activity = biometric.Activity;



        }

        public abstract bool Init();


        protected static string GetAlias(string keyId)
        {
            return "keepass2android." + keyId;
        }
        protected static string GetIvPrefKey(string prefKey)
        {
            return prefKey + "_iv";
        }

        public void StartListening(IBiometricAuthCallback callback)
        {
            
            StartListening(new BiometricAuthCallbackAdapter(callback, _activity));
        }

        public void StopListening()
        {
            
        }

        public bool HasUserInterface
        {
            get { return true; }

        }

        public void StartListening(BiometricPrompt.AuthenticationCallback callback)
        {

            Kp2aLog.Log("Fingerprint: StartListening ");

            var executor = Executors.NewSingleThreadExecutor();
            _biometricPrompt = new Androidx.Biometric.BiometricPrompt(_activity, executor, callback);

            BiometricPrompt.PromptInfo promptInfo = new BiometricPrompt.PromptInfo.Builder()
                .SetTitle(_activity.GetString(AppNames.AppNameResource))
                .SetSubtitle(_activity.GetString(Resource.String.unlock_database_title))
                .SetNegativeButtonText(_activity.GetString(Android.Resource.String.Cancel))
                .Build();


            _biometricPrompt.Authenticate(promptInfo, _cryptoObject);

        }

        public string Encrypt(string textToEncrypt)
        {
            Kp2aLog.Log("FP: Encrypting");
            return Base64.EncodeToString(_cipher.DoFinal(System.Text.Encoding.UTF8.GetBytes(textToEncrypt)), 0);
        }


        public void StoreEncrypted(string textToEncrypt, string prefKey, Context context)
        {
            var edit = PreferenceManager.GetDefaultSharedPreferences(context).Edit();
            StoreEncrypted(textToEncrypt, prefKey, edit);
            edit.Commit();
        }

        public void StoreEncrypted(string textToEncrypt, string prefKey, ISharedPreferencesEditor edit)
        {
            edit.PutString(prefKey, Encrypt(textToEncrypt));
            edit.PutString(GetIvPrefKey(prefKey), Base64.EncodeToString(CipherIv, 0));

        }


        private byte[] CipherIv
        {
            get { return _cipher.GetIV(); }
        }
    }

    public interface IBiometricIdentifier
    {
        bool Init();
        void StartListening(IBiometricAuthCallback callback);

        void StopListening();
        bool HasUserInterface { get; }
    }

    public class BiometricDecryption : BiometricCrypt
    {
        private readonly Context _context;
        private readonly byte[] _iv;


        public BiometricDecryption(BiometricModule biometric, string keyId, byte[] iv) : base(biometric, keyId)
        {
            _iv = iv;
        }

        public BiometricDecryption(BiometricModule biometric, string keyId, Context context, string prefKey)
            : base(biometric, keyId)
        {
            _context = context;
            _iv = Base64.Decode(PreferenceManager.GetDefaultSharedPreferences(context).GetString(GetIvPrefKey(prefKey), null), 0);
        }

        public static bool IsSetUp(Context context, string prefKey)
        {
            return PreferenceManager.GetDefaultSharedPreferences(context).GetString(GetIvPrefKey(prefKey), null) != null;
        }

        public override bool Init()
        {
            Kp2aLog.Log("FP: Init for Dec");
            try
            {
                _keystore.Load(null);
                var key = _keystore.GetKey(GetAlias(_keyId), null);
                var ivParams = new IvParameterSpec(_iv);
                _cipher.Init(CipherMode.DecryptMode, key, ivParams);

                _cryptoObject = new BiometricPrompt.CryptoObject(_cipher);
                return true;
            }
            catch (KeyPermanentlyInvalidatedException e)
            {
                Kp2aLog.Log("FP: KeyPermanentlyInvalidatedException." + e.ToString());
                return false;
            }
            catch (KeyStoreException e)
            {
                throw new RuntimeException(FailedToInitCipher, e);
            }
            catch (CertificateException e)
            {
                throw new RuntimeException(FailedToInitCipher, e);
            }
            catch (UnrecoverableKeyException e)
            {
                throw new RuntimeException(FailedToInitCipher, e);
            }
            catch (IOException e)
            {
                throw new RuntimeException(FailedToInitCipher, e);
            }
            catch (NoSuchAlgorithmException e)
            {
                throw new RuntimeException(FailedToInitCipher, e);
            }
            catch (InvalidKeyException e)
            {
                throw new RuntimeException(FailedToInitCipher, e);
            }
        }


        public string Decrypt(string encryted)
        {
            Kp2aLog.Log("FP: Decrypting ");
            byte[] encryptedBytes = Base64.Decode(encryted, 0);
            return System.Text.Encoding.UTF8.GetString(_cipher.DoFinal(encryptedBytes));
        }

        public string DecryptStored(string prefKey)
        {
            string enc = PreferenceManager.GetDefaultSharedPreferences(_context).GetString(prefKey, null);
            return Decrypt(enc);
        }
    }

    public class BiometricEncryption : BiometricCrypt
    {

        private KeyGenerator _keyGen;


        public BiometricEncryption(BiometricModule biometric, string keyId) :
            base(biometric, keyId)
        {
            _keyGen = biometric.KeyGenerator;
            Kp2aLog.Log("FP: CreateKey ");
            CreateKey();
        }


        /// <summary>
        /// Creates a symmetric key in the Android Key Store which can only be used after the user 
        /// has authenticated with biometry.
        /// </summary>
        private void CreateKey()
        {
            try
            {
                _keystore.Load(null);
                _keyGen.Init(new KeyGenParameterSpec.Builder(GetAlias(_keyId),
                    KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                    .SetBlockModes(KeyProperties.BlockModeCbc)
                    // Require the user to authenticate with biometry to authorize every use
                    // of the key
                    .SetEncryptionPaddings(KeyProperties.EncryptionPaddingPkcs7)
                    .SetUserAuthenticationRequired(true)

                    .Build());
                _keyGen.GenerateKey();
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
            catch (System.Exception e)
            {
                Kp2aLog.LogUnexpectedError(e);
            }
        }

        public override bool Init()
        {
            Kp2aLog.Log("FP: Init for Enc ");
            try
            {
                _keystore.Load(null);
                var key = _keystore.GetKey(GetAlias(_keyId), null);
                _cipher.Init(CipherMode.EncryptMode, key);

                _cryptoObject = new BiometricPrompt.CryptoObject(_cipher);
                return true;
            }
            catch (KeyPermanentlyInvalidatedException)
            {
                return false;
            }
            catch (KeyStoreException e)
            {
                throw new RuntimeException(FailedToInitCipher, e);
            }
            catch (CertificateException e)
            {
                throw new RuntimeException(FailedToInitCipher, e);
            }
            catch (UnrecoverableKeyException e)
            {
                throw new RuntimeException(FailedToInitCipher, e);
            }
            catch (IOException e)
            {
                throw new RuntimeException(FailedToInitCipher, e);
            }
            catch (NoSuchAlgorithmException e)
            {
                throw new RuntimeException(FailedToInitCipher, e);
            }
            catch (InvalidKeyException e)
            {
                throw new RuntimeException(FailedToInitCipher, e);
            }
        }

    }

    public class BiometricAuthCallbackAdapter : BiometricPrompt.AuthenticationCallback
    {
        private readonly IBiometricAuthCallback _callback;
        private readonly Context _context;

        public BiometricAuthCallbackAdapter(IBiometricAuthCallback callback, Context context)
        {
            _callback = callback;
            _context = context;
        }


        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
        {
            new Handler(Looper.MainLooper).Post(() => _callback.OnBiometricAuthSucceeded());
        }

        public override void OnAuthenticationError(int errorCode, ICharSequence errString)
        {
            
            new Handler(Looper.MainLooper).Post(() => _callback.OnBiometricError(errString.ToString()));
        }


        public override void OnAuthenticationFailed()
        {
            new Handler(Looper.MainLooper).Post(() => _callback.OnBiometricError(_context.Resources.GetString(Resource.String.fingerprint_not_recognized)));
        }

    }

}