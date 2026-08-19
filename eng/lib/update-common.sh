#!/usr/bin/env bash
# Shared helpers for the eng/update-intercom*.sh vendoring scripts.
#
# Everything here is written to behave the same on macOS, Linux and Windows
# (Git Bash), because a dependency-watch issue can be picked up on any of them
# and the vendored artifacts have to come out byte-identical either way. That
# rules out the obvious tools: `ditto` is macOS-only, `sed -i` takes an argument
# on BSD and not on GNU, and `shasum` is absent from Git Bash while `sha256sum`
# is absent from stock macOS. Archive extraction goes through python so mode
# bits survive; Info-ZIP's `unzip` drops them on Windows and git then records
# every file as 0644.
#
# Source it, do not execute it:
#   . "$REPO_ROOT/eng/lib/update-common.sh"

# ── Interpreter ─────────────────────────────────────────────────────────────
# Same probe as eng/api-coverage.sh: `python3` on Windows is usually the App
# Execution Alias stub, which prints an install advert instead of running.
UPDATE_COMMON_PYTHON=""
for _candidate in python3 python py; do
  if command -v "$_candidate" >/dev/null 2>&1; then
    if "$_candidate" -c 'import sys; sys.exit(0 if sys.version_info[0] == 3 else 1)' >/dev/null 2>&1; then
      UPDATE_COMMON_PYTHON="$_candidate"
      break
    fi
  fi
done
[[ -n "$UPDATE_COMMON_PYTHON" ]] || { echo "ERROR: no working python3 found on PATH." >&2; exit 1; }
unset _candidate

# make_workdir <name>
# A short temp directory (long paths bite on Windows), echoed on stdout.
make_workdir() {
  local dir="${TMPDIR:-/tmp}/$1"
  rm -rf "$dir"
  mkdir -p "$dir"
  echo "$dir"
}

# sha256_of <file>
sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print $1}'
  else
    "$UPDATE_COMMON_PYTHON" - "$1" <<'PY'
import hashlib, sys
h = hashlib.sha256()
with open(sys.argv[1], 'rb') as f:
    for chunk in iter(lambda: f.read(1 << 20), b''):
        h.update(chunk)
print(h.hexdigest())
PY
  fi
}

# extract_preserving_modes <zip> <dest-dir>
# Fails loudly on symlink entries: a flattened symlink is a broken framework,
# and neither Windows checkouts nor `cp -R` would round-trip one.
extract_preserving_modes() {
  "$UPDATE_COMMON_PYTHON" - "$1" "$2" <<'PY'
import os, stat, sys, zipfile

archive, dest = sys.argv[1], sys.argv[2]
z = zipfile.ZipFile(archive)

links = [i.filename for i in z.infolist() if stat.S_ISLNK(i.external_attr >> 16)]
if links:
    sys.exit("ERROR: %s contains symlinks, which this script cannot vendor: %s"
             % (os.path.basename(archive), ", ".join(links[:5])))

for info in z.infolist():
    target = os.path.join(dest, info.filename)
    if info.is_dir():
        os.makedirs(target, exist_ok=True)
        continue
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with z.open(info) as src, open(target, 'wb') as out:
        out.write(src.read())
    if (info.external_attr >> 16) & 0o111:
        os.chmod(target, 0o755)
print("Extracted %d entries" % len(z.namelist()))
PY
}

# restore_modes_from_zip <zip> <prefix-inside-zip> <dest-dir>
# Re-applies the archive's executable bits to an already-populated tree, for
# when the tree got there via `cp -R` (which does not preserve them on every
# filesystem). Only the executable bit matters — git records nothing else.
restore_modes_from_zip() {
  "$UPDATE_COMMON_PYTHON" - "$1" "$2" "$3" <<'PY'
import os, sys, zipfile

archive, prefix, dest = sys.argv[1], sys.argv[2].rstrip('/') + '/', sys.argv[3]
count = 0
for info in zipfile.ZipFile(archive).infolist():
    if info.is_dir() or not info.filename.startswith(prefix):
        continue
    if not (info.external_attr >> 16) & 0o111:
        continue
    target = os.path.join(dest, info.filename[len(prefix):])
    if os.path.exists(target):
        os.chmod(target, 0o755)
        count += 1
print("Restored the executable bit on %d file(s)" % count)
PY
}

# replace_in_file <file> <python-regex> <replacement>
# `sed -i` is not portable (BSD wants an argument, GNU does not), and the
# replacement text contains XML that would need escaping either way.
replace_in_file() {
  "$UPDATE_COMMON_PYTHON" - "$1" "$2" "$3" <<'PY'
import re, sys

path, pattern, replacement = sys.argv[1], sys.argv[2], sys.argv[3]
with open(path, encoding='utf-8') as f:
    text = f.read()
new, n = re.subn(pattern, lambda _: replacement, text)
if n == 0:
    sys.exit("ERROR: %s matched nothing in %s" % (pattern, path))
with open(path, 'w', encoding='utf-8', newline='\n') as f:
    f.write(new)
print("Rewrote %d occurrence(s) in %s" % (n, path))
PY
}
