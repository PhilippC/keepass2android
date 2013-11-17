using System;
using System.Collections.Generic;
using System.Text;
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
	}
}
