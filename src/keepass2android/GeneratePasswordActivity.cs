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
using Android.Preferences;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
    [Activity(Label = "@string/app_name", Theme = "@style/MyTheme_ActionBar", WindowSoftInputMode = SoftInput.StateHidden)]		    
    public class GeneratePasswordActivity : LockCloseActivity {
		private readonly int[] _buttonIds  = new[]  {Resource.Id.btn_length6, Resource.Id.btn_length8, Resource.Id.btn_length12, Resource.Id.btn_length16};

	    private ActivityDesign _design;
	    public GeneratePasswordActivity()
	    {
		    _design = new ActivityDesign(this);
	    }

		public static void Launch(Activity act) {
			Intent i = new Intent(act, typeof(GeneratePasswordActivity));
			
			act.StartActivityForResult(i, 0);
		}

		public static void LaunchWithoutLockCheck(Activity act)
		{
			Intent i = new Intent(act, typeof(GeneratePasswordActivity));

			i.PutExtra(NoLockCheck, true);

			act.StartActivityForResult(i, 0);
		}

		
		protected override void OnCreate(Bundle savedInstanceState) {
			base.OnCreate(savedInstanceState);
			_design.ApplyTheme();
			SetContentView(Resource.Layout.generate_password);
			SetResult(KeePass.ExitNormal);

			var prefs = GetPreferences(FileCreationMode.Private);
			((CheckBox) FindViewById(Resource.Id.cb_uppercase)).Checked = prefs.GetBoolean("cb_uppercase", true);
			((CheckBox)FindViewById(Resource.Id.cb_lowercase)).Checked = prefs.GetBoolean("cb_lowercase", true);
			((CheckBox)FindViewById(Resource.Id.cb_digits)).Checked = prefs.GetBoolean("cb_digits", true);
			((CheckBox)FindViewById(Resource.Id.cb_minus)).Checked = prefs.GetBoolean("cb_minus", false);
			((CheckBox)FindViewById(Resource.Id.cb_underline)).Checked = prefs.GetBoolean("cb_underline", false);
			((CheckBox)FindViewById(Resource.Id.cb_space)).Checked = prefs.GetBoolean("cb_space", false);
			((CheckBox)FindViewById(Resource.Id.cb_specials)).Checked = prefs.GetBoolean("cb_specials", false);
			((CheckBox)FindViewById(Resource.Id.cb_brackets)).Checked = prefs.GetBoolean("cb_brackets", false);
			((EditText)FindViewById(Resource.Id.length)).Text = prefs.GetInt("length", 12).ToString(CultureInfo.InvariantCulture);
			
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
					
					EditText txtPassword = (EditText) FindViewById(Resource.Id.password_edit);
					txtPassword.Text = password;
			};



			View acceptButton = FindViewById(Resource.Id.accept_button);
			acceptButton.Click += (sender, e) => {
					EditText password = (EditText) FindViewById(Resource.Id.password_edit);
					
					Intent intent = new Intent();
					intent.PutExtra("keepass2android.password.generated_password", password.Text);
					
					SetResult(KeePass.ResultOkPasswordGenerator, intent);
					
					Finish();
			};

			
			View cancelButton = FindViewById(Resource.Id.cancel_button);
			cancelButton.Click += (sender, e) => 
			{
					SetResult(Result.Canceled);
					
					Finish();
			};

			
			EditText txtPasswordToSet = (EditText) FindViewById(Resource.Id.password_edit);
			txtPasswordToSet.Text = GeneratePassword();

            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetHomeButtonEnabled(true);

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

				var prefs = GetPreferences(FileCreationMode.Private);
				prefs.Edit()
				     .PutBoolean("cb_uppercase", ((CheckBox) FindViewById(Resource.Id.cb_uppercase)).Checked)
				     .PutBoolean("cb_lowercase", ((CheckBox) FindViewById(Resource.Id.cb_lowercase)).Checked)
				     .PutBoolean("cb_digits", ((CheckBox) FindViewById(Resource.Id.cb_digits)).Checked)
				     .PutBoolean("cb_minus", ((CheckBox) FindViewById(Resource.Id.cb_minus)).Checked)
				     .PutBoolean("cb_underline", ((CheckBox) FindViewById(Resource.Id.cb_underline)).Checked)
				     .PutBoolean("cb_space", ((CheckBox) FindViewById(Resource.Id.cb_space)).Checked)
				     .PutBoolean("cb_specials", ((CheckBox) FindViewById(Resource.Id.cb_specials)).Checked)
				     .PutBoolean("cb_brackets", ((CheckBox) FindViewById(Resource.Id.cb_brackets)).Checked)
				     .PutInt("length", length)
				     .Commit();



			} catch (ArgumentException e) {
				Toast.MakeText(this, e.Message, ToastLength.Long).Show();
			}
			
			return password;
		}


        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Android.Resource.Id.Home:
                    OnBackPressed();
                    return true;
            }
            return false;
        }
	}

}

