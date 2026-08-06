#!/usr/bin/env bash
# Update the vendored, pinned Intercom Android SDK AARs.
#
# Downloads intercom-sdk-{base,ui,lightcompressor} for the requested version from
# Maven Central, plus the two transitive artifacts that have no Xamarin.AndroidX
# binding and therefore have to be vendored as well (io.intercom.android's
# nexus-client-android, and androidx.paging's paging-compose). Replaces the AARs
# in src/android/Intercom.Android.Binding/Jars, rewrites every pin that names the
# version, and prints the POM dependency delta.
#
# Ordinary builds never download anything: the AARs are checked in.
#
# What it deliberately does NOT do: rewrite the PackageReference graph. The AAR
# POMs give Maven coordinates, and mapping those onto Xamarin.AndroidX binding
# versions is a cascade — raising one floor pulls a sibling's minimum up, and
# NuGet reports each round as a fresh NU1605. That converges in a few `dotnet
# restore` passes but needs a human reading the errors, so this script prints the
# delta and stops there.
#
# Runs on macOS, Linux and Windows (Git Bash).
#
# Usage:
#   eng/update-intercom-android.sh <version>
# Example:
#   eng/update-intercom-android.sh 18.7.0
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: eng/update-intercom-android.sh <version>" >&2
  exit 2
fi

VERSION="$1"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
JARS="$REPO_ROOT/src/android/Intercom.Android.Binding/Jars"
PROPS="$REPO_ROOT/Directory.Build.props"
CSPROJ="$REPO_ROOT/src/android/Intercom.Android.Binding/Intercom.Android.Binding.csproj"
GRADLE="$REPO_ROOT/src/android/native/mauiintercom/build.gradle.kts"
BINDING_README="$REPO_ROOT/src/android/Intercom.Android.Binding/README.md"
CENTRAL="https://repo1.maven.org/maven2"
GOOGLE="https://dl.google.com/dl/android/maven2"

# shellcheck source=eng/lib/update-common.sh
. "$REPO_ROOT/eng/lib/update-common.sh"

OLD_VERSION="$(sed -n 's|.*<IntercomAndroidSdkVersion>\(.*\)</IntercomAndroidSdkVersion>.*|\1|p' "$PROPS" | head -1)"
[[ -n "$OLD_VERSION" ]] || { echo "ERROR: could not read IntercomAndroidSdkVersion from Directory.Build.props" >&2; exit 1; }

if [[ "$OLD_VERSION" == "$VERSION" ]]; then
  echo "Already pinned to $VERSION; nothing to do."
  exit 0
fi

WORK="$(make_workdir intercom-android)"
trap 'rm -rf "$WORK" 2>/dev/null || true' EXIT

fetch() {  # fetch <url> <dest>
  curl -fSL --retry 3 -o "$2" "$1"
}

echo "Fetching POMs for $OLD_VERSION -> $VERSION"
for v in "$OLD_VERSION" "$VERSION"; do
  for a in intercom-sdk-base intercom-sdk-ui; do
    fetch "$CENTRAL/io/intercom/android/$a/$v/$a-$v.pom" "$WORK/$a-$v.pom"
  done
done

# The transitive artifacts we vendor are versioned independently of the SDK, so
# read their versions out of the new POM rather than assuming they held still.
read_dep_version() {  # read_dep_version <pom> <groupId> <artifactId>
  "$UPDATE_COMMON_PYTHON" - "$1" "$2" "$3" <<'PY'
import re, sys
pom, group, artifact = sys.argv[1], sys.argv[2], sys.argv[3]
text = open(pom, encoding='utf-8').read()
for block in re.findall(r'<dependency>(.*?)</dependency>', text, re.S):
    g = re.search(r'<groupId>(.*?)</groupId>', block)
    a = re.search(r'<artifactId>(.*?)</artifactId>', block)
    v = re.search(r'<version>(.*?)</version>', block)
    if g and a and g.group(1) == group and a.group(1) == artifact:
        print(v.group(1) if v else '')
        break
PY
}

NEXUS_VERSION="$(read_dep_version "$WORK/intercom-sdk-base-$VERSION.pom" io.intercom.android nexus-client-android)"
PAGING_VERSION="$(read_dep_version "$WORK/intercom-sdk-base-$VERSION.pom" androidx.paging paging-compose)"
[[ -n "$NEXUS_VERSION" && -n "$PAGING_VERSION" ]] || {
  echo "ERROR: could not read nexus-client-android / paging-compose versions from the $VERSION POM." >&2
  echo "       Intercom may have dropped or renamed them; check the POM before proceeding." >&2
  exit 1
}
echo "  nexus-client-android $NEXUS_VERSION, paging-compose $PAGING_VERSION"

