#!/usr/bin/env bash
# Native API coverage gate.
#
# Extracts the public API of the *pinned, vendored* Intercom SDKs and checks
# every symbol against eng/api-coverage.json, where each one is classified as
# covered (surfaced through IIntercom), skipped (with a reason) or todo.
#
# The published docs at developers.intercom.com are NOT usable as ground truth:
# they document iOS `setThemeOverride:` (absent from the pinned headers) and omit
# `setUserJwt`, `setAuthTokens`, `IntercomContent.Ticket`, `reset()` and the whole
# `IntercomPushClient`. The checked-in artifacts are the only reliable source.
#
#   Android: javap -public over classes.jar inside src/android/.../Jars/*.aar
#   iOS:     text parse of the Intercom.xcframework umbrella headers
#
# Needs bash, python3 (or python), a JDK for javap, and unzip. No .NET, no Xcode,
# no network. Runs on Windows (Git Bash), macOS and Linux.
#
# Usage:
#   eng/api-coverage.sh              # gate: fail on unclassified or stale symbols
#   eng/api-coverage.sh --strict     # also fail on any symbol still marked todo
#   eng/api-coverage.sh --update     # add new symbols to the map as todo, then report
#   eng/api-coverage.sh --print      # dump the extracted inventory and exit
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MAP="$REPO_ROOT/eng/api-coverage.json"
MODE="gate"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --strict) MODE="strict"; shift ;;
    --update) MODE="update"; shift ;;
    --print)  MODE="print";  shift ;;
    --map)    MAP="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

# ── Interpreter ─────────────────────────────────────────────────────────────
# `python3` on Windows is usually the App Execution Alias stub, which prints an
# install advert and exits 0. Probe for one that actually runs.
PYTHON=""
for candidate in python3 python py; do
  if command -v "$candidate" >/dev/null 2>&1; then
    if "$candidate" -c 'import sys; sys.exit(0 if sys.version_info[0] == 3 else 1)' >/dev/null 2>&1; then
      PYTHON="$candidate"
      break
    fi
  fi
done
[[ -n "$PYTHON" ]] || { echo "ERROR: no working python3 found on PATH." >&2; exit 1; }

command -v javap >/dev/null 2>&1 || {
  echo "ERROR: javap not found on PATH. Install a JDK (11+) — without it the Android" >&2
  echo "       inventory would come back empty and the gate would pass vacuously." >&2
  exit 1
}
command -v unzip >/dev/null 2>&1 || { echo "ERROR: unzip not found on PATH." >&2; exit 1; }

# ── Pinned SDK versions ─────────────────────────────────────────────────────
props="$REPO_ROOT/Directory.Build.props"
read_pin() {
  sed -n "s|.*<$1>\(.*\)</$1>.*|\1|p" "$props" | head -1
}
ANDROID_VERSION="$(read_pin IntercomAndroidSdkVersion)"
IOS_VERSION="$(read_pin IntercomIosSdkVersion)"
[[ -n "$ANDROID_VERSION" && -n "$IOS_VERSION" ]] || {
  echo "ERROR: could not read IntercomAndroidSdkVersion / IntercomIosSdkVersion from Directory.Build.props" >&2
  exit 1
}

JARS="$REPO_ROOT/src/android/Intercom.Android.Binding/Jars"
HEADERS="$REPO_ROOT/src/macios/Intercom.iOS.Binding/Intercom.xcframework/ios-arm64/Intercom.framework/Headers"
[[ -d "$HEADERS" ]] || { echo "ERROR: iOS headers not found at $HEADERS" >&2; exit 1; }

WORK="$(mktemp -d)"
# Cleanup must never change the script's exit status — on Windows a virus
# scanner can hold a freshly extracted .class open and make rm fail.
trap 'rm -rf "$WORK" 2>/dev/null || true' EXIT

