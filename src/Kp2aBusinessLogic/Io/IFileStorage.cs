using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace keepass2android.Io
{
	/// <summary>
	/// Called as a callback from CheckForFileChangeAsync.
	/// </summary>
	/// <param name="ioc"></param>
	/// <param name="fileChanged"></param>
	public delegate void OnCheckForFileChangeCompleted(IOConnectionInfo ioc, bool fileChanged);

	/// <summary>
	/// Interface to encapsulate all access to disk or cloud.
	/// </summary>
	/// This interface might be implemented for different cloud storage providers in the future to extend the possibilities of the
	/// "built-in" IOConnection class in the Keepass-Lib. 
	/// Note that it was decided to use the IOConnectionInfo also for cloud storage (unless it turns out that this isn't possible, but 
	/// with prefixes like dropbox:// it should be). The advantage is that the database for saving recent files etc. will then work without 
	/// much work to do. Furthermore, the IOConnectionInfo seems generic info to capture all required data, even though it might be nicer to 
	/// have an IIoStorageId interface in few cases.*/
	public interface IFileStorage
	{
		IEnumerable<string> SupportedProtocols { get; } 

		/// <summary>
		/// Deletes the given file.
		/// </summary>
		void DeleteFile(IOConnectionInfo ioc);

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
		/// <returns>A string which should not be null.</returns>
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

		/// <summary>
		/// Returns an instance of an implementation of IFileStorageSetup or one of the more complex interfaces.
		/// Depending on the type returned, the caller should try to follow the interface as close as possible.
		/// Returns null if the file storage is setup or doesn't require anything to work. 
		/// </summary>
		/// This is due to different storage types requiring different workflows for authentication processes
		IFileStorageSetup RequiredSetup { get; }

		/// <summary>
		/// brings up a dialog to query credentials or something like this.
		/// </summary>
		/// <returns>true if success, false if error or cancelled by user</returns>
		bool CompleteIoId( /*in/out ioId*/);


		/// <summary>
		/// Checks whether the given file exists.
		/// </summary>
		/// <returns>true if it exists, false if not. Null if the check couldn't be performed (e.g. because no credentials available or no connection established.)</returns>
		bool? FileExists( /*ioId*/);

		string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc);
		
		/// <summary>
		/// Returns true if the the given ioc must be filled with username/password
		/// </summary>
		bool RequiresCredentials(IOConnectionInfo ioc);
	}

	/// <summary>
	/// Base interface for required setup code
	/// </summary>
	public interface IFileStorageSetup
	{
		/// <summary>
		/// call this when the user explicitly wants to use this file storage. Might require user interaction.
		/// May throw if the setup failed permanentaly.
		/// </summary>
		/// <returns>true if the setup was succesful immediately (without UI). Returns false if setup was not successful but no error occured or can be displayed.</returns>
		bool TrySetup(Activity activity);
	}

	/// <summary>
	/// Interface which can be used additionally for an IFileStorageSetup to indicate that setup must be completed in OnResume()
	/// </summary>
	public interface IFileStorageSetupOnResume
	{
		/// <summary>
		/// call this after TrySetup() returned false in the next OnResume()
		/// May throw if the setup failed permanentaly.
		/// </summary>
		/// <returns>true if setup was succesful</returns>
		bool TrySetupOnResume(Activity activity);
	}

	public interface IWriteTransaction: IDisposable
	{
		Stream OpenFile();
		void CommitWrite();
	}
}