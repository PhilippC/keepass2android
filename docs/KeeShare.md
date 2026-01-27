# KeeShare - Password Sharing for KeePass2Android

KeeShare enables secure sharing and synchronization of password entries between KeePass databases. Originally developed for [KeePassXC](https://keepassxc.org/docs/KeePassXC_UserGuide), this feature allows you to share subsets of your passwords with family members, team members, or synchronize between your own devices.

## Table of Contents

- [Overview](#overview)
- [How It Works](#how-it-works)
- [Sharing Modes](#sharing-modes)
- [Step-by-Step Setup](#step-by-step-setup)
- [Device-Specific Paths](#device-specific-paths)
- [KeePassXC Compatibility](#keepassxc-compatibility)
- [Security Considerations](#security-considerations)
- [Troubleshooting](#troubleshooting)

## Overview

KeeShare allows you to:

- **Share passwords** with other users via a shared container file
- **Synchronize entries** between multiple devices or databases
- **Import credentials** from shared databases created by others
- **Export credentials** to share with team members or family

Sharing is configured at the **group level** - when you enable KeeShare on a group, all entries within that group (and its subgroups) are included in the share.

> **Warning:** If you enable sharing on the root group, every password in your database will be shared!

## How It Works

```
┌─────────────────┐         ┌──────────────────┐         ┌─────────────────┐
│  Your Database  │ ──────► │ Shared Container │ ◄────── │ Other Database  │
│                 │         │   (.kdbx file)   │         │                 │
│  ┌───────────┐  │         │                  │         │  ┌───────────┐  │
│  │ Shared    │  │ Export  │  Stored on:      │ Import  │  │ Shared    │  │
│  │ Group     │──┼────────►│  - Cloud storage │◄────────┼──│ Group     │  │
│  │           │  │         │  - Network share │         │  │           │  │
│  └───────────┘  │         │  - Local folder  │         │  └───────────┘  │
└─────────────────┘         └──────────────────┘         └─────────────────┘
```

1. **Export**: Your shared group is written to a separate encrypted `.kdbx` file
2. **Storage**: The container file is stored in a location accessible to all parties (cloud storage, network share, etc.)
3. **Import**: Other databases read and merge entries from the shared container
4. **Synchronize**: Two-way sync keeps all databases up to date

## Sharing Modes

| Mode | Description | Use Case |
|------|-------------|----------|
| **Inactive** | Sharing disabled for this group | Temporarily pause sharing |
| **Import** | Read-only; pulls changes from shared file | Receive credentials from others |
| **Export** | Write-only; pushes changes to shared file | Share credentials with others |
| **Synchronize** | Two-way; both imports and exports | Keep multiple devices in sync |

### Mode Selection Guide

- **Personal sync between devices**: Use `Synchronize` on all devices
- **Team password sharing (one admin)**: Admin uses `Export`, team uses `Import`
- **Family sharing (equal access)**: Everyone uses `Synchronize`

## Step-by-Step Setup

### Step 1: Create a Shared Group

1. Open your KeePass database in KeePass2Android
2. Create a new group (or use an existing one) for the passwords you want to share
3. Move or create the entries you want to share into this group

### Step 2: Configure KeeShare Settings

1. Open the **menu** (three dots) → **Settings**
2. Tap **Database** → **Configure KeeShare groups...**
3. You'll see a list of groups that can be configured for sharing

### Step 3: Set Up Export/Synchronize

To share passwords with others:

1. Select the group you want to share
2. Set **Type** to `Export` or `Synchronize`
3. Set the **File Path** to a location accessible to all parties:
   - Cloud storage folder (Dropbox, Google Drive, OneDrive, etc.)
   - Network share
   - Local folder that syncs across devices
4. Set a **Password** for the shared container
   > Use a strong, unique password - share it securely with recipients

5. Save the settings

### Step 4: Set Up Import (Recipients)

For users who will receive shared passwords:

1. Open the KeeShare configuration
2. Create a group (or use an existing one) to receive the shared entries
3. Set **Type** to `Import` or `Synchronize`
4. Set the **File Path** to the same location as the exporter
5. Enter the same **Password** used by the exporter
6. Save the settings

### Step 5: Initial Sync

- On **Export**: Save your database to create the initial shared container
- On **Import**: The shared entries will be merged when you open/save the database
- On **Synchronize**: Both operations occur automatically

## Device-Specific Paths

Since file paths can differ between devices (e.g., `/storage/emulated/0/Dropbox/` on one phone vs. `/sdcard/Dropbox/` on another), KeePass2Android supports **device-specific paths**.

This means each device can have its own path to the shared container file, while all pointing to the same file through cloud sync.

### How to Configure

When you set up KeeShare on a new device:

1. Go to **Settings** → **Database** → **Configure KeeShare groups...**
2. Find groups that show "Groups created in KeePassXC may need a device-specific file path configured here"
3. Tap the group and set the **local path** that points to the shared file on this device

### Example

- **PC (KeePassXC)**: `C:\Users\Me\Dropbox\Shared\team-passwords.kdbx`
- **Android Phone 1**: `/storage/emulated/0/Dropbox/Shared/team-passwords.kdbx`
- **Android Phone 2**: `/sdcard/Android/data/com.dropbox.android/files/Shared/team-passwords.kdbx`

All three paths point to the same Dropbox file, synced by the Dropbox app on each device.

## KeePassXC Compatibility

KeePass2Android is compatible with KeeShare groups created in KeePassXC. The app automatically detects and supports multiple configuration formats:

- **KeePassXC native format** (`KeeShare.Active`, `KeeShare.Type`, etc.)
- **Alternative formats** (`KeeShareReference.Path`, `KPXC_KeeShare_Path`, etc.)

### Migrating from KeePassXC

If you created KeeShare groups in KeePassXC:

1. Open the database in KeePass2Android
2. Go to **Settings** → **Database** → **Configure KeeShare groups...**
3. Groups with KeePassXC configuration will be listed
4. Set the **device-specific path** for each group to point to the shared file on your Android device

## Security Considerations

### Password Protection

- The shared container file (`.kdbx`) is encrypted with the password you specify
- Use a **strong, unique password** different from your main database password
- Share the password securely (in person, encrypted message, etc.)
- Consider this password equally sensitive as your main database password

### Storage Location

- Choose a storage location you trust (your own cloud account, private network share)
- Shared container files contain real passwords - treat them with the same security as your main database
- Ensure cloud storage accounts have strong authentication (2FA recommended)

### Access Control

- Use `Import` mode for users who should only read passwords, not modify them
- Use `Export` mode to share without receiving changes from others
- Use `Synchronize` only when two-way sync is truly needed

### Encryption

- Shared containers use the same strong encryption as regular KeePass databases
- AES-256 encryption by default
- Key derivation function (Argon2) protects against brute-force attacks

## Troubleshooting

### Shared entries not appearing

1. Verify the file path is correct and the file exists
2. Check the password matches exactly (case-sensitive)
3. Ensure the sharing mode is set correctly (Import or Synchronize to receive)
4. Save and reopen the database to trigger sync

### "File not found" errors

1. Check if the shared container file exists at the specified path
2. On Android, verify the app has storage permissions
3. For cloud storage, ensure the sync app has finished syncing
4. Try setting a device-specific path

### Sync conflicts

When the same entry is modified on multiple devices:
- KeeShare merges changes based on modification time
- The newer change wins
- Older data is preserved in the entry's history

### KeePassXC groups not recognized

1. Go to **Configure KeeShare groups...**
2. The app should auto-detect KeePassXC format groups
3. Set the device-specific path for Android access

### Permission issues on Android

1. Go to Android Settings → Apps → KeePass2Android → Permissions
2. Ensure Storage permission is granted
3. For Android 11+, you may need to grant "All files access" for some storage locations

## References

- [KeePassXC KeeShare Documentation](https://github.com/keepassxreboot/keepassxc/blob/develop/docs/topics/KeeShare.adoc)
- [KeePassXC User Guide](https://keepassxc.org/docs/KeePassXC_UserGuide)
- [KeePass2Android Wiki](https://github.com/PhilippC/keepass2android/wiki)
