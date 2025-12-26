using System;
using System.Collections.Generic;
using KeePassLib;
using keepass2android.KeeShare;

namespace keepass2android.KeeShare
{
    public static class KeeShareAuditLog
    {
        public enum AuditAction
        {
            ImportSuccess,
            ImportFailure,
            ExportSuccess,
            ExportFailure,
            TrustDecision,
            SignatureVerified,
            SignatureRejected
        }

        public class AuditEntry
        {
            public DateTime Timestamp { get; set; }
            public AuditAction Action { get; set; }
            public string SourcePath { get; set; }
            public string Details { get; set; }
            public string Fingerprint { get; set; }
        }

        private static List<AuditEntry> _entries = new List<AuditEntry>();

        public static void Log(AuditAction action, string path, string details, string fingerprint = null)
        {
            var entry = new AuditEntry
            {
                Timestamp = DateTime.UtcNow,
                Action = action,
                SourcePath = path,
                Details = details,
                Fingerprint = fingerprint
            };

            lock (_entries)
            {
                _entries.Add(entry);
                if (_entries.Count > 1000)
                    _entries.RemoveAt(0); // Keep last 1000
            }

            // Also log to system log for now
            Kp2aLog.Log($"[KeeShare Audit] {action}: {path} - {details}");
        }

        public static List<AuditEntry> GetEntries()
        {
            lock (_entries)
            {
                return new List<AuditEntry>(_entries);
            }
        }
    }
}
