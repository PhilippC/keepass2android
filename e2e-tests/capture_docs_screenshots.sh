#!/bin/bash
# Capture screenshots for documentation by capturing the macOS emulator window
# This bypasses Android's FLAG_SECURE which blocks adb screencap
#
# Usage: ./capture_docs_screenshots.sh
#
# Prerequisites:
#   - macOS (uses screencapture and osascript)
#   - Android Emulator running
#   - Maestro installed (~/.maestro/bin/maestro)
#   - App installed on emulator
#   - Test database file on emulator

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
MAESTRO="${MAESTRO_PATH:-$HOME/.maestro/bin/maestro}"
OUTPUT_DIR="$PROJECT_ROOT/docs/images"
TEMP_DIR=$(mktemp -d)

# App constants
APP_PACKAGE="keepass2android.keepass2android_nonet"
APP_ACTIVITY="crc64c98c008c0cd742cb.KeePass"

# Cleanup temp dir on exit
trap "rm -rf $TEMP_DIR" EXIT

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Get Android Emulator window ID (CGWindowID for screencapture -l)
get_emulator_window_id() {
    # Use Python/Quartz to get the CGWindowID
    # osascript can't get the numeric window ID that screencapture needs
    python3 -c "
import Quartz
windows = Quartz.CGWindowListCopyWindowInfo(Quartz.kCGWindowListOptionOnScreenOnly, Quartz.kCGNullWindowID)
for w in windows:
    name = w.get('kCGWindowOwnerName', '')
    title = w.get('kCGWindowName', '')
    wid = w.get('kCGWindowNumber', 0)
    # Look for the main emulator window (has a title with 'Android Emulator')
    if 'qemu' in name.lower() and 'Android Emulator' in title:
        print(wid)
        break
" 2>/dev/null
}

# Capture the emulator window
capture_window() {
    local name=$1
    local window_id

    window_id=$(get_emulator_window_id)

    if [[ -z "$window_id" || "$window_id" == "" ]]; then
        log_error "Could not find Android Emulator window. Is the emulator running?"
        return 1
    fi

    log_info "Capturing: $name (window ID: $window_id)"

    # Capture the window
    # -l <windowID> captures a specific window
    # -x suppresses the shutter sound
    # -o excludes window shadow
    if screencapture -l "$window_id" -x -o "$TEMP_DIR/$name.png" 2>/dev/null; then
        # Copy to output directory
        cp "$TEMP_DIR/$name.png" "$OUTPUT_DIR/$name.png"
        log_info "  -> Saved to $OUTPUT_DIR/$name.png"
        return 0
    else
        log_error "Failed to capture screenshot: $name"
        return 1
    fi
}

# Run a Maestro flow
run_maestro() {
    local flow=$1
    local flow_path="$SCRIPT_DIR/.maestro/$flow"

    if [[ ! -f "$flow_path" ]]; then
        log_error "Maestro flow not found: $flow_path"
        return 1
    fi

    log_info "Running Maestro flow: $flow"
    if ! "$MAESTRO" test "$flow_path" 2>&1 | grep -v "^$"; then
        log_warn "Maestro flow may have had issues, but continuing..."
    fi
}

# Wait for a moment to let UI settle
wait_for_ui() {
    local seconds=${1:-1}
    sleep "$seconds"
}

# Get list of available AVDs
get_avd_list() {
    local emulator_path="${ANDROID_HOME:-$HOME/Library/Android/sdk}/emulator/emulator"
    "$emulator_path" -list-avds 2>/dev/null | head -1
}

# Check if emulator is running
is_emulator_running() {
    adb devices 2>/dev/null | grep -q "emulator"
}

# Start the emulator if not running
start_emulator() {
    local avd_name
    avd_name=$(get_avd_list)

    if [[ -z "$avd_name" ]]; then
        log_error "No AVD found. Please create an emulator first."
        exit 1
    fi

    log_info "Starting emulator: $avd_name"
    local emulator_path="${ANDROID_HOME:-$HOME/Library/Android/sdk}/emulator/emulator"

    # Start emulator in background
    "$emulator_path" -avd "$avd_name" -no-snapshot-load &

    # Wait for emulator to boot
    log_info "Waiting for emulator to boot..."
    local max_wait=120
    local waited=0

    while ! adb shell getprop sys.boot_completed 2>/dev/null | grep -q "1"; do
        sleep 2
        waited=$((waited + 2))
        if [[ $waited -ge $max_wait ]]; then
            log_error "Emulator failed to boot within ${max_wait}s"
            exit 1
        fi
        echo -n "."
    done
    echo ""

    log_info "Emulator booted successfully"

    # Wait a bit more for the window to appear
    sleep 5
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    # Check for macOS
    if [[ "$(uname)" != "Darwin" ]]; then
        log_error "This script requires macOS (uses screencapture and osascript)"
        exit 1
    fi

    # Check for Maestro
    if [[ ! -x "$MAESTRO" ]]; then
        log_error "Maestro not found at $MAESTRO"
        log_error "Install with: curl -Ls 'https://get.maestro.mobile.dev' | bash"
        exit 1
    fi

    # Check for emulator and start if needed
    if ! is_emulator_running; then
        log_warn "No emulator detected via adb. Starting emulator..."
        start_emulator
    fi

    # Check for emulator window (wait up to 30s for it to appear)
    local window_id
    local max_wait=30
    local waited=0

    while [[ $waited -lt $max_wait ]]; do
        window_id=$(get_emulator_window_id)
        if [[ -n "$window_id" && "$window_id" != "" ]]; then
            break
        fi
        sleep 2
        waited=$((waited + 2))
        log_info "Waiting for emulator window... (${waited}s)"
    done

    if [[ -z "$window_id" || "$window_id" == "" ]]; then
        log_error "Could not find Android Emulator window"
        log_error "Make sure the emulator is running and visible"
        exit 1
    fi

    log_info "Found emulator window (ID: $window_id)"

    # Create output directory if needed
    mkdir -p "$OUTPUT_DIR"

    log_info "Prerequisites OK"
}

