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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Android.App;
using Android.App.Admin;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Preferences;
using Android.Views;
using Android.Widget;
using Java.Util;
using KeePassLib.Cryptography;
using Newtonsoft.Json;
using OtpKeyProv;

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

        private PasswordProfiles _profiles;

        private bool _updateDisabled = false;

        class PasswordProfiles
        {
            public List<KeyValuePair<string, PasswordGenerator.CombinedKeyOptions>> Profiles { get; set; }

            public PasswordGenerator.CombinedKeyOptions LastUsedSettings { get; set; }

            public int? TryFindLastUsedProfileIndex()
            {
                for (int i=0;i<Profiles.Count;i++)
                {
                    var kvp = Profiles[i];
                    if (kvp.Value.Equals(LastUsedSettings))
                        return i;
                }

                return null;
            }

            public void Add(string key, PasswordGenerator.CombinedKeyOptions options)
            {
                for (var index = 0; index < Profiles.Count; index++)
                {
                    var kvp = Profiles[index];
                    if (kvp.Key == key)
                    {
                        Profiles[index] = new KeyValuePair<string, PasswordGenerator.CombinedKeyOptions>(key, options);
                        return;
                    }
                }

                Profiles.Add(new KeyValuePair<string, PasswordGenerator.CombinedKeyOptions>(key, options));
            }

            public void Remove(in int profileIndex)
            {
                Profiles.RemoveAt(profileIndex);
            }
        }
		
		protected override void OnCreate(Bundle savedInstanceState) {
			_design.ApplyTheme(); 
			base.OnCreate(savedInstanceState);
			
			SetContentView(Resource.Layout.generate_password);
			SetResult(KeePass.ExitNormal);

			var prefs = GetPreferences(FileCreationMode.Private);


            
			string jsonOptions = prefs.GetString("password_generator_profiles", null);
            if (jsonOptions != null)
            {
                try
                {
                    _profiles = JsonConvert.DeserializeObject<PasswordProfiles>(jsonOptions);
                }
                catch (Exception e)
                {
                    Kp2aLog.LogUnexpectedError(e);
                }
            }
            else
            {
                PasswordGenerator.CombinedKeyOptions options = new PasswordGenerator.CombinedKeyOptions()
                {
                    PasswordGenerationOptions = new PasswordGenerator.PasswordGenerationOptions()
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
                    }
                };
                _profiles = new PasswordProfiles()
                {
                    LastUsedSettings = options,
                    Profiles = GetDefaultProfiles()
                };

            }

            _profiles ??= new PasswordProfiles();
            _profiles.LastUsedSettings ??= new PasswordGenerator.CombinedKeyOptions()
            {
                PasswordGenerationOptions = new PasswordGenerator.PasswordGenerationOptions()
                    {Length = 7, UpperCase = true, LowerCase = true, Digits = true}
            };
            _profiles.Profiles ??= new List<KeyValuePair<string, PasswordGenerator.CombinedKeyOptions>>();

            _updateDisabled = true;
            PopulateFieldsFromOptions(_profiles.LastUsedSettings);
            _updateDisabled = false;

            var profileSpinner = UpdateProfileSpinner();

            profileSpinner.ItemSelected += (sender, args) =>
            {
                if (profileSpinner.SelectedItemPosition > 0)
                {
                    _profiles.LastUsedSettings = _profiles.Profiles[profileSpinner.SelectedItemPosition - 1].Value;
                    _updateDisabled = true;
                    PopulateFieldsFromOptions(_profiles.LastUsedSettings);
                    _updateDisabled = false;
                    UpdatePassword();
                }
            };

            foreach (int id in _buttonLengthButtonIds) {
				Button button = (Button) FindViewById(id);
				button.Click += (sender, e) => 
				{
					Button b = (Button) sender;
					
					EditText editText = (EditText) FindViewById(Resource.Id.length);
					editText.Text = b.Text;
				};
			}

            FindViewById<EditText>(Resource.Id.length).TextChanged += (sender, args) => UpdatePassword();
            FindViewById<EditText>(Resource.Id.wordcount).TextChanged += (sender, args) => UpdatePassword();
            FindViewById<EditText>(Resource.Id.wordseparator).TextChanged += (sender, args) => UpdatePassword();

            foreach (int id in _checkboxIds)
            {
                FindViewById<CheckBox>(id).CheckedChange += (sender, args) => UpdatePassword();
            }
            
            var mode_spinner = FindViewById<Spinner>(Resource.Id.spinner_password_generator_mode);
            mode_spinner.ItemSelected += (sender, args) =>
            {
                FindViewById(Resource.Id.passphraseOptions).Visibility =
                    mode_spinner.SelectedItemPosition == 0 ? ViewStates.Gone : ViewStates.Visible;
                FindViewById(Resource.Id.passwordOptions).Visibility =
                    mode_spinner.SelectedItemPosition == 1 ? ViewStates.Gone : ViewStates.Visible;

                UpdatePassword();
            };

            FindViewById<Spinner>(Resource.Id.spinner_password_generator_case_mode).ItemSelected += (sender, args) =>
            {
                UpdatePassword();
            };

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

            FindViewById(Resource.Id.btn_password_generator_profile_save)
                .Click += (sender, args) =>
            {
                var editText = new EditText(this);
                new AlertDialog.Builder(this)
                    .SetMessage(Resource.String.save_password_generation_profile_text)
                    .SetView(editText)
                    .SetPositiveButton(Android.Resource.String.Ok, (o, eventArgs) =>
                    {
                        _profiles.Add(editText.Text, GetOptions());
                        UpdateProfileSpinner();
                    })
                    .Show();
            };

            FindViewById(Resource.Id.btn_password_generator_profile_delete)
                .Click += (sender, args) =>
            {
                if (profileSpinner.SelectedItemPosition > 0)
                {
                    _profiles.Remove(profileSpinner.SelectedItemPosition-1);
                    UpdateProfileSpinner();
                }
            };


            EditText txtPasswordToSet = (EditText) FindViewById(Resource.Id.password_edit);

            _passwordFont.ApplyTo(txtPasswordToSet);

            UpdatePassword();

			SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetHomeButtonEnabled(true);

		}

        private Spinner UpdateProfileSpinner()
        {
            string[] profileNames = new List<string> {GetString(Resource.String.custom_settings)}
                .Concat(_profiles.Profiles.Select(p => p.Key))
                .ToArray();
            ArrayAdapter<String> profileArrayAdapter = new ArrayAdapter<String>(this,
                Android.Resource.Layout.SimpleSpinnerDropDownItem,
                profileNames);
            var profileSpinner = FindViewById<Spinner>(Resource.Id.spinner_password_generator_profile);
            profileSpinner.Adapter = profileArrayAdapter;

            UpdateProfileSpinnerSelection();
            return profileSpinner;
        }

        private static List<KeyValuePair<string, PasswordGenerator.CombinedKeyOptions>> GetDefaultProfiles()
        {
            return new List<KeyValuePair<string, PasswordGenerator.CombinedKeyOptions>>()
            {
                new KeyValuePair<string, PasswordGenerator.CombinedKeyOptions>(
                    "Simple12", new PasswordGenerator.CombinedKeyOptions()
                    {
                        PasswordGenerationOptions
                            = new PasswordGenerator.PasswordGenerationOptions()
                            {
                                Length = 12, AtLeastOneFromEachGroup = true, ExcludeLookAlike = true,
                                Digits = true, LowerCase = true, UpperCase = true
                            }

                    }
                ),
                new KeyValuePair<string, PasswordGenerator.CombinedKeyOptions>(
                    "Special12", new PasswordGenerator.CombinedKeyOptions()
                    {
                        PasswordGenerationOptions
                            = new PasswordGenerator.PasswordGenerationOptions()
                            {
                                Length = 12, AtLeastOneFromEachGroup = true, ExcludeLookAlike = true,
                                Digits = true, LowerCase = true, UpperCase = true,Specials = true,Brackets = true
                            }

                    }
                ),
                new KeyValuePair<string, PasswordGenerator.CombinedKeyOptions>(
                    "Password64", new PasswordGenerator.CombinedKeyOptions()
                    {
                        PasswordGenerationOptions
                            = new PasswordGenerator.PasswordGenerationOptions()
                            {
                                Length = 64, AtLeastOneFromEachGroup = true, 
                                Digits = true, LowerCase = true, UpperCase = true, ExcludeLookAlike = false,Specials = true,Brackets = true, Minus = true, Space = true, SpecialsExtended = true,Underline = true
                            }

                    }
                ),
                new KeyValuePair<string, PasswordGenerator.CombinedKeyOptions>(
                        
                    "Passphrase7", new PasswordGenerator.CombinedKeyOptions()
                    {
                        PassphraseGenerationOptions
                            = new PasswordGenerator.PassphraseGenerationOptions()
                            {
                                WordCount = 7,
                                CaseMode = PasswordGenerator.PassphraseGenerationOptions.PassphraseCaseMode.Lowercase,
                                Separator = " "
                            }
                    }
                ),
                new KeyValuePair<string, PasswordGenerator.CombinedKeyOptions>(
                    "Passphrase5Plus", new PasswordGenerator.CombinedKeyOptions()
                    {
                        PassphraseGenerationOptions
                            = new PasswordGenerator.PassphraseGenerationOptions()
                            {
                                WordCount = 5,
                                CaseMode = PasswordGenerator.PassphraseGenerationOptions.PassphraseCaseMode.PascalCase,
                                Separator = " "
                            },
                        PasswordGenerationOptions = new PasswordGenerator.PasswordGenerationOptions()
                        {
                            Length = 2,
                            AtLeastOneFromEachGroup = true,
                            ExcludeLookAlike = true,
                            Digits = true,
                            Specials = true
                        }
                    }
                )
                        
            };
        }

        private void PopulateFieldsFromOptions(PasswordGenerator.CombinedKeyOptions combinedOptions)
        {
            PasswordGenerator.PasswordGenerationOptions passwordOptions = combinedOptions.PasswordGenerationOptions;
            if (passwordOptions != null)
            {
                ((CheckBox)FindViewById(Resource.Id.cb_uppercase)).Checked = passwordOptions.UpperCase;
                ((CheckBox)FindViewById(Resource.Id.cb_lowercase)).Checked = passwordOptions.LowerCase;
                ((CheckBox)FindViewById(Resource.Id.cb_digits)).Checked = passwordOptions.Digits;
                ((CheckBox)FindViewById(Resource.Id.cb_minus)).Checked = passwordOptions.Minus;
                ((CheckBox)FindViewById(Resource.Id.cb_underline)).Checked = passwordOptions.Underline;
                ((CheckBox)FindViewById(Resource.Id.cb_space)).Checked = passwordOptions.Space;
                ((CheckBox)FindViewById(Resource.Id.cb_specials)).Checked = passwordOptions.Specials;
                ((CheckBox)FindViewById(Resource.Id.cb_specials_extended)).Checked = passwordOptions.SpecialsExtended;
                ((CheckBox)FindViewById(Resource.Id.cb_brackets)).Checked = passwordOptions.Brackets;
                ((CheckBox)FindViewById(Resource.Id.cb_exclude_lookalike)).Checked = passwordOptions.ExcludeLookAlike;
                ((CheckBox)FindViewById(Resource.Id.cb_at_least_one_from_each_group)).Checked = passwordOptions.AtLeastOneFromEachGroup;

                ((EditText)FindViewById(Resource.Id.length)).Text = passwordOptions.Length.ToString(CultureInfo.InvariantCulture);

                FindViewById(Resource.Id.passwordOptions).Visibility = ViewStates.Visible;
            }
            else
            {
                FindViewById(Resource.Id.passwordOptions).Visibility = ViewStates.Gone;
            }

            var passphraseOptions = combinedOptions.PassphraseGenerationOptions;

            if (passphraseOptions != null)
            {
                FindViewById<EditText>(Resource.Id.wordcount).Text = passphraseOptions.WordCount.ToString(CultureInfo.InvariantCulture);
                FindViewById<EditText>(Resource.Id.wordseparator).Text = passphraseOptions.Separator;
                FindViewById<Spinner>(Resource.Id.spinner_password_generator_case_mode)
                    .SetSelection((int)passphraseOptions.CaseMode);

                FindViewById(Resource.Id.passphraseOptions).Visibility = ViewStates.Visible;
            }
            else
            {
                FindViewById(Resource.Id.passphraseOptions).Visibility = ViewStates.Gone;
            }
            

            int mode;
            if (combinedOptions.PasswordGenerationOptions != null &&
                combinedOptions.PassphraseGenerationOptions != null)
                mode = 2;
            else if (combinedOptions.PasswordGenerationOptions == null &&
                     combinedOptions.PassphraseGenerationOptions != null)
                mode = 1;
            else mode = 0;
            
            FindViewById<Spinner>(Resource.Id.spinner_password_generator_mode)
                .SetSelection(mode);

        }

        private void UpdatePassword()
        {
            if (_updateDisabled)
                return;
            String password = GeneratePassword();

            EditText txtPassword = (EditText) FindViewById(Resource.Id.password_edit);
            txtPassword.Text = password;

            var progressBar = FindViewById<ProgressBar>(Resource.Id.pb_password_strength);
            var passwordBits = QualityEstimation.EstimatePasswordBits(password.ToCharArray());
            progressBar.Progress = (int)passwordBits;
            progressBar.Max = 128;

            Color color = new Color(196, 63, 49);
            if (passwordBits > 40)
            {
                color = new Color(219, 152, 55);
            }
            if (passwordBits > 64)
            {
                color = new Color(96, 138, 38);
            }
            if (passwordBits > 100)
            {
                color = new Color(31, 128, 31);
            }
            progressBar.ProgressDrawable.SetColorFilter(new PorterDuffColorFilter(color, PorterDuff.Mode.SrcIn));

            FindViewById<TextView>(Resource.Id.tv_password_strength).Text = " " + passwordBits + " bits";

            

            UpdateProfileSpinnerSelection();
        }

        private void UpdateProfileSpinnerSelection()
        {
            int? lastUsedIndex = _profiles.TryFindLastUsedProfileIndex();
            FindViewById<Spinner>(Resource.Id.spinner_password_generator_profile)
                .SetSelection(lastUsedIndex != null ? lastUsedIndex.Value + 1 : 0);
        }

        public String GeneratePassword() {
			String password = "";
			
			try 
            {
                PasswordGenerator generator = new PasswordGenerator(this);

                var options = GetOptions();

                try
                {
                    password = generator.GeneratePassword(options);
                }
                catch (Exception e)
                {
                    throw new ArgumentException(GetString(Resource.String.error_pass_gen_type));
                }

                _profiles.LastUsedSettings = options;

				SaveProfiles();
            }
            catch (Exception e) 
            {
				Toast.MakeText(this, e.Message, ToastLength.Long).Show();
			}
			
			return password;
		}

        private void SaveProfiles()
        {
            var prefs = GetPreferences(FileCreationMode.Private);
            prefs.Edit()
                .PutString("password_generator_profiles", JsonConvert.SerializeObject(_profiles))
                .Commit();
        }

        private PasswordGenerator.CombinedKeyOptions GetOptions()
        {
            PasswordGenerator.CombinedKeyOptions options = new PasswordGenerator.CombinedKeyOptions();
            if (FindViewById(Resource.Id.passphraseOptions).Visibility == ViewStates.Visible)
            {
                int wordCount;
                if (!int.TryParse(((EditText)FindViewById(Resource.Id.wordcount)).Text, out wordCount))
                {
                    throw new Exception(GetString(Resource.String.error_wrong_length));
                }

                options.PassphraseGenerationOptions =
                    new PasswordGenerator.PassphraseGenerationOptions()
                    {
                        WordCount = wordCount,
                        Separator = FindViewById<EditText>(Resource.Id.wordseparator).Text,
                        CaseMode = (PasswordGenerator.PassphraseGenerationOptions.PassphraseCaseMode)FindViewById<Spinner>(Resource.Id.spinner_password_generator_case_mode).SelectedItemPosition
                    };
            }

            if (FindViewById(Resource.Id.passwordOptions).Visibility == ViewStates.Visible)
            {
                int length;
                if (!int.TryParse(((EditText) FindViewById(Resource.Id.length)).Text, out length))
                {
                    throw new Exception(GetString(Resource.String.error_wrong_length));
                }

                options.PasswordGenerationOptions = 
                    new PasswordGenerator.PasswordGenerationOptions()
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
                        AtLeastOneFromEachGroup = ((CheckBox) FindViewById(Resource.Id.cb_at_least_one_from_each_group))
                            .Checked
                    };
            }

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

