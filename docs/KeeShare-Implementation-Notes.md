# KeeShare Implementation Notes for keepass2android

This document captures the implementation work done on KeeShare support for Android, the challenges encountered, and what remains to be done.

## Overview

KeeShare allows secure password group synchronization between KeePass databases. The goal was to implement the ability for Android-only users to create KeeShare import configurations directly from the app, rather than requiring a desktop KeePass client.

## What Was Implemented

### 1. Add KeeShare Configuration Feature

**Files Modified:**
- `src/keepass2android-app/ConfigureKeeShareActivity.cs`
- `src/keepass2android-app/Resources/layout/config_keeshare.xml` (added FAB)
- `src/keepass2android-app/Resources/layout/dialog_add_keeshare.xml` (new dialog)
- `src/keepass2android-app/Resources/values/strings.xml` (new strings)

**Features Added:**
- Floating Action Button (FAB) to add new KeeShare configurations
- "Add KeeShare" dialog with:
  - Group selection (create new or use existing)
  - Share type selection (Import/Synchronize/Export)
  - Password field for the shared file
  - Browse button to select the KeeShare file

### 2. Edit KeeShare Configuration Feature

**Files Modified:**
- `src/keepass2android-app/Resources/layout/keeshare_config_row.xml` (added Edit button)
- `src/keepass2android-app/Resources/layout/dialog_edit_keeshare.xml` (new dialog)

**Features Added:**
- Edit button on each KeeShare configuration row
- Edit dialog allowing:
  - Password updates
  - Share type changes
- Password status indicator (shows if password is configured)

### 3. Improved Error Handling

**Files Modified:**
- `src/keepass2android-app/KeeShare.cs`

**Changes:**
- User-friendly error messages for wrong password
- Clear guidance to use Edit button when password is needed

## Technical Challenges and Solutions

### Challenge 1: File Selection Flow Going Through PasswordActivity

**Problem:** When using FileSelectActivity to browse for the KeeShare file, the app would launch PasswordActivity to open the database, instead of just returning the selected file path.

**Solution:** Changed the Browse button to use Android's Storage Access Framework directly:
```csharp
Intent intent = new Intent(Intent.ActionOpenDocument);
intent.AddCategory(Intent.CategoryOpenable);
intent.SetType("*/*");
StartActivityForResult(intent, ReqCodeSelectFileForNewConfig);
```

### Challenge 2: SingleInstance LaunchMode Breaking StartActivityForResult

**Problem:** ConfigureKeeShareActivity had `LaunchMode = LaunchMode.SingleInstance`, which caused the file picker result to not be delivered properly because SingleInstance activities run in their own task.

**Solution:** Changed to `LaunchMode = LaunchMode.SingleTop`:
```csharp
[Activity(Label = "@string/keeshare_title", ... LaunchMode = LaunchMode.SingleTop, ...)]
```

### Challenge 3: Group Creation Before File Selection Causing Database Association Issues

**Problem:** When creating a new group in the Browse button handler before launching the file picker, the group would not be properly recognized by `FindDatabaseForElement` after the activity was recreated.

**Solution:** Deferred group creation to OnActivityResult:
- Store `_pendingCreateNewGroup` and `_pendingNewGroupName` flags
- Create the group only when file selection completes in OnActivityResult
- Use `SaveDatabase(db)` directly instead of `SaveGroup(group)` to avoid FindDatabaseForElement lookup

### Challenge 4: Threading Issue in Sync Callback

**Problem:** The Update() call in OnSyncNow's completion callback was being called from a background thread, causing `CalledFromWrongThreadException`.

**Solution:** Wrapped in RunOnUiThread:
```csharp
activity?.RunOnUiThread(() => activity.Update());
```

### Challenge 5: Display Bug with Imported Entries

**Problem:** After a successful KeeShare sync/import, when trying to view the entries in the imported group, the app crashes with:
```
System.Exception: Database element KeePassLib.PwUuid not found in any of 1 databases!
   at keepass2android.Kp2aApp.FindDatabaseForElement(IStructureItem element)
   at keepass2android.view.PwEntryView..ctor(...)
```

**Root Cause:** Imported/cloned entries were not registered with Kp2a's database tracking system (Elements, EntriesById, GroupsById collections).

**Solution:** Added `UpdateGlobals()` and `MarkAllGroupsAsDirty()` after MergeIn in KeeShare.cs:
```csharp
// In SyncGroups method, after MergeIn:
_app.CurrentDb.UpdateGlobals();
_app.MarkAllGroupsAsDirty();
```

