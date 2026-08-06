using CommunityToolkit.Mvvm.DependencyInjection;
using Plugin.Maui.Intercom;

namespace MauiSample;

/// <summary>
///     Manual smoke test for every member of <see cref="IIntercom" />.
/// </summary>
/// <remarks>
///     This page is the only end-to-end test the plugin has: the native SDKs cannot be
///     exercised off-device. Anything added to <see cref="IIntercom" /> should get a control
///     here, otherwise it ships unrun.
/// </remarks>
public partial class MainPage : ContentPage
{
    private static readonly IntercomSpace[] s_spaces =
        [IntercomSpace.Home, IntercomSpace.Messages, IntercomSpace.HelpCenter, IntercomSpace.Tickets];

    private static readonly string[] s_contentTypes =
        ["Article", "Carousel", "Survey", "Conversation", "Ticket", "Help Center collections"];

    private static readonly IntercomThemeMode[] s_themeModes =
        [IntercomThemeMode.System, IntercomThemeMode.Light, IntercomThemeMode.Dark];

    private readonly IntercomOptions _options;
    private bool _subscribed;

    public MainPage(IntercomOptions options)
    {
        InitializeComponent();
        _options = options;

        SpacePicker.ItemsSource = s_spaces.Select(space => space.ToString()).ToList();
        ContentTypePicker.ItemsSource = s_contentTypes.ToList();
        ThemePicker.ItemsSource = s_themeModes.Select(mode => mode.ToString()).ToList();
    }

    private static IIntercom Intercom => Ioc.Default.GetRequiredService<IIntercom>();

