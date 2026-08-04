# Migrating from 0.x to 1.0

1.0 replaces the whole `IIntercom` surface. The 0.x API covered roughly a third of the native
Intercom SDKs and lost information in several of the members it did have; 1.0 covers all of it
and the coverage is enforced by `eng/api-coverage.sh` in CI.

Nothing was deprecated in place, because most of the changes are signature changes rather than
renames — an `[Obsolete]` shim would have had to guess at the parts that were missing.

## Member mapping

| 0.x | 1.0 | Why |
| --- | --- | --- |
| `Initialize(apiKey, appId)` | unchanged | On Android it now initializes from the `Application` rather than the current `Activity`, so calling it before the first window exists works instead of silently failing. |
| `Register(onSuccess, onFailure)` | `await LoginUnidentifiedUserAsync()` | Failures carry the native error code instead of only a message. |
| `RegisterWithUserId(id, onSuccess, onFailure)` | `await LoginUserAsync(new IntercomUserAttributes { UserId = id })` | The attributes object also carries name, phone, companies and custom attributes, which 0.x could not send at all. |
| `RegisterWithEmail(email, …)` | `await LoginUserAsync(new IntercomUserAttributes { Email = email })` | As above. A user ID and email can now be sent together. |
| `PresentMessenger(null)` | `Present()` | `PresentMessenger` conflated two native calls, and could not reach the Messages or Tickets spaces. |
| `PresentMessenger("text")` | `PresentMessageComposer("text")` | Same reason. |
| `PresentHelpCenter()` | `Present(IntercomSpace.HelpCenter)` | |
| `PresentSupportCenter()` | `Present(IntercomSpace.Home)` | The old name did not describe what it opened. |
| `PresentCarousel(id)` | `PresentContent(new IntercomContent.Carousel(id))` | Articles, surveys, conversations, tickets and collection lists are now reachable too. |
| `SetVisible(visible)` | `SetLauncherVisible(visible)` | The old name did not say *what* it showed, and in-app message visibility is a separate native setting — now `SetInAppMessagesVisible`. |
| `SetBottomPadding(int)` | `SetBottomPaddingDp(double)` | **Behaviour change.** The old member passed the value straight through, so it meant pixels on Android and points on iOS: the same number produced different padding per platform. The new one takes dp on both and scales on Android. |
| `LogEvent(name)` | `LogEvent(name, metadata = null)` | Event metadata was previously unreachable. |
| `EnableLogging()` | `EnableLogging(level = Verbose)` | Android supports levels; iOS is still on/off. |
| `SetUserHash(hash)` | unchanged | See `SetUserJwt` below if your workspace enforces Messenger Security. |
| `Logout()`, `IsUserLoggedIn` | unchanged | |

## Error handling

0.x reported failures through `Action<string?>` and dropped the error code. 1.0 faults the
returned `Task` with `IntercomException`:

```csharp
// 0.x
Intercom.Default.RegisterWithEmail(email,
    onSuccess: () => { },
    onFailure: error => logger.LogError("Registration failed: {Error}", error));

// 1.0
try
{
    await Intercom.Default.LoginUserAsync(new IntercomUserAttributes { Email = email });
}
catch (IntercomException e)
{
    // e.ErrorCode is IntercomError.getErrorCode() on Android and NSError.Code on iOS.
    // e.NativeDomain is the NSError domain on iOS, null on Android.
    logger.LogError("Login failed ({Code}): {Message}", e.ErrorCode, e.Message);
}
```

This matters in practice because the Messenger shows the same generic error screen for almost
every failure — the code is the only thing that distinguishes "already logged in" from a
network problem or a workspace misconfiguration.

## New in 1.0

None of these existed in 0.x on either platform:

- **User attributes** — `IntercomUserAttributes`, `IntercomCompany`, `UpdateUserAsync`,
  `FetchLoggedInUserAttributes`
- **Messenger Security** — `SetUserJwt`. Workspaces that enforce it could not use 0.x at all.
- **Fin Actions** — `SetAuthTokensAsync`
- **Unread conversations** — `UnreadConversationCount`, `UnreadConversationCountChanged`
- **Help Center data** — `FetchHelpCenterCollectionsAsync`, `FetchHelpCenterCollectionAsync`,
  `SearchHelpCenterAsync` and their model types
- **Push** — `SendPushTokenToIntercomAsync`, `IsIntercomPush`, `HandlePush`
- **Spaces and content** — `IntercomSpace.Messages`, `IntercomSpace.Tickets`, and the
  `Article`, `Survey`, `Conversation`, `Ticket` and `HelpCenterCollections` content types
- **Chrome** — `HideIntercom`, `SetInAppMessagesVisible`, `SetThemeMode` (Android)
- **Workspace switching** — `ChangeWorkspace` (Android)

## Platform-specific members

`ChangeWorkspace`, `SetThemeMode` and `IntercomContent.Ticket` exist only on Android and throw
`PlatformNotSupportedException` on iOS. Use `Present(IntercomSpace.Tickets)` instead of the
ticket content type if you need one code path.

The documentation site describes an iOS `setThemeOverride:`, but no such symbol exists in the
pinned Intercom iOS SDK's umbrella headers, so there is nothing to bind.
