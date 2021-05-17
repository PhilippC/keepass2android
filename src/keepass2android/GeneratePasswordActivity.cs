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
using Android.Content.PM;
using Android.OS;
using Android.Preferences;
using Android.Views;
using Android.Widget;
using Newtonsoft.Json;

namespace keepass2android
{
    [Activity(Label = "@string/app_name", Theme = "@style/MyTheme_ActionBar", WindowSoftInputMode = SoftInput.StateHidden, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]		    
    public class GeneratePasswordActivity :
#if DEBUG
        LifecycleAwareActivity
#else
		LockCloseActivity 
#endif

    {
		private readonly int[] _buttonLengthButtonIds  = new[]  {Resource.Id.btn_length6,
            Resource.Id.btn_length8, 
            Resource.Id.btn_length12, 
            Resource.Id.btn_length16,
            Resource.Id.btn_length24,
            Resource.Id.btn_length32};

        private readonly int[] _checkboxIds = new[]  {Resource.Id.cb_uppercase,
            Resource.Id.cb_lowercase,
            Resource.Id.cb_digits,
            Resource.Id.cb_minus,
            Resource.Id.cb_underline,
            Resource.Id.cb_space,
            Resource.Id.cb_specials,
            Resource.Id.cb_specials_extended,
            Resource.Id.cb_brackets,
            Resource.Id.cb_at_least_one_from_each_group,
            Resource.Id.cb_exclude_lookalike
        };

		PasswordFont _passwordFont = new PasswordFont();


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

#if DEBUG
#else
			i.PutExtra(NoLockCheck, true);
#endif

			act.StartActivityForResult(i, 0);
		}

		
		protected override void OnCreate(Bundle savedInstanceState) {
			_design.ApplyTheme(); 
			base.OnCreate(savedInstanceState);
			
			SetContentView(Resource.Layout.generate_password);
			SetResult(KeePass.ExitNormal);

			var prefs = GetPreferences(FileCreationMode.Private);


            PasswordGenerator.PasswordGenerationOptions options = null;
			string jsonOptions = prefs.GetString("password_generator_options", null);
            if (jsonOptions != null)
            {
                try
                {
                    options = JsonConvert.DeserializeObject<PasswordGenerator.PasswordGenerationOptions>(jsonOptions);
                }
                catch (Exception e)
                {
                    Kp2aLog.LogUnexpectedError(e);
                }
            }
            else
            {
				options = new PasswordGenerator.PasswordGenerationOptions()
                {
                    Length = prefs.GetInt("length", 12),
                    UpperCase = prefs.GetBoolean("cb_uppercase", true),
                    LowerCase = prefs.GetBoolean("cb_lowercase", true),
                    Digits = prefs.GetBoolean("cb_digits", true),
                    Minus = prefs.GetBoolean("cb_minus", false),
                    Underline = prefs.GetBoolean("cb_underline", false),
                    Space = prefs.GetBoolean("cb_space", false),
                    Specials = prefs.GetBoolean("cb_specials", false),
                    SpecialsExtended = false,
                    Brackets = prefs.GetBoolean("cb_brackets", false)
                };

			}

			((CheckBox)FindViewById(Resource.Id.cb_uppercase)).Checked = options.UpperCase;
			((CheckBox)FindViewById(Resource.Id.cb_lowercase)).Checked = options.LowerCase;
			((CheckBox)FindViewById(Resource.Id.cb_digits)).Checked = options.Digits;
			((CheckBox)FindViewById(Resource.Id.cb_minus)).Checked = options.Minus;
			((CheckBox)FindViewById(Resource.Id.cb_underline)).Checked = options.Underline;
			((CheckBox)FindViewById(Resource.Id.cb_space)).Checked = options.Space;
			((CheckBox)FindViewById(Resource.Id.cb_specials)).Checked = options.Specials;
            ((CheckBox)FindViewById(Resource.Id.cb_specials_extended)).Checked = options.SpecialsExtended;
			((CheckBox)FindViewById(Resource.Id.cb_brackets)).Checked = options.Brackets;
            ((CheckBox)FindViewById(Resource.Id.cb_exclude_lookalike)).Checked = options.ExcludeLookAlike;
            ((CheckBox)FindViewById(Resource.Id.cb_at_least_one_from_each_group)).Checked = options.AtLeastOneFromEachGroup;

			((EditText)FindViewById(Resource.Id.length)).Text = options.Length.ToString(CultureInfo.InvariantCulture);
			
			foreach (int id in _buttonLengthButtonIds) {
				Button button = (Button) FindViewById(id);
				button.Click += (sender, e) => 
				{
					Button b = (Button) sender;
					
					EditText editText = (EditText) FindViewById(Resource.Id.length);
					editText.Text = b.Text;
					UpdatePassword();

				};
			}

            foreach (int id in _checkboxIds)
            {
                FindViewById<CheckBox>(id).CheckedChange += (sender, args) => UpdatePassword();
            }

            Button genPassButton = (Button) FindViewById(Resource.Id.generate_password_button);
			genPassButton.Click += (sender, e) => { UpdatePassword(); };



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

            _passwordFont.ApplyTo(txtPasswordToSet);

			SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetHomeButtonEnabled(true);

		}

        private void UpdatePassword()
        {
            String password = GeneratePassword();

            EditText txtPassword = (EditText) FindViewById(Resource.Id.password_edit);
            txtPassword.Text = password;
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

                var options = GetOptions(length);

				password = generator.GeneratePassword(options);

				var prefs = GetPreferences(FileCreationMode.Private);
				prefs.Edit()
				     .PutString("password_generator_options", JsonConvert.SerializeObject(options))
                         .Commit();



			} catch (ArgumentException e) {
				Toast.MakeText(this, e.Message, ToastLength.Long).Show();
			}
			
			return password;
		}

        private PasswordGenerator.PasswordGenerationOptions GetOptions(int length)
        {
            PasswordGenerator.PasswordGenerationOptions options = new PasswordGenerator.PasswordGenerationOptions()
            {
                Length = length,
                UpperCase = ((CheckBox) FindViewById(Resource.Id.cb_uppercase)).Checked,
                LowerCase = ((CheckBox) FindViewById(Resource.Id.cb_lowercase)).Checked,
                Digits = ((CheckBox) FindViewById(Resource.Id.cb_digits)).Checked,
                Minus = ((CheckBox) FindViewById(Resource.Id.cb_minus)).Checked,
                Underline = ((CheckBox) FindViewById(Resource.Id.cb_underline)).Checked,
                Space = ((CheckBox) FindViewById(Resource.Id.cb_space)).Checked,
                Specials = ((CheckBox) FindViewById(Resource.Id.cb_specials)).Checked,
                SpecialsExtended = ((CheckBox) FindViewById(Resource.Id.cb_specials_extended)).Checked,
                Brackets = ((CheckBox) FindViewById(Resource.Id.cb_brackets)).Checked,
                ExcludeLookAlike = ((CheckBox) FindViewById(Resource.Id.cb_exclude_lookalike)).Checked,
                AtLeastOneFromEachGroup = ((CheckBox) FindViewById(Resource.Id.cb_at_least_one_from_each_group)).Checked
            };
            return options;
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

