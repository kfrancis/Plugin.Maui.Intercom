#if ANDROID
using System.Text.Json;
using Android.Runtime;
using MauiIntercomAndroid;
using Application = Android.App.Application;
using Boolean = Java.Lang.Boolean;
using Double = Java.Lang.Double;
using Integer = Java.Lang.Integer;
using Long = Java.Lang.Long;
using Object = Java.Lang.Object;
using String = Java.Lang.String;

namespace Plugin.Maui.Intercom;

/// <summary>
///     Android implementation, layered on the <c>com.intercom.mauiintercom.IntercomSdk</c>
///     Java wrapper.
/// </summary>
/// <remarks>
///     The Java wrapper is the only Android surface the binding exposes, so every member here
///     maps to a method on it. Requests marshal as <c>java.util.Map</c> (which preserves the
///     string/number/bool distinction Intercom's custom attributes depend on); Help Center
///     responses come back as JSON.
/// </remarks>
partial class IntercomImplementation : IIntercom
{
    private readonly Lock _unreadLock = new();
    private EventHandler<int>? _unreadCountChanged;
    private Object? _unreadListenerToken;

    private static Application CurrentApplication =>
        Application.Context as Application
        ?? throw new IntercomException(
            "No Android Application context is available yet. Call Initialize after the app's Application has been created.");

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;

    // ── Capability ──────────────────────────────────────────────────────────

