#!/usr/bin/env bash

BASE_DIR="${1}"

for arch_dir in "$BASE_DIR"/android-*/; do
  arch=$(basename "$arch_dir")
  arch=${arch#android-}
  APK_DIR="${arch_dir}publish"
  if [[ -d "$APK_DIR" ]]; then
    apk_path=$(find "$APK_DIR" -maxdepth 1 -type f -name "*.apk" | head -n1)
    if [[ -n "$apk_path" ]]; then
      base=$(basename "$apk_path" .apk)
      new_path="$APK_DIR/${base}-${arch}.apk"
      mv "$apk_path" "$new_path"
      echo "Renamed $apk_path to $new_path"
    else
      echo "No APK found in $APK_DIR"
    fi
  else
    echo "Directory $APK_DIR does not exist"
  fi
done