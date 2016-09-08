using Android.Content;
using Android.Util;
using KeePassLib.Cryptography.KeyDerivation;

namespace keepass2android.settings
{
	public class Argon2RoundsPreference: KdfNumberParamPreference
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
				var kdfparams = App.Kp2a.GetDb().KpDatabase.KdfParameters;
				var kdf = KdfPool.Get(App.Kp2a.GetDb().KpDatabase.KdfParameters.KdfUuid);
				if (!(kdf is Argon2Kdf))
				{
					new Argon2Kdf().GetDefaultParameters();
				}
				
				return kdfparams.GetUInt64(Argon2Kdf.ParamIterations, 0);
			}
			set
			{
				App.Kp2a.GetDb().KpDatabase.KdfParameters.SetUInt64(Argon2Kdf.ParamIterations, value);
			}
		}
	}

	public class Argon2ParallelismPreference : KdfNumberParamPreference
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
				var kdfparams = App.Kp2a.GetDb().KpDatabase.KdfParameters;
				var kdf = KdfPool.Get(App.Kp2a.GetDb().KpDatabase.KdfParameters.KdfUuid);
				if (!(kdf is Argon2Kdf))
				{
					new Argon2Kdf().GetDefaultParameters();
				}

				return kdfparams.GetUInt32(Argon2Kdf.ParamParallelism, 0);
			}
			set
			{
				App.Kp2a.GetDb().KpDatabase.KdfParameters.SetUInt32(Argon2Kdf.ParamParallelism, (uint) value);
			}
		}
	}

	public class Argon2MemoryPreference : KdfNumberParamPreference
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
				var kdfparams = App.Kp2a.GetDb().KpDatabase.KdfParameters;
				var kdf = KdfPool.Get(App.Kp2a.GetDb().KpDatabase.KdfParameters.KdfUuid);
				if (!(kdf is Argon2Kdf))
				{
					new Argon2Kdf().GetDefaultParameters();
				}

				return kdfparams.GetUInt64(Argon2Kdf.ParamMemory, 0);
			}
			set
			{
				App.Kp2a.GetDb().KpDatabase.KdfParameters.SetUInt64(Argon2Kdf.ParamMemory, value);
			}
		}
	}

}