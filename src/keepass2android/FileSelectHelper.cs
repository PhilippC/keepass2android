using System;
using System.Collections.Generic;
using System.Linq;
#if !NoNet
using System.Net.FtpClient;
using Keepass2android.Javafilestorage;

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
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace keepass2android
{
	public class FileSelectHelper
	{
		private readonly Activity _activity;
		private readonly bool _isForSave;
		private readonly int _requestCode;
	    private readonly string _schemeSeparator = "://";

	    public string DefaultExtension { get; set; }

		public FileSelectHelper(Activity activity, bool isForSave, int requestCode)
		{
			_activity = activity;
			_isForSave = isForSave;
			_requestCode = requestCode;
		}

		private void ShowSftpDialog(Activity activity, Util.FileSelectedHandler onStartBrowse, Action onCancel, string defaultPath)
		{
#if !EXCLUDE_JAVAFILESTORAGE && !NoNet
			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			View dlgContents = activity.LayoutInflater.Inflate(Resource.Layout.sftpcredentials, null);

		    if (!defaultPath.EndsWith(_schemeSeparator))
		    {
		        var fileStorage = new Keepass2android.Javafilestorage.SftpStorage();
                SftpStorage.ConnectionInfo ci = fileStorage.SplitStringToConnectionInfo(defaultPath);
		        dlgContents.FindViewById<EditText>(Resource.Id.sftp_host).Text = ci.Host;
		        dlgContents.FindViewById<EditText>(Resource.Id.sftp_port).Text = ci.Port.ToString();
		        dlgContents.FindViewById<EditText>(Resource.Id.sftp_user).Text = ci.Username;
		        dlgContents.FindViewById<EditText>(Resource.Id.sftp_password).Text = ci.Password;
		        dlgContents.FindViewById<EditText>(Resource.Id.sftp_initial_dir).Text = ci.LocalPath;
            }

			builder.SetView(dlgContents);
			builder.SetPositiveButton(Android.Resource.String.Ok,
									  (sender, args) =>
									  {
										  string host = dlgContents.FindViewById<EditText>(Resource.Id.sftp_host).Text;
										  string portText = dlgContents.FindViewById<EditText>(Resource.Id.sftp_port).Text;
										  int port = Keepass2android.Javafilestorage.SftpStorage.DefaultSftpPort;
										  if (!string.IsNullOrEmpty(portText))
											  int.TryParse(portText, out port);
										  string user = dlgContents.FindViewById<EditText>(Resource.Id.sftp_user).Text;
										  string password = dlgContents.FindViewById<EditText>(Resource.Id.sftp_password).Text;
										  string initialPath = dlgContents.FindViewById<EditText>(Resource.Id.sftp_initial_dir).Text;
									      if (string.IsNullOrEmpty(initialPath))
									          initialPath = "/";
                                          string sftpPath = new Keepass2android.Javafilestorage.SftpStorage().BuildFullPath(host, port, initialPath, user,
																										  password);
										  onStartBrowse(sftpPath);
									  });
			EventHandler<DialogClickEventArgs> evtH = new EventHandler<DialogClickEventArgs>((sender, e) => onCancel());

			builder.SetNegativeButton(Android.Resource.String.Cancel, evtH);
			builder.SetTitle(activity.GetString(Resource.String.enter_sftp_login_title));
			Dialog dialog = builder.Create();

			dialog.Show();
#endif
		}

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
		        var localPath = pathAndQuery;

                
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


		public void PerformManualFileSelect(string defaultPath)
		{
			if (defaultPath.StartsWith("sftp://"))
				ShowSftpDialog(_activity, ReturnFileOrStartFileChooser, ReturnCancel, defaultPath);
			else if ((defaultPath.StartsWith("ftp://")) || (defaultPath.StartsWith("ftps://")))
				ShowFtpDialog(_activity, ReturnFileOrStartFileChooser, ReturnCancel, defaultPath);
			else if ((defaultPath.StartsWith("http://")) || (defaultPath.StartsWith("https://")))
				ShowHttpDialog(_activity, ReturnFileOrStartFileChooser, ReturnCancel, defaultPath);
			else if (defaultPath.StartsWith("owncloud://"))
				ShowOwncloudDialog(_activity, ReturnFileOrStartFileChooser, ReturnCancel, defaultPath);
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

		private void ShowOwncloudDialog(Activity activity, Util.FileSelectedHandler onStartBrowse, Action onCancel, string defaultPath)
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
										  string httpPath = new Keepass2android.Javafilestorage.WebDavStorage(null).BuildFullPath(WebDavFileStorage.Owncloud2Webdav(host), user,
																										  password);
										  onStartBrowse(httpPath);
									  });
			EventHandler<DialogClickEventArgs> evtH = new EventHandler<DialogClickEventArgs>((sender, e) => onCancel());

			builder.SetNegativeButton(Android.Resource.String.Cancel, evtH);
			builder.SetTitle(activity.GetString(Resource.String.enter_owncloud_login_title));
			Dialog dialog = builder.Create();

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
			IocSelected(IOConnectionInfo.FromPath(filename));
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
				ShowFilenameWarning(filename, () => { IocSelected(ioc); dialog.Dismiss(); }, () => { /* don't do anything, leave dialog open, let user try again*/ });
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
					System.IO.File.Create(filename);

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
				Util.QueryCredentials(ioc, IocSelected, _activity);
			}
			else
			{
				IocSelected(ioc);
			}

			return true;
		}

		private void IocSelected(IOConnectionInfo ioc)
		{
			if (OnOpen != null)
				OnOpen(this, ioc);
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
			Toast.MakeText(this, "File chooser is excluded!", ToastLength.Long).Show();
#endif
			return true;
		}

		public event EventHandler OnCancel;

		public event EventHandler<IOConnectionInfo> OnOpen;

	    public static bool CanEditIoc(IOConnectionInfo ioc)
	    {
	        return ioc.Path.StartsWith("http")
	               || ioc.Path.StartsWith("ftp")
	               || ioc.Path.StartsWith("sftp");

	    }
	}
}