using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Android.Content;
using Android.OS;
using Java.IO;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace keepass2android.Io
{
	public static class IoUtil
	{

		public static bool TryTakePersistablePermissions(ContentResolver contentResolver, Android.Net.Uri uri)
		{
			if ((int)Build.VERSION.SdkInt >= 19)
			{
				//try to take persistable permissions
				try
				{
					Kp2aLog.Log("TakePersistableUriPermission");
					var takeFlags = (ActivityFlags.GrantReadUriPermission
							| ActivityFlags.GrantWriteUriPermission);
					contentResolver.TakePersistableUriPermission(uri, takeFlags);
					return true;
				}
				catch (Exception e)
				{
					Kp2aLog.Log(e.ToString());
				}

			}
			return false;
		}
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

        //creates a local ioc where the sourceIoc can be stored to
	    public static IOConnectionInfo GetInternalIoc(IOConnectionInfo sourceIoc, Context ctx)
	    {
	        Java.IO.File internalDirectory = IoUtil.GetInternalDirectory(ctx);
	        string targetPath = UrlUtil.GetFileName(sourceIoc.Path);
	        targetPath = targetPath.Trim("|\\?*<\":>+[]/'".ToCharArray());
	        if (targetPath == "")
	            targetPath = "internal";
	        if (new File(internalDirectory, targetPath).Exists())
	        {
	            int c = 1;
	            var ext = UrlUtil.GetExtension(targetPath);
	            var filenameWithoutExt = UrlUtil.StripExtension(targetPath);
	            do
	            {
	                c++;
	                targetPath = filenameWithoutExt + c;
	                if (!String.IsNullOrEmpty(ext))
	                    targetPath += "." + ext;
	            } while (new File(internalDirectory, targetPath).Exists());
	        }
	        return IOConnectionInfo.FromPath(new File(internalDirectory, targetPath).CanonicalPath);
        }

	    public static IOConnectionInfo ImportFileToInternalDirectory(IOConnectionInfo sourceIoc, Context ctx, IKp2aApp app)
	    {
	        var targetIoc = GetInternalIoc(sourceIoc, ctx);


            IoUtil.Copy(targetIoc, sourceIoc, app);
	        return targetIoc;
	    }

	    public static string GetIocPrefKey(IOConnectionInfo ioc, string suffix)
	    {
	        var iocAsHexString = IocAsHexString(ioc);

	        return "kp2a_ioc_key_" + iocAsHexString + suffix;
	    }


	    public static string IocAsHexString(IOConnectionInfo ioc)
	    {
	        SHA256Managed sha256 = new SHA256Managed();
	        string iocAsHexString =
	            MemUtil.ByteArrayToHexString(sha256.ComputeHash(Encoding.Unicode.GetBytes(ioc.Path.ToCharArray())));
	        return iocAsHexString;
	    }
    }
}
