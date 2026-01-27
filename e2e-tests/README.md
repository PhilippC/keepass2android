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

## Capturing Screenshots for Documentation

### The Problem: FLAG_SECURE Blocks Screenshots

KeePass2Android uses Android's `FLAG_SECURE` window flag to prevent screenshots for security reasons. This means:

- **`adb shell screencap`** produces black images
- **Maestro's `takeScreenshot`** produces black images
- **Android's built-in screenshot** produces black images

This is intentional security behavior and cannot be bypassed from within Android.

### The Solution: Capture the macOS Emulator Window

Since `FLAG_SECURE` only blocks Android-level screenshot APIs, we can capture the emulator window itself using macOS screen capture:

```bash
# Capture the emulator window directly
screencapture -l <window_id> -x -o screenshot.png
```

The `capture_docs_screenshots.sh` script automates this by:
1. Finding the Android Emulator window ID using Python/Quartz
2. Using Maestro to navigate through the app (Maestro can interact with secure windows)
3. Capturing the emulator window at each step using `screencapture`

### Using the Screenshot Capture Script

```bash
# Run full screenshot capture for documentation
./e2e-tests/capture_docs_screenshots.sh

# Test that window capture works (captures current screen)
./e2e-tests/capture_docs_screenshots.sh --test

# Show help
./e2e-tests/capture_docs_screenshots.sh --help
```

**Prerequisites:**
- macOS (uses `screencapture` and Python/Quartz)
- Android Emulator running and visible
- Maestro installed (`~/.maestro/bin/maestro`)
- App installed with test database in Downloads (`keeshare-test-main.kdbx`, password: `test123`)

**What it captures:**
| Screenshot | Description |
|------------|-------------|
| `02_database_groups.png` | Database view after unlock |
| `03_overflow_menu.png` | Overflow menu open |
| `04_settings_main.png` | Main settings screen |
| `05_database_settings.png` | Database settings with KeeShare option |
| `06_keeshare_groups.png` | KeeShare groups list |
| `keeshare_with_fab.png` | KeeShare list with FAB visible |
| `keeshare_add_dialog.png` | Add KeeShare dialog |

### Technical Details for Future Debugging

**Getting the window ID:**
The script uses Python/Quartz to get the CGWindowID required by `screencapture -l`:

```python
import Quartz
windows = Quartz.CGWindowListCopyWindowInfo(Quartz.kCGWindowListOptionOnScreenOnly, Quartz.kCGNullWindowID)
for w in windows:
    if 'qemu' in w.get('kCGWindowOwnerName', '').lower():
        print(w.get('kCGWindowNumber'))  # This is the window ID
```

**Why osascript doesn't work:**
AppleScript's `id of window` returns a different identifier than what `screencapture -l` expects. You must use the CGWindowID from Quartz.

**Navigation flows:**
The script uses dedicated Maestro YAML files for each navigation step (in `.maestro/`):
- `open_database.yaml` - Launch app, dismiss changelog, open and unlock database
- `nav_open_overflow.yaml` - Open the three-dot menu
- `nav_to_settings.yaml` - Tap Settings
- `nav_to_database_settings.yaml` - Tap Database
- `nav_to_keeshare.yaml` - Scroll to and tap Configure KeeShare groups
- `nav_tap_fab.yaml` - Tap the floating action button

**Common issues:**
- If screenshots are black: You're using adb/Maestro screenshot, not window capture
- If navigation fails: Check Maestro flow files match current UI text/IDs
- If window not found: Ensure emulator is running and visible (not minimized)
- If wrong activity: The main activity is `crc64c98c008c0cd742cb.KeePass` (MAUI-generated)

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
