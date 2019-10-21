using Java.Lang;
using KeePassLib.Cryptography;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using Exception = System.Exception;

namespace keepass2android
{
    public class ChallengeXCKey : IUserKey, ISeedBasedUserKey
    {
        private readonly int _requestCode;

        public ProtectedBinary KeyData
        {
            get
            {
                if (Activity == null)
                    throw new Exception("Need an active Keepass2Android activity to challenge Yubikey!");
                Activity.RunOnUiThread(
                    () =>
                    {
                        byte[] challenge = _kdfSeed;
                        byte[] challenge64 = new byte[64];
                        for (int i = 0; i < 64; i++)
                        {
                            if (i < challenge.Length)
                            {
                                challenge64[i] = challenge[i];
                            }
                            else
                            {
                                challenge64[i] = (byte)(challenge64.Length - challenge.Length);
                            }

                        }
                        var chalIntent = Activity.TryGetYubichallengeIntentOrPrompt(challenge64, true);

                        if (chalIntent == null)
                        {
                            Error = Activity.GetString(Resource.String.NoChallengeApp);
                        }
                        else
                        {
                            Activity.StartActivityForResult(chalIntent, _requestCode);
                        }

                    });
                while ((Response == null) && (Error == null))
                {
                    Thread.Sleep(50);
                }
                if (Error != null)
                {
                    var error = Error;
                    Error = null;
                    throw new Exception("YubiChallenge failed: " + error);
                }

                var result = CryptoUtil.HashSha256(Response);
                Response = null;
                return new ProtectedBinary(true, result);
            }
        }

        public uint GetMinKdbxVersion()
        {
            return KdbxFile.FileVersion32_4;
        }

        private byte[] _kdfSeed;

        public ChallengeXCKey(LockingActivity activity, int requestCode)
        {
            this.Activity = activity;
            _requestCode = requestCode;
            Response = null;
        }

        public void SetParams(byte[] masterSeed, byte[] mPbKdfSeed)
        {
            _kdfSeed = mPbKdfSeed;
        }

        public byte[] Response { get; set; }

        public string Error { get; set; }

        public LockingActivity Activity
        {
            get;
            set;
        }
    }
}