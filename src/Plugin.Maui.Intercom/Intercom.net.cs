namespace Plugin.Maui.Intercom;

/// <summary>
///     Fallback for generic .NET target frameworks.
/// </summary>
/// <remarks>
///     The shipped package only targets net10.0-android and net10.0-ios, so this type is not
///     compiled into either. It exists so the library still builds when someone multi-targets
///     a plain net10.0 TFM (unit-test hosts, design-time builds). Every member throws rather
///     than silently doing nothing — a no-op here reads as "Intercom is broken" at runtime.
/// </remarks>
partial class IntercomImplementation : IIntercom
{
    private const string Unsupported =
        "Intercom is only available on Android and iOS. Reference Plugin.Maui.Intercom from a platform head.";

    // The one member that answers instead of throwing: it exists precisely so callers can
    // learn Intercom is unavailable here without provoking an exception.
    public bool IsSupported => false;

    public bool IsUserLoggedIn => throw new PlatformNotSupportedException(Unsupported);

    public int UnreadConversationCount => throw new PlatformNotSupportedException(Unsupported);

    public event EventHandler<int> UnreadConversationCountChanged
    {
        add => throw new PlatformNotSupportedException(Unsupported);
        remove => throw new PlatformNotSupportedException(Unsupported);
    }

    public IObservable<int> UnreadConversationCounts => throw new PlatformNotSupportedException(Unsupported);

    public void Initialize(string apiKey, string appId) => throw new PlatformNotSupportedException(Unsupported);

    public void ChangeWorkspace(string apiKey, string appId) => throw new PlatformNotSupportedException(Unsupported);

    public void EnableLogging(IntercomLogLevel level = IntercomLogLevel.Verbose) => throw new PlatformNotSupportedException(Unsupported);

    public Task LoginUnidentifiedUserAsync(CancellationToken cancellationToken = default) => throw new PlatformNotSupportedException(Unsupported);

    public Task LoginUserAsync(IntercomUserAttributes attributes, CancellationToken cancellationToken = default) => throw new PlatformNotSupportedException(Unsupported);

    public Task UpdateUserAsync(IntercomUserAttributes attributes, CancellationToken cancellationToken = default) => throw new PlatformNotSupportedException(Unsupported);

    public Task SetAuthTokensAsync(IReadOnlyDictionary<string, string> tokens, CancellationToken cancellationToken = default) => throw new PlatformNotSupportedException(Unsupported);

    public void SetUserHash(string userHash) => throw new PlatformNotSupportedException(Unsupported);

    public void SetUserJwt(string jwt) => throw new PlatformNotSupportedException(Unsupported);

    public void Logout() => throw new PlatformNotSupportedException(Unsupported);

    public IntercomUserAttributes? FetchLoggedInUserAttributes() => throw new PlatformNotSupportedException(Unsupported);

    public void LogEvent(string name, IReadOnlyDictionary<string, object?>? metadata = null) => throw new PlatformNotSupportedException(Unsupported);

    public void Present(IntercomSpace space = IntercomSpace.Home) => throw new PlatformNotSupportedException(Unsupported);

    public void PresentContent(IntercomContent content) => throw new PlatformNotSupportedException(Unsupported);

    public void PresentMessageComposer(string? initialMessage = null) => throw new PlatformNotSupportedException(Unsupported);

    public void HideIntercom() => throw new PlatformNotSupportedException(Unsupported);

    public void SetLauncherVisible(bool visible) => throw new PlatformNotSupportedException(Unsupported);

    public void SetInAppMessagesVisible(bool visible) => throw new PlatformNotSupportedException(Unsupported);

    public void SuppressProactiveContent(IReadOnlyList<IntercomProactiveContentType> types) => throw new PlatformNotSupportedException(Unsupported);

    public void SetBottomPaddingDp(double bottomPaddingDp) => throw new PlatformNotSupportedException(Unsupported);

    public void SetThemeMode(IntercomThemeMode mode) => throw new PlatformNotSupportedException(Unsupported);

    public Task<IReadOnlyList<HelpCenterCollection>> FetchHelpCenterCollectionsAsync(CancellationToken cancellationToken = default) => throw new PlatformNotSupportedException(Unsupported);

    public Task<HelpCenterCollectionContent> FetchHelpCenterCollectionAsync(string collectionId, CancellationToken cancellationToken = default) => throw new PlatformNotSupportedException(Unsupported);

    public Task<IReadOnlyList<HelpCenterArticleSearchResult>> SearchHelpCenterAsync(string searchTerm, CancellationToken cancellationToken = default) => throw new PlatformNotSupportedException(Unsupported);

    public Task SendPushTokenToIntercomAsync(string token, CancellationToken cancellationToken = default) => throw new PlatformNotSupportedException(Unsupported);

    public bool IsIntercomPush(IReadOnlyDictionary<string, string> payload) => throw new PlatformNotSupportedException(Unsupported);

    public void HandlePush(IReadOnlyDictionary<string, string> payload) => throw new PlatformNotSupportedException(Unsupported);
}
