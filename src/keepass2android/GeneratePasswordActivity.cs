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
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", Theme="@style/NoTitleBar")]			
	public class GeneratePasswordActivity : LockCloseActivity {
		private readonly int[] _buttonIds  = new[]  {Resource.Id.btn_length6, Resource.Id.btn_length8, Resource.Id.btn_length12, Resource.Id.btn_length16};
		
		public static void Launch(Activity act) {
			Intent i = new Intent(act, typeof(GeneratePasswordActivity));
			
			act.StartActivityForResult(i, 0);
		}

		
		protected override void OnCreate(Bundle savedInstanceState) {
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.generate_password);
			SetResult(KeePass.ExitNormal);
			
			foreach (int id in _buttonIds) {
				Button button = (Button) FindViewById(id);
				button.Click += (sender, e) => 
				{
					Button b = (Button) sender;
					
					EditText editText = (EditText) FindViewById(Resource.Id.length);
					editText.Text = b.Text;

				};
			}
			
			Button genPassButton = (Button) FindViewById(Resource.Id.generate_password_button);
			genPassButton.Click += (sender, e) =>  {
					String password = GeneratePassword();
					
					EditText txtPassword = (EditText) FindViewById(Resource.Id.password);
					txtPassword.Text = password;
			};



			View acceptButton = FindViewById(Resource.Id.accept_button);
			acceptButton.Click += (sender, e) => {
					EditText password = (EditText) FindViewById(Resource.Id.password);
					
					Intent intent = new Intent();
					intent.PutExtra("keepass2android.password.generated_password", password.Text);
					
					SetResult((Result)EntryEditActivity.ResultOkPasswordGenerator, intent);
					
					Finish();
			};

			
			View cancelButton = FindViewById(Resource.Id.cancel_button);
			cancelButton.Click += (sender, e) => 
			{
					SetResult(Result.Canceled);
					
					Finish();
			};

			
			EditText txtPasswordToSet = (EditText) FindViewById(Resource.Id.password);
			txtPasswordToSet.Text = GeneratePassword();

		}
		
		public String GeneratePassword() {
			String password = "";
			
			try {

				int length;
				if (!int.TryParse(((EditText) FindViewById(Resource.Id.length)).Text, out length))
				{
					Toast.MakeText(this, Resource.String.error_wrong_length, ToastLength.Long).Show();
					return password;
				}


				PasswordGenerator generator = new PasswordGenerator(this);
				
				password = generator.GeneratePassword(length,
				                                      ((CheckBox) FindViewById(Resource.Id.cb_uppercase)).Checked,
				                                      ((CheckBox) FindViewById(Resource.Id.cb_lowercase)).Checked,
				                                      ((CheckBox) FindViewById(Resource.Id.cb_digits)).Checked,
				                                      ((CheckBox) FindViewById(Resource.Id.cb_minus)).Checked,
				                                      ((CheckBox) FindViewById(Resource.Id.cb_underline)).Checked,
				                                      ((CheckBox) FindViewById(Resource.Id.cb_space)).Checked,
				                                      ((CheckBox) FindViewById(Resource.Id.cb_specials)).Checked,
				                                      ((CheckBox) FindViewById(Resource.Id.cb_brackets)).Checked);
			} catch (ArgumentException e) {
				Toast.MakeText(this, e.Message, ToastLength.Long).Show();
			}
			
			return password;
		}
	}

}

