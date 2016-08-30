using System;
using System.Collections.Generic;
using System.Text;
using Android.Content;
using Android.OS;
using Java.IO;
using KeePassLib.Serialization;

namespace keepass2android.Io
{
	public static class IoUtil
	{
		public static bool DeleteDir(File dir, bool contentsOnly=false)
		{
			if (dir != null && dir.IsDirectory)
			{
				String[] children = dir.List();
				for (int i = 0; i < children.Length; i++)
				{
					bool success = DeleteDir(new File(dir, children[i]));
					if (!success)
					{
						return false;
					}
				}
			}
			
			if (contentsOnly)
				return true;

			// The directory is now empty so delete it
			return dir.Delete();
		}


		public static IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
		{
			var iocParent = ioc.CloneDeep();
			if (iocParent.Path.EndsWith("/"))
				iocParent.Path = iocParent.Path.Substring(0, iocParent.Path.Length - 1);

			int slashPos = iocParent.Path.LastIndexOf("/", StringComparison.Ordinal);
			if (slashPos == -1)
				iocParent.Path = "";
			else
			{
				iocParent.Path = iocParent.Path.Substring(0, slashPos);
			}
			return iocParent;
		}

		public static bool IsInInternalDirectory(string path, Context context)
		{
			try
			{
				File filesDir = context.FilesDir.CanonicalFile;
				File noBackupDir = GetInternalDirectory(context).CanonicalFile;
				File ourFile = new File(path).CanonicalFile;
				//http://www.java2s.com/Tutorial/Java/0180__File/Checkswhetherthechilddirectoryisasubdirectoryofthebasedirectory.htm

				File parentFile = ourFile;
				while (parentFile != null)
				{
					if ((filesDir.Equals(parentFile) || noBackupDir.Equals(parentFile)))
					{
						return true;
					}
					parentFile = parentFile.ParentFile;
				}
				return false;
			}
			catch (Exception e)
			{
				Kp2aLog.LogUnexpectedError(e);
				return false;
			}
			
		}

		public static void Copy(IOConnectionInfo targetIoc, IOConnectionInfo sourceIoc, IKp2aApp app)
		{
			IFileStorage sourceStorage = app.GetFileStorage(sourceIoc, false); //don't cache source. file won't be used ever again
			IFileStorage targetStorage = app.GetFileStorage(targetIoc);

			using (
				var writeTransaction = targetStorage.OpenWriteTransaction(targetIoc,
																		  app.GetBooleanPreference(
																			  PreferenceKey.UseFileTransactions)))
			{
				using (var writeStream = writeTransaction.OpenFile())
				{
					sourceStorage.OpenFileForRead(sourceIoc).CopyTo(writeStream);
				}
				writeTransaction.CommitWrite();
			}
		}

		public static Java.IO.File GetInternalDirectory(Context ctx)
		{
			if ((int)Android.OS.Build.VERSION.SdkInt >= 21)
				return ctx.NoBackupFilesDir;
			else
				return ctx.FilesDir;
		}
	}
}
