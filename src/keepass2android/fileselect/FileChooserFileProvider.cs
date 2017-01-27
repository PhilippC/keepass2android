using System;
using System.Collections.Generic;
using Android.Content;
using KeePassLib.Serialization;
#if !EXCLUDE_FILECHOOSER
using Keepass2android.Kp2afilechooser;
#endif
using keepass2android.Io;

namespace keepass2android
{
	#if !EXCLUDE_FILECHOOSER
	[ContentProvider(new[] { "keepass2android." + AppNames.PackagePart + ".kp2afilechooser.kp2afile" }, Exported = false)]
	public class FileChooserFileProvider : Kp2aFileProvider
	{
		/*int taskId, final String dirName,
			final boolean showHiddenFiles, final int filterMode,
			final int limit, String positiveRegex, String negativeRegex,
			final List<FileEntry> results, final boolean hasMoreFiles[]*/

		public override string Authority
		{
			get { return TheAuthority; }
		}

		public static string TheAuthority
		{
			get { return "keepass2android." + AppNames.PackagePart + ".kp2afilechooser.kp2afile"; }
		}

		protected override bool CreateDirectory(string parentDirectory, string newDirName)
		{

			try
			{
				App.Kp2a.GetFileStorage(parentDirectory).CreateDirectory(ConvertPathToIoc(parentDirectory), newDirName);
				return true;
			}
			catch (Exception e)
			{
				Kp2aLog.LogUnexpectedError(e);
				return false;
			}
		}

		private IOConnectionInfo ConvertPathToIoc(string path)
		{
			return new IOConnectionInfo() { Path = path };
		}

		protected override bool DeletePath(string path, bool recursive)
		{
			try
			{
				App.Kp2a.GetFileStorage(path).Delete(ConvertPathToIoc(path));
				return true;
			}
			catch(Exception e)
			{
				Kp2aLog.LogUnexpectedError(e);
				return false;
			}
		}

		protected override FileEntry GetFileEntry(string filename, Java.Lang.StringBuilder errorMessageBuilder)
		{
			try
			{
				return ConvertFileDescription(App.Kp2a.GetFileStorage(filename).GetFileDescription(ConvertPathToIoc(filename)));
			}
			catch (Exception e)
			{
				if (errorMessageBuilder != null)
					errorMessageBuilder.Append(e.Message);
				Kp2aLog.Log(e.ToString());
				return null;
			}
		}


		protected override void ListFiles(int taskId, string dirName, bool showHiddenFiles, int filterMode, int limit, string positiveRegex, 
			string negativeRegex, IList<FileEntry> fileList, bool[] hasMoreFiles)
		{
			try
			{
				var dirContents = App.Kp2a.GetFileStorage(dirName).ListContents(ConvertPathToIoc(dirName));
				foreach (FileDescription e in dirContents)
				{
					fileList.Add(ConvertFileDescription(e));
				}
			}
			catch (Exception e)
			{
				Kp2aLog.LogUnexpectedError(e);
			}
		}

		private FileEntry ConvertFileDescription(FileDescription e)
		{
			return new FileEntry
				{
					CanRead = e.CanRead,
					CanWrite = e.CanWrite,
					DisplayName = e.DisplayName,
					IsDirectory = e.IsDirectory,
					LastModifiedTime = CSharpTimeToJava(e.LastModified),
					Path = e.Path,
					SizeInBytes = e.SizeInBytes	
				};
		}

		private long CSharpTimeToJava(DateTime dateTime)
		{
			try
			{
				return (long)dateTime.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
			}
			catch (Exception)
			{

				return -1;
			}
			
		}
	}
#endif
}