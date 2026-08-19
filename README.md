![](nuget.png)
# Plugin.Maui.Intercom

`Plugin.Maui.Intercom` adds [Intercom](https://www.intercom.com/) to your .NET MAUI application.

- **Android**: a classic binding of the Intercom Android SDK plus a small native wrapper.
- **iOS**: a binding of the official Intercom iOS `Intercom.xcframework`, generated with [swift-dotnet-bindings](https://github.com/justinwojo/swift-dotnet-bindings).

Both platforms are working.

<img width="403" height="696" alt="Screenshot 2026-01-20 134953" src="https://github.com/user-attachments/assets/9696d97e-87a2-450a-bd76-ed261101f2f0" />
<img width="395" height="505" alt="Screenshot 2026-01-20 124137" src="https://github.com/user-attachments/assets/c4f5a049-cdbe-46fc-bce4-bc1b4260c8d2" />

## Quick start

1. Install the package: `dotnet add package Plugin.Maui.Intercom`.
2. Call `.UseIntercom(...)` in `MauiProgram.cs` with your App ID and platform API keys (see [Setup](#setup)).
3. Log a user in and present the messenger:

```csharp
await Intercom.Default.LoginUnidentifiedUserAsync();
Intercom.Default.Present();
```

## Install

[![NuGet](https://img.shields.io/nuget/v/Plugin.Maui.Intercom.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.Maui.Intercom/)

Available on [NuGet](http://www.nuget.org/packages/Plugin.Maui.Intercom). Install with the dotnet CLI (`dotnet add package Plugin.Maui.Intercom`) or the NuGet Package Manager in Visual Studio.

The platform binding packages (`Plugin.Maui.Intercom.iOS.Binding`, `Plugin.Maui.Intercom.Android.Binding`) are platform-specific NuGet dependencies of the main package and restore automatically — never reference them directly.

### Supported platforms and versions

| | Version |
|----------|---------------------------|
| .NET | **.NET 9 and .NET 10** (`net9.0-ios`, `net9.0-android`, `net10.0-ios`, `net10.0-android`) |
| .NET MAUI | 9.x or 10.x |
| iOS | 15.0+ |
| Android | 6.0 (API 23)+ |

### Native SDK versions (pinned)

| Platform | Intercom SDK Version |
|----------|---------------------|
| Android  | 18.8.0              |
| iOS      | 19.7.2              |

### Optional: Android realtime (`Plugin.Maui.Intercom.Android.Ably`)

Intercom's Android SDK uses [Ably](https://ably.com) for live conversation updates — messages arriving while the messenger is open, typing indicators, unread-count changes. That client is **not** included by default, so out of the box Android falls back to polling and logs:

```
W  Intercom realtime  No realtime ...
```

The messenger works fine either way. Add the package only if you want live updates:

```xml
<PackageReference Include="Plugin.Maui.Intercom.Android.Ably" Version="<same as Plugin.Maui.Intercom>" />
```

There is no API to call and nothing to initialize — the Intercom SDK picks the client up off the classpath.

It is a separate, opt-in package because Intercom's POM asks for `io.ably:ably-android`, whose closure includes **Firebase Messaging**. Every Ably type Intercom actually references is core `ably-java`, so this package vendors that instead and imposes no Firebase dependency on anyone. iOS needs nothing equivalent — the Intercom iOS SDK ships its realtime transport inside `Intercom.xcframework`.

### Version notes

> **Breaking change (0.9.0):** the whole `IIntercom` surface was replaced. The previous API reached about a third of the native Intercom SDKs; 0.9 reaches all of it, and `eng/api-coverage.sh` fails the build if that stops being true. Every member was renamed or resignatured — see [MIGRATION.md](MIGRATION.md). Still 0.x deliberately: the surface has not been exercised on real devices long enough to promise compatibility.

> **.NET 9 support is back (0.9.0):** every package multi-targets `net9.0-*` and `net10.0-*` again, so a .NET 9 MAUI app can take the current release. .NET 9 reaches end of support on 2026-11-10; the net9 target frameworks will be dropped after that.

> **Breaking change (0.7.0):** the plugin targeted .NET 10 only from 0.7.0 through 0.8.x — .NET 9 consumers on those versions had to stay on 0.6.x (0.9.0 restores net9). The iOS binding was replaced: the former `MauiIntercomMaciOS` wrapper types and the public `DictionaryExtensions.ToNSDictionary` iOS helper were removed. The `IIntercom` interface itself is source-compatible, with three additions: `LogEvent(string name)`, `EnableLogging()` and `IsUserLoggedIn` — all implemented on both platforms.

## Setup

### Register in MauiProgram.cs

```csharp
using Plugin.Maui.Intercom;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseIntercom(options =>          // Add this
            {
                options.AppId = "abc12345";
                options.AndroidApiKey = "android_sdk-...";
                options.IosApiKey = "ios_sdk-...";
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        return builder.Build();
    }
}
```

Both platforms' keys go in, and the plugin picks the pair that matches the running platform — no `#if ANDROID` in your app. It then calls `Initialize` for you from the platform lifecycle (`Application.OnCreate` on Android, `didFinishLaunching` on iOS), which is the earliest point each native SDK accepts it. See [Initialization](#initialization) for credentials that are not known at startup.

### Android configuration

Add these permissions to your `Platforms/Android/AndroidManifest.xml`:

```xml
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
<uses-permission android:name="android.permission.INTERNET" />
```

### iOS configuration

No additional configuration is required. The native `Intercom.framework` (including its resource bundles and `PrivacyInfo.xcprivacy`) is embedded and signed automatically by the binding package's MSBuild targets.

## API usage

> Upgrading from 0.7/0.8? Every member was renamed or resignatured in 0.9 — see [MIGRATION.md](MIGRATION.md) for the mapping.

### The whole surface

Async members return a `Task` that faults with `IntercomException`, which carries the native error code. Everything else is synchronous and dispatches to the UI thread internally.

| Member | Android | iOS |
| --- | :---: | :---: |
| `IsSupported` | ✅ | ✅ |
| `Initialize(apiKey, appId)` | ✅ | ✅ |
| `ChangeWorkspace(apiKey, appId)` | ✅ | — |
| `EnableLogging(level)` | ✅ | on/off only |
| `LoginUnidentifiedUserAsync()` | ✅ | ✅ |
| `LoginUserAsync(attributes)` | ✅ | ✅ |
| `UpdateUserAsync(attributes)` | ✅ | ✅ |
| `SetAuthTokensAsync(tokens)` | ✅ | ✅ |
| `SetUserHash(userHash)` | ✅ | ✅ |
| `SetUserJwt(jwt)` | ✅ | ✅ |
| `Logout()` | ✅ | ✅ |
| `IsUserLoggedIn` | ✅ | ✅ |
| `FetchLoggedInUserAttributes()` | ✅ | ✅ |
| `LogEvent(name, metadata)` | ✅ | ✅ |
| `Present(space)` | ✅ | ✅ |
| `PresentContent(content)` | ✅ | ✅ |
| `PresentMessageComposer(initialMessage)` | ✅ | ✅ |
| `HideIntercom()` | ✅ | ✅ |
| `SetLauncherVisible(visible)` | ✅ | ✅ |
| `SetInAppMessagesVisible(visible)` | ✅ | ✅ |
| `SuppressProactiveContent(types)` | ✅ | ✅ |
| `SetBottomPaddingDp(dp)` | ✅ | ✅ |
| `SetThemeMode(mode)` | ✅ | session only |
| `UnreadConversationCount` | ✅ | ✅ |
| `UnreadConversationCountChanged` | ✅ | ✅ |
| `UnreadConversationCounts` (`IObservable<int>`) | ✅ | ✅ |
| `FetchHelpCenterCollectionsAsync()` | ✅ | ✅ |
| `FetchHelpCenterCollectionAsync(id)` | ✅ | ✅ |
| `SearchHelpCenterAsync(term)` | ✅ | ✅ |
| `SendPushTokenToIntercomAsync(token)` | ✅ | ✅ |
| `IsIntercomPush(payload)` | ✅ | ✅ |
| `HandlePush(payload)` | ✅ | ✅ |

Members marked `—` throw `PlatformNotSupportedException` with the reason in the message. `ChangeWorkspace` is the only one: the Intercom iOS SDK has no `changeWorkspace` equivalent. `SetThemeMode` works on both platforms as of Intercom iOS 19.x, but iOS resets the override when the app restarts while Android's persists.

That table is enforced, not aspirational: `eng/api-coverage.sh` extracts the public API of the pinned Android AARs and iOS xcframework and fails the build if a native symbol is not classified in `eng/api-coverage.json`.

### Initialization

Find your API keys and App ID in your [Intercom settings](https://app.intercom.com/a/apps/_/settings/android). The API key is platform-specific: an iOS key will not work on Android. So is the identity-verification secret.

`UseIntercom(options => ...)` is the normal path — it initializes during startup, from the platform lifecycle, and registers `IntercomOptions` as a singleton you can inject.

#### From configuration

`UseIntercom` also takes an `IConfiguration`, keyed by property name — `AppId`, `AndroidApiKey`, `IosApiKey`, `AndroidSecret`, `IosSecret`, `LogLevel`, `AutoInitialize`. Blank values are treated as absent, so a checked-in `appsettings.json` full of placeholders will not overwrite anything set in code. Pass a configuration that is already populated — `builder.Configuration` is still empty at this point unless you added your sources to it first.

```csharp
var config = new ConfigurationBuilder().AddJsonFile(...).Build();
builder.Configuration.AddConfiguration(config);

builder.UseIntercom(config.GetSection("Intercom"), options =>
{
#if DEBUG
    options.LogLevel = IntercomLogLevel.Verbose;   // applied just before Initialize
#endif
});
```

#### When the credentials arrive later

Fetching keys from your backend, or choosing a workspace per tenant? Turn the startup hook off and initialize when you have them:

```csharp
builder.UseIntercom(options =>
{
    options.AutoInitialize = false;
    options.AppId = ...;
});

// later, wherever the credentials turn up — idempotent, returns false if already initialized
var options = services.GetRequiredService<IntercomOptions>();
options.AndroidApiKey = fetched.AndroidKey;
options.IosApiKey = fetched.IosKey;
Intercom.Default.Initialize(options);
```

`Intercom.Default.Initialize(apiKey, appId)` is still there if you would rather do the whole thing by hand; `UseIntercom()` with no arguments registers `IIntercom` and initializes nothing.

#### Identity verification

`options.Secret` resolves the platform's secret. `ComputeUserHash` turns it into the HMAC-SHA256 digest `SetUserHash` wants; `ComputeUserJwt` mints the HS256 token `SetUserJwt` wants, which is what a workspace with Messenger Security enforced requires:

```csharp
Intercom.Default.SetUserHash(options.ComputeUserHash("user@example.com"));

// or, with Messenger Security enforced — one hour by default
Intercom.Default.SetUserJwt(options.ComputeUserJwt("user-123", "user@example.com"));

// sensitive attributes Intercom only accepts through a JWT
Intercom.Default.SetUserJwt(options.ComputeUserJwt(
    "user-123",
    lifetime: TimeSpan.FromMinutes(15),
    additionalClaims: new Dictionary<string, object?>
    {
        ["sensitive_attribute1"] = "...",
        ["plan_tier"] = 3
    }));
```

`user_id`, `email`, `iat` and `exp` are written for you and are rejected in `additionalClaims`. Other claim values must be a string, a numeric type, a `bool` or a `DateTimeOffset` (written as Unix seconds); a null value is left out. No IdentityModel dependency — an HS256 token is two Base64Url segments and an HMAC.

Anything in `IntercomOptions` ships inside the app binary and is extractable. That is fine for the API keys, which are client-side by design — it is not fine for the secret, and a JWT minted on device is a bearer token sitting next to the secret that signs it. Intercom's guidance is to issue both from your server and hand the app the result. `AndroidSecret`/`IosSecret`, `ComputeUserHash` and `ComputeUserJwt` are a development convenience.

### Logging users in

```csharp
// No identifiable information
await Intercom.Default.LoginUnidentifiedUserAsync();

// Identified, with attributes sent along with the login
var attributes = new IntercomUserAttributes
{
    UserId = "user-123",
    Email = "user@example.com",
    Name = "Bob",
    SignedUpAt = DateTimeOffset.UtcNow,
};
attributes.CustomAttributes["items_in_cart"] = 8;
attributes.Companies.Add(new IntercomCompany { CompanyId = "abc1234", Name = "Sample Co" });

await Intercom.Default.LoginUserAsync(attributes);
```

Failures throw `IntercomException`, which carries the native code:

```csharp
try
{
    await Intercom.Default.LoginUserAsync(attributes);
}
catch (IntercomException e)
{
    logger.LogError("Intercom login failed ({Code}): {Message}", e.ErrorCode, e.Message);
}
```

Update attributes later with `UpdateUserAsync`, which takes the same object.

### Securing the session

```csharp
// Identity Verification: an HMAC-SHA256 digest generated on your server
Intercom.Default.SetUserHash("hmac-sha256-hash");

// Messenger Security (newer, and required when your workspace enforces it)
Intercom.Default.SetUserJwt(jwtFromYourServer);
```

Call either one *before* logging a user in.

### Presenting Intercom UI

```csharp
Intercom.Default.Present();                              // Home
Intercom.Default.Present(IntercomSpace.Messages);        // Conversations
Intercom.Default.Present(IntercomSpace.HelpCenter);
Intercom.Default.Present(IntercomSpace.Tickets);

Intercom.Default.PresentMessageComposer("I need help");  // Composer, pre-filled

Intercom.Default.PresentContent(new IntercomContent.Article("article-id"));
Intercom.Default.PresentContent(new IntercomContent.Carousel("carousel-id"));
Intercom.Default.PresentContent(new IntercomContent.Survey("survey-id"));
Intercom.Default.PresentContent(new IntercomContent.Conversation("conversation-id"));
Intercom.Default.PresentContent(new IntercomContent.HelpCenterCollections(["collection-id"]));
Intercom.Default.PresentContent(new IntercomContent.Ticket("ticket-id"));

Intercom.Default.HideIntercom();
```

All presentation happens on the main thread automatically.

Intercom reports *every* Messenger failure the same way — a generic "something went wrong" screen — so check that a user is actually logged in before presenting, and turn on the native SDK's own logging while diagnosing:

```csharp
Intercom.Default.EnableLogging();       // call before Initialize; not for release builds
Intercom.Default.Initialize(apiKey, appId);

if (!Intercom.Default.IsUserLoggedIn)
{
    await Intercom.Default.LoginUnidentifiedUserAsync();
}

Intercom.Default.Present();
```

`EnableLogging` writes to the Xcode console on iOS and to logcat (tag `intercom`) on Android. The usual causes of the error screen: no logged-in user, an API key that belongs to the other platform, an App ID that does not match the key, or identity verification enabled on the workspace without a matching `SetUserHash`/`SetUserJwt` call *before* login.

### Events

```csharp
Intercom.Default.LogEvent("clicked_checkout");

Intercom.Default.LogEvent("clicked_checkout", new Dictionary<string, object?>
{
    ["order_total"] = 42.50m,
    ["currency"] = "EUR",
    ["at"] = DateTimeOffset.UtcNow,
});
```

Metadata values must be strings, numbers, booleans or dates. Types are preserved on the way across, so sending `"42"` where you previously sent `42` changes the attribute's type in your workspace.

### Unread conversations

```csharp
badge.Text = Intercom.Default.UnreadConversationCount.ToString();

// The event…
Intercom.Default.UnreadConversationCountChanged += (_, count) => badge.Text = count.ToString();

// …or the observable, for MVVM. It replays the current count immediately on subscribe, so
// there is no separate initial read, and it composes (throttle, DistinctUntilChanged, bind).
// Dispose the subscription to unsubscribe.
_subscription = Intercom.Default.UnreadConversationCounts
    .Subscribe(count => badge.Text = count.ToString());
```

The native listener is only attached while at least one handler is subscribed.

### Help Center data

For building your own Help Center UI instead of presenting Intercom's:

```csharp
var collections = await Intercom.Default.FetchHelpCenterCollectionsAsync();
var content = await Intercom.Default.FetchHelpCenterCollectionAsync(collections[0].Id);
var results = await Intercom.Default.SearchHelpCenterAsync("refund");

Intercom.Default.PresentContent(new IntercomContent.Article(results[0].ArticleId));
```

`HelpCenterCollectionContent.Sections` is Android-only and is always empty on iOS — the iOS SDK's collection model has no sections concept.

### Push notifications

Your app still owns push registration: Firebase Messaging on Android, and `RegisteredForRemoteNotifications` on iOS. Hand Intercom the token that produces, then let it claim the payloads it sent.

```csharp
// Android: from FirebaseMessagingService.OnNewToken
// iOS: from RegisteredForRemoteNotifications, hex-encoded
await Intercom.Default.SendPushTokenToIntercomAsync(token);

if (Intercom.Default.IsIntercomPush(payload))
{
    Intercom.Default.HandlePush(payload);
}
```

### Customization

```csharp
Intercom.Default.SetLauncherVisible(true);        // Show/hide the launcher
Intercom.Default.SetInAppMessagesVisible(false);  // Suppress in-app messages
Intercom.Default.SetBottomPaddingDp(24);          // Device-independent pixels on both platforms
Intercom.Default.SetThemeMode(IntercomThemeMode.Dark);  // iOS resets this on app restart

// Carousels and surveys, suppressed independently of in-app messages. Each call replaces
// the suppressed set, so pass an empty list to un-suppress everything.
Intercom.Default.SuppressProactiveContent([IntercomProactiveContentType.Carousel]);
Intercom.Default.SuppressProactiveContent([]);
```

`SetBottomPaddingDp` takes dp on both platforms. The native APIs disagree — Android's `setBottomPadding` takes raw pixels and iOS's takes points — so the Android implementation scales by the display density, and the same argument means the same physical distance.

### Logout

```csharp
Intercom.Default.Logout();
```

### Dependency injection

`UseIntercom()` registers `IIntercom` and `IntercomOptions` as singletons, so you can constructor-inject them:

```csharp
public class MyViewModel
{
    private readonly IIntercom _intercom;

    public MyViewModel(IIntercom intercom) => _intercom = intercom;

    public void ShowMessenger() => _intercom.Present();
}
```

The registered `IIntercom` is the same object as `Intercom.Default`, so the two styles can be mixed.

For tests, `Intercom.SetDefault(fake)` swaps the implementation behind the static `Intercom.Default` accessor — set a fake in setup and reset it with `Intercom.SetDefault(null)` in teardown. Injected `IIntercom` is already mockable through DI; this covers code that reaches for the static instead.

## Architecture

Three NuGet packages, all versioned identically:

| Package | Role |
|---------|------|
| `Plugin.Maui.Intercom` | User-facing MAUI abstraction (`IIntercom`, DI setup) |
| `Plugin.Maui.Intercom.iOS.Binding` | iOS native binding, generated by swift-dotnet-bindings |
| `Plugin.Maui.Intercom.Android.Binding` | Android native binding (Gradle interop) |

The main package declares the binding packages as platform-conditional dependencies (any `-ios` target framework → iOS binding, any `-android` → Android binding), so consumers only ever add `Plugin.Maui.Intercom`. Each package carries a dependency group and a `lib/` folder per shipped band (`net9.0-*` and `net10.0-*`).

### How the iOS binding works

- The exact Intercom `Intercom.xcframework` (19.7.2) is **checked into the repository** at `src/macios/Intercom.iOS.Binding/` — ordinary builds never download anything. The SHA-256 of the official release archive is recorded in `eng/intercom-ios.sha256`.
- `src/macios/Intercom.iOS.Binding` uses the `SwiftBindings.Sdk` MSBuild project SDK (version pinned in `global.json` under `msbuild-sdks`). Intercom is a mixed Swift/Objective-C framework whose complete public API is exported through its ObjC umbrella header, so the binding uses the generator's pure-ObjC pipeline (`SwiftFrameworkType=ObjC` + `IsBindingProject=true`): at build time on macOS it parses the framework headers with clang, generates the bgen `ApiDefinition`, and compiles a single binding assembly (namespace `IntercomBinding`).
- The whole surface is generated — there is no hand-written supplement. Up to SwiftBindings.Sdk 0.17.0 the generator's `-fmodules` clang retry made clang build Intercom as a module, which collapsed each `#import <Intercom/SiblingHeader.h>` into a module import and silently dropped every declaration behind it (`ICMUserAttributes`, `IntercomContent`, the help-center types, the `Space`/`ContentType` NS_ENUMs). Two supplement files covered that gap; [SwiftBindings 0.18.0](https://github.com/justinwojo/swift-dotnet-bindings/releases/tag/sdk-v0.18.0) passes `-fmodule-name` on the retry so those headers parse textually, and the supplements were removed.
- The package uses the standard iOS binding layout, once per band (`lib/net9.0-ios18.0/` and `lib/net10.0-ios26.0/`): the managed assembly plus `Intercom.iOS.Binding.resources.zip` beside it containing the full `Intercom.xcframework` (device + simulator slices, resource bundles, `PrivacyInfo.xcprivacy`). The .NET iOS SDK unpacks it in consuming apps and applies the `NativeReference` automatically — embedding, linking and signing included.
- The remaining generated C# is a **build output, not committed**: the generator ships as a pinned NuGet SDK, so builds are deterministic from pinned inputs (SDK version + vendored xcframework) without every contributor installing anything. CI uploads the generated sources as an artifact for review.

## Building from source

Requirements:

| Tool | Version |
|------|---------|
| .NET SDK | 10.0.1xx (`global.json`) |
| .NET MAUI workloads | `maui-ios`, `maui-android` |
| Xcode (iOS binding + apps) | 26+ (CI pins 26.6) |
| macOS | Required for anything iOS-native |
| JDK (Android binding) | 17 |

```bash
# Android binding (any OS with JDK 17)
dotnet pack src/android/Intercom.Android.Binding/Intercom.Android.Binding.csproj -c Release -o artifacts/packages

# iOS binding (macOS only — generates the binding and packs it)
eng/generate-ios-binding.sh --output artifacts/packages

# Main package, resolving the freshly packed bindings from the local folder
dotnet restore src/Plugin.Maui.Intercom/Plugin.Maui.Intercom.csproj --configfile <nuget.config pointing at artifacts/packages>
dotnet pack src/Plugin.Maui.Intercom/Plugin.Maui.Intercom.csproj -c Release --output artifacts/packages
```

> The iOS binding project is intentionally not in the solution file; it can only build on macOS.

On Windows, `build.ps1` does the whole Android loop: it packs the Android binding into `artifacts/local-feed` and restores the plugin against it. That step is not optional — the plugin consumes the binding as a package, not a project reference, so a change to `src/android/native` is invisible until it has been packed.

### Native API coverage

```bash
eng/api-coverage.sh            # gate: fail on unclassified or removed native symbols
eng/api-coverage.sh --strict   # also fail on anything still marked todo (what CI runs)
eng/api-coverage.sh --update   # classify newly-appeared symbols as todo for triage
eng/api-coverage.sh --print    # dump the extracted native inventory
```

This is what keeps `IIntercom` honest. It runs `javap` over the pinned Intercom AARs and text-parses the vendored `Intercom.xcframework` headers, then checks every public symbol against `eng/api-coverage.json`, where each is `covered` (with the plugin member that surfaces it), `skipped` (with a reason) or `todo`. An Intercom upgrade that adds or removes API therefore fails the build instead of drifting silently.

Needs bash, python3, a JDK and unzip — no .NET, no Xcode, no network. Runs on Windows via Git Bash. `eng/update-intercom.sh` runs it automatically after bumping the iOS SDK so the API delta lands in the same PR as the version bump.

The published docs at developers.intercom.com are *not* usable as the source of truth: they omit `setUserJwt`, `setAuthTokens` and the whole `IntercomPushClient`, and they described an iOS `setThemeOverride:` for several releases before the shipped headers actually declared one. The vendored artifacts are.

### Tests

```bash
dotnet test src/tests/Plugin.Maui.Intercom.Tests/Plugin.Maui.Intercom.Tests.csproj
```

Runs on plain `net10.0` and compiles the plugin's platform-neutral sources directly, since `Plugin.Maui.Intercom` itself only targets android and ios. It covers the model invariants, the generic-.NET fallback (every `IIntercom` member must be present and must throw), and the integrity of `eng/api-coverage.json` — every `covered` entry has to name a member that really exists, which is the half `eng/api-coverage.sh` cannot check.

### Clean-room package test

To prove the packages work from outside the source tree (this is what CI gates releases on):

```bash
eng/test-consumer.sh --version <version> --feed artifacts/packages --clear-cache
```

This creates a minimal MAUI app in `/tmp/icom-test` that references only `Plugin.Maui.Intercom` by package ID from a temporary NuGet feed, verifies the iOS binding resolves transitively, builds for the iOS simulator and (unsigned) for `ios-arm64`, and inspects the resulting `.app` for the embedded framework, resource bundles and privacy manifest.

```bash
eng/validate-packages.sh --version <version> --feed artifacts/packages
```

opens each `.nupkg` and asserts IDs, versions, dependency groups, native assets and the absence of machine-specific paths or simulator slices in device trees.

### Upgrading the Intercom iOS SDK

```bash
eng/update-intercom.sh <new-version> [<expected-sha256>]
eng/generate-ios-binding.sh
```

The update script downloads the exact tagged release from `intercom/intercom-ios`, verifies/records its SHA-256, replaces the vendored xcframework, updates the `IntercomIosSdkVersion` pin in `Directory.Build.props` and runs `eng/api-coverage.sh --update` so the API delta is visible in the same PR. Triage anything it recorded as `todo`, then regenerate and fix any compile errors in `src/Plugin.Maui.Intercom/Intercom.macios.cs`.

```bash
eng/dump-ios-binding-api.sh --check
```

confirms the generator actually produced every member `Intercom.macios.cs` calls. Anything missing is a generator bug: report it upstream and mark the symbol `todo` in `eng/api-coverage.json` — do not reintroduce hand-written binding supplements.

To update the swift-dotnet-bindings generator, change the `SwiftBindings.Sdk` version in `global.json` (`msbuild-sdks`) — releases are tagged `sdk-vX.Y.Z` upstream.

## Troubleshooting

### Android: Compose version mismatch

Runtime crashes with `NoSuchMethodError` in Compose classes: use Intercom SDK 18.8.0 or later, which is compatible with AndroidX Compose BOM 2026.06.01.

### iOS: Build on Windows

iOS builds require macOS. The sample project skips iOS targets on Windows; the iOS binding project only builds on macOS with Xcode 26+.

### iOS: Missing Swift symbols / linker errors

The binding package ships the required wrapper frameworks and the `buildTransitive` targets add the `NativeReference`s automatically. If the linker reports missing `Intercom` or Swift symbols:

- confirm `Plugin.Maui.Intercom.iOS.Binding` appears in your app's resolved packages (`obj/project.assets.json`),
- clear the NuGet cache (`dotnet nuget locals all --clear`) and restore again,
- make sure you are on the .NET 10 iOS workload matching your Xcode version.

### iOS: Framework embedding / signing

`Intercom.framework` is dynamic: it must end up in `YourApp.app/Frameworks` and be code-signed with the app. Both are handled by the standard `NativeReference` pipeline; if a device build fails signing on the nested framework, verify your signing identity also applies to embedded frameworks (automatic signing does).

### iOS: Trimming / AOT

Release device builds use the default MAUI trimmer settings. The binding assemblies carry the metadata the trimmer needs; do not disable trimming globally. If you enable aggressive trimming (`TrimMode=full`) and hit a missing-selector issue at runtime, file an issue with the trimmed member name.

### iOS: Simulator

The generated binding may route a small number of members through Swift calling conventions that are limited under the Mono JIT on the iOS **simulator** (device builds are unaffected). The plugin's API surface uses Intercom's Objective-C entry points and is not affected.

## Acknowledgements

Thanks to these projects and people:

- [swift-dotnet-bindings](https://github.com/justinwojo/swift-dotnet-bindings) - The Swift/ObjC → .NET binding generator used for the iOS binding
- [.NET MAUI Community Toolkit](https://github.com/CommunityToolkit/Maui) - For the original Native Library Interop pattern
- [Intercom](https://www.intercom.com/) - For their excellent customer messaging platform
