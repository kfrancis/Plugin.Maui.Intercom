---
name: intercom-sdk-bump
description: Upgrade the vendored Intercom Android or iOS SDK in Plugin.Maui.Intercom. Use when the intercom-sdk-watch workflow files a "[dependency] Intercom … SDK … available" issue, when asked to bump IntercomAndroidSdkVersion or IntercomIosSdkVersion, or to re-vendor Intercom.xcframework or the Intercom AARs.
---

# Bumping a vendored Intercom SDK

Both SDKs are **vendored and pinned**: the iOS `Intercom.xcframework` and the Android AARs
are checked in, and ordinary builds never download anything. A bump therefore replaces
binary artifacts, not a version string, and the version string exists in five or six places
that have to move together.

`eng/update-intercom.sh` (iOS) and `eng/update-intercom-android.sh` (Android) do the
mechanical half and print what still needs judgement. Both run on macOS, Linux and Windows
(Git Bash). Run the script first, then work the checklist below.

## 0. Read the release notes first

They are the only source for the things no artifact records: minimum compile/target SDK,
removed APIs, and behaviour changes worth mentioning in the PR.

```bash
gh api "repos/intercom/intercom-android/releases?per_page=100" --jq '.[] | select(.tag_name | startswith("18.")) | "== \(.tag_name)\n\(.body)"'
gh api "repos/intercom/intercom-ios/releases?per_page=100"     --jq '.[] | select(.tag_name | startswith("19.")) | "== \(.tag_name)\n\(.body)"'
```

Read **every** release between the old and new pin, not just the target. A `18.0.0` that
raises `minSdk` and a `18.5.0` that adds an API both land in one bump.

## 1. Run the updater

```bash
eng/update-intercom.sh 19.7.2          # iOS: downloads, verifies SHA-256, re-vendors, repins
eng/update-intercom-android.sh 18.7.0  # Android: same, plus the POM delta and a usage check
```

Each ends by running `eng/api-coverage.sh --update`, so the API delta lands in the same
commit as the version bump.

## 2. Triage the API delta

`eng/api-coverage.json` now has `todo` entries. `--strict` (what CI runs) fails until every
one is `covered` or `skipped`. For each:

- **Both platforms have it, and it is generally useful** → surface it on `IIntercom`. That
  means a Java method in `IntercomSdk.java` first (the Android binding binds only
  `com.intercom.mauiintercom`, so nothing else is reachable), then `Intercom.android.cs`,
  `Intercom.macios.cs`, `Intercom.net.cs`, and a control on the sample's `MainPage`.
- **One platform only** → usually `skipped` with a reason. `ChangeWorkspace` is the
  precedent for the other choice — surface it and throw `PlatformNotSupportedException` on
  the other side — but that is for APIs a typical consumer would miss.
- **Deprecated in favour of a new overload** → mark the old one `skipped` naming the
  replacement, and switch the implementation to the new one.
- **Internal plumbing / model constructors** → `skipped`.

Then check the *other* direction: the map's `member` strings are free text, and
`ApiCoverageMapTests` is what stops a plausible-looking lie. Run `dotnet test` after editing
the map.

If the new SDK adds a *nested* type the extractor does not know about — Android 18 added
`Intercom$ContentType` — add its class glob to `CLASS_GLOBS` in `eng/api-coverage.sh`, or its
members are silently never classified.

## 3. iOS specifics

- The binding is generated on macOS only, so **member names on the generated
  `IntercomBinding.*` types cannot be verified from Windows or Linux.** Add every newly
  called member to the `REQUIRED` list in `eng/dump-ios-binding-api.sh`; CI's `ios-binding`
  job runs `--check` and fails with a clear message instead of a compile error deep in
  `Intercom.macios.cs`.
- ObjC enum members lose the enum-name prefix in the generated C# (`ICMThemeOverrideLight`
  → `ICMThemeOverride.Light`). Do not map them by casting an integer — a named member fails
  at compile time if the generator disagrees, a cast fails silently at runtime.
- `eng/api-coverage.sh` parses the umbrella headers as text. Trailing macros
  (`NS_REFINED_FOR_SWIFT`, `__attribute((deprecated("…")))`) are stripped before the selector
  is read; a deprecation message quoting a *replacement* selector would otherwise be
  concatenated onto the real one.
- developers.intercom.com is not ground truth. It has documented APIs the shipped headers
  did not declare, for releases at a time. Read the headers.

