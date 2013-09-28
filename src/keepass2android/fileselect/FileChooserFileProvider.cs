using System;
using System.Collections.Generic;
using Android.Content;
using KeePassLib.Serialization;
using Keepass2android.Kp2afilechooser;
using keepass2android.Io;

namespace keepass2android
{
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
				parentDirectory = parentDirectory.TrimEnd('/');
				App.Kp2a.GetFileStorage(parentDirectory).CreateDirectory(ConvertPathToIoc(parentDirectory + "/" + newDirName));
				return true;
			}
			catch (Exception e)
			{
				Kp2aLog.Log(e.ToString());
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
				Kp2aLog.Log(e.ToString());
				return false;
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
					fileList.Add(new FileEntry
						{
							CanRead = e.CanRead,
							CanWrite = e.CanWrite,
							IsDirectory = e.IsDirectory,
							LastModifiedTime = CSharpTimeToJava(e.LastModified),
							Path = e.Path,
							SizeInBytes = e.SizeInBytes	
						}
						);
				}
			}
			catch (Exception e)
			{
				Kp2aLog.Log(e.ToString());
			}
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
}