    public bool IsSupported => true;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    public void Initialize(string apiKey, string appId)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey);
        ArgumentException.ThrowIfNullOrEmpty(appId);

        // Intercom.initialize wants the Application, not an Activity. Routing through
        // Platform.CurrentActivity used to throw here whenever Initialize ran before the
        // first window existed.
        IntercomSdk.Initialize(CurrentApplication, apiKey, appId);
    }

    public void ChangeWorkspace(string apiKey, string appId)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey);
        ArgumentException.ThrowIfNullOrEmpty(appId);

        IntercomSdk.ChangeWorkspace(apiKey, appId);
    }

    public void EnableLogging(IntercomLogLevel level = IntercomLogLevel.Verbose) =>
        IntercomSdk.SetLogLevel(IntercomSdk.ToNativeLogLevel((int)level));

    // ── Identity ────────────────────────────────────────────────────────────

    public Task LoginUnidentifiedUserAsync(CancellationToken cancellationToken = default)
    {
        var callback = new StatusCallback(cancellationToken);
        IntercomSdk.LoginUnidentifiedUser(callback);
        return callback.Task;
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

        var callback = new StatusCallback(cancellationToken);
        IntercomSdk.LoginIdentifiedUser(ToJavaMap(attributes), callback);
        return callback.Task;
    }

    public Task UpdateUserAsync(IntercomUserAttributes attributes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var callback = new StatusCallback(cancellationToken);
        IntercomSdk.UpdateUser(ToJavaMap(attributes), callback);
        return callback.Task;
    }

    public Task SetAuthTokensAsync(IReadOnlyDictionary<string, string> tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var map = new JavaDictionary<string, string>();
        foreach (var (key, value) in tokens)
        {
            map[key] = value;
        }

        var callback = new StatusCallback(cancellationToken);
        IntercomSdk.SetAuthTokens(map, callback);
        return callback.Task;
    }

    public void SetUserHash(string userHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(userHash);
        IntercomSdk.SetUserHash(userHash);
    }

    public void SetUserJwt(string jwt)
    {
        ArgumentException.ThrowIfNullOrEmpty(jwt);
        IntercomSdk.SetUserJwt(jwt);
    }

    public void Logout() => IntercomSdk.Logout();

    public bool IsUserLoggedIn => IntercomSdk.IsUserLoggedIn;

    public IntercomUserAttributes? FetchLoggedInUserAttributes()
    {
        var json = IntercomSdk.FetchLoggedInUserAttributes();
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new IntercomUserAttributes
        {
            UserId = ReadString(root, "userId"),
            Email = ReadString(root, "email")
        };
    }

    // ── Events ──────────────────────────────────────────────────────────────

    public void LogEvent(string name, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        IntercomSdk.LogEvent(name, metadata is null ? null : ToJavaMap(metadata, nameof(metadata)));
    }

    // ── Presentation ────────────────────────────────────────────────────────

    public void Present(IntercomSpace space = IntercomSpace.Home) => IntercomSdk.Present((int)space);

    public void PresentContent(IntercomContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        switch (content)
        {
            case IntercomContent.Article article:
                IntercomSdk.PresentContent(0, article.Id, null);
                break;
            case IntercomContent.Carousel carousel:
                IntercomSdk.PresentContent(1, carousel.Id, null);
                break;
            case IntercomContent.Survey survey:
                IntercomSdk.PresentContent(2, survey.Id, null);
                break;
            case IntercomContent.Conversation conversation:
                IntercomSdk.PresentContent(3, conversation.Id, null);
                break;
            case IntercomContent.Ticket ticket:
                IntercomSdk.PresentContent(4, ticket.Id, null);
                break;
            case IntercomContent.HelpCenterCollections collections:
                IntercomSdk.PresentContent(5, null, new JavaList<string>(collections.Ids));
                break;
            default:
                throw new ArgumentException($"Unknown Intercom content type: {content.GetType().Name}", nameof(content));
        }
    }

    public void PresentMessageComposer(string? initialMessage = null) =>
        IntercomSdk.PresentMessageComposer(initialMessage);

    public void HideIntercom() => IntercomSdk.HideIntercom();

    // ── Chrome ──────────────────────────────────────────────────────────────

    public void SetLauncherVisible(bool visible) => IntercomSdk.SetLauncherVisible(visible);

    public void SetInAppMessagesVisible(bool visible) => IntercomSdk.SetInAppMessagesVisible(visible);

    public void SuppressProactiveContent(IReadOnlyList<IntercomProactiveContentType> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        IntercomSdk.SuppressProactiveContent([.. types.Select(type => (int)type)]);
    }

    public void SetBottomPaddingDp(double bottomPaddingDp)
    {
        // Android's setBottomPadding takes raw pixels while iOS's takes points. Scaling by
        // the display density here is what makes the same argument mean the same physical
        // distance on both platforms.
        var density = Application.Context.Resources?.DisplayMetrics?.Density ?? 1f;
        IntercomSdk.SetBottomPadding((int)Math.Round(bottomPaddingDp * density));
    }

    public void SetThemeMode(IntercomThemeMode mode) => IntercomSdk.SetThemeMode((int)mode);

    // ── Unread conversations ────────────────────────────────────────────────

    public int UnreadConversationCount => IntercomSdk.UnreadConversationCount;

    public event EventHandler<int> UnreadConversationCountChanged
    {
        add
        {
            lock (_unreadLock)
            {
                _unreadCountChanged += value;
                // Register with the SDK only while somebody is listening; the native
                // listener keeps a strong reference for as long as it is attached.
                _unreadListenerToken ??= IntercomSdk.AddUnreadConversationCountListener(
                    new UnreadCountListener(count => _unreadCountChanged?.Invoke(this, count)));
            }
        }
        remove
        {
            lock (_unreadLock)
            {
                _unreadCountChanged -= value;
                if (_unreadCountChanged is null && _unreadListenerToken is not null)
                {
                    IntercomSdk.RemoveUnreadConversationCountListener(_unreadListenerToken);
                    _unreadListenerToken = null;
                }
            }
        }
    }

    public IObservable<int> UnreadConversationCounts => GetUnreadConversationCounts();

    // ── Help Center data ────────────────────────────────────────────────────

    public Task<IReadOnlyList<HelpCenterCollection>> FetchHelpCenterCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var callback = new JsonCallback<IReadOnlyList<HelpCenterCollection>>(HelpCenterJson.ParseCollections, cancellationToken);
        IntercomSdk.FetchHelpCenterCollections(callback);
        return callback.Task;
    }

    public Task<HelpCenterCollectionContent> FetchHelpCenterCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(collectionId);

        var callback = new JsonCallback<HelpCenterCollectionContent>(HelpCenterJson.ParseCollectionContent, cancellationToken);
        IntercomSdk.FetchHelpCenterCollection(collectionId, callback);
        return callback.Task;
    }

    public Task<IReadOnlyList<HelpCenterArticleSearchResult>> SearchHelpCenterAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(searchTerm);

        var callback = new JsonCallback<IReadOnlyList<HelpCenterArticleSearchResult>>(HelpCenterJson.ParseSearchResults, cancellationToken);
        IntercomSdk.SearchHelpCenter(searchTerm, callback);
        return callback.Task;
    }

    // ── Push notifications ──────────────────────────────────────────────────

    public Task SendPushTokenToIntercomAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        // Synchronous on Android; the task exists so iOS can report a bad token.
        IntercomSdk.SendPushTokenToIntercom(CurrentApplication, token);
        return Task.CompletedTask;
    }

    public bool IsIntercomPush(IReadOnlyDictionary<string, string> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return IntercomSdk.IsIntercomPush(ToJavaStringMap(payload));
    }

    public void HandlePush(IReadOnlyDictionary<string, string> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        IntercomSdk.HandlePush(CurrentApplication, ToJavaStringMap(payload));
    }

    // ── Marshalling ─────────────────────────────────────────────────────────

    private static JavaDictionary<string, string> ToJavaStringMap(IReadOnlyDictionary<string, string> source)
    {
        var map = new JavaDictionary<string, string>();
        foreach (var (key, value) in source)
        {
            map[key] = value;
        }

        return map;
    }

    private static JavaDictionary<string, Object> ToJavaMap(IntercomUserAttributes attributes)
    {
        var map = new JavaDictionary<string, Object>();
        AddIfSet(map, "userId", attributes.UserId);
        AddIfSet(map, "email", attributes.Email);
        AddIfSet(map, "name", attributes.Name);
        AddIfSet(map, "phone", attributes.Phone);
        AddIfSet(map, "languageOverride", attributes.LanguageOverride);

        if (attributes.SignedUpAt is { } signedUpAt)
        {
            map["signedUpAt"] = Long.ValueOf(signedUpAt.ToUnixTimeSeconds());
        }

        if (attributes.UnsubscribedFromEmails is { } unsubscribed)
        {
            map["unsubscribedFromEmails"] = Boolean.ValueOf(unsubscribed);
        }

        if (attributes.CustomAttributes.Count > 0)
        {
            map["customAttributes"] = ToJavaMap(attributes.CustomAttributes, nameof(attributes.CustomAttributes));
        }

        if (attributes.Companies.Count > 0)
        {
            var companies = new JavaList<Object>();
            foreach (var company in attributes.Companies)
            {
                companies.Add(ToJavaMap(company));
            }

            map["companies"] = companies;
        }

        return map;
    }

    private static JavaDictionary<string, Object> ToJavaMap(IntercomCompany company)
    {
        var map = new JavaDictionary<string, Object>();
        AddIfSet(map, "companyId", company.CompanyId);
        AddIfSet(map, "name", company.Name);
        AddIfSet(map, "plan", company.Plan);

        if (company.CreatedAt is { } createdAt)
        {
            map["createdAt"] = Long.ValueOf(createdAt.ToUnixTimeSeconds());
        }

        if (company.MonthlySpend is { } monthlySpend)
        {
            map["monthlySpend"] = Integer.ValueOf(monthlySpend);
        }

        if (company.CustomAttributes.Count > 0)
        {
            map["customAttributes"] = ToJavaMap(company.CustomAttributes, nameof(company.CustomAttributes));
        }

        return map;
    }

    private static void AddIfSet(JavaDictionary<string, Object> map, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            map[key] = new String(value);
        }
    }

    // Takes the pair sequence rather than a dictionary interface: IDictionary and
    // IReadOnlyDictionary do not convert to each other, and both shapes reach this.
    private static JavaDictionary<string, Object> ToJavaMap(IEnumerable<KeyValuePair<string, object?>> source, string paramName)
    {
        var map = new JavaDictionary<string, Object>();
        foreach (var (key, value) in source)
        {
            if (value is null)
            {
                continue;
            }

            map[key] = ToJavaValue(IntercomMetadata.Normalize(value, key, paramName));
        }

        return map;
    }

    // Intercom stores custom attributes and event metadata as typed values, so the boxed
    // Java type has to match: sending "42" where a number is expected changes the attribute's
    // type on the workspace.
    private static Object ToJavaValue(IntercomMetadataValue value) => value.Type switch
    {
        IntercomMetadataType.String => new String((string)value.Value),
        IntercomMetadataType.Boolean => Boolean.ValueOf((bool)value.Value),
        IntercomMetadataType.Int32 => Integer.ValueOf((int)value.Value),
        IntercomMetadataType.Int64 => Long.ValueOf((long)value.Value),
        IntercomMetadataType.Int16 => Integer.ValueOf((short)value.Value),
        IntercomMetadataType.Byte => Integer.ValueOf((byte)value.Value),
        IntercomMetadataType.Single => Double.ValueOf((float)value.Value),
        IntercomMetadataType.Double => Double.ValueOf((double)value.Value),
        IntercomMetadataType.Decimal => Double.ValueOf((double)(decimal)value.Value),
        IntercomMetadataType.Timestamp => Long.ValueOf(((DateTimeOffset)value.Value).ToUnixTimeSeconds()),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    // ── Callback bridges ────────────────────────────────────────────────────

    private sealed class StatusCallback : Object, IIntercomCallback
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenRegistration _registration;

        public StatusCallback(CancellationToken cancellationToken) =>
            _registration = cancellationToken.Register(static state =>
                ((TaskCompletionSource)state!).TrySetCanceled(), _completion);

        public Task Task => _completion.Task;

        public void OnSuccess()
        {
            _completion.TrySetResult();
            _registration.Dispose();
        }

        public void OnFailure(int errorCode, string? message)
        {
            _completion.TrySetException(new IntercomException(
                message ?? "The Intercom operation failed.",
                errorCode < 0 ? null : errorCode));
            _registration.Dispose();
        }
    }

    private sealed class JsonCallback<T> : Object, IIntercomJsonCallback
    {
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Func<string, T> _parse;
        private readonly CancellationTokenRegistration _registration;

        public JsonCallback(Func<string, T> parse, CancellationToken cancellationToken)
        {
            _parse = parse;
            _registration = cancellationToken.Register(static state =>
                ((TaskCompletionSource<T>)state!).TrySetCanceled(), _completion);
        }

        public Task<T> Task => _completion.Task;

        public void OnComplete(string? json)
        {
            try
            {
                _completion.TrySetResult(_parse(json ?? "[]"));
            }
            catch (JsonException e)
            {
                _completion.TrySetException(new IntercomException(
                    $"Intercom returned Help Center data this version cannot read: {e.Message}", innerException: e));
            }
            finally
            {
                _registration.Dispose();
            }
        }

        public void OnFailure(int errorCode, string? message)
        {
            _completion.TrySetException(new IntercomException(
                message ?? "The Intercom operation failed.",
                errorCode < 0 ? null : errorCode));
            _registration.Dispose();
        }
    }

    private sealed class UnreadCountListener(Action<int> onCountUpdate) : Object, IIntercomUnreadCountListener
    {
        public void OnCountUpdate(int count) => onCountUpdate(count);
    }
}
#endif
