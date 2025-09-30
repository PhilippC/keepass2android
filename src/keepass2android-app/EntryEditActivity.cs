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
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Android.Preferences;
using Java.IO;
using KeePassLib.Utility;
using KeePassLib;
using Android.Text;
using KeePassLib.Security;
using Android.Content.PM;
using System.IO;
using System.Globalization;
using System.Net;
using System.Text;
using Android.Content.Res;
using Android.Database;
#if !NO_QR_SCANNER
using Android.Gms.Common;
using Android.Gms.Tasks;
#endif
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Runtime;
using Android.Util;
using Google.Android.Material.Dialog;
using keepass2android.Io;
using KeePassLib.Serialization;
using KeeTrayTOTP.Libraries;
using PluginTOTP;

using Debug = System.Diagnostics.Debug;
using File = System.IO.File;
using Object = Java.Lang.Object;
using Uri = Android.Net.Uri;
using Resource = keepass2android.Resource;
using Google.Android.Material.TextField;
#if !NO_QR_SCANNER
using Xamarin.Google.MLKit.Vision.Barcode.Common;
using Xamarin.Google.MLKit.Vision.CodeScanner;
#endif
using Console = System.Console;

namespace keepass2android
{
    class TotpEditUtil
    {
        public enum TotpEncoding
        {
            Default, Steam, Custom
        }
        public static string BuildOtpString(string entryTitle, string userName, string secret, string totpLength, string timeStep, TotpEncoding encoding, int algorithm)
        {
            string entryEncoded = string.IsNullOrWhiteSpace(entryTitle)
                ? "Keepass2Android"
                : System.Uri.EscapeUriString(entryTitle);
            return $"otpauth://totp/{entryEncoded}:{System.Uri.EscapeUriString(userName)}?" +
                   $"secret={SanitizeInput(secret)}" +
                   $"&issuer={entryEncoded}"
                   + (encoding != TotpEncoding.Custom ? "" : $"&period={timeStep}&digits={totpLength}&algorithm={AlgorithmIndexToString(algorithm)}") +
                   (encoding == TotpEncoding.Steam ? "&encoder=steam" : "");

        }

        private static string AlgorithmIndexToString(in int algorithm)
        {
            switch (algorithm)
            {
                case 0:
                    return "SHA1";
                case 1:
                    return "SHA256";
                case 2:
                    return "SHA512";
                default:
                    return "";
            }
        }

        static string SanitizeInput(string encodedData)
        {
            if (encodedData.Length <= 0)
            {
                return encodedData;
            }

            StringBuilder newEncodedDataBuilder = new StringBuilder(encodedData);
            int i = 0;
            foreach (var ch in encodedData)
            {
                switch (ch)
                {
                    case '0':
                        newEncodedDataBuilder[i++] = 'O';
                        break;
                    case '1':
                        newEncodedDataBuilder[i++] = 'L';
                        break;
                    case '8':
                        newEncodedDataBuilder[i++] = 'B';
                        break;
                    default:
                        if (('A' <= ch && ch <= 'Z') || ('a' <= ch && ch <= 'z') || ('2' <= ch && ch <= '7'))
                        {
                            newEncodedDataBuilder[i++] = ch;
                        }

                        break;
                }
            }

            string newEncodedData = newEncodedDataBuilder.ToString().Substring(0, i);

            return AddPadding(newEncodedData);

        }


        static string AddPadding(string encodedData)
        {
            if (encodedData.Length <= 0 || encodedData.Length % 8 == 0)
            {
                return encodedData;
            }

            int rBytes = encodedData.Length % 8;
            // rBytes must be a member of {2, 4, 5, 7}
            if (1 == rBytes || 3 == rBytes || 6 == rBytes)
            {
                return encodedData;
            }

            string newEncodedData = encodedData;
            for (int nPads = 8 - rBytes; nPads > 0; --nPads)
            {
                newEncodedData += "=";
            }

            return newEncodedData;
        }

    }


    public abstract class ExtraEditView : RelativeLayout
    {
        protected ExtraEditView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public ExtraEditView(Context? context) : base(context)
        {
            
        }

        public Button EditButton => (Button)FindViewById(Resource.Id.edit_extra);
        public abstract TextView KeyTextView { get; }
        public abstract TextView ValueTextView { get; }
        public abstract CheckBox ProtectionCheckbox { get; }


        //event which is triggered when the user modifies the extra string
        public event EventHandler ExtraModified;

        protected void InvokeExtraModifiedHandler()
        {
            if (ExtraModified != null)
			{
                ExtraModified(this, EventArgs.Empty);
            }
            
        }


    }

    public sealed class ExtraEditViewString: ExtraEditView
    {
        private EntryEditActivity _activity;
        private readonly PasswordFont _passwordFont;

        private ExtraEditViewString(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }
		
        public ExtraEditViewString(EntryEditActivity context, KeyValuePair<string, ProtectedString> pair, PasswordFont passwordFont) : base(context)
        {
            _activity = context;
            _passwordFont = passwordFont;
            Inflate(context, Resource.Layout.entry_edit_section, this);

            
            Tag = pair.Key;
            KeyTextView = ((TextView)FindViewById(Resource.Id.extrakey))!;
            ValueContainer = ((TextInputLayout)FindViewById(Resource.Id.value_container))!;
            ValueEdit = ((EditText)FindViewById(Resource.Id.value))!;
            ProtectionCheckbox = ((CheckBox)FindViewById(Resource.Id.protection))!;

            // Set new IDs for views to ensure they are unique. Otherwise restoring the state may fail.
            // Note that we shouldn't use Resource.Id.XXX with FindViewById afterwards
            ValueEdit.Id = GenerateViewId();
            ValueContainer.Id = GenerateViewId();
            KeyTextView.Id = GenerateViewId();
            ProtectionCheckbox.Id = GenerateViewId();

            KeyTextView.Text = pair.Key;
            string stringValue = pair.Value.ReadString();
            ValueEdit.Text = stringValue;
            ValueContainer.Hint = pair.Key;
            
            ValueEdit.TextChanged +=
                (sender, e) =>
                {
                    //invoke ExtraModified
                    InvokeExtraModifiedHandler();
                };
            passwordFont.ApplyTo(ValueEdit);
				
            ProtectionCheckbox.Checked = pair.Value.IsProtected;
            EditButton.Click += (sender, e) => Edit();

        }

        public EditText ValueEdit { get; }

        public override TextView KeyTextView { get; }
        public override TextView ValueTextView => ValueEdit;

        public TextInputLayout ValueContainer { get; }

        public override CheckBox ProtectionCheckbox { get; }

        public void Edit()
        {
            var sender = EditButton;	

                MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(_activity);
                View dlgView = _activity.LayoutInflater.Inflate(Resource.Layout.
                    edit_extra_string_dialog, null);



                builder.SetView(dlgView);
                builder.SetNegativeButton(Android.Resource.String.Cancel, (o, args) => { });
                builder.SetPositiveButton(Android.Resource.String.Ok, (o, args) =>
                {
                    CopyFieldFromExtraDialog(o, Resource.Id.title, KeyTextView);

                    CopyCheckboxFromExtraDialog(o, Resource.Id.protection, ProtectionCheckbox);
                    var sourceFieldTitle = (EditText)((Dialog)o).FindViewById(Resource.Id.title);
                    var sourceFieldValue = (EditText)((Dialog)o).FindViewById(Resource.Id.value);
                    ValueEdit.Text = sourceFieldValue.Text;
                    ValueContainer.Hint = sourceFieldTitle.Text;
                });
                Dialog dialog = builder.Create();

                //setup delete button:
                var deleteButton = dlgView.FindViewById<Button>(Resource.Id.delete_extra);
                deleteButton.SetCompoundDrawablesWithIntrinsicBounds(Resources.GetDrawable(Resource.Drawable.baseline_delete_24), null, null, null);
                deleteButton.Click += (o, args) =>
                {
                    DeleteAdvancedString();
                    dialog.Dismiss();
                };
                //copy values:
                View ees = (View)sender.Parent;
                dlgView.FindViewById<TextView>(Resource.Id.title).Text = KeyTextView.Text;
                dlgView.FindViewById<EditText>(Resource.Id.value).Text = ValueEdit.Text;
                _passwordFont.ApplyTo(dlgView.FindViewById<EditText>(Resource.Id.value));
                Util.SetNoPersonalizedLearning(dlgView);
                dlgView.FindViewById<CheckBox>(Resource.Id.protection).Checked = ProtectionCheckbox.Checked;

                var titleView = ((AutoCompleteTextView)dlgView.FindViewById(Resource.Id.title));
                titleView.Adapter = new ArrayAdapter(_activity, Android.Resource.Layout.SimpleListItem1, Android.Resource.Id.Text1, _activity.AdditionalKeys);


                dialog.Show();

        }

