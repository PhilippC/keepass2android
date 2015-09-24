using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Text;
using Android.Views;
using Android.Widget;
using KeePassLib.Utility;
using Keepass2android.Pluginsdk;
using keepass2android.Utils;
using Object = Java.Lang.Object;

namespace MasterKeePlugin
{
	[Activity(Label = "Setup Master Entry")]
	public class SetupMasterEntryActivity : Activity
	{
		private bool _showPassword;

		private bool TryGetPassword(out string pass)
		{
			TextView passView = (TextView)FindViewById(Resource.Id.entry_password);
			pass = passView.Text;

			if (_showPassword)
				return true;

			TextView passConfView = (TextView)FindViewById(Resource.Id.entry_confpassword);
			String confpass = passConfView.Text;

			// Verify that passwords match
			if (!pass.Equals(confpass))
			{
				// Passwords do not match
				Toast.MakeText(this, Resource.String.error_pass_match, ToastLength.Long).Show();
				return false;
			}
			return true;
		}

		private void MakePasswordMaskedOrVisible()
		{
			TextView password = (TextView)FindViewById(Resource.Id.entry_password);
			TextView confpassword = (TextView)FindViewById(Resource.Id.entry_confpassword);
			if (_showPassword)
			{
				password.InputType = InputTypes.ClassText | InputTypes.TextVariationVisiblePassword;
				confpassword.Visibility = ViewStates.Gone;
			}
			else
			{
				password.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
				confpassword.Visibility = ViewStates.Visible;
			}

		}

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			SetContentView(Resource.Layout.Setup);
			ImageButton btnTogglePassword = (ImageButton)FindViewById(Resource.Id.toggle_password);
			btnTogglePassword.Click += (sender, e) =>
			{
				_showPassword = !_showPassword;
				MakePasswordMaskedOrVisible();
			};
			Android.Graphics.PorterDuff.Mode mMode = Android.Graphics.PorterDuff.Mode.SrcAtop;
			Android.Graphics.Color color = new Android.Graphics.Color (224, 224, 224);
			btnTogglePassword.SetColorFilter (color, mMode);


			FindViewById(Resource.Id.button_ok).Click += delegate(object sender, EventArgs args)
				{
					string password;
					if (!TryGetPassword(out password))
						return;
					string username = FindViewById<EditText>(Resource.Id.etUser).Text;
					new LoadingDialog<object, object, object>(this, GetString(Resource.String.deriving_key), false,
					                                          delegate
						                                          {
							                                          return
								                                          MemUtil.ByteArrayToHexString(
									                                          MasterPassword.MpAlgorithm.GetKeyForPassword(username, password));

						                                          },
					                                          delegate(Object res)
						                                          {
							                                          string derivedKey = (string) res;
							                                          AddMasterKeeEntry(derivedKey, username);
																	  
						                                          }
						).Execute();
					
				};
		}

		private void AddMasterKeeEntry(string derivedKey, string username)
		{
			Dictionary<string, string> fields = new Dictionary<string, string>();
			fields[KeepassDefs.TitleField] = "MasterKee master entry";
			fields[KeepassDefs.UserNameField] = username;
			fields[KeepassDefs.UrlField] = "androidapp://" + PackageName;
			fields[KeepassDefs.PasswordField] = derivedKey;
			try
			{
				StartActivityForResult(Kp2aControl.GetAddEntryIntent(fields, new List<string>()), 123);
				//TODO: get created entry id and store
				Finish();
			}
			catch (ActivityNotFoundException)
			{
				Toast.MakeText(this, "No Keepass2Android host app found. Please install Keepass2Android 0.9.4 or above!", ToastLength.Long).Show();
			}
			
		}

	}
}