namespace Plugin.Maui.Intercom;

/// <summary>
///     The Intercom Messenger.
/// </summary>
/// <remarks>
///     <para>
///         Every member maps onto a documented entry point of the native Intercom SDKs. The
///         mapping is machine-checked: <c>eng/api-coverage.sh</c> extracts the public API of
///         the pinned Android AARs and iOS xcframework and fails the build if a native symbol
///         is not classified in <c>eng/api-coverage.json</c>.
///     </para>
///     <para>
///         Operations that the native SDK reports asynchronously return a <see cref="Task" />
///         that faults with <see cref="IntercomException" />, carrying the native error code.
///         Everything else is synchronous and dispatches to the UI thread internally.
///     </para>
///     <para>
///         A handful of members exist on only one platform. Those throw
///         <see cref="PlatformNotSupportedException" /> on the other and say so in their docs.
///     </para>
/// </remarks>
public interface IIntercom
{
    // ── Lifecycle ───────────────────────────────────────────────────────────

    /// <summary>
    ///     Initializes Intercom with your API key and App ID.
    /// </summary>
    /// <param name="apiKey">The platform-specific Intercom API key.</param>
    /// <param name="appId">Your Intercom App ID.</param>
    /// <remarks>
    ///     Call once at startup, before any other member. The API key differs per platform:
    ///     an iOS key will not work on Android and vice versa.
    /// </remarks>
    void Initialize(string apiKey, string appId);

    /// <summary>
    ///     Points Intercom at a different workspace at runtime.
    /// </summary>
    /// <param name="apiKey">The Intercom API key of the new workspace.</param>
    /// <param name="appId">The App ID of the new workspace.</param>
    /// <exception cref="PlatformNotSupportedException">
    ///     On iOS — the iOS SDK has no <c>changeWorkspace</c> equivalent.
    /// </exception>
    void ChangeWorkspace(string apiKey, string appId);

    /// <summary>
    ///     Turns on the native SDK's own logging.
    /// </summary>
    /// <param name="level">
    ///     How much to log. On iOS only <see cref="IntercomLogLevel.Disabled" /> versus
    ///     anything else is meaningful, and logging cannot be turned back off once enabled.
    /// </param>
    /// <remarks>
    ///     Call before <see cref="Initialize" />. The native SDK then logs the reason behind
    ///     failures that the Messenger only surfaces as a generic "something went wrong"
    ///     screen (bad API key/App ID, identity-verification mismatch, no logged-in user).
    ///     Output goes to the Xcode console on iOS and to logcat (tag <c>intercom</c>) on
    ///     Android. Do not leave this enabled in release builds.
    /// </remarks>
    void EnableLogging(IntercomLogLevel level = IntercomLogLevel.Verbose);

    // ── Identity ────────────────────────────────────────────────────────────