        private void CopyFieldFromExtraDialog(object dialog, int originFieldId, TextView targetField)
        {
            var sourceField = (EditText)((Dialog)dialog).FindViewById(originFieldId);
            if (sourceField.Text != targetField.Text)
            {
                targetField.Text = sourceField.Text;
                InvokeExtraModifiedHandler();
            }
        }

        private void CopyCheckboxFromExtraDialog(object dialog, int fieldId, CheckBox targetField)
        {
            var sourceField = (CheckBox)((Dialog)dialog).FindViewById(fieldId);
            
            if (sourceField.Checked != targetField.Checked)
            {
                targetField.Checked = sourceField.Checked;
                InvokeExtraModifiedHandler();
            }
        }

        public void DeleteAdvancedString()
        {
            LinearLayout container = (LinearLayout)_activity.FindViewById(Resource.Id.advanced_container);
            InvokeExtraModifiedHandler();
            for (int i = 0; i < container.ChildCount; i++)
            {
                var ees = container.GetChildAt(i);
                if (ees == this)
                {
                    container.RemoveViewAt(i);
                    container.Invalidate();
                    break;
                }
            }
        }


        public void EditTotpString(PwEntry entry)
        {
            MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(_activity);
            View dlgView = _activity.LayoutInflater.Inflate(Resource.Layout.
                configure_totp_dialog, null);



            builder.SetView(dlgView);
            builder.SetNegativeButton(Android.Resource.String.Cancel, (o, args) => { });
            builder.SetPositiveButton(Android.Resource.String.Ok, (o, args) =>
            {

                var targetField = ValueEdit;
                if (targetField != null)
                {
                    string entryTitle = Util.GetEditText(_activity, Resource.Id.entry_title);
                    string username = Util.GetEditText(_activity, Resource.Id.entry_user_name);
                    string secret = dlgView.FindViewById<TextView>(Resource.Id.totp_secret_key).Text;
                    string totpLength = dlgView.FindViewById<EditText>(Resource.Id.totp_length).Text;
                    string timeStep = dlgView.FindViewById<EditText>(Resource.Id.totp_time_step).Text;
                    var checkedTotpId = (int)dlgView.FindViewById<RadioGroup>(Resource.Id.totp_encoding).CheckedRadioButtonId;
                    TotpEditUtil.TotpEncoding encoding = (checkedTotpId == Resource.Id.totp_encoding_steam)
                        ? TotpEditUtil.TotpEncoding.Steam : (checkedTotpId == Resource.Id.totp_encoding_rfc6238 ? TotpEditUtil.TotpEncoding.Default : TotpEditUtil.TotpEncoding.Custom);
                    var algorithm = (int)dlgView.FindViewById<Spinner>(Resource.Id.totp_algorithm).SelectedItemPosition;

                    targetField.Text = TotpEditUtil.BuildOtpString(entryTitle, username, secret, totpLength, timeStep, encoding, algorithm);
                }
                else
                {
                    App.Kp2a.ShowMessage(_activity, "did not find target field", MessageSeverity.Error);
                }


                //not calling State.Entry.Strings.Set(...). We only do this when the user saves the changes.
                InvokeExtraModifiedHandler();

            });
            Dialog dialog = builder.Create();

            dlgView.FindViewById<RadioButton>(Resource.Id.totp_encoding_custom).CheckedChange += (o, args) =>
            {
                dlgView.FindViewById(Resource.Id.totp_custom_settings_group).Visibility = args.IsChecked ? ViewStates.Visible : ViewStates.Gone;
            };

            dlgView.FindViewById<Button>(Resource.Id.totp_scan).Click += async (object o, EventArgs args) =>
            {
                if (GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(_activity) != ConnectionResult.Success)
                {
                    App.Kp2a.ShowMessage(_activity, Resource.String.qr_scanning_error_no_google_play_services,
                        MessageSeverity.Error);
                    return;
                }

                GmsBarcodeScannerOptions options = new GmsBarcodeScannerOptions.Builder()
                    .SetBarcodeFormats(Barcode.FormatQrCode)
                    .Build();
                var scanner = GmsBarcodeScanning.GetClient(_activity, options);

                scanner.StartScan()
                    .AddOnSuccessListener(new SuccessListener((barcode) =>
                    {
                        if (barcode.RawValue?.StartsWith("otpauth://") == true)
                        {
                            dialog.Dismiss();
                            ValueTextView.Text = barcode.RawValue;
                        }
                        else
                        {
                            App.Kp2a.ShowMessage(_activity, "Scanned code should contain an otpauth:// text.", MessageSeverity.Warning);
                        }
                    }))
                    .AddOnFailureListener(new FailureListener((e) =>
                    {
                        Console.WriteLine($"Scan failed: {Util.GetErrorMessage(e)}");
                    }));


            };

            //copy values from entry into dialog
            
            TotpData totpData = new Kp2aTotp().TryGetTotpData(new PwEntryOutput(entry, App.Kp2a.CurrentDb));
            if (totpData != null)
            {
                dlgView.FindViewById<TextView>(Resource.Id.totp_secret_key).Text = totpData.TotpSeed;
                if (totpData.Encoder == TotpData.EncoderSteam)
                {
                    dlgView.FindViewById<RadioButton>(Resource.Id.totp_encoding_steam).Checked = true;
                }
                else if ((totpData.Encoder == TotpData.EncoderRfc6238) && (totpData.IsDefaultRfc6238))
                {
                    dlgView.FindViewById<RadioButton>(Resource.Id.totp_encoding_rfc6238).Checked = true;
                }
                else
                {
                    dlgView.FindViewById<RadioButton>(Resource.Id.totp_encoding_custom).Checked = true;
                }

                dlgView.FindViewById<EditText>(Resource.Id.totp_length).Text = totpData.Length;
                dlgView.FindViewById<EditText>(Resource.Id.totp_time_step).Text = totpData.Duration;
                dlgView.FindViewById<Spinner>(Resource.Id.totp_algorithm).SetSelection(totpData.HashAlgorithm == TotpData.HashSha1 ? 0 : (
                        totpData.HashAlgorithm == TotpData.HashSha256 ? 1 :
                            (totpData.HashAlgorithm == TotpData.HashSha256 ? 2 : 0)));

                dlgView.FindViewById(Resource.Id.totp_custom_settings_group).Visibility = dlgView.FindViewById<RadioButton>(Resource.Id.totp_encoding_custom).Checked ? ViewStates.Visible : ViewStates.Gone;
            }

            _passwordFont.ApplyTo(dlgView.FindViewById<EditText>(Resource.Id.totp_secret_key));
            Util.SetNoPersonalizedLearning(dlgView);

            dialog.Show();

        }



    }

