# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Plugin.Maui.Intercom is a .NET MAUI plugin that wraps the native Intercom SDK for Android and iOS. Every package multi-targets **.NET 9 and .NET 10** (`net9.0-android`, `net9.0-ios`, `net10.0-android`, `net10.0-ios`).

**Status**: Both Android and iOS platforms are working.

## Build Commands

```bash
# Build the solution (plugin + sample + Android binding; iOS binding excluded on purpose)
dotnet build Plugin.Maui.Intercom.slnx -c Release

# Android binding package
dotnet pack src/android/Intercom.Android.Binding/Intercom.Android.Binding.csproj -c Release -o artifacts/packages

# iOS binding package (macOS + Xcode 26+ ONLY — fails on Windows/Linux)
eng/generate-ios-binding.sh --output artifacts/packages

# Clean-room package consumption test (macOS only)
eng/test-consumer.sh --version <v> --feed artifacts/packages

# nupkg content validation (any OS with python3 + bash)
eng/validate-packages.sh --version <v> --feed artifacts/packages

# Native API coverage gate (any OS with bash + python3 + JDK; no .NET, no Xcode, no network)
eng/api-coverage.sh --strict

# Tests (net10.0; compiles the plugin's platform-neutral sources directly)
dotnet test src/tests/Plugin.Maui.Intercom.Tests/Plugin.Maui.Intercom.Tests.csproj

# Run the sample app
dotnet build src/sample/MauiSample.csproj -c Debug -f net10.0-android -t:Run
dotnet build src/sample/MauiSample.csproj -c Debug -f net10.0-ios -t:Run   # macOS only
```

## Architecture

### API coverage is build-enforced

`eng/api-coverage.sh` extracts the public API of the **pinned, vendored** SDKs — `javap` over
`Jars/intercom-sdk-{base,ui}-<v>.aar`, a text parse of the `Intercom.xcframework` umbrella
headers — and checks every symbol against `eng/api-coverage.json`, where each is `covered`
(naming the plugin member that surfaces it), `skipped` (with a reason) or `todo`. CI runs it
`--strict` in the `api-coverage` job, which gates the android and ios-binding jobs.

Three things follow from this and are easy to get wrong:

- **Adding a member to `IIntercom` means updating the map.** Adding a *native* symbol (via an
  SDK bump) fails the build until it is classified: `eng/api-coverage.sh --update` records new
  ones as `todo`, then triage them.
- **`ApiCoverageMapTests` checks the other direction** — every `covered` entry must name a
  member that really exists. Without it the gate is satisfiable by writing a plausible name
  into the JSON.
- **developers.intercom.com is not ground truth.** It documents an iOS `setThemeOverride:` the
  shipped headers do not declare, and omits `setUserJwt`, `setAuthTokens`,
  `IntercomContent.Ticket`, `reset()` and the whole `IntercomPushClient`. Read the artifacts.

### The TFM matrix is one list, and net9 is not free

All four package projects take their `TargetFrameworks` from `IntercomIosTargetFrameworks` /
`IntercomAndroidTargetFrameworks` / `IntercomPluginTargetFrameworks` in
`Directory.Build.props`. The net9 band is there because .NET 9 is supported until
2026-11-10 and a `net9.0-ios` app cannot restore a `net10.0-ios` lib — shipping net10 only
locks those consumers out entirely. Drop the net9 entries after that date.

Both bands build from the single .NET 10 SDK in `global.json`; the installed android/ios
workloads carry the net9 reference packs, so no second SDK band is needed anywhere,
including CI. Resolved platform versions decide the pack paths and are spelled out in
several places: `net9.0-android` → 35.0, `net10.0-android` → 36.0, `net9.0-ios` → 18.0,
`net10.0-ios` → 26.0.

Four things follow, all of which have bitten:

- **`$(MauiVersion)` is band-specific.** It comes from the workload and tracks the SDK
  band, so a `net9.0-*` inner build would ask for `Microsoft.Maui.Controls` 10.x — which
  has no net9 lib (NU1202). `Directory.Build.props` pins it for net9 inner builds;
  `eng/test-consumer.sh` pins it again, because the clean-room app it generates lives
  outside the repo and never sees that file.
- **The Android binding must not build its TFMs in parallel.** Every inner build runs
  Gradle in the same `src/android/native` project directory, differing only by an init
  script that redirects the build directory. Concurrently they share one project cache and
  the net10 build fails resource compilation against `obj/Release/net9.0-android/…` paths.
  `<BuildInParallel>false</BuildInParallel>` in the binding csproj is what prevents it.
- **`buildTransitive/` paths are hand-written per TFM.** Pack metadata on a `None` item is
  collected from the outer build, where `$(TargetPlatformVersion)` is empty, so the folder
  names cannot be derived. A band with no matching folder imports nothing and every
  consuming app on it fails to dex — silently, from this repo's point of view.
- **The iOS binding cannot be built off macOS**, so `build.ps1` narrows the plugin and
  sample to their Android TFMs (via the overridable TFM properties) instead of pointing
  restore at a published iOS binding: no published version carries a net9.0-ios asset.
