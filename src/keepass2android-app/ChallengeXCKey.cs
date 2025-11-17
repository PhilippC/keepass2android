// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using Java.Lang;
using keepass2android;
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
          System.Threading.Thread.Sleep(50);
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