    public sealed class ExtraEditViewBool : ExtraEditView
    {
        public ExtraEditViewBool(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public ExtraEditViewBool(Context? context, KeyValuePair<string, ProtectedString> pair, string title) : base(context)
        {
            Inflate(context, Resource.Layout.entry_edit_section_bool, this);
            Tag = pair.Key;
            KeyTextView = ((TextView)FindViewById(Resource.Id.extrakey))!;
            ProtectionCheckbox = ((CheckBox)FindViewById(Resource.Id.checkbox))!;
            ValueTextView = ((TextView)FindViewById(Resource.Id.value))!;

            KeyTextView.Id = GenerateViewId();
            ProtectionCheckbox.Id = GenerateViewId();
            ValueTextView.Id = GenerateViewId();

            KeyTextView.Text = pair.Key;
            ProtectionCheckbox.Checked = pair.Value.ReadString().Equals("True", StrUtil.CaseIgnoreCmp);
            ProtectionCheckbox.Text = title;
            ValueTextView.Text = ProtectionCheckbox.Checked.ToString();
            ProtectionCheckbox.CheckedChange += (sender, e) =>
            {
                ValueTextView.Text = ProtectionCheckbox.Checked.ToString();
                InvokeExtraModifiedHandler();
            };
        }

        public override CheckBox ProtectionCheckbox { get; }

        public override TextView ValueTextView { get; }

        public override TextView KeyTextView { get; }
    }


    internal sealed class ExtraEditViewFile : ExtraEditView
    {
        public ExtraEditViewFile(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }
        public ExtraEditViewFile(Context context, KeyValuePair<string, ProtectedString> pair, string title)
		: base(context)
        {
            Inflate(context, Resource.Layout.entry_edit_section_file, this);
            Tag = pair.Key;
            KeyTextView = ((TextView)FindViewById(Resource.Id.extrakey))!;
            TitleView = ((TextView)FindViewById(Resource.Id.title))!;
            ValueTextView = ((TextView)FindViewById(Resource.Id.value))!;
            FilestorageDisplay = FindViewById(Resource.Id.filestorage_display)!;
            FilestorageLogoView = FindViewById<ImageView>(Resource.Id.filestorage_logo)!;
            FilestorageLabelTextView = FindViewById<TextView>(Resource.Id.filestorage_label)!;
            FilenameTextView = FindViewById<TextView>(Resource.Id.label_filename)!;
            ProtectionCheckbox = ((CheckBox)FindViewById(Resource.Id.protection))!;

            KeyTextView.Id = GenerateViewId();
            TitleView.Id = GenerateViewId();
			ValueTextView.Id = GenerateViewId();
            FilestorageDisplay.Id = GenerateViewId();
			FilestorageLogoView.Id = GenerateViewId();
            FilestorageLabelTextView.Id = GenerateViewId();
            FilenameTextView.Id = GenerateViewId();
            ProtectionCheckbox.Id = GenerateViewId();

            KeyTextView.Text = pair.Key;
            TitleView.Text = title;
            UpdateFileView(pair.Value.ReadString());
            
        }

        public TextView TitleView { get; set; }

        public override TextView KeyTextView { get; }
		
        public Button ChangeLocationButton => (Button)FindViewById(Resource.Id.btn_change_location);


        public void UpdateFileView(string newValue)
        {
            ValueTextView.Text = newValue;
            IFileStorage fileStorage = null;
            var ioc = IOConnectionInfo.FromPath(newValue);
            try
            {
                fileStorage = App.Kp2a.GetFileStorage(ioc);
            }
            catch (NoFileStorageFoundException ex)
            {
                //ignore.
            }

            
            FilestorageDisplay.Visibility = (string.IsNullOrEmpty(newValue) && fileStorage != null) ? ViewStates.Gone : ViewStates.Visible;
            if (fileStorage != null)
            {
                int protocolSeparatorPos = ioc.Path.IndexOf("://", StringComparison.Ordinal);
                string protocolId = protocolSeparatorPos < 0 ?
                    "file" : ioc.Path.Substring(0, protocolSeparatorPos);
                Drawable drawable = App.Kp2a.GetStorageIcon(protocolId);
					
                FilestorageLogoView.SetImageDrawable(drawable);


                String fs_title = App.Kp2a.GetStorageDisplayName(protocolId);
                FilestorageLabelTextView.Text = fs_title;

                string displayPath = fileStorage.GetDisplayName(ioc);
                protocolSeparatorPos = displayPath.IndexOf("://", StringComparison.Ordinal);
                
                FilenameTextView.Text = protocolSeparatorPos < 0 ?
                    displayPath :
                    displayPath.Substring(protocolSeparatorPos + 3);

            }

        }

        public TextView FilenameTextView { get; set; }

        public TextView FilestorageLabelTextView { get; set; }

        public ImageView FilestorageLogoView { get; set; }

        public View FilestorageDisplay { get; set; }

        public override TextView ValueTextView { get; }
        
        public override CheckBox ProtectionCheckbox { get; }
    }


    [Activity(Label = "@string/app_name", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden, Theme = "@style/Kp2aTheme_ActionBar")]			
	public class EntryEditActivity : LockCloseActivity {

        protected override View? SnackbarAnchorView => FindViewById(Resource.Id.main_content);

        public const String KeyEntry = "entry";
		public const String KeyParent = "parent";
		public const String KeyTemplateUuid = "KeyTemplateUuid";
		
		public const int ResultOkIconPicker = (int)Result.FirstUser + 1000;
		//Certain additional (=extra) fields may have type="file". These fields show a browse button which triggers the file selection process, involving several activities.
		//The same requestCode is used for all such fields. We store the field key to which the last triggered process belongs in the Activity state. 
		public const int requestCodeSelectFileExtra = 44000;
		

		const string IntentContinueWithEditing = "ContinueWithEditing";

        private PasswordFont _passwordFont = new PasswordFont();

        EntryEditActivityState State
		{
			get { return App.Kp2a.EntryEditActivityState; }
		}

		public static void Launch(Activity act, PwEntry pw, AppTask appTask) {
			Intent i = new Intent(act, typeof(EntryEditActivity));
			
			i.PutExtra(KeyEntry, pw.Uuid.ToHexString());
			
			appTask.ToIntent(i);

			act.StartActivityForResult(i, 0);
		}
		
		public static void Launch(Activity act, PwGroup pw, PwUuid templateUuid, AppTask appTask) {
			Intent i = new Intent(act, typeof(EntryEditActivity));
			
			PwGroup parent = pw;
			i.PutExtra(KeyParent, parent.Uuid.ToHexString());
			i.PutExtra(KeyTemplateUuid, templateUuid.ToHexString());

			appTask.ToIntent(i);

			act.StartActivityForResult(i, 0);
		}

		bool _closeForReload;

        private AppTask _appTask;
        private AppTask AppTask
        {
            get { return _appTask; }
            set
            {
                _appTask = value;
                Kp2aLog.LogTask(value, MyDebugName);
            }
        }

