# KeeShare Implementation Guide

## Overview

KeeShare for Keepass2Android enables sharing password groups between databases with full support for Export, Import, and Synchronize modes.

**Key Features:**
- Native UI configuration (no KeePassXC required)
- Device-specific file paths
- Optional signature verification (RSA-2048 + SHA-256)
- Password-protected shared databases
- Automatic sync on database open/save
- Fully compatible with KeePassXC

---

## Quick Start

### Creating a KeeShare Group

1. **Long-press** a group → Select **"Edit KeeShare..."**
2. Check **"Enable KeeShare for this group"**
3. Choose **Type**: Export / Import / Synchronize
4. Select **File Path** and optional **Password**
5. Tap **OK** (saves automatically)

### Configuring Device-Specific Paths

For groups configured on other devices:

1. **Settings** → **Database** → **Configure KeeShare groups**
2. Select group → **"Configure path"** → Choose file location
3. **"Sync now"** to test

---

## Share Types

### Export
- **Purpose:** Share your entries with others
- **Behavior:** Exports group contents to file on database save
- **Use case:** You maintain shared credentials (team/family passwords)

### Import
- **Purpose:** Receive entries from external file
- **Behavior:** Replaces group contents on database open
- **Warning:** ⚠️ Local changes are lost - group is read-only
- **Use case:** Consume credentials maintained by someone else

### Synchronize
- **Purpose:** Two-way sync between databases
- **Behavior:** Exports on save, imports on open, merges intelligently
- **Conflict resolution:** Newer entry wins, history preserved
- **Use case:** Multiple people editing same shared group

---

## CustomData Properties

### Core Properties
| Property | Values | Description |
|----------|--------|-------------|
| KeeShare.Active | "true"/"false" | Enable/disable KeeShare |
| KeeShare.Type | Export/Import/Synchronize | Share mode |
| KeeShare.FilePath | /path/to/file.kdbx | Shared file location |
| KeeShare.Password | string | Optional password for shared file |

### Device-Specific Path
```
KeeShare.FilePath.{DeviceId} = "/device/specific/path.kdbx"
```
Overrides global `KeeShare.FilePath` for this device only.

---

## Advanced Features

### Signature Verification

Add to CustomData:
```
KeeShare.TrustedCertificate = <base64-encoded public key (PEM or DER)>
```

**Security Modes:**
- No certificate → Imports without verification
- Certificate + signature → Verifies before import
- Certificate but no signature → Blocks import

**Creating Signed Shares:**
```bash
# Generate keys
openssl genrsa -out private.pem 2048
openssl rsa -in private.pem -pubout -out public.pem

# Sign and package
openssl dgst -sha256 -sign private.pem -out shared.sig shared.kdbx
base64 shared.sig > shared.sig.b64
zip shared.zip shared.kdbx shared.sig.b64

# Export public key for configuration
openssl rsa -pubin -in public.pem -outform DER | base64
```

### Relative Paths

Supported for both local and remote databases:
```
KeeShare.FilePath = "../shared/team.kdbx"      # Parent directory
KeeShare.FilePath = "subfolder/import.kdbx"    # Subdirectory
KeeShare.FilePath = "team.kdbx"                # Same directory
```

### Read-Only Import Groups

Import groups are automatically read-only to prevent data loss.

---

## How It Works

### Database Open (Import/Synchronize)
1. Scans for active KeeShare groups
2. Resolves file paths (device-specific or global)
3. Opens shared files (handles ZIP/KDBX, verifies signatures)
4. Merges: Import clears then replaces; Synchronize intelligently merges (newer wins, preserves history)

### Database Save (Export/Synchronize)
1. Scans for Export/Synchronize groups
2. Creates new KDBX with group contents
3. Saves to configured path with optional password

---

## API Reference

### Methods

```csharp
// Enable KeeShare on a group
KeeShare.EnableKeeShare(PwGroup group, string type, string filePath, string password = null)

// Update configuration
KeeShare.UpdateKeeShareConfig(PwGroup group, string type, string filePath, string password)

// Disable KeeShare
KeeShare.DisableKeeShare(PwGroup group)

// Set device-specific path
KeeShare.SetDeviceFilePath(PwGroup group, string path)

// Get effective path for this device
string path = KeeShare.GetEffectiveFilePath(PwGroup group)
```

**Example:**
```csharp
KeeShare.EnableKeeShare(myGroup, "Export", "/sdcard/shared.kdbx", "password123");
```

---

## Testing

### Unit Tests
```bash
cd src/KeeShare.Tests && dotnet test
```
15 tests covering signature verification, group detection, and edge cases.

### Manual Testing

**Quick Tests:**
1. **Export**: Enable on group → Add entries → Save → Verify file created
2. **Import**: Create shared KDBX → Configure group → Reopen DB → Verify imported
3. **Synchronize**: Configure → Import → Add entry → Save → Verify exported
4. **Signatures**: Create signed ZIP → Configure certificate → Verify or fail appropriately

**Check Logs:**
```bash
adb logcat | grep -i keeshare
```

---

## Compatibility

### KeePassXC
**Full interoperability:** Same CustomData format, same file format. Configure in either app, use in both.

**File Format Support:**
| Format | KP2A | KeePassXC |
|--------|------|-----------|
| Plain .kdbx | ✓ Full | ✓ Full |
| ZIP containers | ✓ Import | ✓ Full |
| Signed ZIP | ✓ Import | ✓ Full |

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Menu not appearing | Long-press **group** (not entry), option appears when single group selected |
| Import not working | Check file path, permissions, password; reopen database; check logs: `adb logcat \| grep -i keeshare` |
| Export not creating files | Verify type is Export/Synchronize, path is writable, permissions granted |
| Signature fails | Check public key format, .sig file in ZIP, database not modified after signing |
| Performance slow | Use local files, reduce database size, disable unused groups |

---

## Security Notes

**Signature Verification (Optional):**
- RSA-2048 + SHA-256 for secure imports
- Protects against tampering and unauthorized modifications
- No protection against compromised keys or replay attacks

**Best Practices:**
- Use signatures for sensitive data
- Strong passwords on shared databases
- Verify public keys through separate channel
- Monitor logs for failures
- Limit shared data to necessary entries

---

## References

- **KeePassXC KeeShare**: https://github.com/keepassxreboot/keepassxc/blob/develop/docs/topics/KeeShare.adoc
- **KDBX Format**: https://keepass.info/help/kb/kdbx_4.html
- **Original Issue**: https://github.com/PhilippC/keepass2android/issues/839

---

**Version:** 2.0 | **Date:** 2025-01-09 | **License:** GPLv3
