using System;
using Android.App;
using KeePassLib.Serialization;
using keepass2android.addons.OtpKeyProv;

namespace keepass2android
{
	public class OtpAuxCacheSupervisor : OtpAuxCachingFileStorage.IOtpAuxCacheSupervisor
	{
		private readonly Kp2aApp _app;

		public OtpAuxCacheSupervisor(Kp2aApp app)
		{
			_app = app;
		}

		public void CouldntSaveToRemote(IOConnectionInfo ioc, Exception ex)
		{
			_app.CouldntSaveToRemote(ioc, ex);
		}

		public void CouldntOpenFromRemote(IOConnectionInfo ioc, Exception ex)
		{
			_app.CouldntOpenFromRemote(ioc, ex);
		}

		public void UpdatedCachedFileOnLoad(IOConnectionInfo ioc)
		{
			_app.ShowToast(Application.Context.GetString(Resource.String.UpdatedCachedFileOnLoad,
			                                             new Java.Lang.Object[] { Application.Context.GetString(Resource.String.otp_aux_file) }));
		}

		public void UpdatedRemoteFileOnLoad(IOConnectionInfo ioc)
		{
			_app.UpdatedRemoteFileOnLoad(ioc);
		}

		public void NotifyOpenFromLocalDueToConflict(IOConnectionInfo ioc)
		{
			//must not be called . Conflicts should be resolved.
			throw new InvalidOperationException();
		}

		public void LoadedFromRemoteInSync(IOConnectionInfo ioc)
		{
			_app.LoadedFromRemoteInSync(ioc);
		}

		public void ResolvedCacheConflictByUsingRemote(IOConnectionInfo ioc)
		{
			_app.ShowToast(Application.Context.GetString(Resource.String.ResolvedCacheConflictByUsingRemoteOtpAux));
		}

		public void ResolvedCacheConflictByUsingLocal(IOConnectionInfo ioc)
		{
			_app.ShowToast(Application.Context.GetString(Resource.String.ResolvedCacheConflictByUsingLocalOtpAux));
		}
	}
}