# Plugin.Maui.Intercom.Android.Ably

Optional Ably realtime support for [Plugin.Maui.Intercom](https://www.nuget.org/packages/Plugin.Maui.Intercom) on Android.

## What it is for

Intercom's Android SDK uses [Ably](https://ably.com) for live conversation updates — new messages
arriving while the messenger is open, typing indicators, unread-count changes. That client is not
part of `Plugin.Maui.Intercom`, so by default Intercom falls back to polling and logs a warning:

```
W  Intercom realtime  No realtime ...
```

The messenger still works without this package. Add it only if you want live updates.

## Usage

Reference it alongside the main package. There is no API to call and nothing to initialize — the
Intercom SDK picks the client up off the classpath.

```xml
<PackageReference Include="Plugin.Maui.Intercom" Version="..." />
<PackageReference Include="Plugin.Maui.Intercom.Android.Ably" Version="..." />
```

Use the same version as `Plugin.Maui.Intercom`; all packages in this repository share one version.

## Why it is a separate package

Intercom's POM asks for `io.ably:ably-android`, whose dependency closure includes **Firebase
Messaging**. Every Ably type Intercom actually references is core `ably-java`:

```
io.ably.lib.realtime.{AblyRealtime, Channel, Connection, ConnectionState, ConnectionStateListener}
io.ably.lib.rest.Auth$TokenCallback
io.ably.lib.types.{ClientOptions, ErrorInfo, Message}
```

Nothing from the Firebase-backed push surface is touched, so this package vendors `ably-java` and
its four transitive jars instead. No Firebase dependency is imposed on anyone, and apps that do not
want realtime pay nothing.

## Contents

Vendored, version-pinned in `Directory.Build.props`:

| Artifact | Purpose |
|---|---|
| `io.ably:ably-java` | realtime + REST client |
| `io.ably:network-client-core` | transport abstraction |
| `io.ably:network-client-okhttp` | OkHttp transport |
| `org.msgpack:msgpack-core` | wire format |
| `com.davidehrmann.vcdiff:vcdiff-core` | delta decoding |

`gson` and `okhttp` are declared as package dependencies; both are already present in any app using
`Plugin.Maui.Intercom`.

## Platform

- Target frameworks: `net9.0-android` and `net10.0-android`
- Minimum Android: 6.0 (API 23)
- iOS needs nothing equivalent — the Intercom iOS SDK ships its realtime transport inside
  `Intercom.xcframework`.
