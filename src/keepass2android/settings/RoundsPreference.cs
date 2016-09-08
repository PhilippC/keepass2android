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
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Preferences;
using KeePassLib;
using Android.Util;
using KeePassLib.Cryptography.KeyDerivation;

namespace keepass2android.settings
{
	public abstract class KdfNumberParamPreference: DialogPreference {
		
		internal TextView edittext;
		
		protected override View OnCreateDialogView() {
			View view =  base.OnCreateDialogView();
			
			edittext = (TextView) view.FindViewById(Resource.Id.rounds);


			ulong numRounds = ParamValue;
			edittext.Text = numRounds.ToString(CultureInfo.InvariantCulture);

			view.FindViewById<TextView>(Resource.Id.rounds_explaination).Text = ExplanationString;

			return view;
		}

		public virtual string ExplanationString
		{
			get { return ""; }
		}

		public abstract ulong ParamValue { get; set; }
		public KdfNumberParamPreference(Context context, IAttributeSet attrs):base(context, attrs) {
		}

		public KdfNumberParamPreference(Context context, IAttributeSet attrs, int defStyle)
			: base(context, attrs, defStyle)
		{
		}
		
		protected override void OnDialogClosed(bool positiveResult) {
			base.OnDialogClosed(positiveResult);
			
			if ( positiveResult ) {
				ulong paramValue;
				
				String strRounds = edittext.Text; 
				if (!(ulong.TryParse(strRounds,out paramValue)))
				{
					Toast.MakeText(Context, Resource.String.error_param_not_number, ToastLength.Long).Show();
					return;
				}
				
				if ( paramValue < 1 ) {
					paramValue = 1;
				}

				Database db = App.Kp2a.GetDb();

				ulong oldValue = ParamValue;

				if (oldValue == paramValue)
				{
					return;
				}

				ParamValue = paramValue;

				Handler handler = new Handler();
				SaveDb save = new SaveDb(Context, App.Kp2a, new KdfNumberParamPreference.AfterSave(Context, handler, oldValue, this));
				ProgressTask pt = new ProgressTask(App.Kp2a, Context, save);
				pt.Run();
				
			}
			
		}
		
		private class AfterSave : OnFinish {
			private readonly ulong _oldRounds;
			private readonly Context _ctx;
			private readonly KdfNumberParamPreference _pref;
			
			public AfterSave(Context ctx, Handler handler, ulong oldRounds, KdfNumberParamPreference pref):base(handler) {

				_pref = pref;
				_ctx = ctx;
				_oldRounds = oldRounds;
			}
			
			public override void Run() {
				if ( Success ) {

					if ( _pref.OnPreferenceChangeListener != null ) {
						_pref.OnPreferenceChangeListener.OnPreferenceChange(_pref, null);
					}
				} else {
					DisplayMessage(_ctx);

					App.Kp2a.GetDb().KpDatabase.KdfParameters.SetUInt64(AesKdf.ParamRounds, _oldRounds);
				}
				
				base.Run();
			}
			
		}
		
	}

	/// <summary>
	/// Represents the setting for the number of key transformation rounds. Changing this requires to save the database.
	/// </summary>
	public class RoundsPreference : KdfNumberParamPreference {
		private readonly Context _context;


		public ulong KeyEncryptionRounds
		{
			get
			{
				AesKdf kdf = new AesKdf();
				if (!kdf.Uuid.Equals(App.Kp2a.GetDb().KpDatabase.KdfParameters.KdfUuid))
					return (uint) PwDefs.DefaultKeyEncryptionRounds;
				else
				{
					ulong uRounds = App.Kp2a.GetDb().KpDatabase.KdfParameters.GetUInt64(
						AesKdf.ParamRounds, PwDefs.DefaultKeyEncryptionRounds);
					uRounds = Math.Min(uRounds, 0xFFFFFFFEUL);

					return (uint) uRounds;
				}
			}
			set { App.Kp2a.GetDb().KpDatabase.KdfParameters.SetUInt64(AesKdf.ParamRounds, value); }
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

