#!/bin/bash
# Capture Android emulator window screenshot on macOS
# Usage: ./capture_emulator.sh output_filename.png

OUTPUT_FILE="${1:-screenshot.png}"
OUTPUT_DIR="$(dirname "$0")/images"

mkdir -p "$OUTPUT_DIR"

WINDOW_ID=$(python3 -c "
import Quartz.CoreGraphics as CG
window_list = CG.CGWindowListCopyWindowInfo(CG.kCGWindowListOptionOnScreenOnly, CG.kCGNullWindowID)
for window in window_list:
    owner = window.get('kCGWindowOwnerName', '')
    if 'qemu' in owner.lower():
        print(window.get('kCGWindowNumber', ''))
        break
")

if [ -z "$WINDOW_ID" ]; then
    echo "Error: Could not find emulator window"
    exit 1
fi

screencapture -l"$WINDOW_ID" "$OUTPUT_DIR/$OUTPUT_FILE"
echo "Captured: $OUTPUT_DIR/$OUTPUT_FILE"
