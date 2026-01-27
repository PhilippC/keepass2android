# KeeShare - Password Sharing for KeePass2Android

KeeShare enables secure sharing and synchronization of password entries between KeePass databases. Originally developed for [KeePassXC](https://keepassxc.org/docs/KeePassXC_UserGuide), this feature allows you to share subsets of your passwords with family members, team members, or synchronize between your own devices.

## Table of Contents

- [Overview](#overview)
- [How It Works](#how-it-works)
- [Sharing Modes](#sharing-modes)
- [Setup Guide](#setup-guide)
  - [Step 1: Configure in KeePassXC](#step-1-configure-in-keepassxc-desktop)
  - [Step 2: Configure in KeePass2Android](#step-2-configure-device-paths-in-keepass2android)
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

## Setup Guide

KeeShare setup involves two steps:
1. **Configure the share in KeePassXC** (desktop) - set the sharing mode, file path, and password
2. **Configure device paths in KeePass2Android** - since Android file paths differ from desktop paths

### Step 1: Configure in KeePassXC (Desktop)

KeeShare configuration must be done in KeePassXC on your computer first.

1. **Open your database** in KeePassXC on your computer

2. **Enable KeeShare** in settings:
   - Go to **Tools → Settings → KeeShare**
   - Check **"Allow import"** to receive shared credentials
   - Check **"Allow export"** to share your credentials

3. **Configure a group for sharing**:
   - Right-click on the group you want to share
   - Select **"Edit Group"**
   - Go to the **"KeeShare"** tab
   - Select the sharing mode (Import, Export, or Synchronize)
   - Set the **file path** for the shared container (use a cloud-synced folder like Dropbox, Google Drive, etc.)
   - Set a **password** for the shared container

4. **Save your database** - This creates the shared container file

### Step 2: Configure Device Paths in KeePass2Android

Since file paths on Android differ from desktop paths, you need to tell KeePass2Android where to find the shared container file on your device.

#### Opening the KeeShare Configuration

1. **Open your database** in KeePass2Android

   ![Database Groups](images/02_database_groups.png)

2. **Open the menu** by tapping the three dots (⋮) in the top right

   ![Overflow Menu](images/03_overflow_menu.png)

3. **Tap "Settings"**

   ![Settings Main](images/04_settings_main.png)

4. **Tap "Database"** to access database settings

   ![Database Settings](images/05_database_settings.png)

5. **Tap "Configure KeeShare groups..."**

   ![KeeShare Groups](images/06_keeshare_groups.png)

#### Setting Device-Specific Paths

If you have groups configured for KeeShare (from KeePassXC), they will appear in the list. For each group:

1. **Tap "Configure path"** to set the Android-specific location of the shared container file

2. **Navigate to the shared file** on your device:
   - If using Dropbox: Look in `/storage/emulated/0/Dropbox/...`
   - If using Google Drive: The file may be in `/storage/emulated/0/Android/data/com.google/...`
   - If using a local folder synced via another method, navigate to that location

3. **Select the shared container file** (`.kdbx` file)

4. The path will be saved and sync will work on this device

#### Example Path Mappings

| Platform | Example Path |
|----------|--------------|
| Windows (KeePassXC) | `C:\Users\Me\Dropbox\Shared\team-passwords.kdbx` |
| macOS (KeePassXC) | `/Users/Me/Dropbox/Shared/team-passwords.kdbx` |
| Android (KeePass2Android) | `/storage/emulated/0/Dropbox/Shared/team-passwords.kdbx` |

All three paths point to the same Dropbox file, synced by the Dropbox app on each device.

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

### "No KeeShare groups found" in KeePass2Android

This means no groups in your database have KeeShare configured. You need to:
1. Open the database in KeePassXC on your computer
2. Configure KeeShare on the groups you want to share
3. Save the database
4. Re-open it in KeePass2Android

### Shared entries not appearing

1. Verify the device-specific path is correct and points to the shared container file
2. Check the password matches exactly (case-sensitive)
3. Ensure the sharing mode is set correctly (Import or Synchronize to receive)
4. Tap "Sync now" in the KeeShare configuration screen

### "File not found" errors

1. Check if the shared container file exists at the specified path
2. On Android, verify the app has storage permissions
3. For cloud storage, ensure the sync app has finished syncing the file
4. Try reconfiguring the device-specific path

### Sync conflicts

When the same entry is modified on multiple devices:
- KeeShare merges changes based on modification time
- The newer change wins
- Older data is preserved in the entry's history

### Permission issues on Android

1. Go to Android Settings → Apps → KeePass2Android → Permissions
2. Ensure Storage permission is granted
3. For Android 11+, you may need to grant "All files access" for some storage locations

### KeePassXC groups not showing in KeePass2Android

1. Make sure you saved the database after configuring KeeShare in KeePassXC
2. Close and reopen the database in KeePass2Android
3. Check that the groups have `KeeShare.Active = true` in their CustomData

## References

- [KeePassXC KeeShare Documentation](https://github.com/keepassxreboot/keepassxc/blob/develop/docs/topics/KeeShare.adoc)
- [KeePassXC User Guide](https://keepassxc.org/docs/KeePassXC_UserGuide)
- [KeePass2Android Wiki](https://github.com/PhilippC/keepass2android/wiki)