		protected override void OnCreate(Bundle savedInstanceState)
		{
            //we don't want any background thread to update/reload the database while we're in this activity.
            if (!App.Kp2a.DatabasesBackgroundModificationLock.TryEnterReadLock(TimeSpan.FromSeconds(5)))
            {
                App.Kp2a.ShowMessage(this, GetString(Resource.String.failed_to_access_database), MessageSeverity.Error);
                Finish();
                return;
            }

            base.OnCreate(savedInstanceState);

			AppTask = AppTask.GetTaskInOnCreate(savedInstanceState, Intent);

			SetContentView(Resource.Layout.entry_edit);
			_closeForReload = false;

            Util.SetNoPersonalizedLearning(FindViewById(Resource.Id.entry_scroll));

			// Likely the app has been killed exit the activity
			if (!App.Kp2a.DatabaseIsUnlocked)
			{
				Finish();
				return;
			}


            if (savedInstanceState != null)
            {
                //recreating, e.g. after configuration change.
				//property "State" will return the state

			} else
			{
				Database db = App.Kp2a.CurrentDb;

				App.Kp2a.EntryEditActivityState = new EntryEditActivityState();
				ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
				State.ShowPassword = ! prefs.GetBoolean(GetString(Resource.String.maskpass_key), Resources.GetBoolean(Resource.Boolean.maskpass_default));
			
				Intent i = Intent;
				String uuidBytes = i.GetStringExtra(KeyEntry);
				
				PwUuid entryId = PwUuid.Zero;
				if (uuidBytes != null)
					entryId = new PwUuid(MemUtil.HexStringToByteArray(uuidBytes));
				
				State.ParentGroup = null;
				if (entryId.Equals(PwUuid.Zero))
				{
					//creating new entry
					String groupId = i.GetStringExtra(KeyParent);
					State.ParentGroup = db.KpDatabase.RootGroup.FindGroup(new PwUuid(MemUtil.HexStringToByteArray(groupId)), true);

					PwUuid templateId = new PwUuid(MemUtil.HexStringToByteArray(i.GetStringExtra(KeyTemplateUuid)));
					PwEntry templateEntry = null;
					if (!PwUuid.Zero.Equals(templateId))
					{
						templateEntry = db.EntriesById[templateId];
					}
					
					if (KpEntryTemplatedEdit.IsTemplate(templateEntry))
					{
						CreateNewFromKpEntryTemplate(db, templateEntry);
					}
					else if (templateEntry != null)
					{
						CreateNewFromStandardTemplate(templateEntry);
					}
					else
					{
						CreateNewWithoutTemplate(db);
					}
					
					AppTask.PrepareNewEntry(State.EntryInDatabase);
					State.IsNew = true;
					State.EntryModified = true;
					
				} 
				else
				{
					
					Debug.Assert(entryId != null);
					
					State.EntryInDatabase = db.EntriesById [entryId];
					State.IsNew = false;
					
					
				} 
				
				State.Entry = State.EntryInDatabase.CloneDeep();
			    if (State.Entry.ParentGroup != null && State.Entry.ParentGroup.Name.Equals("AutoOpen", StrUtil.CaseIgnoreCmp))
			        State.EditMode = new AutoOpenEdit(State.Entry);
			    else if (KpEntryTemplatedEdit.IsTemplated(db, State.Entry))
			        State.EditMode = new KpEntryTemplatedEdit(db, State.Entry);
			    else
			        State.EditMode = new DefaultEdit();


			    State.EditMode.InitializeEntry(State.Entry);

			}
		
			if (!State.EntryModified)
			    SetResult(KeePass.ExitNormal);
			else
				SetResult(KeePass.ExitRefreshTitle);



			FillData();
			View scrollView = FindViewById(Resource.Id.entry_scroll);
			scrollView.ScrollBarStyle = ScrollbarStyles.InsideInset;
			
			ImageButton iconButton = (ImageButton)FindViewById(Resource.Id.icon_button);
			
			if (State.SelectedIcon)
            {
                UpdateIconButton();
            }
			iconButton.Click += (sender, evt) => {
				UpdateEntryFromUi(State.Entry);
				IconPickerActivity.Launch(this);
			};
		

			// Generate password button
			FindViewById(Resource.Id.generate_button).Click += (sender, e) => 
            {
				UpdateEntryFromUi(State.Entry);
				GeneratePasswordActivity.Launch(this);
			};


            if (State.IsNew)
		    {
		        SupportActionBar.Title = GetString(Resource.String.add_entry);
		    }
		    else
		    {
                SupportActionBar.Title = GetString(Resource.String.edit_entry);
		    }

            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar.SetHomeButtonEnabled(true);


			Button addButton = (Button) FindViewById(Resource.Id.add_advanced);
			addButton.Visibility = ViewStates.Visible;
			addButton.Click += (sender, e) =>
			{
				LinearLayout container = (LinearLayout) FindViewById(Resource.Id.advanced_container);

				KeyValuePair<string, ProtectedString> pair = new KeyValuePair<string, ProtectedString>("" , new ProtectedString(true, ""));
                var ees = (ExtraEditViewString) CreateExtraStringEditView(pair);
				container.AddView(ees);

				State.EntryModified = true;
                ees.ExtraModified += (o, args) => State.EntryModified = true;

                /*TextView keyView = (TextView) ees.FindViewById(Resource.Id.title);
				keyView.RequestFocus();*/
                ees.Edit();
            };
			SetAddExtraStringEnabled();
			

            Button configureTotpButton = (Button)FindViewById(Resource.Id.configure_totp);

            configureTotpButton.Visibility = CanConfigureOtpSettings() ? ViewStates.Gone : ViewStates.Visible;
			configureTotpButton.Click += (sender, e) =>
            {
                ExtraEditViewString ees = FindExtraEditSection("otp") as ExtraEditViewString;
				if (ees == null)
                {
                    LinearLayout container = (LinearLayout) FindViewById(Resource.Id.advanced_container);

                    KeyValuePair<string, ProtectedString> pair =
                        new KeyValuePair<string, ProtectedString>("otp", new ProtectedString(true, ""));
                    ees = (ExtraEditViewString?)CreateExtraStringEditView(pair);
                    container.AddView(ees);
                    State.EntryModified = true;
                }
				ees?.EditTotpString(State.Entry);
            };

			FindViewById(Resource.Id.entry_extras_container).Visibility =
		        State.EditMode.ShowAddExtras || State.Entry.Strings.Any(s => !PwDefs.IsStandardField(s.Key)) ? ViewStates.Visible : ViewStates.Gone;
		    FindViewById(Resource.Id.entry_binaries_container).Visibility =
		        State.EditMode.ShowAddAttachments || State.Entry.Binaries.Any() ? ViewStates.Visible : ViewStates.Gone;

            ((CheckBox)FindViewById(Resource.Id.entry_expires_checkbox)).CheckedChange += (sender, e) => 
			{
				State.Entry.Expires = e.IsChecked;
				if (e.IsChecked)
				{
					if (State.Entry.ExpiryTime < DateTime.Now)
						State.Entry.ExpiryTime = DateTime.Now;
				}
				UpdateExpires();
				State.EntryModified = true;
			};

            


        }

        private void UpdateIconButton()
        {
			ImageButton iconButton = (ImageButton)FindViewById(Resource.Id.icon_button);
            App.Kp2a.CurrentDb.DrawableFactory.AssignDrawableTo(iconButton, this, App.Kp2a.CurrentDb.KpDatabase, (PwIcon)State.SelectedIconId, State.SelectedCustomIconId, false);
        }

        protected override void OnDestroy()
        {
            if (!IsFinishing)
            {
				//Store the edits made so far in the State variable so we can recover it e.g. after a configuration change
                UpdateEntryFromUi(State.Entry);
            }
            
            base.OnDestroy();
            //we don't want any background thread to update/reload the database while we're in this activity.
            App.Kp2a.DatabasesBackgroundModificationLock.ExitReadLock();
        }

        private bool hasRequestedKeyboardActivation = false;
        protected override void OnStart()
        {
			
	        base.OnStart();
	        if (PreferenceManager.GetDefaultSharedPreferences(this)
	            .GetBoolean(GetString(Resource.String.UseKp2aKeyboardInKp2a_key), false)
                && (!hasRequestedKeyboardActivation))
	        {
                //only try this once. if the user clicks cancel, we don't want to ask again
				// (it may happen that the activity is restarted because of the NewTask flag immediately after the dialog)
                hasRequestedKeyboardActivation = true;
                CopyToClipboardService.ActivateKeyboard(this);
	        }
        }

	    void AddBinaryOrAsk(Uri filename)
	    {

	        string strItem = GetFileName(filename);
	        if (String.IsNullOrEmpty(strItem))
	            strItem = "attachment.bin";

	        if (State.Entry.Binaries.Get(strItem) != null)
	        {
                MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(this);
	            builder.SetTitle(GetString(Resource.String.AskOverwriteBinary_title));

	            builder.SetMessage(GetString(Resource.String.AskOverwriteBinary));

	            builder.SetPositiveButton(GetString(Resource.String.AskOverwriteBinary_yes), (dlgSender, dlgEvt) =>
	            {
	                AddBinary(filename, true);
	            });

	            builder.SetNegativeButton(GetString(Resource.String.AskOverwriteBinary_no), (dlgSender, dlgEvt) =>
	            {
	                AddBinary(filename, false);
	            });

	            builder.SetNeutralButton(GetString(Android.Resource.String.Cancel),
	                (dlgSender, dlgEvt) => { });

	            Dialog dialog = builder.Create();
	            dialog.Show();


	        }
	        else
	            AddBinary(filename, true);
	    }

