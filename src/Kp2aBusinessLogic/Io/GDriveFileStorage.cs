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
using KeePassLib.Serialization;

namespace keepass2android.Io
{
	public class GDriveFileStorage: IFileStorage
	{
		public IEnumerable<string> SupportedProtocols { get { yield return "gdrive"; } }
		public void Delete(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
		{
			throw new NotImplementedException();
		}

		public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public Stream OpenFileForRead(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			throw new NotImplementedException();
		}

		public IFileStorageSetup RequiredSetup { get; private set; }

		public bool CompleteIoId()
		{
			throw new NotImplementedException();
		}

		public bool? FileExists()
		{
			throw new NotImplementedException();
		}

		public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public bool RequiresCredentials(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public void CreateDirectory(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public IEnumerable<FileDescription> ListContents(IOConnectionInfo convertPathToIoc)
		{
			throw new NotImplementedException();
		}
	}
}