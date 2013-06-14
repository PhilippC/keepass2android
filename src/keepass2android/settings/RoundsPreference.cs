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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Preferences;
using KeePassLib;
using Android.Util;
using keepass2android;

namespace keepass2android.settings
{

	public class RoundsPreference : DialogPreference {
		
		internal PwDatabase mPM;
		internal TextView mRoundsView;
		
		protected override View OnCreateDialogView() {
			View view =  base.OnCreateDialogView();
			
			mRoundsView = (TextView) view.FindViewById(Resource.Id.rounds);
			
			Database db = App.Kp2a.GetDb();
			mPM = db.pm;
			ulong numRounds = mPM.KeyEncryptionRounds;
			mRoundsView.Text = numRounds.ToString();

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
				
				String strRounds = mRoundsView.Text; 
				if (!(ulong.TryParse(strRounds,out rounds)))
				{
					Toast.MakeText(Context, Resource.String.error_rounds_not_number, ToastLength.Long).Show();
					return;
				}
				
				if ( rounds < 1 ) {
					rounds = 1;
				}
				
				ulong oldRounds = mPM.KeyEncryptionRounds;

				if (oldRounds == rounds)
				{
					return;
				}

				mPM.KeyEncryptionRounds = rounds;

				Handler handler = new Handler();
				SaveDB save = new SaveDB(Context, App.Kp2a.GetDb(), new AfterSave(Context, handler, oldRounds, this));
				ProgressTask pt = new ProgressTask(App.Kp2a, Context, save, UiStringKey.saving_database);
				pt.run();
				
			}
			
		}
		
		private class AfterSave : OnFinish {
			private ulong mOldRounds;
			private Context mCtx;
			private RoundsPreference pref;
			
			public AfterSave(Context ctx, Handler handler, ulong oldRounds, RoundsPreference pref):base(handler) {

				this.pref = pref;
				mCtx = ctx;
				mOldRounds = oldRounds;
			}
			
			public override void run() {
				if ( mSuccess ) {

					if ( pref.OnPreferenceChangeListener != null ) {
						pref.OnPreferenceChangeListener.OnPreferenceChange(pref, null);
					}
				} else {
					displayMessage(mCtx);
					pref.mPM.KeyEncryptionRounds = mOldRounds;
				}
				
				base.run();
			}
			
		}
		
	}

}