        protected override void OnResume()
	    {
	        if (_uriToAddOrAsk != null)
	        {
                AddBinaryOrAsk(_uriToAddOrAsk);
	            _uriToAddOrAsk = null;
	        }
            base.OnResume();
	    }

	    private void CreateNewFromKpEntryTemplate(Database db, PwEntry templateEntry)
		{
			var entry = new PwEntry(true, true);
			KpEntryTemplatedEdit.InitializeEntry(entry, templateEntry);


			State.EntryInDatabase = entry;
		}

		private void CreateNewFromStandardTemplate(PwEntry templateEntry)
		{
			var newEntry = templateEntry.CloneDeep();
			newEntry.SetUuid(new PwUuid(true), true); // Create new UUID
			newEntry.CreationTime = newEntry.LastModificationTime = newEntry.LastAccessTime = DateTime.Now;
			State.EntryInDatabase = newEntry;
		}

		private void CreateNewWithoutTemplate(Database db)
		{
			State.EntryInDatabase = new PwEntry(true, true);
			State.EntryInDatabase.Strings.Set(PwDefs.UserNameField, new ProtectedString(
						db.KpDatabase.MemoryProtection.ProtectUserName, db.KpDatabase.DefaultUserName));

			if ((State.ParentGroup.IconId != PwIcon.Folder) && (State.ParentGroup.IconId != PwIcon.FolderOpen) &&
				(State.ParentGroup.IconId != PwIcon.FolderPackage))
			{
				State.EntryInDatabase.IconId = State.ParentGroup.IconId; // Inherit icon from group
			}
			else
				State.EntryInDatabase.IconId = PwIcon.Key;
			State.EntryInDatabase.CustomIconUuid = State.ParentGroup.CustomIconUuid;
					
		}

		private void SetAddExtraStringEnabled()
		{
			((Button)FindViewById(Resource.Id.add_advanced)).Visibility = (!App.Kp2a.CurrentDb.DatabaseFormat.CanHaveCustomFields || !State.EditMode.ShowAddExtras) ? ViewStates.Gone : ViewStates.Visible;
            ((Button)FindViewById(Resource.Id.configure_totp)).Visibility = CanConfigureOtpSettings() ? ViewStates.Gone : ViewStates.Visible;
		}

        private bool CanConfigureOtpSettings()
        {
            return (!App.Kp2a.CurrentDb.DatabaseFormat.CanHaveCustomFields || !State.EditMode.ShowAddExtras) 
                && (new Kp2aTotp().TryGetAdapter(new PwEntryOutput(State.Entry, App.Kp2a.CurrentDb)) == null || (State.Entry.Strings.GetKeys().Contains("otp"))) //only allow to edit KeeWeb/KeepassXC style otps
                ;
        }

        

		void SaveEntry()
		{
			Database db = App.Kp2a.CurrentDb;
			EntryEditActivity act = this;
			
			if (!ValidateBeforeSaving())
				return;
			
			PwEntry initialEntry = State.EntryInDatabase.CloneDeep();
			
			PwEntry newEntry = State.EntryInDatabase;
			
			//Clone history and re-assign:
			newEntry.History = newEntry.History.CloneDeep();
			
			//Based on KeePass Desktop
			bool bCreateBackup = (!State.IsNew);
			if(bCreateBackup) newEntry.CreateBackup(null);
			
			if (State.SelectedIcon) 
			{
				newEntry.IconId = State.SelectedIconId;
				newEntry.CustomIconUuid = State.SelectedCustomIconId;
			} //else the State.EntryInDatabase.Icon
			
			UpdateEntryFromUi(newEntry);
			newEntry.Binaries = State.Entry.Binaries;
			newEntry.Expires = State.Entry.Expires;
			if (newEntry.Expires)
			{
				newEntry.ExpiryTime = State.Entry.ExpiryTime;
			}

		    State.EditMode.PrepareForSaving(newEntry);

			
			newEntry.Touch(true, false); // Touch *after* backup
			
			StrUtil.NormalizeNewLines(newEntry.Strings, true);
			
			bool bUndoBackup = false;
			PwCompareOptions cmpOpt =  (PwCompareOptions.NullEmptyEquivStd |
			                            PwCompareOptions.IgnoreTimes);
			if(bCreateBackup) cmpOpt |= PwCompareOptions.IgnoreLastBackup;
			if(newEntry.EqualsEntry(initialEntry, cmpOpt, MemProtCmpMode.CustomOnly))
			{
				// No modifications at all => restore last mod time and undo backup
				newEntry.LastModificationTime = initialEntry.LastModificationTime;
				bUndoBackup = bCreateBackup;
			}
			else if(bCreateBackup)
			{
				// If only history items have been modified (deleted) => undo
				// backup, but without restoring the last mod time
				PwCompareOptions cmpOptNh = (cmpOpt | PwCompareOptions.IgnoreHistory);
				if(newEntry.EqualsEntry(initialEntry, cmpOptNh, MemProtCmpMode.CustomOnly))
					bUndoBackup = true;
			}
			if(bUndoBackup) newEntry.History.RemoveAt(newEntry.History.UCount - 1);
			
			newEntry.MaintainBackups(db.KpDatabase);

			//if ( newEntry.Strings.ReadSafe (PwDefs.TitleField).Equals(State.Entry.Strings.ReadSafe (PwDefs.TitleField)) ) {
			//	SetResult(KeePass.EXIT_REFRESH);
			//} else {
			//it's safer to always update the title as we might add further information in the title like expiry etc.
			SetResult(KeePass.ExitRefreshTitle);
			//}
			
			OperationWithFinishHandler runnable;

			ActionOnOperationFinished closeOrShowError = new ActionInContextInstanceOnOperationFinished(ContextInstanceId, App.Kp2a, (success, message, context) => {
				if (success)
				{
                    (context as Activity)?.Finish();
				} else
				{
				    OnOperationFinishedHandler.DisplayMessage(context, message, true);
                    //Re-initialize for editing:
                    State.EditMode.InitializeEntry(State.Entry);
				}
			});
            //make sure we can close the EntryEditActivity activity even if the app went to background till we get to the OnOperationFinishedHandler Action


            ActionOnOperationFinished afterAddEntry = new ActionInContextInstanceOnOperationFinished(ContextInstanceId, App.Kp2a, (success, message, context) => 
			{
				if (success && context is EntryEditActivity entryEditActivity)
					AppTask.AfterAddNewEntry(entryEditActivity, newEntry);
			},closeOrShowError);

			if ( State.IsNew ) {
				runnable = AddEntry.GetInstance(App.Kp2a, newEntry, State.ParentGroup, afterAddEntry, db);
			} else {
				runnable = new UpdateEntry(App.Kp2a, initialEntry, newEntry, closeOrShowError);
			}
            BlockingOperationStarter pt = new BlockingOperationStarter(App.Kp2a, runnable);
			pt.Run();
			

		}

