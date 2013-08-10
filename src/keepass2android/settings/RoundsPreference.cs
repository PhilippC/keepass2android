/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
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

namespace keepass2android.settings
{
	/// <summary>
	/// Represents the setting for the number of key transformation rounds. Changing this requires to save the database.
	/// </summary>
	public class RoundsPreference : DialogPreference {
		
		internal TextView RoundsView;
		
		protected override View OnCreateDialogView() {
			View view =  base.OnCreateDialogView();
			
			RoundsView = (TextView) view.FindViewById(Resource.Id.rounds);
			
			Database db = App.Kp2a.GetDb();
			ulong numRounds = db.KpDatabase.KeyEncryptionRounds;
			RoundsView.Text = numRounds.ToString(CultureInfo.InvariantCulture);

			return view;
		}
		
		public RoundsPreference(Context context, IAttributeSet attrs):base(context, attrs) {
		}
		
		public RoundsPreference(Context context, IAttributeSet attrs, int defStyle): base(context, attrs, defStyle) {
		}
		
		protected override void OnDialogClosed(bool positiveResult) {
			base.OnDialogClosed(positiveResult);
			
			if ( positiveResult ) {
				ulong rounds;
				
				String strRounds = RoundsView.Text; 
				if (!(ulong.TryParse(strRounds,out rounds)))
				{
					Toast.MakeText(Context, Resource.String.error_rounds_not_number, ToastLength.Long).Show();
					return;
				}
				
				if ( rounds < 1 ) {
					rounds = 1;
				}

				Database db = App.Kp2a.GetDb();

				ulong oldRounds = db.KpDatabase.KeyEncryptionRounds;

				if (oldRounds == rounds)
				{
					return;
				}

				db.KpDatabase.KeyEncryptionRounds = rounds;

				Handler handler = new Handler();
				SaveDb save = new SaveDb(Context, App.Kp2a, new AfterSave(Context, handler, oldRounds, this));
				ProgressTask pt = new ProgressTask(App.Kp2a, Context, save);
				pt.Run();
				
			}
			
		}
		
		private class AfterSave : OnFinish {
			private readonly ulong _oldRounds;
			private readonly Context _ctx;
			private readonly RoundsPreference _pref;
			
			public AfterSave(Context ctx, Handler handler, ulong oldRounds, RoundsPreference pref):base(handler) {

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

					App.Kp2a.GetDb().KpDatabase.KeyEncryptionRounds = _oldRounds;
				}
				
				base.Run();
			}
			
		}
		
	}

}

