#!/bin/bash
set -euo pipefail
if [[ "$(uname -s)" != Darwin ]]; then echo 'Run this script on macOS.' >&2; exit 1; fi
identity="${1:-}"
if [[ "$identity" != 'Developer ID Application: '* ]]; then
    echo 'Usage: ./sign-macos.sh "Developer ID Application: Name (TEAMID)"' >&2
    exit 1
fi
base="$(cd -- "$(dirname -- "$0")" && pwd)"
bundle="$base/AI Guard Lock.app"
entitlements="$base/entitlements.plist"
test -d "$bundle/Contents/MacOS"
test -f "$entitlements"
while IFS= read -r -d '' binary; do
    if /usr/bin/file -b "$binary" | /usr/bin/grep -q 'Mach-O'; then
        /usr/bin/codesign --force --options runtime --timestamp --entitlements "$entitlements" --sign "$identity" "$binary"
    fi
done < <(/usr/bin/find "$bundle/Contents/MacOS" -type f -print0)
/usr/bin/codesign --force --options runtime --timestamp --entitlements "$entitlements" --sign "$identity" "$bundle"
/usr/bin/codesign --verify --deep --strict --verbose=2 "$bundle"
echo 'Signing complete. Notarization and stapling remain before normal distribution.'
