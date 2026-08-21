using Foundation;
using IntercomBinding;
using Microsoft.Maui.ApplicationModel;
using ObjCRuntime;
using NativeContent = IntercomBinding.IntercomContent;
using NativeIntercom = IntercomBinding.Intercom;
using NativeSpace = IntercomBinding.Space;

namespace Plugin.Maui.Intercom;

/// <summary>
///     iOS implementation, layered directly on the generated <c>IntercomBinding</c> types.
/// </summary>
/// <remarks>
///     Unlike Android — where <c>IntercomSdk.java</c> is the only surface the binding
///     exposes — the generated iOS binding publishes Intercom's whole ObjC API, so an iOS
///     consumer can always drop down to <c>IntercomBinding.Intercom</c> for anything this
///     interface deliberately leaves out.
///     <para>
///         Every call is marshalled to the main thread: the Intercom iOS SDK requires it and
///         says so only by crashing.
///     </para>
/// </remarks>
partial class IntercomImplementation : IIntercom
{
    private readonly Lock _unreadLock = new();
    private EventHandler<int>? _unreadCountChanged;
    private NSObject? _unreadObserver;

    // ── Capability ──────────────────────────────────────────────────────────

    public bool IsSupported => true;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    public void Initialize(string apiKey, string appId)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey);
        ArgumentException.ThrowIfNullOrEmpty(appId);

        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.SetApiKey(apiKey, appId));
    }

    public void ChangeWorkspace(string apiKey, string appId) =>
        throw new PlatformNotSupportedException(
            "Intercom for iOS has no changeWorkspace equivalent. Initialize once with the workspace you need.");

    public void EnableLogging(IntercomLogLevel level = IntercomLogLevel.Verbose)
    {
        // iOS has an on/off switch rather than levels, and no way to turn it back off.
        if (level == IntercomLogLevel.Disabled)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(NativeIntercom.EnableLogging);
    }

    // ── Identity ────────────────────────────────────────────────────────────

    public Task LoginUnidentifiedUserAsync(CancellationToken cancellationToken = default)
    {
        var completion = new Completion(cancellationToken);
        MainThread.BeginInvokeOnMainThread(() =>
            NativeIntercom.LoginUnidentifiedUserWithSuccess(completion.Succeed, completion.Fail));
        return completion.Task;
    }

    public Task LoginUserAsync(IntercomUserAttributes attributes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        if (!attributes.HasIdentifier)
        {
            throw new ArgumentException(
                $"Either {nameof(IntercomUserAttributes.UserId)} or {nameof(IntercomUserAttributes.Email)} must be set to log an identified user in.",
                nameof(attributes));
        }

        var native = ToNative(attributes);
        var completion = new Completion(cancellationToken);
        MainThread.BeginInvokeOnMainThread(() =>
            NativeIntercom.LoginUserWithUserAttributes(native, completion.Succeed, completion.Fail));
        return completion.Task;
    }

    public Task UpdateUserAsync(IntercomUserAttributes attributes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var native = ToNative(attributes);
        var completion = new Completion(cancellationToken);
        MainThread.BeginInvokeOnMainThread(() =>
            NativeIntercom.UpdateUser(native, completion.Succeed, completion.Fail));
        return completion.Task;
    }

    public Task SetAuthTokensAsync(IReadOnlyDictionary<string, string> tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var native = NSDictionary<NSString, NSString>.FromObjectsAndKeys(
            [.. tokens.Values.Select(value => new NSString(value))],
            [.. tokens.Keys.Select(key => new NSString(key))]);

        var completion = new Completion(cancellationToken);
        MainThread.BeginInvokeOnMainThread(() =>
            NativeIntercom.SetAuthTokens(native, completion.Succeed, completion.Fail));
        return completion.Task;
    }

    public void SetUserHash(string userHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(userHash);
        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.SetUserHash(userHash));
    }

    public void SetUserJwt(string jwt)
    {
        ArgumentException.ThrowIfNullOrEmpty(jwt);
        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.SetUserJwt(jwt));
    }

    public void Logout() => MainThread.BeginInvokeOnMainThread(NativeIntercom.Logout);

    public bool IsUserLoggedIn => NativeIntercom.IsUserLoggedIn();

    public IntercomUserAttributes? FetchLoggedInUserAttributes()
    {
        var native = NativeIntercom.FetchLoggedInUserAttributes();
        if (native is null)
        {
            return null;
        }

        return new IntercomUserAttributes
        {
            UserId = native.UserId,
            Email = native.Email
        };
    }

    // ── Events ──────────────────────────────────────────────────────────────

    public void LogEvent(string name, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (metadata is null || metadata.Count == 0)
        {
            MainThread.BeginInvokeOnMainThread(() => NativeIntercom.LogEventWithName(name));
            return;
        }

        var native = ToNativeDictionary(metadata, nameof(metadata));
        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.LogEventWithName(name, native));
    }

    // ── Presentation ────────────────────────────────────────────────────────

    public void Present(IntercomSpace space = IntercomSpace.Home)
    {
        var native = space switch
        {
            IntercomSpace.Home => NativeSpace.Home,
            IntercomSpace.Messages => NativeSpace.Messages,
            IntercomSpace.HelpCenter => NativeSpace.HelpCenter,
            IntercomSpace.Tickets => NativeSpace.Tickets,
            _ => throw new ArgumentOutOfRangeException(nameof(space), space, "Unknown Intercom space.")
        };

        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.PresentIntercom(native));
    }

    public void PresentContent(IntercomContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var native = content switch
        {
            IntercomContent.Article article => NativeContent.ArticleWithId(article.Id),
            IntercomContent.Carousel carousel => NativeContent.CarouselWithId(carousel.Id),
            IntercomContent.Survey survey => NativeContent.SurveyWithId(survey.Id),
            IntercomContent.Conversation conversation => NativeContent.ConversationWithId(conversation.Id),
            IntercomContent.Ticket ticket => NativeContent.TicketWithId(ticket.Id),
            IntercomContent.HelpCenterCollections collections => NativeContent.HelpCenterCollectionsWithIds([.. collections.Ids]),
            _ => throw new ArgumentException($"Unknown Intercom content type: {content.GetType().Name}", nameof(content))
        };

        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.PresentContent(native));
    }

    public void PresentMessageComposer(string? initialMessage = null) =>
        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.PresentMessageComposer(initialMessage));

    public void HideIntercom() => MainThread.BeginInvokeOnMainThread(NativeIntercom.HideIntercom);

    // ── Chrome ──────────────────────────────────────────────────────────────

    public void SetLauncherVisible(bool visible) =>
        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.SetLauncherVisible(visible));

    public void SetInAppMessagesVisible(bool visible) =>
        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.SetInAppMessagesVisible(visible));

    public void SuppressProactiveContent(IReadOnlyList<IntercomProactiveContentType> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        // suppressProactiveContent: takes NSArray<NSNumber *> of IntercomProactiveContentType
        // raw values rather than a typed enum array, so the ordinal is the wire format on this
        // side too — see IntercomProactiveContentType for why the two enums line up.
        NSNumber[] native = [.. types.Select(type => NSNumber.FromInt64((long)type))];
        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.SuppressProactiveContent(native));
    }

    // iOS points and dp are the same unit, so this passes straight through; Android is the
    // side that has to scale.
    public void SetBottomPaddingDp(double bottomPaddingDp) =>
        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.SetBottomPadding((nfloat)bottomPaddingDp));

    public void SetThemeMode(IntercomThemeMode mode)
    {
        // ICMThemeOverride is not IntercomThemeMode's shape: it leads with a `None` member
        // that clears the override and restores the workspace setting, which this API has no
        // way to express, so the ordinals cannot cross unmapped.
        var native = mode switch
        {
            IntercomThemeMode.System => ICMThemeOverride.System,
            IntercomThemeMode.Light => ICMThemeOverride.Light,
            IntercomThemeMode.Dark => ICMThemeOverride.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown Intercom theme mode.")
        };

        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.SetThemeOverride(native));
    }

    // ── Unread conversations ────────────────────────────────────────────────

    public int UnreadConversationCount => (int)NativeIntercom.UnreadConversationCount();

    public event EventHandler<int> UnreadConversationCountChanged
    {
        add
        {
            lock (_unreadLock)
            {
                _unreadCountChanged += value;
                _unreadObserver ??= NSNotificationCenter.DefaultCenter.AddObserver(
                    UnreadCountDidChangeNotification,
                    _ => _unreadCountChanged?.Invoke(this, UnreadConversationCount));
            }
        }
        remove
        {
            lock (_unreadLock)
            {
                _unreadCountChanged -= value;
                if (_unreadCountChanged is null && _unreadObserver is not null)
                {
                    NSNotificationCenter.DefaultCenter.RemoveObserver(_unreadObserver);
                    _unreadObserver.Dispose();
                    _unreadObserver = null;
                }
            }
        }
    }

    public IObservable<int> UnreadConversationCounts => GetUnreadConversationCounts();

    // Intercom declares its notification names as UIKIT_EXTERN NSString constants. Reading
    // the symbol out of the loaded image is independent of whether the binding generator
    // chose to surface them, and it uses the constant's real value rather than assuming the
    // value equals the symbol name.
    private static readonly Lazy<NSString> s_unreadCountDidChangeNotification = new(() =>
        Dlfcn.GetStringConstant(Dlfcn.dlopen(null, 0), "IntercomUnreadConversationCountDidChangeNotification")
        ?? throw new IntercomException(
            "IntercomUnreadConversationCountDidChangeNotification is not present in the loaded Intercom framework."));

    private static NSString UnreadCountDidChangeNotification => s_unreadCountDidChangeNotification.Value;

    // ── Help Center data ────────────────────────────────────────────────────

    public Task<IReadOnlyList<HelpCenterCollection>> FetchHelpCenterCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var completion = new Completion<IReadOnlyList<HelpCenterCollection>>(cancellationToken);
        MainThread.BeginInvokeOnMainThread(() =>
            NativeIntercom.FetchHelpCenterCollectionsWithCompletion((collections, error) =>
            {
                if (error is not null)
                {
                    completion.Fail(error);
                    return;
                }

                completion.Succeed([.. (collections ?? []).Select(ToCollection)]);
            }));
        return completion.Task;
    }

    public Task<HelpCenterCollectionContent> FetchHelpCenterCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(collectionId);

        var completion = new Completion<HelpCenterCollectionContent>(cancellationToken);
        MainThread.BeginInvokeOnMainThread(() =>
            NativeIntercom.FetchHelpCenterCollection(collectionId, (content, error) =>
            {
                if (error is not null || content is null)
                {
                    completion.Fail(error);
                    return;
                }

                completion.Succeed(new HelpCenterCollectionContent
                {
                    Id = content.CollectionId,
                    Title = content.Title,
                    Summary = content.Summary,
                    ArticleCount = (int)content.ArticleCount,
                    Articles = [.. (content.Articles ?? []).Select(a => new HelpCenterArticle
                    {
                        ArticleId = a.ArticleId,
                        Title = a.Title
                    })],
                    // ICMHelpCenterCollectionContent has no sections; see HelpCenterCollectionContent.Sections.
                    Sections = [],
                    SubCollections = [.. (content.Collections ?? []).Select(ToCollection)],
                    Authors = [.. (content.Authors ?? []).Select(author => new HelpCenterArticleAuthor
                    {
                        AuthorId = author.AuthorId,
                        DisplayName = author.DisplayName,
                        AvatarUrl = author.AvatarURL?.AbsoluteString
                    })]
                });
            }));
        return completion.Task;
    }

    public Task<IReadOnlyList<HelpCenterArticleSearchResult>> SearchHelpCenterAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(searchTerm);

        var completion = new Completion<IReadOnlyList<HelpCenterArticleSearchResult>>(cancellationToken);
        MainThread.BeginInvokeOnMainThread(() =>
            NativeIntercom.SearchHelpCenter(searchTerm, (results, error) =>
            {
                if (error is not null)
                {
                    completion.Fail(error);
                    return;
                }

                completion.Succeed([.. (results ?? []).Select(result => new HelpCenterArticleSearchResult
                {
                    ArticleId = result.ArticleId,
                    Title = result.Title,
                    Summary = result.Summary,
                    MatchingSnippet = result.MatchingSnippet
                })]);
            }));
        return completion.Task;
    }

    // ── Push notifications ──────────────────────────────────────────────────

    public Task SendPushTokenToIntercomAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var deviceToken = ParseHexToken(token);
        var completion = new Completion(cancellationToken);
        // setDeviceToken:success:failure:, not the two-argument setDeviceToken:failure: the
        // SDK deprecated in 19.2.0. The old form only called back on failure, so success was
        // inferred from a null error — which never arrived at all when registration silently
        // did nothing, leaving the task pending forever.
        MainThread.BeginInvokeOnMainThread(() =>
            NativeIntercom.SetDeviceToken(deviceToken, completion.Succeed, completion.Fail));
        return completion.Task;
    }

    public bool IsIntercomPush(IReadOnlyDictionary<string, string> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return NativeIntercom.IsIntercomPushNotification(ToNativeDictionary(payload));
    }

    public void HandlePush(IReadOnlyDictionary<string, string> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var native = ToNativeDictionary(payload);
        MainThread.BeginInvokeOnMainThread(() => NativeIntercom.HandleIntercomPushNotification(native));
    }

    // ── Marshalling ─────────────────────────────────────────────────────────

    private static HelpCenterCollection ToCollection(ICMHelpCenterCollection collection) => new()
    {
        Id = collection.CollectionId,
        Title = collection.Title,
        Summary = collection.Summary,
        ArticleCount = (int)collection.ArticleCount,
        CollectionCount = (int)collection.CollectionCount
    };

    private static ICMUserAttributes ToNative(IntercomUserAttributes attributes)
    {
        var native = new ICMUserAttributes
        {
            UserId = attributes.UserId,
            Email = attributes.Email,
            Name = attributes.Name,
            Phone = attributes.Phone,
            LanguageOverride = attributes.LanguageOverride
        };

        if (attributes.SignedUpAt is { } signedUpAt)
        {
            native.SignedUpAt = (NSDate)signedUpAt.UtcDateTime;
        }

        if (attributes.UnsubscribedFromEmails is { } unsubscribed)
        {
            native.UnsubscribedFromEmails = unsubscribed;
        }

        if (attributes.CustomAttributes.Count > 0)
        {
            native.CustomAttributes = ToNativeAttributes(attributes.CustomAttributes, nameof(attributes.CustomAttributes));
        }

        if (attributes.Companies.Count > 0)
        {
            native.Companies = [.. attributes.Companies.Select(ToNative)];
        }

        return native;
    }

    private static ICMCompany ToNative(IntercomCompany company)
    {
        var native = new ICMCompany
        {
            CompanyId = company.CompanyId,
            Name = company.Name,
            Plan = company.Plan
        };

        if (company.CreatedAt is { } createdAt)
        {
            native.CreatedAt = (NSDate)createdAt.UtcDateTime;
        }

        if (company.MonthlySpend is { } monthlySpend)
        {
            native.MonthlySpend = NSNumber.FromInt32(monthlySpend);
        }

        if (company.CustomAttributes.Count > 0)
        {
            native.CustomAttributes = ToNativeAttributes(company.CustomAttributes, nameof(company.CustomAttributes));
        }

        return native;
    }

    private static NSDictionary ToNativeDictionary(IReadOnlyDictionary<string, string> source)
    {
        NSObject[] keys = [.. source.Keys.Select(key => new NSString(key))];
        NSObject[] values = [.. source.Values.Select(value => new NSString(value))];
        return NSDictionary.FromObjectsAndKeys(values, keys);
    }

    // The generated binding mirrors the headers' nullability and genericity, so the shape
    // needed differs by call site: ICMUserAttributes.customAttributes and
    // ICMCompany.customAttributes are declared NSDictionary<NSString *, id> and bind as
    // NSDictionary<NSString, NSObject>, while logEventWithName:metaData: takes a bare
    // NSDictionary. Same content, two static types — hence the split below.
    //
    // Both take the pair sequence rather than a dictionary interface: IDictionary and
    // IReadOnlyDictionary do not convert to each other, and both shapes reach here.
    private static NSDictionary ToNativeDictionary(IEnumerable<KeyValuePair<string, object?>> source, string paramName)
    {
        var (keys, values) = ToNativeEntries(source, paramName);
        // Widened to NSObject[] so overload resolution lands on
        // FromObjectsAndKeys(NSObject[], NSObject[]) rather than the object[] overload,
        // which NSString[] also converts to.
        NSObject[] objectKeys = keys;
        return NSDictionary.FromObjectsAndKeys(values, objectKeys);
    }

    private static NSDictionary<NSString, NSObject> ToNativeAttributes(
        IEnumerable<KeyValuePair<string, object?>> source,
        string paramName)
    {
        var (keys, values) = ToNativeEntries(source, paramName);
        return NSDictionary<NSString, NSObject>.FromObjectsAndKeys(values, keys);
    }

    private static (NSString[] Keys, NSObject[] Values) ToNativeEntries(
        IEnumerable<KeyValuePair<string, object?>> source,
        string paramName)
    {
        var keys = new List<NSString>();
        var values = new List<NSObject>();
        foreach (var (key, value) in source)
        {
            if (value is null)
            {
                continue;
            }

            keys.Add(new NSString(key));
            values.Add(ToNativeValue(IntercomMetadata.Normalize(value, key, paramName)));
        }

        return ([.. keys], [.. values]);
    }

    // Intercom stores custom attributes and event metadata as typed values, so the NSObject
    // type has to match: sending "42" where a number is expected changes the attribute's
    // type on the workspace.
    private static NSObject ToNativeValue(IntercomMetadataValue value) => value.Type switch
    {
        IntercomMetadataType.String => new NSString((string)value.Value),
        IntercomMetadataType.Boolean => NSNumber.FromBoolean((bool)value.Value),
        IntercomMetadataType.Int32 => NSNumber.FromInt32((int)value.Value),
        IntercomMetadataType.Int64 => NSNumber.FromInt64((long)value.Value),
        IntercomMetadataType.Int16 => NSNumber.FromInt16((short)value.Value),
        IntercomMetadataType.Byte => NSNumber.FromByte((byte)value.Value),
        IntercomMetadataType.Single => NSNumber.FromFloat((float)value.Value),
        IntercomMetadataType.Double => NSNumber.FromDouble((double)value.Value),
        IntercomMetadataType.Decimal => NSNumber.FromDouble((double)(decimal)value.Value),
        IntercomMetadataType.Timestamp => (NSDate)((DateTimeOffset)value.Value).UtcDateTime,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static NSData ParseHexToken(string token)
    {
        if (token.Length % 2 != 0)
        {
            throw new ArgumentException(
                "An APNs device token is an even-length hex string; convert NSData with ToArray() and hex-encode it.",
                nameof(token));
        }

        return NSData.FromArray(Convert.FromHexString(token));
    }

    // ── Completion bridges ──────────────────────────────────────────────────

    private sealed class Completion
    {
        private readonly TaskCompletionSource _source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenRegistration _registration;

        public Completion(CancellationToken cancellationToken) =>
            _registration = cancellationToken.Register(static state =>
                ((TaskCompletionSource)state!).TrySetCanceled(), _source);

        public Task Task => _source.Task;

        public void Succeed()
        {
            _source.TrySetResult();
            _registration.Dispose();
        }

        public void Fail(NSError? error)
        {
            _source.TrySetException(ToException(error));
            _registration.Dispose();
        }
    }

    private sealed class Completion<T>
    {
        private readonly TaskCompletionSource<T> _source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenRegistration _registration;

        public Completion(CancellationToken cancellationToken) =>
            _registration = cancellationToken.Register(static state =>
                ((TaskCompletionSource<T>)state!).TrySetCanceled(), _source);

        public Task<T> Task => _source.Task;

        public void Succeed(T result)
        {
            _source.TrySetResult(result);
            _registration.Dispose();
        }

        public void Fail(NSError? error)
        {
            _source.TrySetException(ToException(error));
            _registration.Dispose();
        }
    }

    private static IntercomException ToException(NSError? error) => error is null
        ? new IntercomException("The Intercom operation failed and reported no error.")
        : new IntercomException(error.LocalizedDescription, (int)error.Code, error.Domain);
}