- **`CompressBindingResourcePackage` has to be forced to `true`.** Its default, `auto`,
  compresses only when the xcframework has symlinks — Intercom's has none — and the two
  bands then disagreed: net10 packed a `.resources.zip`, net9 a loose `.resources/` tree
  worth 76 NU5123 long-path warnings that would land past MAX_PATH under a Windows
  consumer's package cache. Both bands' iOS SDKs consume either layout.

`eng/validate-packages.sh` asserts every band in every package, and CI's consumer test
runs once per iOS band — a green net10 run says nothing about net9.

**Each band's iOS tooling pack demands one exact Xcode.** `_ValidateXcodeVersion` errors
(E0191) on any other: the net9 pack on macos-26 is 26.5.9004 and wants Xcode 26.5, the
net10 pack wants 26.6. The check is gated on `'$(_CanOutputAppBundle)' == 'true'`, so a
*library* — including the iOS binding itself — builds under either, and only app builds
notice. That is why the `consumer-test` matrix selects Xcode per band instead of using
the workflow-wide `$XCODE_PATH`, and why `ValidateXcodeVersion=false` is the wrong
answer there: proving a real consumer app builds is the job's entire purpose.

Related: the *tooling* pack version and the `TargetPlatformVersion` are independent. The
binding packs into `lib/net9.0-ios18.0/` (TPV 18.0, what a consumer on bare `net9.0-ios`
resolves) while being built by the 26.5 tooling pack. A consumer at a higher TPV restores
the 18.0 lib fine; pinning the pack path to 26.x would break the common case.

### The Android surface has a hard ceiling; iOS does not

The Android binding binds only `com.intercom.mauiintercom` (confirmable in
`obj/Release/net10.0-android/api.xml`: two types). `IntercomSdk.java` is therefore the entire
Android surface — an app cannot reach past it into `io.intercom.android.sdk`, so every new
`IIntercom` member needs a Java method first. iOS is the opposite: the generated binding
publishes Intercom's whole ObjC API as `IntercomBinding.*`, so iOS consumers always have an
escape hatch. Keep this asymmetry in mind when judging what "unsupported" costs on each side.

Marshalling across the Java boundary is split by direction on purpose: requests cross as
`java.util.Map` with boxed Java types (custom attributes are type-sensitive, and JSON would
lose the string/number distinction), responses cross as JSON (the Help Center models are Kotlin
data classes whose generated members would otherwise all need binding). Enums cross as
ordinals.

### Project Structure

- `src/Plugin.Maui.Intercom/` - Main MAUI plugin library (multi-targeted net9.0/net10.0 × android/ios)
- `src/android/Intercom.Android.Binding/` - Android native binding project using Gradle interop
- `src/android/native/` - Native Android Java code (MauiIntercom module)
- `src/macios/Intercom.iOS.Binding/` - iOS binding generated by swift-dotnet-bindings; contains the vendored, pinned `Intercom.xcframework`
- `src/sample/` - Sample MAUI application; the only end-to-end test of the native SDKs, so every `IIntercom` member should have a control on `MainPage`
- `src/tests/Plugin.Maui.Intercom.Tests/` - net10.0 TUnit project. Compiles the plugin's platform-neutral sources directly (the plugin itself cannot be referenced from a non-platform TFM) and covers model invariants, the `.net.cs` fallback, and coverage-map integrity
- `eng/` - Reproducible build/validation scripts (binding generation, Intercom updates, clean-room consumer test, package validation, API coverage)

### iOS binding (swift-dotnet-bindings)