echo ""
echo "Downloading AARs"
for a in intercom-sdk-base intercom-sdk-ui intercom-sdk-lightcompressor; do
  fetch "$CENTRAL/io/intercom/android/$a/$VERSION/$a-$VERSION.aar" "$WORK/$a-$VERSION.aar"
  echo "  $a-$VERSION.aar"
done
fetch "$CENTRAL/io/intercom/android/nexus-client-android/$NEXUS_VERSION/nexus-client-android-$NEXUS_VERSION.aar" \
      "$WORK/nexus-client-android-$NEXUS_VERSION.aar"
echo "  nexus-client-android-$NEXUS_VERSION.aar"
# paging-compose is a KMP facade from 3.4.x on: the facade AAR holds only a
# manifest and a licence, and the classes live in paging-compose-android.
fetch "$GOOGLE/androidx/paging/paging-compose-android/$PAGING_VERSION/paging-compose-android-$PAGING_VERSION.aar" \
      "$WORK/paging-compose-android-$PAGING_VERSION.aar"
echo "  paging-compose-android-$PAGING_VERSION.aar"

echo ""
echo "Replacing vendored AARs"
for old in "$JARS"/intercom-sdk-*-"$OLD_VERSION".aar "$JARS"/nexus-client-android-*.aar \
           "$JARS"/paging-compose*.aar; do
  [[ -e "$old" ]] || continue
  rm -f "$old"
  echo "  - $(basename "$old")"
done
for new in "$WORK"/intercom-sdk-*-"$VERSION".aar \
           "$WORK/nexus-client-android-$NEXUS_VERSION.aar" \
           "$WORK/paging-compose-android-$PAGING_VERSION.aar"; do
  cp "$new" "$JARS/"
  echo "  + $(basename "$new")"
done

echo ""
echo "Rewriting pins"
replace_in_file "$PROPS" \
  '<IntercomAndroidSdkVersion>.*</IntercomAndroidSdkVersion>' \
  "<IntercomAndroidSdkVersion>$VERSION</IntercomAndroidSdkVersion>"
# The csproj names every vendored AAR by file name (AndroidLibrary items carry no
# version metadata), the Gradle module names the Maven coordinates, and the
# binding README names the version in prose.
"$UPDATE_COMMON_PYTHON" - "$CSPROJ" "$VERSION" "$NEXUS_VERSION" "$PAGING_VERSION" <<'PY'
import re, sys
path, new, nexus, paging = sys.argv[1:5]
text = open(path, encoding='utf-8').read()
text = re.sub(r'(intercom-sdk-(?:base|ui|lightcompressor)-)[0-9][0-9.]*(\.aar)',
              lambda m: m.group(1) + new + m.group(2), text)
text = re.sub(r'(nexus-client-android-)[0-9][0-9.]*(\.aar)',
              lambda m: m.group(1) + nexus + m.group(2), text)
text = re.sub(r'(paging-compose(?:-android)?-?)[0-9][0-9.]*(\.aar)',
              lambda m: 'paging-compose-android-' + paging + m.group(2), text)
open(path, 'w', encoding='utf-8', newline='\n').write(text)
print("Rewrote AAR file names in %s" % path)
PY
replace_in_file "$GRADLE" "io\\.intercom\\.android:intercom-sdk-base:$OLD_VERSION" \
  "io.intercom.android:intercom-sdk-base:$VERSION"
replace_in_file "$GRADLE" "io\\.intercom\\.android:intercom-sdk-ui:$OLD_VERSION" \
  "io.intercom.android:intercom-sdk-ui:$VERSION"
replace_in_file "$GRADLE" "intercom-sdk-ui-$OLD_VERSION\\.aar" "intercom-sdk-ui-$VERSION.aar"
# re.escape, not the raw version: the dots would otherwise match any character.
replace_in_file "$BINDING_README" "$(printf '%s' "$OLD_VERSION" | sed 's/\./\\./g')" "$VERSION"

echo ""
echo "POM dependency delta ($OLD_VERSION -> $VERSION)"
"$UPDATE_COMMON_PYTHON" - "$WORK" "$OLD_VERSION" "$VERSION" <<'PY'
import os, re, sys
work, old, new = sys.argv[1], sys.argv[2], sys.argv[3]

def deps(path):
    text = open(path, encoding='utf-8').read()
    out = {}
    for block in re.findall(r'<dependency>(.*?)</dependency>', text, re.S):
        g = re.search(r'<groupId>(.*?)</groupId>', block).group(1)
        a = re.search(r'<artifactId>(.*?)</artifactId>', block).group(1)
        v = re.search(r'<version>(.*?)</version>', block)
        out[g + ':' + a] = v.group(1) if v else '(from BOM)'
    return out