**Status:** FIXED. Imported entries can now be viewed without crashing.

## Unit Tests

The KeeShare functionality has comprehensive unit tests in `src/KeeShare.Tests/`.

### Test Architecture

Due to framework incompatibility (the app targets `net9.0-android`, tests target `net8.0`), tests use a pattern of copying pure logic into test helpers to avoid Android dependencies.

**Test Helpers (`TestHelpers/`):**
| File | Purpose |
|------|---------|
| `PwGroupStub.cs` | Stub for PwGroup with CustomData, Groups, Entries |
| `PwEntryStub.cs` | Stub for PwEntry with ParentGroup |
| `PwDatabaseStub.cs` | Stub for PwDatabase with IsOpen, RootGroup |
| `KeeShareConfigLogic.cs` | Copy of configuration methods from KeeShare.cs |
| `KeeShareCompatibilityLogic.cs` | Copy of KeePassXC compatibility logic |
| `KeeShareStateLogic.cs` | Copy of state checking methods |
| `KeeShareTestHelpers.cs` | Signature verification logic |

### Running Unit Tests

```bash
cd src/KeeShare.Tests
dotnet test --verbosity normal
```

### Test Coverage (141 tests)

| Test Class | Tests | Coverage |
|------------|-------|----------|
| `ConfigurationTests.cs` | 37 | EnableKeeShare, DisableKeeShare, GetEffectiveFilePath, etc. |
| `KeePassXCCompatibilityTests.cs` | 30 | HasKeePassXCFormat, TryImportKeePassXCConfig |
| `StateCheckingTests.cs` | 39 | IsReadOnlyBecauseKeeShareImport, HasExportableKeeShareGroups |
| `KeeShareItemTests.cs` | 15 | KeeShareItem properties |
| `SignatureVerificationTests.cs` | 14 | VerifySignatureCore with various inputs |
| `HasKeeShareGroupsTests.cs` | 6 | HasKeeShareGroups recursive detection |

### Mutation Testing

To validate that tests are meaningful (actually catch bugs), we use manual mutation testing.

**What is Mutation Testing?**
Mutation testing introduces deliberate bugs (mutations) into the code and verifies that tests fail. If tests still pass after a mutation, the tests aren't catching that class of bug.

**Running Manual Mutation Tests:**

1. Introduce a mutation in a test helper file
2. Run tests
3. Verify tests fail (mutation "killed")
4. Revert the mutation

**Example Mutations and Results:**

| Mutation | Description | Result |
|----------|-------------|--------|
| Change `"true"` to `"false"` in EnableKeeShare | Sets Active to wrong value | **KILLED** (4 tests failed) |
| Remove type validation | Accept invalid types | **KILLED** (1 test failed) |
| Check "Export" instead of "Import" in IsReadOnlyBecauseKeeShareImport | Wrong type check | **KILLED** (7 tests failed) |
| HasKeePassXCFormat always returns false | Skip format detection | **KILLED** (21 tests failed) |
| GetEffectiveFilePath returns original path first | Wrong priority | **KILLED** (1 test failed) |

All mutations were killed, demonstrating the tests validate:
- Correct values being set
- Validation logic working
- Business logic (Import vs Export distinctions)
- Priority/ordering logic
- Detection algorithms

**Automated Mutation Testing with Stryker.NET:**

Stryker.NET is the standard tool for .NET mutation testing, but requires separate source and test projects. Since our logic is in test helpers (same project), Stryker can't directly mutate it.

If you restructure to have logic in a separate library:

```bash
# Install Stryker
dotnet tool install --global dotnet-stryker

# Run mutation testing
cd src/KeeShare.Tests
dotnet-stryker
```

### Keeping Test Helpers in Sync

When modifying `KeeShare.cs`, update the corresponding test helper:

| Production Code | Test Helper |
|-----------------|-------------|
| `KeeShare.cs` lines 46-195 (config) | `KeeShareConfigLogic.cs` |
| `KeeShare.cs` lines 197-308 (KeePassXC) | `KeeShareCompatibilityLogic.cs` |
| `KeeShare.cs` lines 358-542 (state) | `KeeShareStateLogic.cs` |
| `KeeShareCheckOperation.VerifySignatureCore` | `KeeShareTestHelpers.cs` |

Each helper includes a comment with the sync date - update this when syncing.

## E2E Test Files

Test database files in `e2e-tests/test-data/`:
- `keeshare-test-main.kdbx` - Password: `test123` - Main database to open
- `keeshare-test-export.kdbx` - Password: `share123` - Contains "Test Service Account" entry to import

