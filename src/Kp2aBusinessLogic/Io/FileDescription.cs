using System;

namespace keepass2android.Io
{
	public class FileDescription
	{
		public string Path { get; set; }
		public bool IsDirectory { get; set; }
		public DateTime LastModified { get; set; }
		public bool CanRead { get; set; }
		public bool CanWrite { get; set; }
		public long SizeInBytes { get; set; }

		public String DisplayName { get; set; }
	}
}