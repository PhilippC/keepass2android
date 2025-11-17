// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using System;
using Android.App;
using KeePassLib.Serialization;
using keepass2android.addons.OtpKeyProv;
using keepass2android;

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
            _app.ShowToast(LocaleManager.LocalizedAppContext.GetString(Resource.String.UpdatedCachedFileOnLoad,
                                                         new Java.Lang.Object[] { LocaleManager.LocalizedAppContext.GetString(Resource.String.otp_aux_file) }), MessageSeverity.Info);
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
            _app.ShowToast(LocaleManager.LocalizedAppContext.GetString(Resource.String.ResolvedCacheConflictByUsingRemoteOtpAux), MessageSeverity.Info);
        }

        public void ResolvedCacheConflictByUsingLocal(IOConnectionInfo ioc)
        {
            _app.ShowToast(LocaleManager.LocalizedAppContext.GetString(Resource.String.ResolvedCacheConflictByUsingLocalOtpAux), MessageSeverity.Info);
        }
    }
}