using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Android.App;
using System.IO;
using Android.Content;
using Android.OS;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using keepass2android.Io;
#if !NoNet
using Keepass2android.Javafilestorage;
#endif 

namespace keepass2android
{
	public interface ICertificateValidationHandler
	{
		/// <summary>
		/// Handles a failed certificate validation. Returns true if the users wants to continue, false otherwise.
		/// see http://msdn.microsoft.com/en-us/library/system.net.icertificatepolicy(v=vs.110).aspx
		/// </summary>
		//bool OnServerCertificateError(int certificateProblem);

		RemoteCertificateValidationCallback CertificateValidationCallback { get; }

	}

	/// <summary>
	/// Interface through which Activities and the logic layer can access some app specific functionalities and Application static data
	/// </summary>
	/// This also contains methods which are UI specific and should be replacable for testing.
	public interface IKp2aApp : ICertificateValidationHandler
	{
		/// <summary>
		/// Locks the currently open database, quicklocking if available (unless false is passed for allowQuickUnlock)
		/// </summary>
		void LockDatabase(bool allowQuickUnlock = true);

		/// <summary>
		/// Loads the specified data as the currently open database, as unlocked.
		/// </summary>
		void LoadDatabase(IOConnectionInfo ioConnectionInfo, MemoryStream memoryStream, CompositeKey compKey,
		                  ProgressDialogStatusLogger statusLogger, IDatabaseFormat databaseFormat);

		/// <summary>
		/// Returns the current database
		/// </summary>
		Database GetDb();

		/// <summary>
		/// Tell the app that the file from ioc was opened with keyfile.
		/// </summary>
		void StoreOpenedFileAsRecent(IOConnectionInfo ioc, string keyfile);

		/// <summary>
		/// Creates a new database and returns it
		/// </summary>
		Database CreateNewDatabase();

		/// <summary>
		/// Returns the user-displayable string identified by stringKey
		/// </summary>
		string GetResourceString(UiStringKey stringKey);

		/// <summary>
		/// Returns the value from the preferences corresponding to key
		/// </summary>
		bool GetBooleanPreference(PreferenceKey key);

		/// <summary>
		/// Asks the user the question "messageKey" with the options Yes/No/Cancel, calls the handler corresponding to the answer.
		/// </summary>
		void AskYesNoCancel(UiStringKey titleKey, UiStringKey messageKey,
		                    EventHandler<DialogClickEventArgs> yesHandler,
		                    EventHandler<DialogClickEventArgs> noHandler,
		                    EventHandler<DialogClickEventArgs> cancelHandler,
		                    Context ctx);

		/// <summary>
		/// Asks the user the question "messageKey" with the options Yes/No/Cancel, but the yes/no strings can be selected freely, calls the handler corresponding to the answer.
		/// </summary>
		void AskYesNoCancel(UiStringKey titleKey, UiStringKey messageKey,
		                    UiStringKey yesString, UiStringKey noString,
		                    EventHandler<DialogClickEventArgs> yesHandler,
		                    EventHandler<DialogClickEventArgs> noHandler,
		                    EventHandler<DialogClickEventArgs> cancelHandler,
		                    Context ctx);

		/// <summary>
		/// Returns a Handler object which can run tasks on the UI thread
		/// </summary>
		Handler UiThreadHandler { get; }

		IProgressDialog CreateProgressDialog(Context ctx);

		/// <summary>
		/// returns the file storage for the given ioc. might be a caching file storage
		/// </summary>
		IFileStorage GetFileStorage(IOConnectionInfo iocInfo);

		/// <summary>
		/// returns the file storage for the given ioc. if allowCache=false, no cached file storage is returned
		/// </summary>
		IFileStorage GetFileStorage(IOConnectionInfo iocInfo, bool allowCache);

		void TriggerReload(Context context);

		
		bool CheckForDuplicateUuids { get; }
#if !NoNet
		ICertificateErrorHandler CertificateErrorHandler { get; }
#endif
	    bool CanEditIoc(IOConnectionInfo ioc);
	}
}