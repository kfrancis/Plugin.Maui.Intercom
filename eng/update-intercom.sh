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
# Runs on macOS, Linux and Windows (Git Bash) — see eng/lib/update-common.sh for
# why the extraction goes through python instead of ditto or unzip. Only the
# *generation* of the binding needs macOS; vendoring the artifact does not.
#
# Usage:
#   eng/update-intercom.sh <version> [<expected-sha256>]
# Example:
#   eng/update-intercom.sh 19.7.2 0123abc...
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: eng/update-intercom.sh <version> [<expected-sha256>]" >&2
  exit 2
fi

VERSION="$1"
EXPECTED_SHA="${2:-}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="$REPO_ROOT/src/macios/Intercom.iOS.Binding/Intercom.xcframework"
URL="https://github.com/intercom/intercom-ios/releases/download/$VERSION/Intercom.xcframework.zip"

# shellcheck source=eng/lib/update-common.sh
. "$REPO_ROOT/eng/lib/update-common.sh"

WORK="$(make_workdir intercom-ios)"
trap 'rm -rf "$WORK" 2>/dev/null || true' EXIT

echo "Downloading Intercom iOS SDK $VERSION"
echo "  $URL"
curl -fSL --retry 3 -o "$WORK/Intercom.xcframework.zip" "$URL"

ACTUAL_SHA="$(sha256_of "$WORK/Intercom.xcframework.zip")"
echo "SHA-256: $ACTUAL_SHA"

if [[ -n "$EXPECTED_SHA" && "$ACTUAL_SHA" != "$EXPECTED_SHA" ]]; then
  echo "ERROR: SHA-256 mismatch. Expected $EXPECTED_SHA" >&2
  exit 1
fi
if [[ -z "$EXPECTED_SHA" ]]; then
  echo "WARNING: no expected SHA-256 provided; recording the downloaded hash as the pin."
fi

extract_preserving_modes "$WORK/Intercom.xcframework.zip" "$WORK/unzipped"

XCF="$WORK/unzipped/Intercom.xcframework"
[[ -d "$XCF" ]] || {
  echo "ERROR: Intercom.xcframework not found at the root of the downloaded archive." >&2
  exit 1
}

rm -rf "$DEST"
cp -R "$XCF" "$DEST"
# cp does not carry the mode bits on every filesystem, so restore them from the
# archive rather than trusting the copy. Getting this wrong is invisible until a
# consumer's app fails to launch.
restore_modes_from_zip "$WORK/Intercom.xcframework.zip" "Intercom.xcframework" "$DEST"

# Update pins.
replace_in_file "$REPO_ROOT/Directory.Build.props" \
  '<IntercomIosSdkVersion>.*</IntercomIosSdkVersion>' \
  "<IntercomIosSdkVersion>$VERSION</IntercomIosSdkVersion>"
printf '%s  Intercom.xcframework.zip (v%s)\n' "$ACTUAL_SHA" "$VERSION" > "$REPO_ROOT/eng/intercom-ios.sha256"

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
echo "Next: triage any symbol newly marked \"todo\" in eng/api-coverage.json, add"
echo "any newly called member to the REQUIRED list in eng/dump-ios-binding-api.sh,"
echo "run eng/generate-ios-binding.sh, fix any API drift in"
echo "src/Plugin.Maui.Intercom/Intercom.macios.cs, and commit the result."
