using System;
using System.Collections.Generic;
using System.IO;
using Android.Content;
using Android.OS;
using KeePassLib.Serialization;

namespace keepass2android.Io
{

	public enum FileStorageResults
	{
		FullFilenameSelected = 874345 + 1,
		FileChooserPrepared = FullFilenameSelected + 1,
		FileUsagePrepared = FileChooserPrepared + 1
	}

	public static class FileStorageSetupDefs
	{
		public static String ProcessNameSelectfile = "SELECT_FILE";
		public static String ProcessNameFileUsageSetup = "FILE_USAGE_SETUP";

		public static String ExtraProcessName = "EXTRA_PROCESS_NAME";
		public static String ExtraAlwaysReturnSuccess = "EXTRA_ALWAYS_RETURN_SUCCESS";
		public static String ExtraPath = "PATH";
		public static String ExtraIsForSave = "IS_FOR_SAVE";
		public static String ExtraErrorMessage = "EXTRA_ERROR_MESSAGE";

	}

	/// <summary>
	/// Interface to encapsulate all access to disk or cloud.
	/// </summary>
	/// Note that it was decided to use the IOConnectionInfo also for cloud storage.
	/// The advantage is that the database for saving recent files etc. will then work without 
	/// much work to do. Furthermore, the IOConnectionInfo seems generic info to capture all required data, even though it might be nicer to 
	/// have an IIoStorageId interface in few cases.*/
	public interface IFileStorage
	{
		/// <summary>
		/// returns the protocol ids supported by this FileStorage. Can return pseudo-protocols like "dropbox" or real protocols like "ftp"
		/// </summary>
		IEnumerable<string> SupportedProtocols { get; } 

		/// <summary>
		/// Deletes the given file or directory.
		/// </summary>
		void Delete(IOConnectionInfo ioc);

		/// <summary>
		/// Tests whether the file was changed. 
		/// </summary>
		/// Note: This function may return false even if the file might have changed. The function
		/// should focus on being fast and cheap instead of doing things like hashing or downloading a full file.
		/// previousFileVersion may be null to indicate no previous version is known.
		/// <returns>Returns true if a change was detected, false otherwise.</returns>
		bool CheckForFileChangeFast(IOConnectionInfo ioc , string previousFileVersion);

		/// <summary>
		/// Returns a string describing the "version" of the file specified by ioc.
		/// </summary>
		/// This string may have a deliberate value (except null) and should not be used by callers except for passing it to
		/// CheckForFileChangeFast().
		/// <returns>A string describing the version. Null means, there is no way to get a file version (or it's not implemented).</returns>
		string GetCurrentFileVersionFast(IOConnectionInfo ioc);

		/// <summary>
		/// Opens the given file for reading
		/// </summary>
		Stream OpenFileForRead(IOConnectionInfo ioc);

		/// <summary>
		/// Opens a write transaction for writing to the given ioc. 
		/// </summary>
		/// <param name="ioc">ioc to write to</param>
		/// <param name="useFileTransaction">if true, force to use file system level transaction. This might be ignored if the file storage has built in transaction support</param>
		IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction);

		string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc);
		
		/// <summary>
		/// Returns true if the the given ioc must be filled with username/password
		/// </summary>
		bool RequiresCredentials(IOConnectionInfo ioc);

		/// <summary>
		/// Creates the directory described by ioc
		/// </summary>
		void CreateDirectory(IOConnectionInfo ioc, string newDirName);

		/// <summary>
		/// Lists the contents of the given path
		/// </summary>
		IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc);

		/// <summary>
		/// returns the description of the given file
		/// </summary>
		FileDescription GetFileDescription(IOConnectionInfo ioc);

		/// <summary>
		/// returns true if everything is ok with connecting to the given file. 
		/// Returns False if PrepareFileUsage must be called first.
		/// </summary>
		bool RequiresSetup(IOConnectionInfo ioConnection);

		/// <summary>
		/// converts the ioc to a path which may contain the credentials
		/// </summary>
		string IocToPath(IOConnectionInfo ioc);

		/// <summary>
		/// Initiates the process for choosing a file in the given file storage.
		/// The file storage should either call OnImmediateResult or StartSelectFileProcess
		/// </summary>
		void StartSelectFile(IFileStorageSetupInitiatorActivity activity, bool isForSave, int requestCode, string protocolId);

		/// <summary>
		/// Initiates the process for using a file in the given file storage.
		/// The file storage should either call OnImmediateResult or StartFileUsageProcess
		/// If alwaysReturnSuccess is true, the activity should be finished with ResultCode Ok.
		/// This can make sense if a higher-level file storage has the file cached but still wants to 
		/// give the cached storage the chance to initialize file access.
		/// </summary>
		void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode, bool alwaysReturnSuccess);

		/// <summary>
		/// Initiates the process for using a file in the given file storage.
		/// This method either silently prepares using the file (if any preparation is required) or throws
		/// UserInteractionRequiredException (or any other exception in case of an error). 
		/// Can be used from a service, i.e. when no Activity is open.
		/// </summary>
		void PrepareFileUsage(Context ctx, IOConnectionInfo ioc);

		//Setup methods: these are called from the setup activity so the file storage can handle UI events for authorization etc.
		void OnCreate(IFileStorageSetupActivity activity, Bundle savedInstanceState);
		void OnResume(IFileStorageSetupActivity activity);
		void OnStart(IFileStorageSetupActivity activity);
		void OnActivityResult(IFileStorageSetupActivity activity, int requestCode, int resultCode, Intent data);

		/// <summary>
		/// Converts the given path to a displayable string
		/// </summary>
		string GetDisplayName(IOConnectionInfo ioc);

		//returns the path of a file "newFilename" in the folder "parent"
		//this may create the file if this is required to get a path (if a UUID is part of the file path)
		string CreateFilePath(string parent, string newFilename);

		/// <summary>
		/// returns the parent folder of ioc
		/// </summary>
		IOConnectionInfo GetParentPath(IOConnectionInfo ioc);

		/// <summary>
		/// returns the file path of the file "filename" in the folderPath.
		/// </summary>
		/// The method may throw FileNotFoundException or not in case the file doesn't exist.
		IOConnectionInfo GetFilePath(IOConnectionInfo folderPath, string filename);
	}

	public interface IWriteTransaction: IDisposable
	{
		Stream OpenFile();
		void CommitWrite();
	}

	public class FileStorageSelectionInfo
	{
		public enum FileStorageSelectionMessageType
		{
			Info,  //show only ok button
			CancellableInfo, //show Ok/Cancel
			Error //show cancel only
		}

		public UiStringKey SelectionMessage { get; set; }
		public FileStorageSelectionMessageType MessageType { get; set; }
	}

	/// <summary>
	/// Can be implemented by IFileStorage implementers to add additional information for the 
	/// process of selecting the file storage
	/// </summary>
	public interface IFileStorageSelectionInfoProvider
	{
		FileStorageSelectionInfo TryGetSelectionInfo(string protocolId);
	}
}