# KeeShare Implementation Notes

## Overview

This implementation provides KeeShare receiving/import support for Keepass2Android,
enabling secure synchronization of shared password groups between databases.

## Fingerprint Scheme

Public keys are identified by their SHA-256 fingerprint for trust management.

### Calculation Method

```
fingerprint = SHA256(RSA_Modulus || RSA_Exponent)
```

Where:
- `RSA_Modulus` is the raw bytes of the RSA public key modulus
- `RSA_Exponent` is the raw bytes of the RSA public key exponent  
- `||` denotes concatenation
- Result is lowercase hexadecimal (64 characters)

### Display Format

For user display, fingerprints are formatted with colons:
```
AB:CD:EF:12:34:56:78:90:...
```

### Interoperability Note

This fingerprint scheme may differ from KeePassXC's implementation. If fingerprint
matching between applications is required, verify that both use the same calculation.

## Trust Model

1. **First encounter**: When a signed share is opened and the signer's key is not
   in the trusted store, the import is rejected with `SignerNotTrusted` status.

2. **Trust decision**: The UI layer should prompt the user showing:
   - Signer name (from signature file)
   - Key fingerprint (formatted for readability)
   - Option to trust permanently or reject

3. **Persistent storage**: Trusted keys are stored in the database's `CustomData`
   under the key `KeeShare.TrustedKeys` as Base64-encoded XML.

## API Usage

### Basic (no UI)
```csharp
// Rejects all untrusted signers
var results = KeeShareImporter.CheckAndImport(db, app);
```

### With UI handler
```csharp
// Prompts user for untrusted signers
var results = KeeShareImporter.CheckAndImport(db, app, myUiHandler);
```

### Implementing IKeeShareUserInteraction

```csharp
public class MyKeeShareUI : IKeeShareUserInteraction
{
    public async Task<TrustDecision> PromptTrustDecisionAsync(UntrustedSignerInfo info)
    {
        // Show dialog with info.SignerName, info.FormattedFingerprint
        // Return TrustDecision.TrustPermanently, TrustOnce, or Reject
    }
    
    public void NotifyImportResults(List<KeeShareImportResult> results)
    {
        // Show toast/notification summarizing imports
    }
    
    public bool IsAutoImportEnabled => PreferenceManager.GetAutoImport();
}
```

## KeePassLib API Dependencies

This implementation uses the following KeePassLib APIs:

| API | Location | Purpose |
|-----|----------|---------|
| `PwGroup.CloneDeep()` | PwGroup.cs:399 | Safe cloning for merge |
| `PwGroup.Entries.UCount` | PwObjectList | Entry counting |
| `PwDatabase.MergeIn()` | PwDatabase.cs | Database synchronization |
| `PwDatabase.CustomData` | PwDatabase.cs | Trust store persistence |
| `CompositeKey` | Keys/ | Share password handling |

All APIs verified to exist in KeePassLib2Android as of 2025-12-25.