		void UpdateEntryFromUi(PwEntry entry)
		{
			Database db = App.Kp2a.CurrentDb;
			EntryEditActivity act = this;

			entry.Strings.Set(PwDefs.TitleField, new ProtectedString(db.KpDatabase.MemoryProtection.ProtectTitle,
			                                                            Util.GetEditText(act, Resource.Id.entry_title)));
			entry.Strings.Set(PwDefs.UserNameField, new ProtectedString(db.KpDatabase.MemoryProtection.ProtectUserName,
			                                                               Util.GetEditText(act, Resource.Id.entry_user_name)));
			
			String pass = Util.GetEditText(act, Resource.Id.entry_password);
			byte[] password = StrUtil.Utf8.GetBytes(pass);
			entry.Strings.Set(PwDefs.PasswordField, new ProtectedString(db.KpDatabase.MemoryProtection.ProtectPassword,
			                                                               password));
			MemUtil.ZeroByteArray(password);
			
			entry.Strings.Set(PwDefs.UrlField, new ProtectedString(db.KpDatabase.MemoryProtection.ProtectUrl,
			                                                          Util.GetEditText(act, Resource.Id.entry_url)));
			entry.Strings.Set(PwDefs.NotesField, new ProtectedString(db.KpDatabase.MemoryProtection.ProtectNotes,
			                                                            Util.GetEditText(act, Resource.Id.entry_comment)));
		
			// Validate expiry date
			DateTime newExpiry = new DateTime();
			if ((State.Entry.Expires) && (!DateTime.TryParse( Util.GetEditText(this,Resource.Id.entry_expires), out newExpiry)))
			{
				//ignore here
			}
			else
			{
				State.Entry.ExpiryTime = newExpiry.ToUniversalTime();
			}

			// Delete all non standard strings
			var keys = entry.Strings.GetKeys();
			foreach (String key in keys)
				if (PwDefs.IsStandardField(key) == false)
					entry.Strings.Remove(key);
			
			LinearLayout container = (LinearLayout) FindViewById(Resource.Id.advanced_container);
			
			for (int index = 0; index < container.ChildCount; index++) {
				ExtraEditView view = container.GetChildAt(index) as ExtraEditView;
				TextView keyView = view.KeyTextView;
				String key = keyView.Text;

				if (String.IsNullOrEmpty(key))
					continue;

				TextView valueView = view.ValueTextView;
                

                String value = valueView.Text;

				bool protect = view.ProtectionCheckbox?.Checked ?? State.EntryInDatabase.Strings.GetSafe(key).IsProtected;
				entry.Strings.Set(key, new ProtectedString(protect, value));
			}
			

			entry.OverrideUrl = Util.GetEditText(this,Resource.Id.entry_override_url);
			
			List<string> vNewTags = StrUtil.StringToTags(Util.GetEditText(this,Resource.Id.entry_tags));
			entry.Tags.Clear();
			foreach(string strTag in vNewTags) entry.AddTag(strTag);
			
			/*KPDesktop


				m_atConfig.Enabled = m_cbAutoTypeEnabled.Checked;
				m_atConfig.ObfuscationOptions = (m_cbAutoTypeObfuscation.Checked ?
				                                 AutoTypeObfuscationOptions.UseClipboard :
				                                 AutoTypeObfuscationOptions.None);

				SaveDefaultSeq();
				
				newEntry.AutoType = m_atConfig;
				*/

		}

		public String GetFileName(Uri uri)
		{
			String result = null;
			if (uri.Scheme.Equals("content"))
			{
				ICursor cursor = null;

				try
				{
					cursor = ContentResolver.Query(uri, null, null, null, null);

					if (cursor != null && cursor.MoveToFirst())
					{
						result = cursor.GetString(cursor.GetColumnIndex(OpenableColumns.DisplayName));
					}
				}
				catch (Exception e)
				{
					Kp2aLog.Log(e.ToString());
				}
				finally
				{
					if (cursor != null) 
						cursor.Close();
				}
			}
			if (result == null)
			{
				result = uri.Path;
				int cut = result.LastIndexOf('/');
				if (cut != -1)
				{
					result = result.Substring(cut + 1);
				}

				cut = result.LastIndexOf('?');
				if (cut != -1)
				{
					result = result.Substring(0, cut);
				}
				
			}
			return result;
		}

		
		void AddBinary(Uri filename, bool overwrite)
		{
			string strItem = GetFileName(filename);
			if (!overwrite)
			{
				string strFileName = UrlUtil.StripExtension(strItem);
				string strExtension = "." + UrlUtil.GetExtension(strItem);
				
				int nTry = 0;
				while(true)
				{
					string strNewName = strFileName + nTry.ToString(CultureInfo.InvariantCulture) + strExtension;
					if(State.Entry.Binaries.Get(strNewName) == null)
					{
						strItem = strNewName;
						break;
					}
					
					++nTry;
				}
			}
			try
			{

				byte[] vBytes = null;
				try
				{
					//Android standard way to read the contents (content or file scheme)
					vBytes = ReadFully(ContentResolver.OpenInputStream(filename));
				}
				catch (Exception ex)
				{
					Kp2aLog.Log(ex.ToString());
					//if standard way fails, try to read as a file
					vBytes = File.ReadAllBytes(filename.Path);
				}
				
				ProtectedBinary pb = new ProtectedBinary(false, vBytes);
				State.Entry.Binaries.Set(strItem, pb);
			}
			catch(Exception exAttach)
			{
				App.Kp2a.ShowMessage(this, GetString(Resource.String.AttachFailed)+" "+ Util.GetErrorMessage(exAttach),  MessageSeverity.Error);
			}
			State.EntryModified = true;
			PopulateBinaries();
		}
		public static byte[] ReadFully(Stream input)
		{
			byte[] buffer = new byte[16 * 1024];
			using (MemoryStream ms = new MemoryStream())
			{
				int read;
				while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
				{
					ms.Write(buffer, 0, read);
				}
				return ms.ToArray();
			}
		}

		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			AppTask.ToBundle(outState);
		}

		public override void OnBackPressed()
		{
			if (State.EntryModified == false)
			{
				base.OnBackPressed();
			} else
			{
				MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(this);
				builder.SetTitle(GetString(Resource.String.AskDiscardChanges_title));
				
				builder.SetMessage(GetString(Resource.String.AskDiscardChanges));
				
				builder.SetPositiveButton(GetString(Android.Resource.String.Yes), (dlgSender, dlgEvt) => 
				                                                                                                                  {
					Finish();
					
					});
				
				builder.SetNegativeButton(GetString(Android.Resource.String.No), (dlgSender, dlgEvt) => 
				                                                                                                                 {
					
					});
				
				
				Dialog dialog = builder.Create();
				dialog.Show();
			}

		}
		
		public void Reload() {
			//this reload ìs necessary to overcome a strange problem with the extra string fields which get lost
			//somehow after re-creating the activity. Maybe a Mono for Android bug?
			Intent intent = Intent;
			intent.PutExtra(IntentContinueWithEditing, true);
			//OverridePendingTransition(0, 0);
			intent.AddFlags(ActivityFlags.NoAnimation | ActivityFlags.ForwardResult);
			_closeForReload = true;
			SetResult(KeePass.ExitRefreshTitle); //probably the entry will be modified -> let the EditActivity refresh to be safe
			Finish();
			
			//OverridePendingTransition(0, 0);
			StartActivity(intent);
		}
		
		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
		    base.OnActivityResult(requestCode, resultCode, data);

		    FileSelectHelper fileSelectHelper = new FileSelectHelper(this, false, true, requestCodeSelectFileExtra);
		    fileSelectHelper.OnOpen += (sender, info) =>
		    {
		        State.EntryModified = true;
		        var ees = FindExtraEditSection(State.LastTriggeredFileSelectionProcessKey);
                (ees as ExtraEditViewFile)?.UpdateFileView(info.Path);
		    };

		    if (fileSelectHelper.HandleActivityResult(this, requestCode, resultCode, data))
		        return;

