using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using keepass2android.database.edit;
using KeePassLib;
using KeePassLib.Serialization;

namespace keepass2android
{
    [Activity(Label = "@string/keeshare_edit_title", MainLauncher = false, Theme = "@style/Kp2aTheme_BlueNoActionBar", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
    public class EditKeeShareActivity : LockCloseActivity
    {
        private const int ReqCodeSelectFile = 1;
        private const string GroupUuidKey = "GroupUuid";
        private const string MakeCurrentKey = "MakeCurrent";
        private const string IocExtraKey = "ioc";
        
        private PwGroup _group;
        private CheckBox _enableCheckbox;
        private Spinner _typeSpinner;
        private EditText _filePathEditText;
        private EditText _passwordEditText;
        private Button _selectFileButton;
        private Button _okButton;
        private Button _cancelButton;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.edit_keeshare);

            string groupUuidString = Intent.GetStringExtra(GroupUuidKey);
            if (string.IsNullOrEmpty(groupUuidString))
            {
                Finish();
                return;
            }

            PwUuid groupUuid;
            try
            {
                groupUuid = new PwUuid(Convert.FromBase64String(groupUuidString));
            }
            catch (FormatException)
            {
                App.Kp2a.ShowMessage(this, "Invalid group identifier", MessageSeverity.Error);
                Finish();
                return;
            }
            
            _group = App.Kp2a.CurrentDb?.KpDatabase?.RootGroup?.FindGroup(groupUuid, true);
            
            if (_group == null)
            {
                App.Kp2a.ShowMessage(this, GetString(Resource.String.error_group_not_found), MessageSeverity.Error);
                Finish();
                return;
            }

            InitializeViews();
            LoadCurrentSettings();
        }

        private void InitializeViews()
        {
            _enableCheckbox = FindViewById<CheckBox>(Resource.Id.keeshare_enable_checkbox);
            _typeSpinner = FindViewById<Spinner>(Resource.Id.keeshare_type_spinner);
            _filePathEditText = FindViewById<EditText>(Resource.Id.keeshare_filepath);
            _passwordEditText = FindViewById<EditText>(Resource.Id.keeshare_password);
            _selectFileButton = FindViewById<Button>(Resource.Id.keeshare_select_file);
            _okButton = FindViewById<Button>(Resource.Id.ok);
            _cancelButton = FindViewById<Button>(Resource.Id.cancel);

            var typeAdapter = ArrayAdapter.CreateFromResource(this, Resource.Array.keeshare_types, Android.Resource.Layout.SimpleSpinnerItem);
            typeAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _typeSpinner.Adapter = typeAdapter;

            _enableCheckbox.CheckedChange += (sender, e) => UpdateFieldsEnabled();
            _selectFileButton.Click += OnSelectFile;
            _okButton.Click += OnOk;
            _cancelButton.Click += (sender, e) => Finish();
        }

        private void LoadCurrentSettings()
        {
            bool isActive = _group.CustomData.Get("KeeShare.Active") == "true";
            _enableCheckbox.Checked = isActive;

            string type = _group.CustomData.Get("KeeShare.Type") ?? "Export";
            int typeIndex = type == "Import" ? 1 : type == "Synchronize" ? 2 : 0;
            _typeSpinner.SetSelection(typeIndex);

            string filePath = _group.CustomData.Get("KeeShare.FilePath") ?? "";
            _filePathEditText.Text = filePath;

            string password = _group.CustomData.Get("KeeShare.Password") ?? "";
            _passwordEditText.Text = password;

            UpdateFieldsEnabled();
        }

        private void UpdateFieldsEnabled()
        {
            bool enabled = _enableCheckbox.Checked;
            _typeSpinner.Enabled = enabled;
            _filePathEditText.Enabled = enabled;
            _passwordEditText.Enabled = enabled;
            _selectFileButton.Enabled = enabled;
        }

        private void OnSelectFile(object sender, EventArgs e)
        {
            Intent intent = new Intent(this, typeof(FileSelectActivity));
            intent.PutExtra(FileSelectActivity.NoForwardToPasswordActivity, true);
            intent.PutExtra(MakeCurrentKey, false);
            StartActivityForResult(intent, ReqCodeSelectFile);
        }

        private void OnOk(object sender, EventArgs e)
        {
            bool isEnabled = _enableCheckbox.Checked;

            if (isEnabled)
            {
                string filePath = _filePathEditText.Text?.Trim();
                if (string.IsNullOrEmpty(filePath))
                {
                    App.Kp2a.ShowMessage(this, GetString(Resource.String.keeshare_filepath_required), MessageSeverity.Error);
                    return;
                }

                string[] types = Resources.GetStringArray(Resource.Array.keeshare_types);
                string type = types[_typeSpinner.SelectedItemPosition];
                string password = _passwordEditText.Text?.Trim();

                KeeShare.EnableKeeShare(_group, type, filePath, password);
            }
            else
            {
                KeeShare.DisableKeeShare(_group);
            }

            SaveDatabase();
        }

        private void SaveDatabase()
        {
            var saveTask = new SaveDb(App.Kp2a, App.Kp2a.FindDatabaseForElement(_group), 
                new ActionOnOperationFinished(App.Kp2a, 
                    (success, message, importantMessage, exception, context) =>
                    {
                        if (success)
                        {
                            Finish();
                        }
                        else
                        {
                            App.Kp2a.ShowMessage(this, message ?? GetString(Resource.String.error_save_failed), MessageSeverity.Error);
                        }
                    }));

            BlockingOperationStarter pt = new BlockingOperationStarter(App.Kp2a, saveTask);
            pt.Run();
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == ReqCodeSelectFile && resultCode == Result.Ok)
            {
                string iocString = data?.GetStringExtra(IocExtraKey);
                if (!string.IsNullOrEmpty(iocString))
                {
                    IOConnectionInfo ioc = IOConnectionInfo.UnserializeFromString(iocString);
                    _filePathEditText.Text = ioc.Path;
                }
            }
        }

        public static Intent CreateIntent(Context context, PwGroup group)
        {
            Intent intent = new Intent(context, typeof(EditKeeShareActivity));
            intent.PutExtra(GroupUuidKey, Convert.ToBase64String(group.Uuid.UuidBytes));
            return intent;
        }
    }
}