    private void SetStatus(string message) =>
        MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = message);

    private bool EnsureInitialized()
    {
        if (_options.IsInitialized)
        {
            Subscribe();
            return true;
        }

        SetStatus("Not initialized — tap Initialize first");
        return false;
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        Intercom.UnreadConversationCountChanged += OnUnreadCountChanged;
        _subscribed = true;
    }

    // Every handler funnels through here so a native failure shows up in the status label
    // instead of tearing the app down.
    private async Task RunAsync(string label, Func<Task> action)
    {
        if (!EnsureInitialized())
        {
            return;
        }

        try
        {
            await action();
        }
        catch (IntercomException e)
        {
            SetStatus($"{label} failed ({e.ErrorCode?.ToString() ?? "no code"}): {e.Message}");
        }
        catch (PlatformNotSupportedException e)
        {
            SetStatus($"{label} is not supported on this platform: {e.Message}");
        }
        catch (Exception e)
        {
            SetStatus($"{label} failed: {e.Message}");
        }
    }

    private void Run(string label, Action action) => _ = RunAsync(label, () =>
    {
        action();
        return Task.CompletedTask;
    });

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private void OnInitializeClicked(object sender, EventArgs e)
    {
        try
        {
            if (!_options.HasCredentials)
            {
                SetStatus("Missing credentials — set Intercom:AndroidApiKey/IosApiKey and Intercom:AppId in appsettings.Development.json");
                return;
            }

            // UseIntercom already did this at startup when the credentials were present, and
            // this call then reports false rather than initializing the SDK twice. It exists
            // so the sample still works when AutoInitialize was turned off.
            var initialized = Intercom.Initialize(_options);
            Subscribe();
            SetStatus(initialized
                ? $"Initialized (appId {_options.AppId})"
                : $"Already initialized at startup (appId {_options.AppId})");
        }
        catch (Exception ex)
        {
            SetStatus($"Initialize failed: {ex.Message}");
        }
    }

    private void OnUnreadCountChanged(object? sender, int count) =>
        MainThread.BeginInvokeOnMainThread(() => UnreadLabel.Text = $"Unread conversations: {count}");

    private void OnLogoutClicked(object sender, EventArgs e) => Run("Logout", () =>
    {
        Intercom.Logout();
        SetStatus($"Logged out (logged in: {Intercom.IsUserLoggedIn})");
    });

    // ── Identity ────────────────────────────────────────────────────────────

    private void OnLoginUnidentifiedClicked(object sender, EventArgs e) => _ = RunAsync("Login", async () =>
    {
        await Intercom.LoginUnidentifiedUserAsync();
        SetStatus($"Unidentified login OK (logged in: {Intercom.IsUserLoggedIn}, unread: {Intercom.UnreadConversationCount})");
    });

    private void OnLoginIdentifiedClicked(object sender, EventArgs e) => _ = RunAsync("Login", async () =>
    {
        const string email = "test@test.com";
        if (!string.IsNullOrEmpty(_options.Secret))
        {
            // Only needed when identity verification is enabled for the workspace, and only
            // done on device because this is a sample — issue these from your server instead.
            // A workspace with Messenger Security enforced rejects the hash and needs the JWT.
            if (JwtSwitch.IsToggled)
            {
                Intercom.SetUserJwt(_options.ComputeUserJwt(email: email));
            }
            else
            {
                Intercom.SetUserHash(_options.ComputeUserHash(email));
            }
        }

        var attributes = new IntercomUserAttributes
        {
            Email = email,
            Name = NameEntry.Text,
            SignedUpAt = DateTimeOffset.UtcNow.AddDays(-30),
            LanguageOverride = "en"
        };
        attributes.CustomAttributes["sample_run"] = DateTimeOffset.UtcNow;

        await Intercom.LoginUserAsync(attributes);
        SetStatus($"Logged in {email} (logged in: {Intercom.IsUserLoggedIn})");
    });

    private void OnUpdateUserClicked(object sender, EventArgs e) => _ = RunAsync("Update user", async () =>
    {
        var attributes = new IntercomUserAttributes
        {
            Name = NameEntry.Text,
            Phone = PhoneEntry.Text,
            UnsubscribedFromEmails = false
        };
        attributes.CustomAttributes["items_in_cart"] = 8;
        attributes.CustomAttributes["is_beta_tester"] = true;
        attributes.Companies.Add(new IntercomCompany
        {
            CompanyId = "sample-co",
            Name = "Sample Co",
            Plan = "Pro",
            MonthlySpend = 99,
            CreatedAt = DateTimeOffset.UtcNow.AddYears(-2)
        });

        await Intercom.UpdateUserAsync(attributes);
        SetStatus("User updated");
    });

    private void OnFetchLoggedInUserClicked(object sender, EventArgs e) => Run("Fetch user", () =>
    {
        var attributes = Intercom.FetchLoggedInUserAttributes();
        SetStatus(attributes is null
            ? "No user logged in"
            : $"Logged in as userId={attributes.UserId ?? "—"}, email={attributes.Email ?? "—"}");
    });

    // ── Presentation ────────────────────────────────────────────────────────

    private void OnPresentSpaceClicked(object sender, EventArgs e) => Run("Present", () =>
    {
        if (!Intercom.IsUserLoggedIn)
        {
            SetStatus("No user logged in — the Messenger will show its generic error screen. Log in first.");
            return;
        }

        var space = s_spaces[Math.Max(SpacePicker.SelectedIndex, 0)];
        Intercom.Present(space);
        SetStatus($"Presented {space}");
    });

    private void OnPresentComposerClicked(object sender, EventArgs e) => Run("Present composer", () =>
    {
        Intercom.PresentMessageComposer("Hello from the Plugin.Maui.Intercom smoke test");
        SetStatus("Message composer presented");
    });

    private void OnPresentContentClicked(object sender, EventArgs e) => Run("Present content", () =>
    {
        var id = ContentIdEntry.Text;
        if (string.IsNullOrWhiteSpace(id))
        {
            SetStatus("Enter a content ID first");
            return;
        }

        IntercomContent content = Math.Max(ContentTypePicker.SelectedIndex, 0) switch
        {
            0 => new IntercomContent.Article(id),
            1 => new IntercomContent.Carousel(id),
            2 => new IntercomContent.Survey(id),
            3 => new IntercomContent.Conversation(id),
            4 => new IntercomContent.Ticket(id),
            _ => new IntercomContent.HelpCenterCollections([.. id.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)])
        };

        Intercom.PresentContent(content);
        SetStatus($"Presented {content.GetType().Name} {id}");
    });

    private void OnHideIntercomClicked(object sender, EventArgs e) => Run("Hide", () =>
    {
        Intercom.HideIntercom();
        SetStatus("Intercom hidden");
    });

    // ── Chrome ──────────────────────────────────────────────────────────────

    private void OnLauncherToggled(object sender, ToggledEventArgs e) => Run("Set launcher visibility", () =>
    {
        Intercom.SetLauncherVisible(e.Value);
        SetStatus($"Launcher visible: {e.Value}");
    });

    private void OnInAppMessagesToggled(object sender, ToggledEventArgs e) => Run("Set in-app message visibility", () =>
    {
        Intercom.SetInAppMessagesVisible(e.Value);
        SetStatus($"In-app messages visible: {e.Value}");
    });

    // One handler for both switches: suppressProactiveContent replaces the suppressed set
    // rather than adding to it, so each toggle has to send the whole set.
    private void OnSuppressProactiveContentToggled(object sender, ToggledEventArgs e) => Run("Suppress proactive content", () =>
    {
        List<IntercomProactiveContentType> types = [];
        if (SuppressCarouselsSwitch.IsToggled)
        {
            types.Add(IntercomProactiveContentType.Carousel);
        }

        if (SuppressSurveysSwitch.IsToggled)
        {
            types.Add(IntercomProactiveContentType.Survey);
        }

        Intercom.SuppressProactiveContent(types);
        SetStatus(types.Count == 0
            ? "Proactive content: nothing suppressed"
            : $"Proactive content suppressed: {string.Join(", ", types)}");
    });

    private void OnBottomPaddingChanged(object sender, ValueChangedEventArgs e) => Run("Set bottom padding", () =>
    {
        Intercom.SetBottomPaddingDp(e.NewValue);
        SetStatus($"Bottom padding: {e.NewValue:F0} dp");
    });

    private void OnSetThemeClicked(object sender, EventArgs e) => Run("Set theme mode", () =>
    {
        var mode = s_themeModes[Math.Max(ThemePicker.SelectedIndex, 0)];
        Intercom.SetThemeMode(mode);
        SetStatus($"Theme mode: {mode}");
    });

    // ── Events and Help Center ──────────────────────────────────────────────

    private void OnLogEventClicked(object sender, EventArgs e) => Run("Log event", () =>
    {
        Intercom.LogEvent("sample_button_tapped", new Dictionary<string, object?>
        {
            ["source"] = "MainPage",
            ["tap_count"] = 1,
            ["at"] = DateTimeOffset.UtcNow
        });
        SetStatus("Event logged");
    });

    private void OnFetchCollectionsClicked(object sender, EventArgs e) => _ = RunAsync("Fetch collections", async () =>
    {
        var collections = await Intercom.FetchHelpCenterCollectionsAsync();
        SetStatus($"{collections.Count} Help Center collection(s)");
        HelpCenterResultsLabel.Text = collections.Count == 0
            ? "No collections"
            : string.Join("\n", collections.Select(c => $"{c.Title} ({c.ArticleCount} articles, {c.CollectionCount} sub-collections) — {c.Id}"));

        if (collections.Count > 0)
        {
            var content = await Intercom.FetchHelpCenterCollectionAsync(collections[0].Id);
            HelpCenterResultsLabel.Text +=
                $"\n\nFirst collection '{content.Title}': {content.Articles.Count} article(s), " +
                $"{content.Sections.Count} section(s), {content.SubCollections.Count} sub-collection(s), " +
                $"{content.Authors.Count} author(s)";
        }
    });

    private void OnSearchHelpCenterClicked(object sender, EventArgs e) => _ = RunAsync("Search", async () =>
    {
        var term = SearchEntry.Text;
        if (string.IsNullOrWhiteSpace(term))
        {
            SetStatus("Enter a search term first");
            return;
        }

        var results = await Intercom.SearchHelpCenterAsync(term);
        SetStatus($"{results.Count} search result(s)");
        HelpCenterResultsLabel.Text = results.Count == 0
            ? "No results"
            : string.Join("\n", results.Select(r => $"{r.Title} — {r.MatchingSnippet ?? r.Summary ?? r.ArticleId}"));
    });

    // ── Push ────────────────────────────────────────────────────────────────

    private void OnSendPushTokenClicked(object sender, EventArgs e) => _ = RunAsync("Send push token", async () =>
    {
        var token = PushTokenEntry.Text;
        if (string.IsNullOrWhiteSpace(token))
        {
            SetStatus("Enter a push token first");
            return;
        }

        await Intercom.SendPushTokenToIntercomAsync(token);
        SetStatus("Push token sent");
    });

    private void OnCheckPushPayloadClicked(object sender, EventArgs e) => Run("Check push payload", () =>
    {
        // Not a real Intercom payload — this exercises the detection path, which should
        // answer false and leave the payload alone.
        var payload = new Dictionary<string, string>
        {
            ["message"] = "sample",
            ["source"] = "MainPage"
        };

        if (Intercom.IsIntercomPush(payload))
        {
            Intercom.HandlePush(payload);
            SetStatus("Payload was an Intercom push and was handed to the SDK");
        }
        else
        {
            SetStatus("Payload is not an Intercom push (expected for this sample payload)");
        }
    });
}
