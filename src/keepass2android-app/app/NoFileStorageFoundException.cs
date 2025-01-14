using System;
using System.Runtime.Serialization;

namespace keepass2android
{
	[Serializable]
	public class NoFileStorageFoundException : Exception
	{
		//
		// For guidelines regarding the creation of new exception types, see
		//    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
		// and
		//    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
		//

		public NoFileStorageFoundException()
		{
		}

		public NoFileStorageFoundException(string message) : base(message)
		{
		}

		public NoFileStorageFoundException(string message, Exception inner) : base(message, inner)
		{
		}

		protected NoFileStorageFoundException(
			SerializationInfo info,
			StreamingContext context) : base(info, context)
		{
		}
	}
}