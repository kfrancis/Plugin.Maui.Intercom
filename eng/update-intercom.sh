#!/usr/bin/env bash
# Update the vendored, pinned Intercom iOS SDK xcframework.
#
# Downloads the EXACT release asset for the requested version from the official
# intercom/intercom-ios GitHub releases, verifies its SHA-256, and replaces
# src/macios/Intercom.iOS.Binding/Intercom.xcframework. Also updates the
# IntercomIosSdkVersion pin in Directory.Build.props and records the checksum
# in eng/intercom-ios.sha256.
#
# Ordinary builds never download anything: the xcframework is checked in.
#
# Usage:
#   eng/update-intercom.sh <version> [<expected-sha256>]
# Example:
#   eng/update-intercom.sh 18.7.2 0123abc...
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: eng/update-intercom.sh <version> [<expected-sha256>]" >&2
  exit 2
fi

VERSION="$1"
EXPECTED_SHA="${2:-}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="$REPO_ROOT/src/macios/Intercom.iOS.Binding/Intercom.xcframework"
# Short temp root to avoid long-path problems.
WORK="/tmp/intercom-binding"
URL="https://github.com/intercom/intercom-ios/releases/download/$VERSION/Intercom.xcframework.zip"

rm -rf "$WORK"
mkdir -p "$WORK"

echo "Downloading Intercom iOS SDK $VERSION"
echo "  $URL"
curl -fSL --retry 3 -o "$WORK/Intercom.xcframework.zip" "$URL"

ACTUAL_SHA="$(shasum -a 256 "$WORK/Intercom.xcframework.zip" | awk '{print $1}')"
echo "SHA-256: $ACTUAL_SHA"

if [[ -n "$EXPECTED_SHA" && "$ACTUAL_SHA" != "$EXPECTED_SHA" ]]; then
  echo "ERROR: SHA-256 mismatch. Expected $EXPECTED_SHA" >&2
  exit 1
fi
if [[ -z "$EXPECTED_SHA" ]]; then
  echo "WARNING: no expected SHA-256 provided; recording the downloaded hash as the pin."
fi

unzip -q "$WORK/Intercom.xcframework.zip" -d "$WORK/unzipped"
XCF="$(find "$WORK/unzipped" -maxdepth 2 -name 'Intercom.xcframework' -type d | head -1)"
if [[ -z "$XCF" ]]; then
  echo "ERROR: Intercom.xcframework not found in the downloaded archive." >&2
  exit 1
fi

rm -rf "$DEST"
# ditto preserves code signatures and extended attributes.
ditto "$XCF" "$DEST"

# Update pins.
sed -i '' "s|<IntercomIosSdkVersion>.*</IntercomIosSdkVersion>|<IntercomIosSdkVersion>$VERSION</IntercomIosSdkVersion>|" "$REPO_ROOT/Directory.Build.props"
printf '%s  Intercom.xcframework.zip (v%s)\n' "$ACTUAL_SHA" "$VERSION" > "$REPO_ROOT/eng/intercom-ios.sha256"

rm -rf "$WORK"

echo ""
echo "Updated:"
echo "  - $DEST"
echo "  - Directory.Build.props (IntercomIosSdkVersion=$VERSION)"
echo "  - eng/intercom-ios.sha256"
echo ""

# Show what the new SDK changed about the public API, and record it, so the API
# delta lands in the same PR as the version bump instead of being discovered later.
echo "API delta:"
"$REPO_ROOT/eng/api-coverage.sh" --update
echo ""
echo "Next: triage any symbol newly marked \"todo\" in eng/api-coverage.json,"
echo "run eng/generate-ios-binding.sh, fix any API drift in"
echo "src/Plugin.Maui.Intercom/Intercom.macios.cs, and commit the result."