## 4. Android specifics

This is the long half.

- **`compileSdk` and `minSdk` are not machine-readable.** Intercom's
  `aar-metadata.properties` says `minCompileSdk=1` regardless. Take them from the release
  notes and set `src/android/native/mauiintercom/build.gradle.kts` and
  `SupportedOSPlatformVersion` in the binding csproj (and the Ably add-on csproj, which
  mirrors it).
- **The PackageReference graph is a cascade.** Map the POM delta onto
  `Xamarin.AndroidX.*` versions, then loop:

  ```bash
  dotnet restore src/android/Intercom.Android.Binding/Intercom.Android.Binding.csproj
  ```

  Each pass reports NU1605 downgrades for floors a *newly raised* sibling now demands.
  Raise those, repeat. Expect three or four rounds. Two traps: the exact revision may not
  exist for every package in a family (`Xamarin.AndroidX.Fragment` stopped at `1.8.9.3`
  while `Fragment.Ktx` went to `1.8.9.4`), and `NoWarn` does not apply — these are errors.
- **Removed dependencies.** The script's usage check scans the new bytecode for each
  vendored artifact's packages and flags unreferenced ones. Do the same for
  `PackageReference`s the POM delta dropped before removing them; a POM entry disappearing
  is suggestive, zero bytecode references is proof.
- **Artifacts that split into a KMP facade.** `androidx.paging:paging-compose` did this at
  3.4.x: the facade AAR holds only a manifest, and the classes moved to
  `paging-compose-android`. If a vendored AAR suddenly drops to a few KB, that is why.
- **The net9 band.** `Directory.Build.props` pins MAUI 9 for net9 inner builds, and the
  binding csproj pins AndroidX Navigation per band, because Navigation 2.9.x split each
  package into a facade plus `.Android` and the assembly MAUI 9 binds against stops
  existing. Ask the same question of any family this bump moves. The answer only shows up
  in an **app** build, not a library build.

## 5. The Ably add-on

`src/android/Intercom.Android.Ably` vendors `ably-java` at whatever version Intercom's POM
asks of `io.ably:ably-android`. The POM delta will show it. Bump `AblyJavaSdkVersion`,
`AblyNetworkClientVersion`, `MsgpackCoreVersion` and `VcdiffCoreVersion` in
`Directory.Build.props` from the new `ably-java` POM, re-vendor the jars, and keep its
`GoogleGson` reference in step with the binding's or the two disagree in a consumer's graph.

## 6. Verify

In order, cheapest first:

```bash
eng/api-coverage.sh --strict
dotnet test src/tests/Plugin.Maui.Intercom.Tests/Plugin.Maui.Intercom.Tests.csproj
./build.ps1                       # binding pack + plugin (both bands) + sample at net10
```

Then the check `build.ps1` does not do, and the one that catches AndroidX/MAUI 9 breakage:

```bash
dotnet build src/sample/MauiSample.csproj -c Release -f net9.0-android --configfile nuget.local.config
```

`build.ps1` re-packs the binding at the same version each run, so NuGet's extracted copy
goes stale. If the plugin fails with `CS0117: 'IntercomSdk' does not contain a definition
for …` right after adding a Java method, clear it:

```bash
rm -rf ~/.nuget/packages/plugin.maui.intercom.android.binding
```

macOS-only, and CI's job anyway: `eng/generate-ios-binding.sh`,
`eng/dump-ios-binding-api.sh --check`, `eng/test-consumer.sh`, `eng/validate-packages.sh`.

## 7. Update the prose

Version strings and platform-support claims live outside the build:

- `README.md` — the pinned-versions table, the per-platform support matrix (a bump can
  *remove* a `PlatformNotSupportedException`), the xcframework version, the Compose BOM
  compatibility note.
- `CLAUDE.md` — SDK versions, the API-floor notes, the Ably paragraph.
- `src/android/Intercom.Android.Binding/README.md` — handled by the script.
- `.github/workflows/publish.yml` — the pinned-toolchain comment at the top.

## 8. Close the issue

The watch workflow's issue body is a checklist. Reference it from the PR so it closes:
`Closes #13`, `Closes #14`. Its last item — testing login, logout, Messenger presentation,
push and unread counts on a real device — is the one thing none of the above covers. Say so
explicitly in the PR rather than implying CI green means device-tested.
