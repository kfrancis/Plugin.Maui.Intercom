#!/usr/bin/env bash
# Programmatic validation of packed .nupkg contents.
#
# Opens each package as a ZIP and asserts the expected layout, dependency
# groups, native assets, and the absence of build-machine paths or simulator
# slices where they don't belong. Runs on macOS and Linux (python3 + bash).
#
# Usage: eng/validate-packages.sh --version <pkg-version> --feed <dir-with-nupkgs> [--ios-only]
set -euo pipefail

VERSION=""
FEED=""
IOS_ONLY="false"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --version) VERSION="$2"; shift 2 ;;
    --feed)    FEED="$(cd "$2" && pwd)"; shift 2 ;;
    --ios-only) IOS_ONLY="true"; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[[ -n "$VERSION" && -n "$FEED" ]] || { echo "Usage: eng/validate-packages.sh --version <v> --feed <dir> [--ios-only]" >&2; exit 2; }

python3 - "$FEED" "$VERSION" "$IOS_ONLY" <<'PYEOF'
import re
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree

feed = Path(sys.argv[1])
version = sys.argv[2]
ios_only = len(sys.argv) > 3 and sys.argv[3] == "true"
failures = []


def check(cond, message):
    status = "ok " if cond else "FAIL"
    print(f"  [{status}] {message}")
    if not cond:
        failures.append(message)


def load(pkg_id):
    path = feed / f"{pkg_id}.{version}.nupkg"
    if not path.exists():
        failures.append(f"{path.name} missing from feed")
        print(f"  [FAIL] {path.name} missing from feed")
        return None, []
    z = zipfile.ZipFile(path)
    return z, z.namelist()


def nuspec(z, pkg_id):
    ns = {"n": ""}
    data = z.read(f"{pkg_id}.nuspec").decode("utf-8-sig")
    # Strip default namespace for simpler XPath.
    data = re.sub(r'xmlns="[^"]+"', "", data, count=1)
    return ElementTree.fromstring(data), data


def dependency_groups(root):
    groups = {}
    for g in root.findall(".//dependencies/group"):
        tfm = g.get("targetFramework") or ""
        groups[tfm] = {d.get("id"): d.get("version") for d in g.findall("dependency")}
    return groups


BAD_PATH_RX = re.compile(r"(/Users/[a-zA-Z0-9_]+/|C:\\Users\\|/home/runner|/private/tmp|obj/(Debug|Release)|(^|/)bin/(Debug|Release))", re.IGNORECASE)

# Every shipped package multi-targets both supported bands. A missing band is invisible
# to a build on the other one and only surfaces as NU1202 in a consumer's restore, so it
# is asserted per package here. Keep in step with Directory.Build.props.
IOS_BANDS = ("net9.0-ios", "net10.0-ios")
ANDROID_BANDS = ("net9.0-android", "net10.0-android")


def check_bands(names, bands, prefix, suffix, label):
    """Assert one entry under <prefix>/<band>*/ ending in <suffix> for every band."""
    for band in bands:
        hits = [n for n in names
                if n.startswith(f"{prefix}/{band}") and n.endswith(suffix)]
        check(bool(hits), f"{label} for {band} ({hits})")

