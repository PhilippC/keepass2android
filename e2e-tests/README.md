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

| File | Description |
|------|-------------|
| `1_launch_app.yaml` | Basic app launch and changelog dismissal |
| `2_create_database.yaml` | Create a new test database with password |
| `3_unlock_and_navigate.yaml` | Unlock existing database |
| `keeshare_full_test.yaml` | Full KeeShare configuration flow |

## Notes

- The app uses `FLAG_SECURE` for password protection, so screenshots will appear black
- Maestro can still interact with UI elements despite the secure flag
- The test database is created with password: `testpassword123`

## KeeShare Testing

The `keeshare_full_test.yaml` verifies:
1. App launches and unlocks successfully
2. Navigation to Settings → Database → Configure KeeShare groups works
3. KeeShare configuration screen loads correctly

To test KeeShare import/export functionality, you would need to:
1. Create a shared database file
2. Configure a group with KeeShare settings
3. Verify synchronization occurs