The iOS binding uses the `SwiftBindings.Sdk` MSBuild project SDK from
[swift-dotnet-bindings](https://github.com/justinwojo/swift-dotnet-bindings). Key facts:

- The SDK version is pinned in `global.json` (`msbuild-sdks`); the Intercom SDK version is pinned in `Directory.Build.props` (`IntercomIosSdkVersion`) and vendored as `src/macios/Intercom.iOS.Binding/Intercom.xcframework`.
- Binding C# is generated at build time on macOS (Xcode 26+, .NET 10). Generated sources are NOT committed; determinism comes from the pinned SDK + pinned xcframework. CI uploads generated sources as an artifact.
- Intercom is a mixed Swift/ObjC framework whose full public API is on the ObjC umbrella header, so the binding uses the generator's pure-ObjC pipeline (`SwiftFrameworkType=ObjC` + `IsBindingProject=true`). Generated namespace is `IntercomBinding`; `Intercom.macios.cs` uses `IntercomBinding.Intercom`, `Space`, `IntercomContent`, `ICMUserAttributes`.
- The binding has no hand-written supplement. `ApiDefinitions.extra.cs` / `StructsAndEnums.extra.cs` existed up to SwiftBindings.Sdk 0.17.0, whose `-fmodules` clang retry dropped every declaration reachable only through `#import <Intercom/SiblingHeader.h>`. 0.18.0 added `-fmodule-name` to that retry, so the ICM* classes, `IntercomContent` and the `Space`/`ContentType` NS_ENUMs are generated; the supplements were removed. Do not reintroduce them — if a member is missing after an Intercom upgrade, that is a generator bug worth filing upstream.
- The nupkg uses the classic iOS binding layout, once per band: `lib/net9.0-ios18.0/` and `lib/net10.0-ios26.0/`, each holding `Intercom.iOS.Binding.dll` + `Intercom.iOS.Binding.resources.zip` (full xcframework); the .NET iOS SDK applies the NativeReference in consumers automatically.
- SwiftBindings.Sdk documents a .NET 10 floor, but nothing in its MSBuild enforces the band: the SWIFTBIND010 gate matches on the platform substring, the pack layout derives from `$(TargetFramework)` + `$(TargetPlatformVersion)`, and on the pure-ObjC lane the implicit `SwiftBindings.Runtime` / `SwiftBindings.Apple` package references are skipped, so no managed runtime assembly has to match the consumer's band. Generation runs once per inner build. Asked upstream in [issue #45](https://github.com/justinwojo/swift-dotnet-bindings/issues/45) — if the answer is that net9 is unsupported, the fallback is to compile the generated ApiDefinition in a plain `Microsoft.NET.Sdk` binding project.
- There is no Xcode wrapper project and no full manual ApiDefinition anymore; do not reintroduce them.

### Android binding

Native Java code in `src/android/native/mauiintercom/` wraps the Intercom Android SDK; `AndroidGradleProject` in the binding csproj drives Gradle. Vendored AARs live in `src/android/Intercom.Android.Binding/Jars/`. The Intercom Android SDK version (17.4.1) must stay compatible with the pinned Xamarin.AndroidX.Compose packages.

Three Android-specific traps, all fixed and all easy to reintroduce:

- **`RootNamespace` must not start with `Intercom`.** The default (project name `Intercom.Android.Binding`) put the generated `Resource` class in a namespace that declared `Intercom` in the *global* namespace of every consuming app, so `Intercom.Default` failed to resolve with CS0234. Set to `MauiIntercomAndroid`. `src/sample/NamespaceCollisionRegression.cs` guards it.
- **The binding ships `buildTransitive/*.targets`** that strips the duplicate Compose `runtime-annotation-jvm.jar`; without it every consumer fails to dex. The removal must hook `_DetermineJavaLibrariesToCompile` — `BeforeTargets="_CompileDex"` is too late.
- **Android floor is API 23**, dictated by AndroidX Emoji2 1.6.0 in the resolved graph, not by MAUI.

### Optional Ably add-on

`src/android/Intercom.Android.Ably/` packs `Plugin.Maui.Intercom.Android.Ably` — the realtime client Intercom uses for live conversation updates. Opt-in: the main package must never depend on it (`eng/validate-packages.sh` asserts this). It vendors **`ably-java`**, not `ably-android`, because Intercom only references core `io.ably.lib.{realtime,rest,types}` types and `ably-android`'s closure includes Firebase Messaging. Without the package Intercom degrades gracefully to polling and logs a "No realtime" warning.

### Platform-Specific Code Pattern

- `*.shared.cs` - Shared code (all platforms)
- `*.android.cs` - Android-specific implementation
- `*.macios.cs` - iOS-specific implementation
- `*.net.cs` - Generic .NET fallback (not compiled for the shipped TFMs)

### Key Classes

- `IIntercom` - Public interface defining all Intercom operations
- `Intercom` - Static accessor class providing `Intercom.Default` singleton
- `IntercomImplementation` - Platform-specific implementations
- `IntercomOptions` - Both platforms' API keys and identity-verification secrets, plus
  `LogLevel`/`AutoInitialize`. Platform-neutral on purpose (`OperatingSystem.IsAndroid()`,
  not `#if`), so the tests compile it and exercise credential resolution through the internal
  `PlatformOverride` seam. `UseIntercom` registers it as a singleton and, unless
  `AutoInitialize` is off, initializes from the MAUI lifecycle — `OnApplicationCreate` on
  Android, `FinishedLaunching` on iOS. Doing it inline in `UseIntercom` would run inside
  `CreateMauiApp`, before the platform has finished its own startup.

### Packaging rules

- All three packages (`Plugin.Maui.Intercom`, `.iOS.Binding`, `.Android.Binding`) share one version (`Directory.Build.props` / `-p:Version`).
- The main package declares the bindings as platform-conditional NuGet dependencies; consumers must never need to reference a binding directly.
- Pack with explicit `dotnet pack`; `GeneratePackageOnBuild` is off everywhere.
- CI (`.github/workflows/publish.yml`): api-coverage (ubuntu) + version → android (Windows) + ios-binding (macos-26, Xcode 26.6) → main-package → consumer-test (clean-room) → deploy (release only, pushes the exact validated artifacts).
- The plugin consumes both bindings as packages, never project references, so a change to `src/android/native` is invisible until the binding is packed. `build.ps1` does that locally (packs into `artifacts/local-feed`, restores against it). It also overrides `IntercomIosBindingVersion` with an already-published version, because the iOS binding cannot be built off macOS — never pass that override when packing.

## Configuration Notes

- Sample app uses `appsettings.json` (+ git-ignored `appsettings.Development.json`) for Intercom credentials; never commit real keys.
- Intercom requires initialization with API key and App ID before use.