    /// <summary>
    ///     Logs in a user with no identifiable information.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait, not the native operation.</param>
    /// <exception cref="IntercomException">The native SDK rejected the login.</exception>
    Task LoginUnidentifiedUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Logs in an identified user.
    /// </summary>
    /// <param name="attributes">
    ///     The user's attributes. <see cref="IntercomUserAttributes.UserId" /> or
    ///     <see cref="IntercomUserAttributes.Email" /> must be set; any other attributes set
    ///     here are sent along with the login.
    /// </param>
    /// <param name="cancellationToken">Cancels the wait, not the native operation.</param>
    /// <exception cref="ArgumentException"><paramref name="attributes" /> carries no identifier.</exception>
    /// <exception cref="IntercomException">The native SDK rejected the login.</exception>
    Task LoginUserAsync(IntercomUserAttributes attributes, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates the logged-in user's attributes.
    /// </summary>
    /// <param name="attributes">The attributes to change. Unset properties are left alone.</param>
    /// <param name="cancellationToken">Cancels the wait, not the native operation.</param>
    /// <exception cref="IntercomException">The native SDK rejected the update.</exception>
    Task UpdateUserAsync(IntercomUserAttributes attributes, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Supplies auth tokens for features that call your own APIs, such as Fin Actions.
    /// </summary>
    /// <param name="tokens">Token names mapped to their values.</param>
    /// <param name="cancellationToken">Cancels the wait, not the native operation.</param>
    /// <exception cref="IntercomException">The native SDK rejected the tokens.</exception>
    /// <remarks>A user must be logged in first.</remarks>
    Task SetAuthTokensAsync(IReadOnlyDictionary<string, string> tokens, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Secures the user session with an identity-verification hash.
    /// </summary>
    /// <param name="userHash">An HMAC-SHA256 digest of the user ID or email, keyed with your app secret.</param>
    /// <remarks>
    ///     Call before logging a user in. <see cref="SetUserJwt" /> is the newer mechanism;
    ///     use it when your workspace enforces Messenger Security.
    /// </remarks>
    void SetUserHash(string userHash);

    /// <summary>
    ///     Secures the user session with a JWT.
    /// </summary>
    /// <param name="jwt">A JWT signed with your app's secret key.</param>
    /// <remarks>
    ///     Call before logging a user in. Required when the workspace enforces Messenger
    ///     Security, which rejects hash-only sessions.
    /// </remarks>
    void SetUserJwt(string jwt);

    /// <summary>
    ///     Logs the current user out, dismissing any Intercom UI and clearing the local cache.
    /// </summary>
    void Logout();

    /// <summary>
    ///     Whether a user is currently logged in to Intercom.
    /// </summary>
    /// <remarks>
    ///     Presenting the Messenger before a successful login is the most common cause of the
    ///     Messenger's generic error screen. Check this before calling <see cref="Present" />.
    /// </remarks>
    bool IsUserLoggedIn { get; }

    /// <summary>
    ///     Reads back the logged-in user's identifiers.
    /// </summary>
    /// <returns>
    ///     The user's attributes, or <see langword="null" /> when no user is logged in. Only
    ///     the identifiers are populated: the native SDKs return the identity they hold, not
    ///     the full attribute set stored on the server.
    /// </returns>
    IntercomUserAttributes? FetchLoggedInUserAttributes();

    // ── Events ──────────────────────────────────────────────────────────────

    /// <summary>
    ///     Logs an event against the current user.
    /// </summary>
    /// <param name="name">The event name.</param>
    /// <param name="metadata">
    ///     Optional metadata. Values must be <see cref="string" />, a numeric type,
    ///     <see cref="bool" /> or <see cref="DateTimeOffset" />.
    /// </param>
    void LogEvent(string name, IReadOnlyDictionary<string, object?>? metadata = null);

    // ── Presentation ────────────────────────────────────────────────────────

    /// <summary>
    ///     Opens the Messenger at the given space.
    /// </summary>
    /// <param name="space">The space to open. Defaults to <see cref="IntercomSpace.Home" />.</param>
    void Present(IntercomSpace space = IntercomSpace.Home);

    /// <summary>
    ///     Opens a specific piece of Intercom content directly.
    /// </summary>
    /// <param name="content">The article, survey, carousel, conversation, ticket or collection list to show.</param>
    /// <exception cref="PlatformNotSupportedException">
    ///     On iOS, when <paramref name="content" /> is an <see cref="IntercomContent.Ticket" />.
    /// </exception>
    void PresentContent(IntercomContent content);

    /// <summary>
    ///     Opens the message composer.
    /// </summary>
    /// <param name="initialMessage">Text to pre-populate the composer with, or <see langword="null" /> for an empty composer.</param>
    void PresentMessageComposer(string? initialMessage = null);

    /// <summary>
    ///     Hides every Intercom window currently on screen.
    /// </summary>
    void HideIntercom();

    // ── Chrome ──────────────────────────────────────────────────────────────

    /// <summary>
    ///     Shows or hides the Intercom launcher.
    /// </summary>
    /// <param name="visible">Whether the launcher should be visible. It is hidden by default.</param>
    void SetLauncherVisible(bool visible);

    /// <summary>
    ///     Shows or hides in-app messages.
    /// </summary>
    /// <param name="visible">Whether in-app messages should be visible. They are visible by default.</param>
    /// <remarks>Does not affect carousels or surveys, which are presented explicitly.</remarks>
    void SetInAppMessagesVisible(bool visible);

    /// <summary>
    ///     Sets the distance between the bottom of the screen and the launcher and in-app messages.
    /// </summary>
    /// <param name="bottomPaddingDp">The padding in device-independent pixels.</param>
    /// <remarks>
    ///     The unit is device-independent pixels on both platforms. The native APIs disagree —
    ///     Android's <c>setBottomPadding</c> takes raw pixels while iOS's takes points — so the
    ///     Android implementation scales by the display density to keep the two consistent.
    /// </remarks>
    void SetBottomPaddingDp(double bottomPaddingDp);

    /// <summary>
    ///     Overrides the Messenger's light/dark appearance.
    /// </summary>
    /// <param name="mode">The theme to apply.</param>
    /// <exception cref="PlatformNotSupportedException">
    ///     On iOS — the pinned Intercom iOS SDK exposes no theme override on its public ObjC
    ///     surface. See <see cref="IntercomThemeMode" />.
    /// </exception>
    void SetThemeMode(IntercomThemeMode mode);

    // ── Unread conversations ────────────────────────────────────────────────

    /// <summary>
    ///     The number of unread conversations for the logged-in user.
    /// </summary>
    /// <remarks>Useful for badging your own launch button. Returns 0 when no user is logged in.</remarks>
    int UnreadConversationCount { get; }

    /// <summary>
    ///     Raised when <see cref="UnreadConversationCount" /> changes.
    /// </summary>
    /// <remarks>
    ///     The native listener is only registered while at least one handler is subscribed.
    ///     Handlers are invoked on the UI thread.
    /// </remarks>
    event EventHandler<int> UnreadConversationCountChanged;

    // ── Help Center data ────────────────────────────────────────────────────

    /// <summary>
    ///     Fetches every Help Center collection, for building your own Help Center UI.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait, not the native operation.</param>
    /// <exception cref="IntercomException">The fetch failed; the error code says why.</exception>
    Task<IReadOnlyList<HelpCenterCollection>> FetchHelpCenterCollectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Fetches the contents of one Help Center collection.
    /// </summary>
    /// <param name="collectionId">The collection ID.</param>
    /// <param name="cancellationToken">Cancels the wait, not the native operation.</param>
    /// <exception cref="IntercomException">The fetch failed; the error code says why.</exception>
    Task<HelpCenterCollectionContent> FetchHelpCenterCollectionAsync(string collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Searches the Help Center.
    /// </summary>
    /// <param name="searchTerm">The text to search for.</param>
    /// <param name="cancellationToken">Cancels the wait, not the native operation.</param>
    /// <exception cref="IntercomException">The search failed; the error code says why.</exception>
    Task<IReadOnlyList<HelpCenterArticleSearchResult>> SearchHelpCenterAsync(string searchTerm, CancellationToken cancellationToken = default);

    // ── Push notifications ──────────────────────────────────────────────────

    /// <summary>
    ///     Registers the device's push token with Intercom.
    /// </summary>
    /// <param name="token">
    ///     The FCM registration token on Android, or the APNs device token as a lowercase hex
    ///     string on iOS.
    /// </param>
    /// <param name="cancellationToken">Cancels the wait, not the native operation.</param>
    /// <exception cref="IntercomException">iOS rejected the token.</exception>
    /// <remarks>
    ///     Your app still owns push registration itself: Firebase Messaging on Android, and
    ///     <c>RegisteredForRemoteNotifications</c> on iOS. Call this with whatever token that
    ///     produces, after a user is logged in.
    ///     <para>
    ///         Asynchronous because iOS reports a bad token through a failure block. Android's
    ///         <c>sendTokenToIntercom</c> is synchronous, so there the returned task is always
    ///         already completed.
    ///     </para>
    /// </remarks>
    Task SendPushTokenToIntercomAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Whether a push payload came from Intercom.
    /// </summary>
    /// <param name="payload">The notification payload.</param>
    /// <remarks>Check this before handing a payload to <see cref="HandlePush" />.</remarks>
    bool IsIntercomPush(IReadOnlyDictionary<string, string> payload);

    /// <summary>
    ///     Hands an Intercom push payload to the SDK, which displays it.
    /// </summary>
    /// <param name="payload">The notification payload, already confirmed with <see cref="IsIntercomPush" />.</param>
    void HandlePush(IReadOnlyDictionary<string, string> payload);
}