            switch (resultCode)
			{
			case (Result)ResultOkIconPicker:
				State.SelectedIconId = (PwIcon) data.Extras.GetInt(IconPickerActivity.KeyIconId,(int)PwIcon.Key);
				State.SelectedCustomIconId = PwUuid.Zero;
				String customIconIdString = data.Extras.GetString(IconPickerActivity.KeyCustomIconId);
				if (!String.IsNullOrEmpty(customIconIdString))
					State.SelectedCustomIconId = new PwUuid(MemUtil.HexStringToByteArray(customIconIdString));
				State.SelectedIcon = true;
				State.EntryModified = true;
                UpdateIconButton();

                return;
				
			case KeePass.ResultOkPasswordGenerator:
				string generatedPassword = data.GetStringExtra(GeneratePasswordActivity.GeneratedPasswordKey);
				
				byte[] password = StrUtil.Utf8.GetBytes(generatedPassword);
                var protectedPassword = new ProtectedString(
                    App.Kp2a.CurrentDb.KpDatabase.MemoryProtection.ProtectPassword,
                    password);

                State.Entry.Strings.Set(PwDefs.PasswordField, protectedPassword);
                PopulateText(Resource.Id.entry_password, generatedPassword);
                MemUtil.ZeroByteArray(password);

				State.EntryModified = true;
                
                return;
			case Result.Ok:
				if (requestCode == Intents.RequestCodeFileBrowseForBinary)
				{
					Uri uri = data.Data;
					if (data.Data == null)
					{
						string s = Util.GetFilenameFromInternalFileChooser(data, this);
						if (s == null)
						{
							App.Kp2a.ShowMessage(this, "No URI retrieved.",  MessageSeverity.Error);
							return;
						}
						uri = Uri.Parse(s);
					}
				    _uriToAddOrAsk = uri; //we can't launch a dialog in onActivityResult, so delay this to onResume		
				}
				return;
			case Result.Canceled:
                return;
			}
			
		}

        ExtraEditView? FindExtraEditSection(string key)
		{
			
			LinearLayout container = (LinearLayout) FindViewById(Resource.Id.advanced_container);
			for (int i = 0; i < container.ChildCount; i++) 
			{
				var ees = container.GetChildAt(i) as ExtraEditView;	
				if (ees?.KeyTextView.Text == key)
				{
					return ees!;
				}
			}

			return null;
			
		}

		void PopulateBinaries()
		{
			ViewGroup binariesGroup = (ViewGroup)FindViewById(Resource.Id.binaries);
			binariesGroup.RemoveAllViews();
			RelativeLayout.LayoutParams layoutParams = new RelativeLayout.LayoutParams(ViewGroup.LayoutParams.FillParent, ViewGroup.LayoutParams.WrapContent);
			foreach (KeyValuePair<string, ProtectedBinary> pair in State.Entry.Binaries.OrderBy(p => p.Key) )
			{
				String key = pair.Key;
				String label = key;
				if ((String.IsNullOrEmpty(label) || (!App.Kp2a.CurrentDb.DatabaseFormat.SupportsAttachmentKeys)))
				{
					label = "<attachment>";
				}
				//Button binaryButton = new Button(this, null, Resource.Style.EditEntryButton) {Text = label};
			    Button binaryButton = (Button)LayoutInflater.Inflate(Resource.Layout.EntryEditButtonDelete, null);
			    binaryButton.Text = label;

				//binaryButton.SetCompoundDrawablesWithIntrinsicBounds( Resources.GetDrawable(Android.Resource.Drawable.IcMenuDelete),null, null, null);
				binaryButton.Click += (sender, e) => 
				{
					State.EntryModified = true;
					State.Entry.Binaries.Remove(key);
					PopulateBinaries();

				};
				binariesGroup.AddView(binaryButton,layoutParams);
				
				
			}
            
			//Button addBinaryButton = new Button(this, null, Resource.Style.EditEntryButton ) {Text = GetString(Resource.String.add_binary)};
			//addBinaryButton.SetCompoundDrawablesWithIntrinsicBounds( Resources.GetDrawable(Android.Resource.Drawable.IcMenuAdd) , null, null, null);
            Button addBinaryButton = (Button)LayoutInflater.Inflate(Resource.Layout.EntryEditButtonAdd, null);
            addBinaryButton.Text = GetString(Resource.String.add_binary);

			addBinaryButton.Enabled = true;
            
			if (!App.Kp2a.CurrentDb.DatabaseFormat.CanHaveMultipleAttachments)
				addBinaryButton.Enabled = !State.Entry.Binaries.Any();
			addBinaryButton.Click += (sender, e) => 
			{
				Util.ShowBrowseDialog(this, Intents.RequestCodeFileBrowseForBinary, false, true /*force OpenDocument if available, GetContent is not well support starting with Android 7 */);

			};
			
			binariesGroup.AddView(addBinaryButton,layoutParams);

			var binariesLabel = FindViewById(Resource.Id.entry_binaries_label);
			if (binariesLabel != null)
				binariesLabel.Visibility = State.Entry.Binaries.UCount > 0 ? ViewStates.Visible : ViewStates.Gone;
			
			binariesGroup.Visibility = State.EditMode.ShowAddAttachments  ? ViewStates.Visible : ViewStates.Gone;
		}

		public override bool OnPrepareOptionsMenu(IMenu menu)
		{
			Util.PrepareDonateOptionMenu(menu, this);
			menu.FindItem(Resource.Id.menu_show_all).SetVisible(_editModeHiddenViews.Any());
			return base.OnPrepareOptionsMenu(menu);
		}

		public override bool OnCreateOptionsMenu(IMenu menu) {
			base.OnCreateOptionsMenu(menu);
			
			MenuInflater inflater = MenuInflater;
			inflater.Inflate(Resource.Menu.entry_edit, menu);
			
			
			return true;
		}
		
		public override bool OnOptionsItemSelected(IMenuItem item) {
			switch ( item.ItemId ) {
			case Resource.Id.menu_save:
			        SaveEntry();
			        return true;
                case Resource.Id.menu_cancel:
			        Finish();
			        return true;
				case Resource.Id.menu_show_all:
					item.SetVisible(false);
					foreach (View v in _editModeHiddenViews)
						v.Visibility = ViewStates.Visible;
                    State.EditMode.ShowAddAttachments = true;
                    State.EditMode.ShowAddExtras = true;
                    ViewGroup binariesGroup = (ViewGroup)FindViewById(Resource.Id.binaries);
                    binariesGroup.Visibility = ViewStates.Visible;
                    FindViewById(Resource.Id.entry_binaries_container).Visibility = ViewStates.Visible;
                    ((Button)FindViewById(Resource.Id.add_advanced)).Visibility = ViewStates.Visible;
                    ((Button)FindViewById(Resource.Id.configure_totp)).Visibility = ViewStates.Visible;
					FindViewById(Resource.Id.entry_extras_container).Visibility = ViewStates.Visible;

                    return true;
                case Android.Resource.Id.Home:
                    OnBackPressed();
			        return true;
                default:
                    return base.OnOptionsItemSelected(item);
			}
		}
		

		void UpdateExpires()
		{
			if (State.Entry.Expires)
			{
				PopulateText(Resource.Id.entry_expires, getDateTime(State.Entry.ExpiryTime));
			}
			else
			{
				PopulateText(Resource.Id.entry_expires, GetString(Resource.String.never));
			}
			((CheckBox)FindViewById(Resource.Id.entry_expires_checkbox)).Checked = State.Entry.Expires;
			FindViewById(Resource.Id.entry_expires).Enabled = State.Entry.Expires;
		}

        ExtraEditView CreateExtraStringEditView(KeyValuePair<string, ProtectedString> pair, string title = null, string type = "")
		{
			if (title == null)
				title = pair.Key;
			if (type == "bool")
			{
				var ees = new ExtraEditViewBool(this, pair, title);
                ees.ExtraModified += (sender, args) =>
                {
                    State.EntryModified = true;
                };
                return ees;
			}
			else if (type == "file")
			{
				var ees = new ExtraEditViewFile(this, pair, title);
				ees.ChangeLocationButton.Click += (sender, e) =>
                {
                    State.LastTriggeredFileSelectionProcessKey = pair.Key;
                    Intent intent = new Intent(this, typeof(FileStorageSelectionActivity));
                    StartActivityForResult(intent, requestCodeSelectFileExtra);
                };

                return ees;
			}
			else
            {
                var ees = new ExtraEditViewString(this, pair, _passwordFont);
                

                return ees;
			}
		}

