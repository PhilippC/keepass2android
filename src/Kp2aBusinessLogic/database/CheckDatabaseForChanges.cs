using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Android.App;
using Android.Content;
using KeePassLib.Cryptography;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using keepass2android.Io;

namespace keepass2android
{
	public class CheckDatabaseForChanges: RunnableOnFinish
	{
		private readonly Context _context;
		private readonly IKp2aApp _app;


		public CheckDatabaseForChanges(Context context, IKp2aApp app, OnFinish finish)
			: base(finish)
		{
			_context = context;
			_app = app;
		}

		public override void Run()
		{
			try
			{
				IOConnectionInfo ioc = _app.GetDb().Ioc;
				IFileStorage fileStorage = _app.GetFileStorage(ioc);
				if (fileStorage is CachingFileStorage)
				{
					throw new Exception("Cannot sync a cached database!");
				}
				StatusLogger.UpdateMessage(UiStringKey.CheckingDatabaseForChanges);
				
				//download file from remote location and calculate hash:
				StatusLogger.UpdateSubMessage(_app.GetResourceString(UiStringKey.DownloadingRemoteFile));
				

				MemoryStream remoteData = new MemoryStream();
				using (
					HashingStreamEx hashingRemoteStream = new HashingStreamEx(fileStorage.OpenFileForRead(ioc), false,
																				new SHA256Managed()))
				{
					hashingRemoteStream.CopyTo(remoteData);
					hashingRemoteStream.Close();
					
					if (!MemUtil.ArraysEqual(_app.GetDb().KpDatabase.HashOfFileOnDisk, hashingRemoteStream.Hash))
					{
						_app.TriggerReload(_context);
						Finish(true);
					}
					else
					{
						Finish(true, _app.GetResourceString(UiStringKey.RemoteDatabaseUnchanged));
					}
				}
			
				
				
			}
			catch (Exception e)
			{
				Finish(false, e.Message);
			}

		}

	}
}
