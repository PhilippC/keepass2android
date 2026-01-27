# KeePass2Android E2E Tests

End-to-end tests using [Maestro](https://maestro.mobile.dev/) for testing the KeePass2Android app.

## Prerequisites

1. Install Maestro:
   ```bash
   curl -Ls "https://get.maestro.mobile.dev" | bash
   ```

2. Have an Android emulator running or physical device connected:
   ```bash
   # List devices
   adb devices

   # Start emulator (example)
   emulator -avd Pixel_8_API_35
   ```

3. Install the app APK:
   ```bash
   adb install path/to/keepass2android.apk
   ```

## Running Tests

Run all tests:
```bash
cd e2e-tests
maestro test .maestro/
```

Run a specific test:
```bash
maestro test .maestro/keeshare_full_test.yaml
```

## Test Files

### Core Tests
| File | Description |
|------|-------------|
| `1_launch_app.yaml` | Basic app launch and changelog dismissal |
| `2_create_database.yaml` | Create a new test database with password |
| `3_unlock_and_navigate.yaml` | Unlock existing database |

### KeeShare Tests
| File | Description |
|------|-------------|
| `keeshare_import_flow.yaml` | **Complete KeeShare import workflow** - Creates config, sets password, syncs |
| `keeshare_view_imported_entries.yaml` | **Display bug regression test** - Verifies imported entries can be viewed |
| `keeshare_edit_password.yaml` | Tests editing KeeShare password via Edit dialog |
| `keeshare_wrong_password.yaml` | Tests error handling for incorrect passwords |
| `keeshare_full_test.yaml` | Basic KeeShare configuration screen test |

## Notes

- The app uses `FLAG_SECURE` for password protection, so screenshots will appear black
- Maestro can still interact with UI elements despite the secure flag
- Test databases are in `test-data/` directory

## KeeShare Testing

### Test Databases

Located in `test-data/`:
- `keeshare-test-main.kdbx` - Main database (password: `test123`)
- `keeshare-test-export.kdbx` - KeeShare export file (password: `share123`)

### Running KeeShare Tests

```bash
# Push test files to emulator
adb push e2e-tests/test-data/keeshare-test-main.kdbx /sdcard/Download/
adb push e2e-tests/test-data/keeshare-test-export.kdbx /sdcard/Download/

# Clear app data and run test
adb shell pm clear keepass2android.keepass2android_nonet
maestro test e2e-tests/.maestro/keeshare_import_flow.yaml
```

### What the Tests Verify

1. **keeshare_import_flow.yaml**:
   - Opens test database
   - Navigates to KeeShare configuration
   - Creates new KeeShare import configuration
   - Sets password via Edit dialog
   - Triggers sync and verifies completion

2. **keeshare_view_imported_entries.yaml**:
   - Complete import flow (same as above)
   - Navigates back to database view
   - Opens the imported "Shared Passwords" group
   - **Verifies "Test Service Account" entry is visible** (regression test for display bug)

### Recreating Test Databases

If tests fail with stale keeshare groups, recreate the main database:
```bash
rm e2e-tests/test-data/keeshare-test-main.kdbx
echo -e "test123\ntest123" | keepassxc-cli db-create -p e2e-tests/test-data/keeshare-test-main.kdbx
echo "test123" | keepassxc-cli add -u testuser -p e2e-tests/test-data/keeshare-test-main.kdbx "Sample Entry" <<< "testpass"
```