	    private string[] _additionalKeys = null;
	    private List<View> _editModeHiddenViews;
	    private Uri _uriToAddOrAsk;
      
		public string[] AdditionalKeys
	    {
		    get
		    {
			    if (_additionalKeys == null)
			    {
				    _additionalKeys = App.Kp2a.CurrentDb.EntriesById
						.Select(kvp => kvp.Value)
						.SelectMany(x => x.Strings.GetKeys().Where(k => !PwDefs.IsStandardField(k)))
						.Where(k => (k != null) && !k.StartsWith("_etm_") )
						.Distinct()
						.ToArray();
			    }
			    return _additionalKeys;
		    }
		    
	    }
		


        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

		
		private void FillData()
		{
			_editModeHiddenViews = new List<View>();
			ImageButton currIconButton = (ImageButton) FindViewById(Resource.Id.icon_button);
			App.Kp2a.CurrentDb.DrawableFactory.AssignDrawableTo(currIconButton, this, App.Kp2a.CurrentDb.KpDatabase, State.Entry.IconId, State.Entry.CustomIconUuid, false);
			
			PopulateText(Resource.Id.entry_title, State.Entry.Strings.ReadSafe (PwDefs.TitleField));
			PopulateText(Resource.Id.entry_user_name, State.Entry.Strings.ReadSafe (PwDefs.UserNameField));
			PopulateText(Resource.Id.entry_url, State.Entry.Strings.ReadSafe (PwDefs.UrlField));
			
			String password = State.Entry.Strings.ReadSafe(PwDefs.PasswordField);
			PopulateText(Resource.Id.entry_password, password);


            _passwordFont.ApplyTo(FindViewById<EditText>(Resource.Id.entry_password));


			
			PopulateText(Resource.Id.entry_comment, State.Entry.Strings.ReadSafe (PwDefs.NotesField));

			LinearLayout container = (LinearLayout) FindViewById(Resource.Id.advanced_container);
            container.RemoveAllViews();
            foreach (var key in State.EditMode.SortExtraFieldKeys(State.Entry.Strings.Select(ps => ps.Key)))
			{
				if (!PwDefs.IsStandardField(key)) 
				{
					RelativeLayout ees = CreateExtraStringEditView(new KeyValuePair<string, ProtectedString>(key, State.Entry.Strings.Get(key)), State.EditMode.GetTitle(key), State.EditMode.GetFieldType(key));
					var isVisible = State.EditMode.IsVisible(key);
					ees.Visibility =  isVisible ? ViewStates.Visible : ViewStates.Gone;
					if (!isVisible)
						_editModeHiddenViews.Add(ees);
					container.AddView(ees);
				}
			}

			PopulateBinaries();

			if (App.Kp2a.CurrentDb.DatabaseFormat.SupportsOverrideUrl)
			{
				PopulateText(Resource.Id.entry_override_url, State.Entry.OverrideUrl);
			}
			else
			{
				FindViewById(Resource.Id.entry_override_url_container).Visibility = ViewStates.Gone;
			}
			
			if (App.Kp2a.CurrentDb.DatabaseFormat.SupportsTags)
			{
				PopulateText(Resource.Id.entry_tags, StrUtil.TagsToString(State.Entry.Tags, true));	
			}
			else
			{
				var view = FindViewById(Resource.Id.entry_tags_label);
				if (view != null)
					view.Visibility = ViewStates.Gone;
				FindViewById(Resource.Id.entry_tags).Visibility = ViewStates.Gone;
			}
			

			UpdateExpires();

			List<KeyValuePair<string, int>> keyLayoutIds = new List<KeyValuePair<string, int>>()
			{
				new KeyValuePair<string, int>(PwDefs.TitleField, Resource.Id.title_section),
				new KeyValuePair<string, int>(PwDefs.UserNameField, Resource.Id.user_section),
				new KeyValuePair<string, int>(PwDefs.PasswordField, Resource.Id.password_section),
				new KeyValuePair<string, int>(PwDefs.UrlField, Resource.Id.url_section),
				new KeyValuePair<string, int>(PwDefs.NotesField, Resource.Id.comments_section),
				new KeyValuePair<string, int>(KeePass.TagsKey, Resource.Id.tags_section),
				new KeyValuePair<string, int>(KeePass.OverrideUrlKey, Resource.Id.entry_override_url_container),
				new KeyValuePair<string, int>(KeePass.ExpDateKey, Resource.Id.expires_section),
			};
			foreach (var kvp in keyLayoutIds)
			{
				var isVisible = State.EditMode.IsVisible(kvp.Key);
				var field = FindViewById(kvp.Value);
				if (!isVisible)
					_editModeHiddenViews.Add(field);
				
				field.Visibility = isVisible ? ViewStates.Visible : ViewStates.Gone;
			}

			
		}
		private String getDateTime(DateTime dt) {
			return dt.ToLocalTime().ToString ("g", CultureInfo.CurrentUICulture);
		}


		

		protected bool ValidateBeforeSaving() {
			// Require title
			String title = Util.GetEditText(this, Resource.Id.entry_title);
			if ( title.Length == 0 ) {
				App.Kp2a.ShowMessage(this, Resource.String.error_title_required,  MessageSeverity.Error);
				return false;
			}
			
			

			// Validate expiry date
			DateTime newExpiry = new DateTime();
			if ((State.Entry.Expires) && (!DateTime.TryParse( Util.GetEditText(this,Resource.Id.entry_expires), out newExpiry)))
		    {
				App.Kp2a.ShowMessage(this, Resource.String.error_invalid_expiry_date,  MessageSeverity.Error);
				return false;
			}
			State.Entry.ExpiryTime = newExpiry.ToUniversalTime();


			LinearLayout container = (LinearLayout) FindViewById(Resource.Id.advanced_container);
			HashSet<string> allKeys = new HashSet<string>();
			for (int i = 0; i < container.ChildCount; i++) {
				ExtraEditView? ees = container.GetChildAt(i) as ExtraEditView;
				TextView keyView =  ees?.KeyTextView;
				string key = keyView?.Text;
				
				if (String.IsNullOrEmpty(key)) {
					App.Kp2a.ShowMessage(this, Resource.String.error_string_key,  MessageSeverity.Error);
					return false;
				}

				if (allKeys.Contains(key))
				{
					App.Kp2a.ShowMessage(this, GetString(Resource.String.error_string_duplicate_key, new Object[]{key}),  MessageSeverity.Error);
					return false;
				}

				allKeys.Add(key);

			}
			
			return true;
		}

	
		private void PopulateText(int viewId, String text) {
			TextView tv = (TextView) FindViewById(viewId);
		    if (tv == null)
		    {
		        var e = new Exception("Invalid viewId " + viewId);
				Kp2aLog.LogUnexpectedError(e);
		        return;
		    }
			tv.Text = text;
			tv.TextChanged += (sender, e) => {State.EntryModified = true;};
		}

		protected override void OnPause()
		{
			if (!_closeForReload)
				UpdateEntryFromUi(State.Entry);

			base.OnPause();

		}
	}
#if !NO_QR_SCANNER
    public class SuccessListener : Object, IOnSuccessListener
    {
        private readonly Action<Barcode> _onSuccess;

        public SuccessListener(Action<Barcode> onSuccess)
        {
            _onSuccess = onSuccess;
        }

        public void OnSuccess(Object result)
        {
            _onSuccess?.Invoke((Barcode)result);
        }
    }

    public class FailureListener : Java.Lang.Object, IOnFailureListener
    {
        private readonly Action<Exception> _onFailure;

        public FailureListener(Action<Exception> onFailure)
        {
            _onFailure = onFailure;
        }

        public void OnFailure(Java.Lang.Exception e)
        {
            _onFailure?.Invoke(e);
        }
    }
#endif

	public class DefaultEdit : EditModeBase
	{
		
	}
}