# ── iOS binding package ─────────────────────────────────────────────────────
print(f"\n== Plugin.Maui.Intercom.iOS.Binding {version} ==")
z, names = load("Plugin.Maui.Intercom.iOS.Binding")
if z:
    root, raw = nuspec(z, "Plugin.Maui.Intercom.iOS.Binding")
    check(root.find(".//id").text == "Plugin.Maui.Intercom.iOS.Binding", "package id")
    check(root.find(".//version").text == version, f"package version == {version}")

    check_bands(names, IOS_BANDS, "lib", ".dll", "managed dll")

    # Native assets ship via the classic iOS binding-resources sidecar:
    # lib/<tfm>/<Assembly>.resources.zip containing the NativeReference xcframework.
    # The .NET iOS SDK unpacks it in consuming apps and applies the NativeReference,
    # which is the supported transitive mechanism for IsBindingProject packages.
    #
    # One sidecar per TFM, since it rides next to that TFM's assembly. Each is
    # inspected in full: a band whose sidecar is empty or partial restores
    # managed-only and fails at the consumer's link step, not here.
    import io
    res_names = [n for n in names if n.endswith(".resources.zip")]
    check(len(res_names) == len(IOS_BANDS),
          f"one binding resources.zip per TFM ({res_names})")
    check_bands(names, IOS_BANDS, "lib", ".resources.zip", "binding resources.zip")
    for res_name in res_names:
        tfm = res_name.split("/")[1] if "/" in res_name else res_name
        rz = zipfile.ZipFile(io.BytesIO(z.read(res_name)))
        rn = rz.namelist()
        check("manifest" in rn, f"[{tfm}] resources.zip carries the binding manifest")
        check("Intercom.xcframework/ios-arm64/Intercom.framework/Intercom" in rn,
              f"[{tfm}] device Intercom.framework binary in resources.zip")
        check(any(n.startswith("Intercom.xcframework/ios-arm64_x86_64-simulator/Intercom.framework/Intercom")
                  for n in rn),
              f"[{tfm}] simulator Intercom.framework binary in resources.zip")
        check("Intercom.xcframework/ios-arm64/Intercom.framework/PrivacyInfo.xcprivacy" in rn,
              f"[{tfm}] PrivacyInfo.xcprivacy shipped with the device framework")
        for bundle in ("Intercom.bundle", "IntercomTranslations.bundle"):
            check(any(f"/ios-arm64/Intercom.framework/{bundle}/" in n for n in rn),
                  f"[{tfm}] {bundle} resources present in device framework")
        roots = {n.split("/")[0] for n in rn}
        check(roots <= {"Intercom.xcframework", "manifest"},
              f"[{tfm}] resources.zip contains only the Intercom.xcframework (roots: {sorted(roots)})")

    # Absolute/build-machine paths in MSBuild assets.
    for n in names:
        if n.endswith((".targets", ".props")):
            content = z.read(n).decode("utf-8", errors="replace")
            check(not BAD_PATH_RX.search(content), f"no machine-specific paths in {n}")

    check(not any(BAD_PATH_RX.search(n) for n in names), "no machine-specific package entry paths")

    groups = dependency_groups(root)
    print(f"  dependency groups: { {k: sorted(v) for k, v in groups.items()} }")

# ── Android binding package ────────────────────────────────────────────────
z, names = (None, [])
if ios_only:
    print("\n== Plugin.Maui.Intercom.Android.Binding: SKIPPED (--ios-only) ==")
else:
    print(f"\n== Plugin.Maui.Intercom.Android.Binding {version} ==")
    z, names = load("Plugin.Maui.Intercom.Android.Binding")
if z:
    root, _ = nuspec(z, "Plugin.Maui.Intercom.Android.Binding")
    check(root.find(".//version").text == version, f"package version == {version}")
    check_bands(names, ANDROID_BANDS, "lib", ".dll", "managed dll")
    check_bands(names, ANDROID_BANDS, "lib", ".aar", "bundled .aar")
    # Ships the Compose runtime-annotation dedup fix to consumers; without it every
    # consuming app fails to dex on a duplicate androidx.compose.runtime.Immutable.
    # Needed per band: NuGet imports buildTransitive from the folder matching the
    # consumer's TFM only, so a band without one silently imports nothing.
    check_bands(names, ANDROID_BANDS, "buildTransitive", ".targets", "buildTransitive targets")

# ── Android Ably add-on package (optional) ──────────────────────────────────
if ios_only:
    print("\n== Plugin.Maui.Intercom.Android.Ably: SKIPPED (--ios-only) ==")
    z, names = (None, [])
else:
    print(f"\n== Plugin.Maui.Intercom.Android.Ably {version} ==")
    z, names = load("Plugin.Maui.Intercom.Android.Ably")
