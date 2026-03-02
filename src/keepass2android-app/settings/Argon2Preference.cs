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

using Android.Content;
using Android.Util;
using KeePassLib.Cryptography.KeyDerivation;

namespace keepass2android.settings
{
  public class Argon2RoundsPreference : KdfNumberDialogPreference
  {
    public Argon2RoundsPreference(Context context, IAttributeSet attrs) : base(context, attrs)
    {
    }

    public Argon2RoundsPreference(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
    {
    }

    public override ulong ParamValue
    {
      get
      {
        var kdfparams = App.Kp2a.CurrentDb.KpDatabase.KdfParameters;
        var kdf = KdfPool.Get(App.Kp2a.CurrentDb.KpDatabase.KdfParameters.KdfUuid);
        if (!(kdf is Argon2Kdf))
        {
          new Argon2Kdf().GetDefaultParameters();
        }

        return kdfparams.GetUInt64(Argon2Kdf.ParamIterations, 0);
      }
      set
      {
        App.Kp2a.CurrentDb.KpDatabase.KdfParameters.SetUInt64(Argon2Kdf.ParamIterations, value);
      }
    }
  }

  public class Argon2ParallelismPreference : KdfNumberDialogPreference
  {
    public Argon2ParallelismPreference(Context context, IAttributeSet attrs)
        : base(context, attrs)
    {
    }

    public Argon2ParallelismPreference(Context context, IAttributeSet attrs, int defStyle)
        : base(context, attrs, defStyle)
    {
    }

    public override ulong ParamValue
    {
      get
      {
        var kdfparams = App.Kp2a.CurrentDb.KpDatabase.KdfParameters;
        var kdf = KdfPool.Get(App.Kp2a.CurrentDb.KpDatabase.KdfParameters.KdfUuid);
        if (!(kdf is Argon2Kdf))
        {
          new Argon2Kdf().GetDefaultParameters();
        }

        return kdfparams.GetUInt32(Argon2Kdf.ParamParallelism, 0);
      }
      set
      {
        App.Kp2a.CurrentDb.KpDatabase.KdfParameters.SetUInt32(Argon2Kdf.ParamParallelism, (uint)value);
      }
    }
  }

  public class Argon2MemoryPreference : KdfNumberDialogPreference
  {
    public Argon2MemoryPreference(Context context, IAttributeSet attrs)
        : base(context, attrs)
    {
    }

    public Argon2MemoryPreference(Context context, IAttributeSet attrs, int defStyle)
        : base(context, attrs, defStyle)
    {
    }

    public override ulong ParamValue
    {
      get
      {
        var kdfparams = App.Kp2a.CurrentDb.KpDatabase.KdfParameters;
        var kdf = KdfPool.Get(App.Kp2a.CurrentDb.KpDatabase.KdfParameters.KdfUuid);
        if (!(kdf is Argon2Kdf))
        {
          new Argon2Kdf().GetDefaultParameters();
        }

        return kdfparams.GetUInt64(Argon2Kdf.ParamMemory, 0);
      }
      set
      {
        App.Kp2a.CurrentDb.KpDatabase.KdfParameters.SetUInt64(Argon2Kdf.ParamMemory, value);
      }
    }
  }

}