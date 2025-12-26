using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using KeePassLib;

namespace keepass2android.KeeShare
{
    /// <summary>
    /// Manages trusted public keys for KeeShare signature verification.
    /// Stores trusted keys in the database's CustomData so they persist with the database.
    /// </summary>
    public class KeeShareTrustSettings
    {
        private const string TrustedKeysCustomDataKey = "KeeShare.TrustedKeys";
        
        /// <summary>
        /// Represents a trusted public key entry
        /// </summary>
        public class TrustedKey
        {
            public string KeyFingerprint { get; set; }
            public string Signer { get; set; }
            public DateTime TrustedSince { get; set; }
        }
        
        private readonly PwDatabase _database;
        private readonly List<TrustedKey> _trustedKeys;
        
        public KeeShareTrustSettings(PwDatabase database)
        {
            _database = database;
            _trustedKeys = LoadTrustedKeys();
        }
        
        /// <summary>
        /// Checks if a public key is trusted
        /// </summary>
        /// <param name="keyFingerprint">SHA-256 fingerprint of the public key</param>
        /// <returns>True if the key is in the trusted list</returns>
        public bool IsKeyTrusted(string keyFingerprint)
        {
            if (string.IsNullOrEmpty(keyFingerprint))
                return false;
                
            return _trustedKeys.Any(k => 
                string.Equals(k.KeyFingerprint, keyFingerprint, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Adds a public key to the trusted list
        /// </summary>
        /// <param name="keyFingerprint">SHA-256 fingerprint of the public key</param>
        /// <param name="signer">Name of the signer (for display purposes)</param>
        public void TrustKey(string keyFingerprint, string signer)
        {
            if (string.IsNullOrEmpty(keyFingerprint))
                return;
                
            if (IsKeyTrusted(keyFingerprint))
                return; // Already trusted
                
            _trustedKeys.Add(new TrustedKey
            {
                KeyFingerprint = keyFingerprint,
                Signer = signer ?? "Unknown",
                TrustedSince = DateTime.UtcNow
            });
            
            SaveTrustedKeys();
        }
        
        /// <summary>
        /// Removes a public key from the trusted list
        /// </summary>
        /// <param name="keyFingerprint">SHA-256 fingerprint of the public key to remove</param>
        public void UntrustKey(string keyFingerprint)
        {
            _trustedKeys.RemoveAll(k => 
                string.Equals(k.KeyFingerprint, keyFingerprint, StringComparison.OrdinalIgnoreCase));
            SaveTrustedKeys();
        }

        /// <summary>
        /// Clears all trusted keys
        /// </summary>
        public void ClearAllTrustedKeys()
        {
            _trustedKeys.Clear();
            SaveTrustedKeys();
        }
        
        /// <summary>
        /// Gets all trusted keys
        /// </summary>
        public IReadOnlyList<TrustedKey> GetTrustedKeys()
        {
            return _trustedKeys.AsReadOnly();
        }
        
        /// <summary>
        /// Calculates the SHA-256 fingerprint of an RSA public key
        /// </summary>
        public static string CalculateKeyFingerprint(byte[] modulusBytes, byte[] exponentBytes)
        {
            if (modulusBytes == null || exponentBytes == null)
                return null;
                
            // Concatenate modulus and exponent for fingerprint calculation
            var keyData = new byte[modulusBytes.Length + exponentBytes.Length];
            Buffer.BlockCopy(modulusBytes, 0, keyData, 0, modulusBytes.Length);
            Buffer.BlockCopy(exponentBytes, 0, keyData, modulusBytes.Length, exponentBytes.Length);
            
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(keyData);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
        
        private List<TrustedKey> LoadTrustedKeys()
        {
            var keys = new List<TrustedKey>();
            
            try
            {
                if (_database?.CustomData == null)
                    return keys;
                    
                var data = _database.CustomData.Get(TrustedKeysCustomDataKey);
                if (string.IsNullOrEmpty(data))
                    return keys;
                    
                var xml = Encoding.UTF8.GetString(Convert.FromBase64String(data));
                var doc = XDocument.Parse(xml);
                
                foreach (var keyElem in doc.Root?.Elements("Key") ?? Enumerable.Empty<XElement>())
                {
                    keys.Add(new TrustedKey
                    {
                        KeyFingerprint = keyElem.Element("Fingerprint")?.Value,
                        Signer = keyElem.Element("Signer")?.Value,
                        TrustedSince = DateTime.TryParse(keyElem.Element("TrustedSince")?.Value, out var dt) 
                            ? dt : DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("KeeShare: Failed to load trusted keys: " + ex.Message);
            }
            
            return keys;
        }
        
        private void SaveTrustedKeys()
        {
            try
            {
                if (_database?.CustomData == null)
                    return;
                    
                var doc = new XDocument(
                    new XElement("TrustedKeys",
                        _trustedKeys.Select(k => new XElement("Key",
                            new XElement("Fingerprint", k.KeyFingerprint),
                            new XElement("Signer", k.Signer),
                            new XElement("TrustedSince", k.TrustedSince.ToString("O"))
                        ))
                    )
                );
                
                var xml = doc.ToString();
                var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
                _database.CustomData.Set(TrustedKeysCustomDataKey, data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("KeeShare: Failed to save trusted keys: " + ex.Message);
            }
        }
    }
}
