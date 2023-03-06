using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
#if !NoNet
using FluentFTP;
#endif
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using Java.IO;
using keepass2android.Io;
#if !EXCLUDE_JAVAFILESTORAGE
using Keepass2android.Javafilestorage;
#endif
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace keepass2android
{
	public class FileSelectHelper
	{
		public static string ConvertFilenameToIocPath(string filename)
		{
			if ((filename != null) && (filename.StartsWith("file://")))
			{
				filename = filename.Substring(7);
				filename = Java.Net.URLDecoder.Decode(filename);
			}
			return filename;
		}
		
		private readonly Activity _activity;
		private readonly bool _isForSave;
		private readonly int _requestCode;
	    private readonly string _schemeSeparator = "://";
	    private bool _tryGetPermanentAccess;

		private const int SftpModeSpinnerPasswd = 0;
		private const int SftpModeSpinnerPubKey = 1;
		private const int SftpModeSpinnerCustomKey = 2;

		private const int SftpKeySpinnerCreateNewIdx = 0;

	    public string DefaultExtension { get; set; }

		public FileSelectHelper(Activity activity, bool isForSave, bool tryGetPermanentAccess, int requestCode)
		{
			_activity = activity;
			_isForSave = isForSave;
			_requestCode = requestCode;
			_tryGetPermanentAccess = tryGetPermanentAccess;
		}

		private void ShowSftpDialog(Activity activity, Util.FileSelectedHandler onStartBrowse, Action onCancel, string defaultPath)
		{
#if !EXCLUDE_JAVAFILESTORAGE && !NoNet
			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			View dlgContents = activity.LayoutInflater.Inflate(Resource.Layout.sftpcredentials, null);
			var ctx = activity.ApplicationContext;
            var fileStorage = new Keepass2android.Javafilestorage.SftpStorage(ctx);

            LinearLayout addNewBtn = dlgContents.FindViewById<LinearLayout>(Resource.Id.sftp_add_key_group);
            Button deleteBtn = dlgContents.FindViewById<Button>(Resource.Id.sftp_delete_key_button);
            EditText keyNameTxt = dlgContents.FindViewById<EditText>(Resource.Id.sftp_key_name);
            EditText keyContentTxt = dlgContents.FindViewById<EditText>(Resource.Id.sftp_key_content);

            var keySpinner = dlgContents.FindViewById<Spinner>(Resource.Id.sftp_key_names);
			var keyNamesAdapter = new ArrayAdapter(ctx, Android.Resource.Layout.SimpleSpinnerDropDownItem, new List<string>());
			UpdatePrivateKeyNames(keyNamesAdapter, fileStorage, ctx);
			keyNamesAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
			keySpinner.Adapter = keyNamesAdapter;
			keySpinner.SetSelection(SftpKeySpinnerCreateNewIdx);

			keySpinner.ItemSelected += (sender, args) =>
			{
                if (keySpinner.SelectedItemPosition == SftpKeySpinnerCreateNewIdx)
				{
					keyNameTxt.Text = "";
					keyContentTxt.Text = "";
					addNewBtn.Visibility = ViewStates.Visible;
					deleteBtn.Visibility = ViewStates.Gone;
				}
				else
				{
					addNewBtn.Visibility = ViewStates.Gone;
					deleteBtn.Visibility = ViewStates.Visible;
				}
			};

            var authModeSpinner = dlgContents.FindViewById<Spinner>(Resource.Id.sftp_auth_mode_spinner);
			dlgContents.FindViewById<Button>(Resource.Id.send_public_key_button).Click += (sender, args) =>
			{
                string pub_filename = fileStorage.CreateKeyPair();

				Intent sendIntent = new Intent();
				sendIntent.SetAction(Intent.ActionSend);
				sendIntent.PutExtra(Intent.ExtraText, System.IO.File.ReadAllText(pub_filename));

				sendIntent.PutExtra(Intent.ExtraSubject, "Keepass2Android sftp public key");
				sendIntent.SetType("text/plain");
				activity.StartActivity(Intent.CreateChooser(sendIntent, "Send public key to..."));
			};

			dlgContents.FindViewById<Button>(Resource.Id.sftp_save_key_button).Click += (sender, args) =>
			{
				string keyName = keyNameTxt.Text;
				string keyContent = keyContentTxt.Text;

				string toastMsg = null;
                if (!string.IsNullOrEmpty(keyName) && !string.IsNullOrEmpty(keyContent))
				{
					try
					{
						fileStorage.SavePrivateKeyContent(keyName, keyContent);
                        keyNameTxt.Text = "";
						keyContentTxt.Text = "";
                        toastMsg = ctx.GetString(Resource.String.private_key_saved);
                    }
					catch (Exception e)
					{
						toastMsg = ctx.GetString(Resource.String.private_key_save_failed,
							new Java.Lang.Object[] { e.Message });
					}
				}
				else
				{
					toastMsg = ctx.GetString(Resource.String.private_key_info);
				}

				if (toastMsg!= null) {
                    Toast.MakeText(_activity, toastMsg, ToastLength.Long).Show();
                }

				UpdatePrivateKeyNames(keyNamesAdapter, fileStorage, ctx);
				keySpinner.SetSelection(ResolveKeySpinnerSelection(keyNamesAdapter, keyName));
			};

			dlgContents.FindViewById<Button>(Resource.Id.sftp_delete_key_button).Click += (sender, args) =>
			{
				int selectedKey = dlgContents.FindViewById<Spinner>(Resource.Id.sftp_key_names).SelectedItemPosition;
				string keyName = ResolveSelectedKeyName(keyNamesAdapter, selectedKey);
				if (!string.IsNullOrEmpty(keyName))
				{
					bool deleted = fileStorage.DeleteCustomKey(keyName);

					int msgId = deleted ? Resource.String.private_key_delete : Resource.String.private_key_delete_failed;
					string msg = ctx.GetString(msgId, new Java.Lang.Object[] { keyName });
                    Toast.MakeText(_activity, msg, ToastLength.Long).Show();

					UpdatePrivateKeyNames(keyNamesAdapter, fileStorage, ctx);
					keySpinner.SetSelection(SftpKeySpinnerCreateNewIdx);
				}
			};

			authModeSpinner.ItemSelected += (sender, args) =>
		    {
				var passwordBox = dlgContents.FindViewById<EditText>(Resource.Id.sftp_password);
				var publicKeyButton = dlgContents.FindViewById<Button>(Resource.Id.send_public_key_button);
				var keyfileGroup = dlgContents.FindViewById<LinearLayout>(Resource.Id.sftp_keyfile_group);

                switch (authModeSpinner.SelectedItemPosition)
			    {
				    case SftpModeSpinnerPasswd:
					    passwordBox.Visibility = ViewStates.Visible;
					    publicKeyButton.Visibility = ViewStates.Gone;
					    keyfileGroup.Visibility = ViewStates.Gone;
					    break;
				    case SftpModeSpinnerPubKey:
					    passwordBox.Visibility = ViewStates.Gone;
					    publicKeyButton.Visibility = ViewStates.Visible;
					    keyfileGroup.Visibility = ViewStates.Gone;
					    break;
				    case SftpModeSpinnerCustomKey:
					    passwordBox.Visibility = ViewStates.Gone;
					    publicKeyButton.Visibility = ViewStates.Gone;
					    keyfileGroup.Visibility = ViewStates.Visible;
					    break;
			    }
		    };

            if (!defaultPath.EndsWith(_schemeSeparator))
		    {
                SftpStorage.ConnectionInfo ci = fileStorage.SplitStringToConnectionInfo(defaultPath);
		        dlgContents.FindViewById<EditText>(Resource.Id.sftp_host).Text = ci.Host;
		        dlgContents.FindViewById<EditText>(Resource.Id.sftp_port).Text = ci.Port.ToString();
		        dlgContents.FindViewById<EditText>(Resource.Id.sftp_user).Text = ci.Username;
		        dlgContents.FindViewById<EditText>(Resource.Id.sftp_password).Text = ci.Password;
				dlgContents.FindViewById<EditText>(Resource.Id.sftp_key_name).Text = ci.KeyName;
				dlgContents.FindViewById<EditText>(Resource.Id.sftp_key_passphrase).Text = ci.KeyPassphrase;
		        dlgContents.FindViewById<EditText>(Resource.Id.sftp_initial_dir).Text = ci.LocalPath;
		        if (ci.ConnectTimeoutSec != SftpStorage.UnsetSftpConnectTimeout)
		        {
			        dlgContents.FindViewById<EditText>(Resource.Id.sftp_connect_timeout).Text = ci.ConnectTimeoutSec.ToString();
		        }

				if (!string.IsNullOrEmpty(ci.Password))
				{
					authModeSpinner.SetSelection(SftpModeSpinnerPasswd);
				} else if (!string.IsNullOrEmpty(ci.KeyName))
				{
					authModeSpinner.SetSelection(SftpModeSpinnerCustomKey);
					keySpinner.SetSelection(ResolveKeySpinnerSelection(keyNamesAdapter, ci.KeyName));
				} else
				{
					authModeSpinner.SetSelection(SftpModeSpinnerPubKey);
				}

            }

			builder.SetView(dlgContents);
            builder.SetPositiveButton(Android.Resource.String.Ok, (sender, args) => {
				int idx = 0;
                string host = dlgContents.FindViewById<EditText>(Resource.Id.sftp_host).Text;
                string portText = dlgContents.FindViewById<EditText>(Resource.Id.sftp_port).Text;
                int port = SftpStorage.DefaultSftpPort;
                if (!string.IsNullOrEmpty(portText))
                    int.TryParse(portText, out port);
                string user = dlgContents.FindViewById<EditText>(Resource.Id.sftp_user).Text;

                string password = null;
                string keyName = null, keyPassphrase = null;
                switch (dlgContents.FindViewById<Spinner>(Resource.Id.sftp_auth_mode_spinner).SelectedItemPosition)
                {
                    case SftpModeSpinnerPasswd:
                        password = dlgContents.FindViewById<EditText>(Resource.Id.sftp_password).Text;
                        break;
                    case SftpModeSpinnerPubKey:
                        break;
                    case SftpModeSpinnerCustomKey:
						keyName = ResolveSelectedKeyName(keyNamesAdapter, keySpinner.SelectedItemPosition);
						keyPassphrase = dlgContents.FindViewById<EditText>(Resource.Id.sftp_key_passphrase).Text;
                        break;
                }

                string initialPath = dlgContents.FindViewById<EditText>(Resource.Id.sftp_initial_dir).Text;
                if (string.IsNullOrEmpty(initialPath))
                    initialPath = "/";
                string connectTimeoutText = dlgContents.FindViewById<EditText>(Resource.Id.sftp_connect_timeout).Text;
                int connectTimeout = SftpStorage.UnsetSftpConnectTimeout;
                if (!string.IsNullOrEmpty(connectTimeoutText))
                {
                    int.TryParse(connectTimeoutText, out connectTimeout);
                }

                string sftpPath = fileStorage.BuildFullPath(
                    host, port, initialPath, user, password, connectTimeout, keyName, keyPassphrase);

                onStartBrowse(sftpPath);
            });
            EventHandler<DialogClickEventArgs> evtH = new EventHandler<DialogClickEventArgs>((sender, e) => onCancel());

			builder.SetNegativeButton(Android.Resource.String.Cancel, evtH);
			builder.SetTitle(activity.GetString(Resource.String.enter_sftp_login_title));
			Dialog dialog = builder.Create();

			dialog.Show();
#endif
        }

#if !EXCLUDE_JAVAFILESTORAGE && !NoNet
        private void UpdatePrivateKeyNames(ArrayAdapter dataView, SftpStorage storage, Context ctx)
        {
			dataView.Clear();
			dataView.Add(ctx.GetString(Resource.String.private_key_create_new));
			foreach (string keyName in storage.GetCustomKeyNames()) 
				dataView.Add(keyName);
        }

        private int ResolveKeySpinnerSelection(ArrayAdapter dataView, string keyName)
        {
			int idx = -1;
			for (int i = 0; i < dataView.Count; i++)
			{
				string itemName = dataView.GetItem(i).ToString();
				if (string.Equals(keyName, itemName)) {
					idx = i;
					break;
				}
			}
			return idx < 0 ? SftpKeySpinnerCreateNewIdx : idx;
        }

        private string ResolveSelectedKeyName(ArrayAdapter dataView, int selectedItem)
        {
            if (selectedItem != SftpKeySpinnerCreateNewIdx && selectedItem > 0 && selectedItem < dataView.Count)
                return dataView.GetItem(selectedItem).ToString();
            else
                return null;
        }
#endif

        private void ShowHttpDialog(Activity activity, Util.FileSelectedHandler onStartBrowse, Action onCancel, string defaultPath)
		{
#if !EXCLUDE_JAVAFILESTORAGE && !NoNet
			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			View dlgContents = activity.LayoutInflater.Inflate(Resource.Layout.httpcredentials, null);
		    if (!defaultPath.EndsWith(_schemeSeparator))
		    {
		        var webdavStorage = new Keepass2android.Javafilestorage.WebDavStorage(App.Kp2a.CertificateErrorHandler);
		        var connInfo = webdavStorage.SplitStringToConnectionInfo(defaultPath);
		        dlgContents.FindViewById<EditText>(Resource.Id.http_url).Text = connInfo.Url;
		        dlgContents.FindViewById<EditText>(Resource.Id.http_user).Text = connInfo.Username;
		        dlgContents.FindViewById<EditText>(Resource.Id.http_password).Text = connInfo.Password;


            }
			builder.SetView(dlgContents);
			builder.SetPositiveButton(Android.Resource.String.Ok,
									  (sender, args) =>
									  {
										  string host = dlgContents.FindViewById<EditText>(Resource.Id.http_url).Text;
											
										  string user = dlgContents.FindViewById<EditText>(Resource.Id.http_user).Text;
										  string password = dlgContents.FindViewById<EditText>(Resource.Id.http_password).Text;

										  string scheme = defaultPath.Substring(0, defaultPath.IndexOf(_schemeSeparator, StringComparison.Ordinal));
										  if (host.Contains(_schemeSeparator) == false)
											  host = scheme + _schemeSeparator + host;
										  string httpPath = new Keepass2android.Javafilestorage.WebDavStorage(null).BuildFullPath(host, user,
																										  password);
										  onStartBrowse(httpPath);
									  });
			EventHandler<DialogClickEventArgs> evtH = new EventHandler<DialogClickEventArgs>((sender, e) => onCancel());

			builder.SetNegativeButton(Android.Resource.String.Cancel, evtH);
			builder.SetTitle(activity.GetString(Resource.String.enter_http_login_title));
			Dialog dialog = builder.Create();

			dialog.Show();
#endif
		}

		private void ShowFtpDialog(Activity activity, Util.FileSelectedHandler onStartBrowse, Action onCancel, string defaultPath)
		{
#if !NoNet
			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			View dlgContents = activity.LayoutInflater.Inflate(Resource.Layout.ftpcredentials, null);
		    if (!defaultPath.EndsWith(_schemeSeparator))
		    {
		        var connection = NetFtpFileStorage.ConnectionSettings.FromIoc(IOConnectionInfo.FromPath(defaultPath));
		        dlgContents.FindViewById<EditText>(Resource.Id.ftp_user).Text = connection.Username;
		        dlgContents.FindViewById<EditText>(Resource.Id.ftp_password).Text = connection.Password;
		        dlgContents.FindViewById<Spinner>(Resource.Id.ftp_encryption).SetSelection((int)connection.EncryptionMode);

		        var uri = NetFtpFileStorage.IocToUri(IOConnectionInfo.FromPath(defaultPath));
                string pathAndQuery = uri.PathAndQuery;

		        var host = uri.Host;
		        var localPath = WebUtility.UrlDecode(pathAndQuery);

                
		        if (!uri.IsDefaultPort)
		        {
		            dlgContents.FindViewById<EditText>(Resource.Id.ftp_port).Text = uri.Port.ToString();
		        }
		        dlgContents.FindViewById<EditText>(Resource.Id.ftp_host).Text = host;
                dlgContents.FindViewById<EditText>(Resource.Id.ftp_initial_dir).Text = localPath;


            }
            builder.SetView(dlgContents);
			builder.SetPositiveButton(Android.Resource.String.Ok,
									  (sender, args) =>
									  {
										  string host = dlgContents.FindViewById<EditText>(Resource.Id.ftp_host).Text;
										  string portText = dlgContents.FindViewById<EditText>(Resource.Id.ftp_port).Text;
										  FtpEncryptionMode encryption =
											  (FtpEncryptionMode) dlgContents.FindViewById<Spinner>(Resource.Id.ftp_encryption).SelectedItemPosition;
										  int port = NetFtpFileStorage.GetDefaultPort(encryption);
										  if (!string.IsNullOrEmpty(portText))
											  int.TryParse(portText, out port);
										  string user = dlgContents.FindViewById<EditText>(Resource.Id.ftp_user).Text;
										  string password = dlgContents.FindViewById<EditText>(Resource.Id.ftp_password).Text;
										  string initialPath = dlgContents.FindViewById<EditText>(Resource.Id.ftp_initial_dir).Text;
										  string ftpPath = new NetFtpFileStorage(_activity, App.Kp2a).BuildFullPath(host, port, initialPath, user,
																										  password, encryption);
										  onStartBrowse(ftpPath);
									  });
			EventHandler<DialogClickEventArgs> evtH = new EventHandler<DialogClickEventArgs>((sender, e) => onCancel());

			builder.SetNegativeButton(Android.Resource.String.Cancel, evtH);
			builder.SetTitle(activity.GetString(Resource.String.enter_ftp_login_title));
			Dialog dialog = builder.Create();

			dialog.Show();
#endif
		}


		private void ShowMegaDialog(Activity activity, Util.FileSelectedHandler onStartBrowse, Action onCancel, string defaultPath)
		{
#if !NoNet
            var settings = MegaFileStorage.GetAccountSettings(activity);
			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			View dlgContents = activity.LayoutInflater.Inflate(Resource.Layout.megacredentials, null);
			if (!defaultPath.EndsWith(_schemeSeparator))
            {
                string user = "";
                string password = "";
                if (!defaultPath.EndsWith(_schemeSeparator))
				{
					 user = MegaFileStorage.GetAccount(defaultPath);
                     if (!settings.PasswordByUsername.TryGetValue(user, out password))
                         password = "";
                     dlgContents.FindViewById<EditText>(Resource.Id.mega_user).Enabled = false;

                }
				dlgContents.FindViewById<EditText>(Resource.Id.mega_user).Text = user;
				dlgContents.FindViewById<EditText>(Resource.Id.mega_password).Text = password;

			}

            var userView = ((AutoCompleteTextView)dlgContents.FindViewById(Resource.Id.mega_user));
            userView.Adapter = new ArrayAdapter(activity, Android.Resource.Layout.SimpleListItem1, Android.Resource.Id.Text1, settings.PasswordByUsername.Keys.ToArray());

            userView.TextChanged += (sender, args) =>
            {
                if (userView.Text != null && settings.PasswordByUsername.TryGetValue(userView.Text, out string pwd))
                {
                    dlgContents.FindViewById<EditText>(Resource.Id.mega_password).Text = pwd;
				}
            };
            builder.SetCancelable(false);
			builder.SetView(dlgContents);
			builder.SetPositiveButton(Android.Resource.String.Ok,
									  (sender, args) =>
									  {
										  string user = dlgContents.FindViewById<EditText>(Resource.Id.mega_user).Text;
										  string password = dlgContents.FindViewById<EditText>(Resource.Id.mega_password).Text;
										  //store the credentials in the mega credentials store:
                                          

                                          settings.PasswordByUsername[user] = password;

                                          MegaFileStorage.UpdateAccountSettings(settings, activity);

                                          onStartBrowse(MegaFileStorage.ProtocolId + "://" + user);
                                      });
			EventHandler<DialogClickEventArgs> evtH = new EventHandler<DialogClickEventArgs>((sender, e) => onCancel());

			builder.SetNegativeButton(Android.Resource.String.Cancel, evtH);
			builder.SetTitle(activity.GetString(Resource.String.enter_mega_login_title));
			Dialog dialog = builder.Create();

			dialog.Show();
#endif
		}



		public void PerformManualFileSelect(string defaultPath)
		{
			if (defaultPath.StartsWith("sftp://"))
				ShowSftpDialog(_activity, ReturnFileOrStartFileChooser, ReturnCancel, defaultPath);
			else if ((defaultPath.StartsWith("ftp://")) || (defaultPath.StartsWith("ftps://")))
				ShowFtpDialog(_activity, ReturnFileOrStartFileChooser, ReturnCancel, defaultPath);
			else if ((defaultPath.StartsWith("http://")) || (defaultPath.StartsWith("https://")))
				ShowHttpDialog(_activity, ReturnFileOrStartFileChooser, ReturnCancel, defaultPath);
			else if (defaultPath.StartsWith("owncloud://"))
				ShowOwncloudDialog(_activity, ReturnFileOrStartFileChooser, ReturnCancel, defaultPath, "owncloud");
			else if (defaultPath.StartsWith("nextcloud://"))
			    ShowOwncloudDialog(_activity, ReturnFileOrStartFileChooser, ReturnCancel, defaultPath, "nextcloud");
            else if (defaultPath.StartsWith("mega://"))
                ShowMegaDialog(_activity, ReturnFileOrStartFileChooser, ReturnCancel, defaultPath);
			else
			{
				Func<string, Dialog, bool> onOpen = OnOpenButton;
				Util.ShowFilenameDialog(_activity,
										!_isForSave ? onOpen : null,
										_isForSave ? onOpen : null,
										ReturnCancel,
										false,
										defaultPath,
										_activity.GetString(Resource.String.enter_filename_details_url),
										_requestCode)
				;
			}
		}

		private void ShowOwncloudDialog(Activity activity, Util.FileSelectedHandler onStartBrowse, Action onCancel, string defaultPath, string subtype)
		{
#if !EXCLUDE_JAVAFILESTORAGE && !NoNet
			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			View dlgContents = activity.LayoutInflater.Inflate(Resource.Layout.owncloudcredentials, null);
			builder.SetView(dlgContents);

			builder.SetPositiveButton(Android.Resource.String.Ok,
									  (sender, args) =>
									  {
										  string host = dlgContents.FindViewById<EditText>(Resource.Id.owncloud_url).Text;

										  string user = dlgContents.FindViewById<EditText>(Resource.Id.http_user).Text;
										  string password = dlgContents.FindViewById<EditText>(Resource.Id.http_password).Text;

										  string scheme = defaultPath.Substring(0,defaultPath.IndexOf(_schemeSeparator, StringComparison.Ordinal));
										  if (host.Contains(_schemeSeparator) == false)
											  host = scheme + _schemeSeparator + host;
										  string httpPath = new Keepass2android.Javafilestorage.WebDavStorage(null).BuildFullPath(WebDavFileStorage.Owncloud2Webdav(host, subtype == "owncloud" ? WebDavFileStorage.owncloudPrefix : WebDavFileStorage.nextcloudPrefix), user,
																										  password);
										  onStartBrowse(httpPath);
									  });
			EventHandler<DialogClickEventArgs> evtH = new EventHandler<DialogClickEventArgs>((sender, e) => onCancel());

			builder.SetNegativeButton(Android.Resource.String.Cancel, evtH);
			builder.SetTitle(activity.GetString(subtype == "owncloud" ?  Resource.String.enter_owncloud_login_title : Resource.String.enter_nextcloud_login_title));
			Dialog dialog = builder.Create();
		    dlgContents.FindViewById<EditText>(Resource.Id.owncloud_url).SetHint(subtype == "owncloud" ? Resource.String.hint_owncloud_url : Resource.String.hint_nextcloud_url);
            dialog.Show();
#endif

		}

		private bool ReturnFileOrStartFileChooser(string filename)
		{
		    string filenameWithoutProt = filename;
		    if (filenameWithoutProt.Contains(_schemeSeparator))
		    {
		        filenameWithoutProt =
		            filenameWithoutProt.Substring(filenameWithoutProt.IndexOf(_schemeSeparator, StringComparison.Ordinal) + 3);
		    }

            int lastSlashPos = filenameWithoutProt.LastIndexOf('/');
			int lastDotPos = filenameWithoutProt.LastIndexOf('.');
			if ((lastSlashPos < 0 ) //no slash, probably only a server address (my.server.com)
                || (lastSlashPos >= lastDotPos)) //no dot after last slash or == in case neither / nor .
			{
				//looks like a folder.
				return StartFileChooser(filename);
			}
			//looks like a file
			IocSelected(null, IOConnectionInfo.FromPath(filename));
			return true;
		}

		private void ReturnCancel()
		{
			if (OnCancel != null)
				OnCancel(this, null);
		}


		protected void ShowFilenameWarning(string fileName, Action onUserWantsToContinue, Action onUserWantsToCorrect)
		{
			new AlertDialog.Builder(_activity)
					.SetPositiveButton(keepass2android.Resource.String.Continue, delegate { onUserWantsToContinue(); })
					.SetMessage(Resource.String.NoFilenameWarning)
					.SetCancelable(false)
					.SetNegativeButton(Android.Resource.String.Cancel, delegate { onUserWantsToCorrect(); })
					.Create()
					.Show();

		}
		private bool OnOpenButton(string filename, Dialog dialog)
		{

			IOConnectionInfo ioc = new IOConnectionInfo
			{
				Path = filename
			};

			// Make sure file name exists
			if (filename.Length == 0)
			{
				Toast.MakeText(_activity,
								Resource.String.error_filename_required,
								ToastLength.Long).Show();
				return false;
			}


			int lastSlashPos = filename.LastIndexOf('/');
			int lastDotPos = filename.LastIndexOf('.');
			if (lastSlashPos >= lastDotPos) //no dot after last slash or == in case neither / nor .
			{
				ShowFilenameWarning(filename, () => { IocSelected(null, ioc); dialog.Dismiss(); }, () => { /* don't do anything, leave dialog open, let user try again*/ });
				//signal that the dialog should be kept open
				return false;
			}

			IFileStorage fileStorage;
			try
			{
				fileStorage = App.Kp2a.GetFileStorage(ioc);
			}
			catch (NoFileStorageFoundException)
			{
				Toast.MakeText(_activity,
								"Unexpected scheme in "+filename,
								ToastLength.Long).Show();
				return false;
			}

			if (_isForSave && ioc.IsLocalFile())
			{
				// Try to create the file
				File file = new File(filename);
				try
				{
					File parent = file.ParentFile;

					if (parent == null || (parent.Exists() && !parent.IsDirectory))
					{
						Toast.MakeText(_activity,
							            Resource.String.error_invalid_path,
							            ToastLength.Long).Show();
						return false;
					}

					if (!parent.Exists())
					{
						// Create parent dircetory
						if (!parent.Mkdirs())
						{
							Toast.MakeText(_activity,
								            Resource.String.error_could_not_create_parent,
								            ToastLength.Long).Show();
							return false;

						}
					}
					System.IO.File.Create(filename).Dispose();

				}
				catch (IOException ex)
				{
					Toast.MakeText(
						_activity,
						_activity.GetText(Resource.String.error_file_not_create) + " "
						+ ex.LocalizedMessage,
						ToastLength.Long).Show();
					return false;
				}

			}
			if (fileStorage.RequiresCredentials(ioc))
			{
				Util.QueryCredentials(ioc, iocResult => IocSelected(null, iocResult), _activity);
			}
			else
			{
				IocSelected(null, ioc);
			}

			return true;
		}

		private void IocSelected(Activity activity, IOConnectionInfo ioc)
		{
			if (OnOpen != null)
				OnOpen(activity, ioc);
		}

		public bool StartFileChooser(string defaultPath)
		{
#if !EXCLUDE_FILECHOOSER
			string fileProviderAuthority = FileChooserFileProvider.TheAuthority;
			if (defaultPath.StartsWith("file://"))
			{
				fileProviderAuthority = _activity.PackageName + ".android-filechooser.localfile";
			}
			Intent i = Keepass2android.Kp2afilechooser.Kp2aFileChooserBridge.GetLaunchFileChooserIntent(_activity, fileProviderAuthority,
																										defaultPath);


			if (_isForSave)
			{
				i.PutExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.save_dialog", true);
				string ext;
				if (!string.IsNullOrEmpty(DefaultExtension))
				{
					ext = DefaultExtension;
				}
				else
				{
					ext = UrlUtil.GetExtension(defaultPath);
				}
				if ((ext != String.Empty) && (ext.Contains("?") == false))
					i.PutExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.default_file_ext", ext);
			}
			_activity.StartActivityForResult(i, _requestCode);

#else
			Toast.MakeText(LocaleManager.LocalizedAppContext, "File chooser is excluded!", ToastLength.Long).Show();
#endif
			return true;
		}

		public event EventHandler OnCancel;

		public event EventHandler<IOConnectionInfo> OnOpen;

	    public static bool CanEditIoc(IOConnectionInfo ioc)
	    {
	        return ioc.Path.StartsWith("http")
	               || ioc.Path.StartsWith("ftp")
	               || ioc.Path.StartsWith("sftp")
                   || ioc.Path.StartsWith("mega");

	    }

	    public bool HandleActivityResult(Activity activity, int requestCode, Result resultCode, Intent data)
	    {
	        if (requestCode != _requestCode)
	            return false;
			
			if (resultCode == KeePass.ExitFileStorageSelectionOk)
			{
				string protocolId = data.GetStringExtra("protocolId");
				if (protocolId == "content")
				{
					Util.ShowBrowseDialog(activity, _requestCode, _isForSave, _tryGetPermanentAccess);
				}
				else
				{					
					App.Kp2a.GetFileStorage(protocolId).StartSelectFile(
							new FileStorageSetupInitiatorActivity(activity,(i, result, arg3) =>HandleActivityResult(activity, i, result, arg3),s => PerformManualFileSelect(s)), 
							_isForSave, 
							_requestCode, 
							protocolId);	
				}
			}
			
			if (resultCode == Result.Ok)
				{
					
					if (data.Data.Scheme == "content")
					{
						if ((int)Build.VERSION.SdkInt >= 19)
						{
							//try to take persistable permissions
							try
							{
								Kp2aLog.Log("TakePersistableUriPermission");
								var takeFlags = data.Flags
										& (ActivityFlags.GrantReadUriPermission
										| ActivityFlags.GrantWriteUriPermission);
								activity.ContentResolver.TakePersistableUriPermission(data.Data, takeFlags);
							}
							catch (Exception e)
							{
								Kp2aLog.Log(e.ToString());
							}

						}
					}

					
					string filename = Util.IntentToFilename(data, activity);
					if (filename == null)
						filename = data.DataString;

					bool fileExists = data.GetBooleanExtra("group.pals.android.lib.ui.filechooser.FileChooserActivity.result_file_exists", true);

					if (fileExists)
					{
						var ioc = new IOConnectionInfo { Path = ConvertFilenameToIocPath(filename) };
						IocSelected(activity,ioc);
					}
					else
					{
						var task = new CreateNewFilename(activity, new ActionOnFinish(activity, (success, messageOrFilename, newActivity) =>
							{
								if (!success)
								{
									Toast.MakeText(newActivity, messageOrFilename, ToastLength.Long).Show();
									return;
								}
								var ioc = new IOConnectionInfo { Path = ConvertFilenameToIocPath(messageOrFilename) };
							    IocSelected(newActivity, ioc);
								
							}), filename);

						new ProgressTask(App.Kp2a, activity, task).Run();
					}

				}
				
				
			if (resultCode == (Result)FileStorageResults.FileUsagePrepared)
			{
				var ioc = new IOConnectionInfo();
				Util.SetIoConnectionFromIntent(ioc, data);
				IocSelected(null, ioc);
			}
			if (resultCode == (Result)FileStorageResults.FileChooserPrepared )
			{
				IOConnectionInfo ioc = new IOConnectionInfo();
				Util.SetIoConnectionFromIntent(ioc, data);
				StartFileChooser(ioc.Path);
				
			}
	        return true;

        }
    }
}