# Run a Maestro flow file
run_maestro_flow() {
    local flow_name=$1
    local flow_path="$SCRIPT_DIR/.maestro/$flow_name"

    if [[ ! -f "$flow_path" ]]; then
        log_warn "Flow not found: $flow_path"
        return 1
    fi

    log_info "  Running: $flow_name"
    "$MAESTRO" --no-ansi test "$flow_path" 2>&1 | grep -E "(COMPLETED|WARNED|Error|Tap|Assert)" || true
}

# Main screenshot capture sequence using Maestro for navigation
capture_all_screenshots() {
    log_info "Starting screenshot capture sequence..."
    log_info "Output directory: $OUTPUT_DIR"
    echo ""

    # Clear app state and launch fresh
    log_info "Clearing app state..."
    adb shell pm clear "$APP_PACKAGE" 2>/dev/null || true
    wait_for_ui 2

    # Use Maestro to open database - this flow handles the full navigation
    log_info "Opening database with Maestro..."
    run_maestro_flow "open_database.yaml"
    wait_for_ui 2

    # SCREENSHOT 1: Database groups view
    log_info ""
    log_info "=== Screenshot 1: Database Groups ==="
    capture_window "02_database_groups"

    # Open overflow menu
    log_info ""
    log_info "=== Screenshot 2: Overflow Menu ==="
    run_maestro_flow "nav_open_overflow.yaml"
    wait_for_ui 1
    capture_window "03_overflow_menu"

    # Navigate to Settings
    log_info ""
    log_info "=== Screenshot 3: Settings Main ==="
    run_maestro_flow "nav_to_settings.yaml"
    wait_for_ui 1
    capture_window "04_settings_main"

    # Navigate to Database settings
    log_info ""
    log_info "=== Screenshot 4: Database Settings ==="
    run_maestro_flow "nav_to_database_settings.yaml"
    wait_for_ui 1
    capture_window "05_database_settings"

    # Navigate to KeeShare groups
    log_info ""
    log_info "=== Screenshot 5 & 6: KeeShare Groups ==="
    run_maestro_flow "nav_to_keeshare.yaml"
    wait_for_ui 2
    capture_window "06_keeshare_groups"
    capture_window "keeshare_with_fab"

    # Open Add KeeShare dialog - tap the FAB
    log_info ""
    log_info "=== Screenshot 7: Add KeeShare Dialog ==="
    run_maestro_flow "nav_tap_fab.yaml"
    wait_for_ui 1
    capture_window "keeshare_add_dialog"

    echo ""
    log_info "Screenshot capture complete!"
    echo ""
    log_info "Captured screenshots:"
    ls -la "$OUTPUT_DIR"/*.png | awk '{print "  " $NF " (" $5 " bytes)"}'
}

# Alternative: Use the simpler approach (kept for backwards compatibility)
capture_with_maestro() {
    # Now the default capture_all_screenshots uses Maestro, so just call it
    capture_all_screenshots
}

# Print usage
usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Capture screenshots for KeePass2Android documentation by capturing
the macOS emulator window (bypasses FLAG_SECURE).

Options:
  -h, --help     Show this help message
  -t, --test     Test screenshot capture without navigation
  -m, --maestro  Use Maestro for navigation (experimental)

Prerequisites:
  - macOS (uses screencapture and osascript)
  - Android Emulator running and visible
  - Maestro installed
  - App installed with test database available

EOF
}

# Test mode - just capture current screen
test_capture() {
    log_info "Test mode: Capturing current emulator screen..."
    check_prerequisites
    capture_window "test_capture"
    log_info "Test capture saved to $OUTPUT_DIR/test_capture.png"
}

# Main entry point
main() {
    case "${1:-}" in
        -h|--help)
            usage
            exit 0
            ;;
        -t|--test)
            test_capture
            exit 0
            ;;
        -m|--maestro)
            check_prerequisites
            capture_with_maestro
            ;;
        *)
            check_prerequisites
            capture_all_screenshots
            ;;
    esac
}

main "$@"
