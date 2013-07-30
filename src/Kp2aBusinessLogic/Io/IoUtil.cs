using System;
using System.Collections.Generic;
using System.Text;
using Java.IO;

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


	}
}