# Public entry points a consumer of this plugin could ever need. Deliberately an
# allowlist: the SDK ships thousands of internal Compose/Kotlin classes, and
# gating on all of them would be noise, not coverage.
CLASS_GLOBS=(
  'io/intercom/android/sdk/Intercom.class'
  'io/intercom/android/sdk/Intercom$Visibility.class'
  'io/intercom/android/sdk/Intercom$LogLevel.class'
  'io/intercom/android/sdk/Intercom$ContentType.class'
  'io/intercom/android/sdk/IntercomSpace.class'
  'io/intercom/android/sdk/IntercomContent.class'
  'io/intercom/android/sdk/IntercomContent$*.class'
  'io/intercom/android/sdk/IntercomError.class'
  'io/intercom/android/sdk/IntercomStatusCallback.class'
  'io/intercom/android/sdk/UnreadConversationCountListener.class'
  'io/intercom/android/sdk/UserAttributes.class'
  'io/intercom/android/sdk/UserAttributes$Builder.class'
  'io/intercom/android/sdk/Company.class'
  'io/intercom/android/sdk/Company$Builder.class'
  'io/intercom/android/sdk/AuthToken.class'
  'io/intercom/android/sdk/identity/Registration.class'
  'io/intercom/android/sdk/push/IntercomPushClient.class'
  'io/intercom/android/sdk/helpcenter/api/CollectionRequestCallback.class'
  'io/intercom/android/sdk/helpcenter/api/CollectionContentRequestCallback.class'
  'io/intercom/android/sdk/helpcenter/api/SearchRequestCallback.class'
  'io/intercom/android/sdk/helpcenter/api/HelpCenterArticleSearchResult.class'
  'io/intercom/android/sdk/helpcenter/collections/HelpCenterCollection.class'
  'io/intercom/android/sdk/helpcenter/sections/HelpCenterCollectionContent.class'
  'io/intercom/android/sdk/helpcenter/sections/HelpCenterArticle.class'
  'io/intercom/android/sdk/helpcenter/sections/HelpCenterSection.class'
  'io/intercom/android/sdk/helpcenter/sections/Author.class'
  'io/intercom/android/sdk/ui/theme/ThemeMode.class'
)

# ── Android: unpack classes.jar, javap the public entry points ──────────────
# The Intercom SDK is split across two AARs; ui.theme.ThemeMode lives in -ui.
# Only the allowlisted classes are extracted — the jars hold ~20k entries and
# unpacking them whole is slow and leaves a large tree to clean up.
mkdir -p "$WORK/cls"
for aar in "intercom-sdk-base-$ANDROID_VERSION.aar" "intercom-sdk-ui-$ANDROID_VERSION.aar"; do
  [[ -f "$JARS/$aar" ]] || { echo "ERROR: $JARS/$aar missing (pin says $ANDROID_VERSION)" >&2; exit 1; }
  rm -rf "$WORK/aar"; mkdir -p "$WORK/aar"
  unzip -o -q "$JARS/$aar" classes.jar -d "$WORK/aar"
  # unzip warns and exits 11 when a pattern matches nothing, which is expected
  # here: each AAR only holds a subset of the allowlist. A class missing from
  # *both* jars is caught by the MISSING check below, so the warnings are noise.
  ( cd "$WORK/cls" && unzip -o -q "$WORK/aar/classes.jar" "${CLASS_GLOBS[@]}" 2>/dev/null ) || true
done

cd "$WORK/cls"
CLASS_FILES=()
MISSING=()
for glob in "${CLASS_GLOBS[@]}"; do
  matched=0
  # Unquoted so the glob expands; the `$` in inner-class names is literal
  # because it came from a single-quoted array element (bash does not re-expand).
  for f in $glob; do
    if [[ -f "$f" ]]; then
      CLASS_FILES+=("$f")
      matched=1
    fi
  done
  [[ $matched -eq 1 ]] || MISSING+=("$glob")
done

