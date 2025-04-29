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
using System.Globalization;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;

using KeePassLib;
using Android.Util;
using AndroidX.Preference;
using keepass2android;
using KeePassLib.Cryptography.KeyDerivation;
using Google.Android.Material.Dialog;

namespace keepass2android.settings
{
	/*
	 *
	   public class KdfNumberDialogPreference : DialogPreference
	   {
	       private readonly Context _context;
	       public KdfNumberDialogPreference(Context context) : base(context)
	       {
	           _context = context;
	       }
	   
	       public KdfNumberDialogPreference(Context context, IAttributeSet attrs) : base(context, attrs)
	       {
	           _context = context;
	       }
	   
	       public KdfNumberDialogPreference(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
	       {
	           _context = context;
	       }
	       
	       public override int DialogLayoutResource => Resource.Layout.activity_main;
	   
	   }
	   

	 */
	public abstract class KdfNumberDialogPreference : DialogPreference {
		
        public KdfNumberDialogPreference(Context context) : base(context)
        {
            
        }

        public KdfNumberDialogPreference(Context context, IAttributeSet attrs) : base(context, attrs)
        {
           
        }

        public KdfNumberDialogPreference(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
           
        }

        public void ShowDialog(PreferenceFragmentCompat containingFragment)
        {
            var activity = containingFragment.Activity;
            MaterialAlertDialogBuilder db = new MaterialAlertDialogBuilder(activity);

            View dialogView = activity.LayoutInflater.Inflate(Resource.Layout.database_kdf_settings, null);
            var inputEditText = dialogView.FindViewById<TextView>(Resource.Id.rounds);
            
            inputEditText.Text = ParamValue.ToString();

            db.SetView(dialogView);
            db.SetTitle(Title);
            db.SetPositiveButton(Android.Resource.String.Ok, (sender, args) =>
            {
                //store the old value for restoring in case of failure
                ulong paramValue;

                String strRounds = inputEditText.Text;
                if (!(ulong.TryParse(strRounds, out paramValue)))
                {
                    App.Kp2a.ShowMessage(Context, Resource.String.error_param_not_number,  MessageSeverity.Error);
                    return;
                }

                if (paramValue < 1)
                {
                    paramValue = 1;
                }

                ulong oldValue = ParamValue;

                if (oldValue == paramValue)
                {
                    return;
                }

                ParamValue = paramValue;

                Handler handler = new Handler();
                SaveDb save = new SaveDb((Activity)Context, App.Kp2a, App.Kp2a.CurrentDb, new AfterSave((Activity)Context, handler, oldValue, this));
                BlockingOperationRunner pt = new BlockingOperationRunner(App.Kp2a, (Activity)Context, save);
                pt.Run();
            });
            db.SetNegativeButton(Android.Resource.String.Cancel, ((sender, args) => { }));

            db.Create().Show();
        }

		public virtual string ExplanationString
		{
			get { return ""; }
		}

		public abstract ulong ParamValue { get; set; }

		private class AfterSave : OnOperationFinishedHandler {
			private readonly ulong _oldParamValue;
			private readonly Context _ctx;
			private readonly KdfNumberDialogPreference _pref;
			
			public AfterSave(Activity ctx, Handler handler, ulong oldParamValue, KdfNumberDialogPreference pref):base(ctx, handler) {

				_pref = pref;
				_ctx = ctx;
				_oldParamValue = oldParamValue;
			}
			
			public override void Run() {
				if ( Success ) {

					if ( _pref.OnPreferenceChangeListener != null ) {
						_pref.OnPreferenceChangeListener.OnPreferenceChange(_pref, null);
					}
				} else {
					DisplayMessage(_ctx);

					_pref.ParamValue = _oldParamValue;
                }
				
				base.Run();
			}
			
		}
		
	}

	/// <summary>
	/// Represents the setting for the number of key transformation rounds. Changing this requires to save the database.
	/// </summary>
	public class RoundsPreference : KdfNumberDialogPreference
    {
		private readonly Context _context;


		public ulong KeyEncryptionRounds
		{
			get
			{
				AesKdf kdf = new AesKdf();
				if (!kdf.Uuid.Equals(App.Kp2a.CurrentDb.KpDatabase.KdfParameters.KdfUuid))
					return (uint) PwDefs.DefaultKeyEncryptionRounds;
				else
				{
					ulong uRounds = App.Kp2a.CurrentDb.KpDatabase.KdfParameters.GetUInt64(
						AesKdf.ParamRounds, PwDefs.DefaultKeyEncryptionRounds);
					uRounds = Math.Min(uRounds, 0xFFFFFFFEUL);

					return (uint) uRounds;
				}
			}
			set { App.Kp2a.CurrentDb.KpDatabase.KdfParameters.SetUInt64(AesKdf.ParamRounds, value); }
		}

		public RoundsPreference(Context context, IAttributeSet attrs):base(context, attrs)
		{
			_context = context;
		}

		public RoundsPreference(Context context, IAttributeSet attrs, int defStyle): base(context, attrs, defStyle)
		{
			_context = context;
		}

		public override string ExplanationString
		{
			get { return _context.GetString(Resource.String.rounds_explaination); }
		}

		public override ulong ParamValue
		{
			get { return KeyEncryptionRounds; }
			set { KeyEncryptionRounds = value; }
		}
	
	}

}