def bom(path):
    m = re.search(r'compose-bom</artifactId>\s*<version>(.*?)</version>',
                  open(path, encoding='utf-8').read(), re.S)
    return m.group(1) if m else '(none)'

for name in ('intercom-sdk-base', 'intercom-sdk-ui'):
    o = deps(os.path.join(work, '%s-%s.pom' % (name, old)))
    n = deps(os.path.join(work, '%s-%s.pom' % (name, new)))
    print('  %s   compose-bom %s -> %s'
          % (name, bom(os.path.join(work, '%s-%s.pom' % (name, old))),
             bom(os.path.join(work, '%s-%s.pom' % (name, new)))))
    quiet = True
    for key in sorted(set(o) | set(n)):
        ov, nv = o.get(key), n.get(key)
        if ov == nv:
            continue
        quiet = False
        if ov is None:
            print('    + %s = %s' % (key, nv))
        elif nv is None:
            print('    - %s (was %s)' % (key, ov))
        else:
            print('    ~ %s: %s -> %s' % (key, ov, nv))
    if quiet:
        print('    (no change)')
PY

echo ""
echo "Vendored-artifact usage check"
# Every non-Intercom jar/aar in Jars/ is there because some Intercom class names
# it and no Xamarin.AndroidX binding exists. When Intercom drops a dependency the
# artifact stops being referenced but keeps shipping in every consumer's APK —
# io.sentry did exactly that in 18.x — so re-derive the answer from the bytecode
# instead of trusting the POM, which never listed sentry in the first place.
"$UPDATE_COMMON_PYTHON" - "$JARS" "$VERSION" <<'PY'
import os, re, sys, zipfile

jars_dir, version = sys.argv[1], sys.argv[2]

def classes(path):
    """The .class entries of a jar, or of the classes.jar inside an aar."""
    z = zipfile.ZipFile(path)
    if path.endswith('.aar'):
        if 'classes.jar' not in z.namelist():
            return None
        import io
        z = zipfile.ZipFile(io.BytesIO(z.read('classes.jar')))
    return z

# The Intercom bytecode is the only thing whose references matter: everything
# else in Jars/ is there to satisfy it.
sdk = [os.path.join(jars_dir, 'intercom-sdk-%s-%s.aar' % (part, version))
       for part in ('base', 'ui')]
blob = b''.join(z.read(n)
                for path in sdk
                for z in [classes(path)]
                for n in z.namelist() if n.endswith('.class'))

flagged = []
for name in sorted(os.listdir(jars_dir)):
    if name.startswith('intercom-sdk-') or not name.endswith(('.jar', '.aar')):
        continue
    z = classes(os.path.join(jars_dir, name))
    if z is None:
        continue
    # Two package segments is the useful granularity: 'io/sentry', 'io/coil-kt'
    # style roots are too coarse to be wrong and too fine to be noisy.
    prefixes = {'/'.join(n.split('/')[:2])
                for n in z.namelist() if n.endswith('.class') and n.count('/') >= 2}
    if not prefixes:
        continue
    hits = sum(len(re.findall(re.escape(p).encode(), blob)) for p in sorted(prefixes))
    status = 'UNREFERENCED' if hits == 0 else '%d refs' % hits
    print('  %-52s %s' % (name, status))
    if hits == 0:
        flagged.append(name)

if flagged:
    print('')
    print('  The Intercom %s bytecode names none of these. Confirm, then drop them' % version)
    print('  from Jars/ and from the AndroidLibrary items in the binding csproj:')
    for name in flagged:
        print('    %s' % name)
PY

echo ""
echo "Updated:"
echo "  - $JARS"
echo "  - Directory.Build.props (IntercomAndroidSdkVersion=$VERSION)"
echo "  - $(basename "$CSPROJ"), build.gradle.kts, binding README"
echo ""

# Record the API delta in the same pass, so it lands in the same PR as the bump.
echo "API delta:"
"$REPO_ROOT/eng/api-coverage.sh" --update
echo ""
echo "Next:"
echo "  1. Triage any symbol newly marked \"todo\" in eng/api-coverage.json."
echo "  2. Work the POM delta above into the PackageReference graph in the binding"
echo "     csproj, then loop 'dotnet restore' until NU1605 stops firing."
echo "  3. Act on the usage check above, and drop the PackageReferences for any"
echo "     dependency the POM delta removed."
echo "  4. Read the release notes for compileSdk / minSdk changes. Neither is"
echo "     machine-readable — Intercom's aar-metadata.properties still says"
echo "     minCompileSdk=1 — so build.gradle.kts and SupportedOSPlatformVersion"
echo "     have to be raised by hand when a major bump moves the floor."
echo "  5. Build BOTH bands and the sample app: ./build.ps1, then the sample at"
echo "     net9.0-android. A net10-only build hides the AndroidX/MAUI 9 breakage."