if [[ ${#MISSING[@]} -gt 0 ]]; then
  echo "ERROR: expected Intercom Android classes are missing from the pinned AARs:" >&2
  printf '  - %s\n' "${MISSING[@]}" >&2
  echo "The SDK moved or renamed them; update CLASS_GLOBS in this script." >&2
  exit 1
fi

javap -public "${CLASS_FILES[@]}" > "$WORK/android-api.txt"
cd "$REPO_ROOT"

# ── Compare ─────────────────────────────────────────────────────────────────
"$PYTHON" - "$WORK/android-api.txt" "$HEADERS" "$MAP" "$MODE" "$ANDROID_VERSION" "$IOS_VERSION" <<'PYEOF'
import json
import re
import sys
from pathlib import Path

javap_dump, headers_dir, map_path, mode, android_version, ios_version = sys.argv[1:7]
headers_dir = Path(headers_dir)
map_path = Path(map_path)

# ── Noise filters ───────────────────────────────────────────────────────────
# Kotlin/compiler-generated members that are not API: data-class plumbing,
# synthetic default-argument bridges, enum boilerplate, accessor thunks.
NOISE_NAMES = {
    "equals", "hashCode", "toString", "copy", "values", "valueOf",
    "getEntries", "$stable", "write$Self", "Companion",
}
# `getFoo$annotations` holders are emitted by kotlinx.serialization to carry the
# @SerialName metadata; they are not callable API.
NOISE_RX = re.compile(r"(\$default$|\$annotations$|^access\$|^component\d+$|\$serializer)")

# Types that only appear in internal DI constructors and Kotlin machinery. Any
# member mentioning one of these is SDK-internal regardless of its `public`ness.
INTERNAL_TYPES = {
    "DefaultConstructorMarker", "Provider", "Twig", "DeDuper", "UnreadCountTracker",
    "MetricTracker", "ErrorReporter", "OverlayPresenter", "UserIdentity",
    "ResetManager", "ActivityFinisher", "IntercomDataLayer", "Api", "NexusClient",
    "AppConfig", "UserUpdater", "Function1", "Continuation", "EnumEntries",
    "KSerializer", "SerialDescriptor", "SerializationConstructorMarker",
}

CLASS_RX = re.compile(r"^(?:public\s+)?(?:final\s+|abstract\s+)*(?:class|interface|enum)\s+([\w.$]+)")
MEMBER_RX = re.compile(r"^\s+public\s+(.*);\s*$")


def simplify(type_str):
    """java.util.Map<java.lang.String, ?> -> Map ; java.lang.String[] -> String[]"""
    type_str = re.sub(r"<[^<>]*>", "", type_str)
    while "<" in type_str:  # nested generics
        type_str = re.sub(r"<[^<>]*>", "", type_str)
    suffix = ""
    while type_str.endswith("[]"):
        suffix += "[]"
        type_str = type_str[:-2]
    return type_str.split(".")[-1] + suffix


def split_params(raw):
    """Split a parameter list on top-level commas (generics contain commas too)."""
    parts, depth, current = [], 0, ""
    for ch in raw:
        if ch == "<":
            depth += 1
        elif ch == ">":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append(current)
            current = ""
        else:
            current += ch
    if current.strip():
        parts.append(current)
    return [p.strip() for p in parts if p.strip()]


# ── Android inventory ───────────────────────────────────────────────────────
def parse_android(dump_path):
    symbols = {}
    current = None
    for line in Path(dump_path).read_text(encoding="utf-8", errors="replace").splitlines():
        m = CLASS_RX.match(line.strip()) if not line.startswith(" ") else None
        if m:
            current = m.group(1)
            continue
        m = MEMBER_RX.match(line)
        if not m or current is None:
            continue
        decl = m.group(1)

        if "(" in decl:
            head, _, rest = decl.partition("(")
            raw_params = rest.rsplit(")", 1)[0]
            tokens = head.split()
            name = tokens[-1]
            params = [simplify(p) for p in split_params(raw_params)]
            if name == current:  # constructor
                name = "<init>"
            sig = "%s(%s)" % (name, ",".join(params))
            mentioned = set(params)
        else:  # field
            tokens = decl.split()
            name = tokens[-1]
            sig = name
            mentioned = {simplify(tokens[-2])} if len(tokens) > 1 else set()

        base = sig.split("(")[0]
        if base in NOISE_NAMES or NOISE_RX.search(base):
            continue
        if mentioned & INTERNAL_TYPES:
            continue
        symbols["android:%s#%s" % (current, sig)] = True
    return set(symbols)


# ── iOS inventory ───────────────────────────────────────────────────────────
PROPERTY_RX = re.compile(r"^@property\s*(\([^)]*\))?\s*(.+?)\s*;\s*$")
EXTERN_RX = re.compile(r"^(?:UIKIT_EXTERN|FOUNDATION_EXPORT)\s+.*?\b(\w+)\s*;\s*$")
ENUM_RX = re.compile(r"^typedef\s+NS_(?:ERROR_)?ENUM\s*\(\s*[\w\s*]+\s*,\s*(\w+)\s*\)")


def parse_ios(dirpath):
    symbols = set()
    for header in sorted(dirpath.glob("*.h")):
        text = header.read_text(encoding="utf-8", errors="replace")
        # Strip comments so `//` and `/** */` text is never parsed as code.
        text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
        text = re.sub(r"//[^\n]*", "", text)

        current_class = None
        enum_name = None
        pending = ""
        for raw in text.splitlines():
            line = raw.strip()
            if not line:
                continue

            if line.startswith("@interface"):
                current_class = re.split(r"[\s:(<]", line[len("@interface"):].strip())[0]
                continue
            if line.startswith("@end"):
                current_class = None
                continue
            if line.startswith("@protocol") and not line.endswith(";"):
                current_class = re.split(r"[\s:(<]", line[len("@protocol"):].strip())[0]
                continue

            m = ENUM_RX.match(line)
            if m:
                enum_name = m.group(1)
                continue
            if enum_name:
                if line.startswith("}"):
                    enum_name = None
                    continue
                for member in line.split(","):
                    member = member.split("=")[0].strip()
                    if re.fullmatch(r"\w+", member):
                        symbols.add("ios:enum:%s.%s" % (enum_name, member))
                continue

            m = EXTERN_RX.match(line)
            if m:
                symbols.add("ios:const:%s" % m.group(1))
                continue

            m = PROPERTY_RX.match(line)
            if m and current_class:
                name = re.split(r"[\s*]+", m.group(2).strip())[-1]
                symbols.add("ios:%s.%s" % (current_class, name))
                continue

            # Methods can wrap across lines; accumulate until the semicolon.
            if line.startswith(("+ (", "- (")) or pending:
                pending += " " + line
                if ";" not in pending:
                    continue
                decl, pending = pending.split(";", 1)[0].strip(), ""
                kind = decl[0]
                body = decl[1:].strip()
                body = re.sub(r"^\([^)]*\)", "", body).strip()  # drop return type
                # Trailing macros are not part of the selector, and their payloads
                # contain colons: a deprecation attribute quotes the replacement
                # selector, which otherwise gets appended to the real one —
                #   +[Intercom setDeviceToken:failure:] __attribute((deprecated(
                #       "… Use '+[Intercom setDeviceToken:success:failure:]' …")))
                # parsed as setDeviceToken:failure:setDeviceToken:success:failure:.
                # Truncating at the first ALL-CAPS macro or __attribute is safe:
                # parameter types are parenthesised and none of them match.
                body = re.split(r"\b(?:__attribute\w*|__deprecated\w*|NS_[A-Z][A-Z_]+"
                                r"|API_[A-Z][A-Z_]+|[A-Z][A-Z0-9]*_[A-Z0-9_]+)\b", body)[0]
                # A selector is either `name` or `partA:partB:` — parameter names,
                # types and attributes between the parts are irrelevant to identity.
                parts = re.findall(r"(\w+)\s*:", body)
                if parts:
                    selector = "".join(p + ":" for p in parts)
                else:
                    selector = re.match(r"(\w+)", body).group(1) if re.match(r"(\w+)", body) else None
                if not selector or not current_class:
                    continue
                # Model-object initialisers are constructed by the SDK, never by us.
                if selector.startswith("initWith"):
                    continue
                symbols.add("ios:%s%s%s" % (current_class, kind, selector))
    return symbols


native = parse_android(javap_dump) | parse_ios(headers_dir)

if mode == "print":
    for symbol in sorted(native):
        print(symbol)
    sys.exit(0)

# ── Load / update the coverage map ──────────────────────────────────────────
if map_path.exists():
    data = json.loads(map_path.read_text(encoding="utf-8"))
else:
    data = {"androidSdkVersion": android_version, "iosSdkVersion": ios_version, "symbols": {}}

symbols = data.setdefault("symbols", {})

unclassified = sorted(s for s in native if s not in symbols)
stale = sorted(s for s in symbols if s not in native)
todo = sorted(s for s, v in symbols.items() if v.get("status") == "todo" and s in native)

if mode == "update":
    for symbol in unclassified:
        symbols[symbol] = {"status": "todo"}
    for symbol in stale:
        symbols.pop(symbol)
    data["androidSdkVersion"] = android_version
    data["iosSdkVersion"] = ios_version
    data["symbols"] = dict(sorted(symbols.items()))
    map_path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("Updated %s" % map_path.name)
    print("  + %d new symbol(s) recorded as todo" % len(unclassified))
    for symbol in unclassified:
        print("      %s" % symbol)
    print("  - %d stale symbol(s) removed" % len(stale))
    for symbol in stale:
        print("      %s" % symbol)
    sys.exit(0)

# ── Report ──────────────────────────────────────────────────────────────────
covered = sum(1 for s, v in symbols.items() if v.get("status") == "covered" and s in native)
skipped = sum(1 for s, v in symbols.items() if v.get("status") == "skipped" and s in native)
total = len(native)

print("Intercom native API coverage")
print("  Android SDK %s, iOS SDK %s" % (android_version, ios_version))
print("  %d symbols: %d covered, %d skipped, %d todo" % (total, covered, skipped, len(todo)))

failures = []

if data.get("androidSdkVersion") != android_version:
    failures.append("map records Android SDK %s but the pin says %s — re-run with --update"
                    % (data.get("androidSdkVersion"), android_version))
if data.get("iosSdkVersion") != ios_version:
    failures.append("map records iOS SDK %s but the pin says %s — re-run with --update"
                    % (data.get("iosSdkVersion"), ios_version))

for symbol in unclassified:
    failures.append("unclassified: %s" % symbol)
for symbol in stale:
    failures.append("stale (gone from the SDK): %s" % symbol)

for symbol, entry in sorted(symbols.items()):
    if symbol not in native:
        continue
    status = entry.get("status")
    if status not in ("covered", "skipped", "todo"):
        failures.append("bad status %r: %s" % (status, symbol))
    elif status == "covered" and not entry.get("member"):
        failures.append("covered without a `member`: %s" % symbol)
    elif status == "skipped" and not entry.get("reason"):
        failures.append("skipped without a `reason`: %s" % symbol)

if mode == "strict":
    for symbol in todo:
        failures.append("todo: %s" % symbol)

print()
if failures:
    print("API coverage FAILED (%d problem(s)):" % len(failures))
    for f in failures:
        print("  - %s" % f)
    print()
    print("Classify new symbols with: eng/api-coverage.sh --update")
    sys.exit(1)

if todo:
    print("API coverage PASSED with %d todo symbol(s):" % len(todo))
    for symbol in todo:
        print("  - %s" % symbol)
else:
    print("API coverage PASSED.")
PYEOF