**Important:** The main test database can accumulate KeeShare groups from previous test runs. If tests start failing with "wrong password" errors on multiple groups, recreate the main database:
```bash
# Recreate fresh main database
rm e2e-tests/test-data/keeshare-test-main.kdbx
echo -e "test123\ntest123" | keepassxc-cli db-create -p e2e-tests/test-data/keeshare-test-main.kdbx
echo "test123" | keepassxc-cli add -u testuser -p e2e-tests/test-data/keeshare-test-main.kdbx "Sample Entry" <<< "testpass"

# Push to emulator
adb push e2e-tests/test-data/keeshare-test-main.kdbx /sdcard/Download/
adb push e2e-tests/test-data/keeshare-test-export.kdbx /sdcard/Download/
```

## Maestro E2E Tests

**Location:** `e2e-tests/.maestro/`

### keeshare_import_flow.yaml
Tests the complete flow:
1. Open main test database
2. Navigate to Settings -> Database -> Configure KeeShare
3. Tap FAB to add new KeeShare
4. Enter group name
5. Browse and select export file
6. Edit to set password
7. Tap Sync now
8. Verify sync completes

**Current Status:** Test passes completely, including verifying imported entries can be viewed.

### Other test files
- `keeshare_edit_password.yaml` - Tests editing password
- `keeshare_wrong_password.yaml` - Tests error handling

## What's Working

1. **KeeShare Configuration Screen** - Shows existing configurations
2. **Add KeeShare Button (FAB)** - Opens add dialog
3. **Add KeeShare Dialog** - Group selection, type selection, password, browse
4. **File Selection** - Uses direct Android file picker (ACTION_OPEN_DOCUMENT)
5. **Edit KeeShare** - Can update password and type
6. **Sync** - Actually imports entries from the KeeShare file
7. **Error Messages** - Shows user-friendly password errors

## What's Working (All Features!)

1. **Viewing Imported Entries** - FIXED! The display bug has been resolved
   - Root cause was: `FindDatabaseForElement` didn't recognize imported entries because they weren't in the tracking collections
   - Fix: Added `UpdateGlobals()` and `MarkAllGroupsAsDirty()` after MergeIn in KeeShare.cs

## State Preservation

Added state preservation for activity recreation during file selection:
- `_pendingConfigItem` (existing)
- `_pendingNewConfigGroup`
- `_pendingNewConfigType`
- `_pendingNewConfigPassword`
- `_pendingCreateNewGroup` (new)
- `_pendingNewGroupName` (new)

## Key Files to Review

1. `src/keepass2android-app/ConfigureKeeShareActivity.cs` - Main configuration UI
2. `src/keepass2android-app/KeeShare.cs` - Core sync logic
3. `src/keepass2android-app/KeeShareCheckOperation.cs` - Sync operation
4. `src/keepass2android-app/Kp2aApp.cs` - Database tracking (`FindDatabaseForElement`)

## Completed Fixes

1. **Display Bug - FIXED:**
   - Added `UpdateGlobals()` and `MarkAllGroupsAsDirty()` after MergeIn in `KeeShare.cs` (SyncGroups method)
   - This registers imported entries in the tracking collections (Elements, EntriesById, GroupsById)
   - Imported entries can now be viewed without crashing

2. **UI Refresh Bug - FIXED:**
   - Added synchronous `db.UpdateGlobals()` call in `SaveDatabase` callback in `ConfigureKeeShareActivity.cs`
   - Newly created KeeShare groups are now visible immediately in the UI

## Next Steps

1. **Test on Real Device:**
   - Verify functionality on physical Android device
   - Test with real KeeShare files from KeePass desktop

2. **Consider Additional Tests:**
   - Test Synchronize mode (bidirectional sync)
   - Test Export mode
   - Test with signed containers

## Building and Testing

```bash
# Build Release APK
cd src
dotnet build keepass2android-app/keepass2android-app.csproj -c Release -f net9.0-android

# Install
adb install -r keepass2android-app/bin/Release/net9.0-android/keepass2android.keepass2android_nonet-Signed.apk

# Run Maestro test
cd /path/to/keepass2android
~/.maestro/bin/maestro test e2e-tests/.maestro/keeshare_import_flow.yaml

# Check logs
adb logcat -d | grep "KP2A\|KeeShare"
```

## PR Status

This work is part of the keeshare-support branch. The core "Add KeeShare" feature is fully functional:
- Add, Edit, and Sync KeeShare configurations work correctly
- Imported entries can be viewed without crashing (display bug fixed)
- E2E tests pass for the complete flow including viewing imported entries
