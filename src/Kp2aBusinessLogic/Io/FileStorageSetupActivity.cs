using System;
using System.Collections.Generic;
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
	public interface IFileStorageSetupActivity
	{
		IOConnectionInfo Ioc { get; }
		String ProcessName { get; }
		bool IsForSave { get; }
		Bundle State { get; }
	}
}