if z:
    root, _ = nuspec(z, "Plugin.Maui.Intercom.Android.Ably")
    check(root.find(".//version").text == version, f"package version == {version}")
    aars = [n for n in names if n.endswith(".aar")]
    check(len(aars) == len(ANDROID_BANDS), f"one bundled .aar per TFM ({aars})")
    check_bands(names, ANDROID_BANDS, "lib", ".aar", "bundled .aar")
    import io as _io
    for aar in aars:
        tfm = aar.split("/")[1] if "/" in aar else aar
        inner = zipfile.ZipFile(_io.BytesIO(z.read(aar))).namelist()
        jars = [n for n in inner if n.startswith("libs/") and n.endswith(".jar")]
        # ably-java + network-client-core + network-client-okhttp + msgpack-core + vcdiff-core
        check(len(jars) == 5, f"[{tfm}] all five vendored Ably jars embedded in the aar ({len(jars)})")
    deps = dependency_groups(root)
    all_dep_ids = {d for g in deps.values() for d in g}
    # Must not drag Firebase in — that is the whole reason this vendors ably-java
    # rather than ably-android.
    check(not any("Firebase" in d for d in all_dep_ids),
          f"no Firebase dependency ({sorted(all_dep_ids)})")

# ── Main package ────────────────────────────────────────────────────────────
print(f"\n== Plugin.Maui.Intercom {version} ==")
z, names = load("Plugin.Maui.Intercom")
if z:
    root, _ = nuspec(z, "Plugin.Maui.Intercom")
    check(root.find(".//version").text == version, f"package version == {version}")
    check_bands(names, IOS_BANDS, "lib", "Plugin.Maui.Intercom.dll", "iOS lib")
    if not ios_only:
        check_bands(names, ANDROID_BANDS, "lib", "Plugin.Maui.Intercom.dll", "Android lib")

    groups = dependency_groups(root)
    ios_groups = {tfm: deps for tfm, deps in groups.items() if "ios" in tfm.lower()}
    android_groups = {tfm: deps for tfm, deps in groups.items() if "android" in tfm.lower()}
    check(bool(ios_groups), f"iOS dependency group exists ({list(groups)})")
    if not ios_only:
        check(bool(android_groups), f"Android dependency group exists ({list(groups)})")
    # A band with a lib but no dependency group restores without its binding and fails
    # at first use with a missing native symbol, so both halves are asserted.
    for band in IOS_BANDS:
        check(any(tfm.startswith(band) for tfm in ios_groups),
              f"dependency group for {band} ({list(ios_groups)})")
    if not ios_only:
        for band in ANDROID_BANDS:
            check(any(tfm.startswith(band) for tfm in android_groups),
                  f"dependency group for {band} ({list(android_groups)})")
    for tfm, deps in ios_groups.items():
        check(deps.get("Plugin.Maui.Intercom.iOS.Binding") == version,
              f"{tfm} depends on iOS binding {version} (got {deps.get('Plugin.Maui.Intercom.iOS.Binding')})")
        check("Plugin.Maui.Intercom.Android.Binding" not in deps,
              f"{tfm} does not leak the Android binding")
    for tfm, deps in android_groups.items():
        check(deps.get("Plugin.Maui.Intercom.Android.Binding") == version,
              f"{tfm} depends on Android binding {version}")
        check("Plugin.Maui.Intercom.iOS.Binding" not in deps,
              f"{tfm} does not leak the iOS binding")
        # The Ably add-on is opt-in: it carries a realtime stack most apps do not need,
        # so depending on it here would silently impose it on everyone.
        check("Plugin.Maui.Intercom.Android.Ably" not in deps,
              f"{tfm} does not depend on the optional Ably add-on")

    snupkg = feed / f"Plugin.Maui.Intercom.{version}.snupkg"
    check(snupkg.exists(), "symbols package (.snupkg) present for main package")

print()
if failures:
    print(f"Package validation FAILED ({len(failures)} problem(s)):")
    for f in failures:
        print(f"  - {f}")
    sys.exit(1)
print("Package validation PASSED.")
PYEOF
