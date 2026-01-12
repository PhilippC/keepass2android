using System;
using System.Threading.Tasks;

namespace keepass2android.KeeShare
{
    /// <summary>
    /// Result of a user's trust decision for an untrusted signer
    /// </summary>
    public enum TrustDecision
    {
        /// <summary>Trust this signer permanently (add to trusted keys)</summary>
        TrustPermanently,
        /// <summary>Trust this signer for this session only</summary>
        TrustOnce, 
        /// <summary>Reject this signer (do not import)</summary>
        Reject,
        /// <summary>User cancelled the dialog</summary>
        Cancel
    }

    /// <summary>
    /// Information about an untrusted signer presented to the user
    /// </summary>
    public class UntrustedSignerInfo
    {
        /// <summary>Name of the signer from the signature file</summary>
        public string SignerName { get; set; }
        
        /// <summary>SHA-256 fingerprint of the public key (hex, lowercase)</summary>
        public string KeyFingerprint { get; set; }
        
        /// <summary>Path to the share file</summary>
        public IOConnectionInfo ShareLocation { get; set; }
        
        /// <summary>
        /// Get a formatted fingerprint for display (e.g., "AB:CD:EF:12:...")
        /// </summary>
        public string FormattedFingerprint
        {
            get
            {
                if (string.IsNullOrEmpty(KeyFingerprint) || KeyFingerprint.Length < 2)
                    return KeyFingerprint;
                    
                // Format as colon-separated pairs for readability
                var result = new System.Text.StringBuilder();
                for (int i = 0; i < KeyFingerprint.Length; i += 2)
                {
                    if (i > 0) result.Append(':');
                    result.Append(KeyFingerprint.Substring(i, Math.Min(2, KeyFingerprint.Length - i)).ToUpperInvariant());
                }
                return result.ToString();
            }
        }
    }

    /// <summary>
    /// Interface for handling user prompts during KeeShare import.
    /// Implement this in the Android UI layer to show dialogs to the user.
    /// </summary>
    public interface IKeeShareUserInteraction
    {
        /// <summary>
        /// Prompt the user to trust an unknown signer.
        /// Called when a share file is signed by a key not in the trusted store.
        /// </summary>
        /// <param name="signerInfo">Information about the untrusted signer</param>
        /// <returns>User's trust decision</returns>
        Task<TrustDecision> PromptTrustDecisionAsync(UntrustedSignerInfo signerInfo);
        
        /// <summary>
        /// Notify the user that imports were completed.
        /// Called after CheckAndImport finishes processing all shares.
        /// </summary>
        /// <param name="results">List of import results</param>
        void NotifyImportResults(System.Collections.Generic.List<KeeShareImportResult> results);
        
        /// <summary>
        /// Check if auto-import is enabled in user preferences.
        /// If false, shares will not be imported automatically on database load.
        /// </summary>
        bool IsAutoImportEnabled { get; }
    }
    
    /// <summary>
    /// Default implementation that rejects all untrusted signers (no UI).
    /// Use this as a fallback when no UI handler is registered.
    /// </summary>
    public class DefaultKeeShareUserInteraction : IKeeShareUserInteraction
    {
        public Task<TrustDecision> PromptTrustDecisionAsync(UntrustedSignerInfo signerInfo)
        {
            // No UI available - reject by default for security
            Kp2aLog.Log($"KeeShare: No UI handler registered. Rejecting untrusted signer '{signerInfo?.SignerName}'");
            return Task.FromResult(TrustDecision.Reject);
        }
        
        public void NotifyImportResults(System.Collections.Generic.List<KeeShareImportResult> results)
        {
            // No UI - just log
            if (results == null) return;
            foreach (var result in results)
            {
                if (result.IsSuccess)
                    Kp2aLog.Log($"KeeShare: Imported {result.EntriesImported} entries from {result.ShareLocation?.GetDisplayName()}");
            }
        }
        
        public bool IsAutoImportEnabled => true; // Default to enabled for backward compatibility
    }
}
