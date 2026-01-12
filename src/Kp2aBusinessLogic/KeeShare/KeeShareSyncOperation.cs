using System;
using System.Collections.Generic;
using KeePassLib;
using keepass2android.Io;
using keepass2android.KeeShare;
using KeePassLib.Interfaces;

namespace keepass2android.KeeShare
{
    /// <summary>
    /// Background operation for KeeShare synchronization (import/export).
    /// </summary>
    public class KeeShareSyncOperation : OperationWithFinishHandler
    {
        private readonly PwDatabase _db;
        private readonly bool _import;
        private readonly bool _export;
        private readonly IKp2aApp _app;

        public KeeShareSyncOperation(IKp2aApp app, PwDatabase db, bool import, bool export, OnOperationFinishedHandler finishHandler = null)
            : base(app, finishHandler)
        {
            _app = app;
            _db = db;
            _import = import;
            _export = export;
        }

        public override void Run()
        {
            try
            {
                if (_db == null)
                {
                    Finish(false, "Database is null");
                    return;
                }

                if (_import)
                {
                    StatusLogger.UpdateMessage("Performing KeeShare Import...");
                    KeeShareImporter.CheckAndImport(new Database(null, _app) { KpDatabase = _db }, _app, StatusLogger);
                }

                if (_export)
                {
                    StatusLogger.UpdateMessage("Performing KeeShare Export...");
                    KeeShareExporter.CheckAndExport(_app, _db, StatusLogger);
                }

                Finish(true);
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShareSyncOperation failed: " + ex.Message);
                Finish(false, ex.Message);
            }
        }
    }
}
