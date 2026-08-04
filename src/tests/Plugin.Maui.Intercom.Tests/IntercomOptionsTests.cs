using Microsoft.Extensions.Configuration;

namespace Plugin.Maui.Intercom.Tests;

/// <summary>
///     Credential resolution, configuration binding and the startup initialization contract.
/// </summary>
/// <remarks>
///     These run on a plain net10.0 host, where <see cref="IntercomOptions.CurrentPlatform" />
///     is <see cref="IntercomPlatform.Unsupported" />, so the platform is set through the
///     internal <c>PlatformOverride</c> seam.
/// </remarks>
public sealed class IntercomOptionsTests
{
    private static IntercomOptions Configured(IntercomPlatform platform) => new()
    {
        PlatformOverride = platform,
        AppId = "app-id",
        AndroidApiKey = "android-key",
        IosApiKey = "ios-key",
        AndroidSecret = "droid-secret",
        IosSecret = "ios-secret"
    };

    [Test]
    [Arguments(IntercomPlatform.Android, "android-key", "droid-secret")]
    [Arguments(IntercomPlatform.IOS, "ios-key", "ios-secret")]
    public async Task ResolvesTheRunningPlatformsCredentials(IntercomPlatform platform, string apiKey, string secret)
    {
        var options = Configured(platform);

        await Assert.That(options.ApiKey).IsEqualTo(apiKey);
        await Assert.That(options.Secret).IsEqualTo(secret);
        await Assert.That(options.HasCredentials).IsTrue();
    }

    [Test]
    public async Task ResolvesNothingOffPlatform()
    {
        var options = Configured(IntercomPlatform.Unsupported);

        await Assert.That(options.ApiKey).IsNull();
        await Assert.That(options.Secret).IsNull();
        await Assert.That(options.HasCredentials).IsFalse();
    }

    [Test]
    public async Task HasCredentialsNeedsBothTheAppIdAndTheKey()
    {
        var noAppId = Configured(IntercomPlatform.Android);
        noAppId.AppId = "   ";
        var noKey = Configured(IntercomPlatform.IOS);
        noKey.IosApiKey = null;

        await Assert.That(noAppId.HasCredentials).IsFalse();
        await Assert.That(noAppId.DescribeMissingCredentials()).IsEqualTo("AppId");
        await Assert.That(noKey.HasCredentials).IsFalse();
        await Assert.That(noKey.DescribeMissingCredentials()).IsEqualTo("IosApiKey");
    }

    [Test]
    [Arguments(IntercomPlatform.Android, "10aaa850ea81f66158a0e2c7701ce4f9bd53a779773278f1dcfbc1d357b69765")]
    [Arguments(IntercomPlatform.IOS, "7915788c007d61f378b75c0f1a0da9bb7178f13514ec2a2aa0e2a5fbb666ce71")]
    public async Task ComputeUserHashUsesThePlatformSecret(IntercomPlatform platform, string expected) =>
        await Assert.That(Configured(platform).ComputeUserHash("user@example.com")).IsEqualTo(expected);

    [Test]
    public async Task ComputeUserHashWithoutASecretSaysWhichPropertyToSet()
    {
        var options = Configured(IntercomPlatform.Android);
        options.AndroidSecret = null;

        await Assert.That(() => options.ComputeUserHash("user@example.com"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task BindReadsEveryKey()
    {
        var options = new IntercomOptions { PlatformOverride = IntercomPlatform.Android }.Bind(Configuration(new()
        {
            ["AppId"] = "bound-app",
            ["AndroidApiKey"] = "bound-android",
            ["IosApiKey"] = "bound-ios",
            ["AndroidSecret"] = "bound-droid-secret",
            ["IosSecret"] = "bound-ios-secret",
            ["LogLevel"] = "verbose",
            ["AutoInitialize"] = "false"
        }));

        await Assert.That(options.AppId).IsEqualTo("bound-app");
        await Assert.That(options.ApiKey).IsEqualTo("bound-android");
        await Assert.That(options.IosApiKey).IsEqualTo("bound-ios");
        await Assert.That(options.Secret).IsEqualTo("bound-droid-secret");
        await Assert.That(options.IosSecret).IsEqualTo("bound-ios-secret");
        await Assert.That(options.LogLevel).IsEqualTo(IntercomLogLevel.Verbose);
        await Assert.That(options.AutoInitialize).IsFalse();
    }

    [Test]
    public async Task BindLeavesValuesSetInCodeAloneWhenTheKeyIsBlank()
    {
        // A checked-in appsettings.json with empty placeholders must not wipe out credentials
        // supplied by the configure delegate.
        var options = new IntercomOptions { AndroidApiKey = "from-code", AppId = "from-code" }
            .Bind(Configuration(new() { ["AndroidApiKey"] = "", ["AppId"] = "  " }));

        await Assert.That(options.AndroidApiKey).IsEqualTo("from-code");
        await Assert.That(options.AppId).IsEqualTo("from-code");
    }

    [Test]
    public async Task BindRejectsAnUnparseableValue()
    {
        await Assert.That(() => new IntercomOptions().Bind(Configuration(new() { ["LogLevel"] = "chatty" })))
            .Throws<ArgumentException>();

        await Assert.That(() => new IntercomOptions().Bind(Configuration(new() { ["AutoInitialize"] = "maybe" })))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task InitializeAppliesTheLogLevelBeforeTheApiKey()
    {
        var options = Configured(IntercomPlatform.IOS);
        options.LogLevel = IntercomLogLevel.Warn;
        var intercom = new RecordingIntercom();

        await Assert.That(intercom.Initialize(options)).IsTrue();

        await Assert.That(intercom.Calls).IsEquivalentTo(new List<string> { "EnableLogging(Warn)", "Initialize(ios-key, app-id)" });
        await Assert.That(options.IsInitialized).IsTrue();
    }

    [Test]
    public async Task InitializeSkipsLoggingWhenDisabled()
    {
        var intercom = new RecordingIntercom();

        intercom.Initialize(Configured(IntercomPlatform.Android));

        await Assert.That(intercom.Calls).IsEquivalentTo(new List<string> { "Initialize(android-key, app-id)" });
    }

    [Test]
    public async Task InitializeRunsOncePerOptionsInstance()
    {
        var options = Configured(IntercomPlatform.Android);
        var intercom = new RecordingIntercom();

        await Assert.That(intercom.Initialize(options)).IsTrue();
        await Assert.That(intercom.Initialize(options)).IsFalse();
        await Assert.That(intercom.Calls.Count).IsEqualTo(1);
    }

    [Test]
    public async Task InitializeWithoutCredentialsThrowsBeforeTouchingTheSdk()
    {
        var options = new IntercomOptions { PlatformOverride = IntercomPlatform.Android };
        var intercom = new RecordingIntercom();

        await Assert.That(() => intercom.Initialize(options)).Throws<InvalidOperationException>();
        await Assert.That(intercom.Calls).IsEmpty();
        await Assert.That(options.IsInitialized).IsFalse();
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    /// <summary>
    ///     Records the two lifecycle calls the initializer is allowed to make; everything else
    ///     throws, so a member creeping into the startup path shows up as a failure.
    /// </summary>
    private sealed class RecordingIntercom : IIntercom
    {
        public List<string> Calls { get; } = [];

        public void Initialize(string apiKey, string appId) => Calls.Add($"Initialize({apiKey}, {appId})");

        public void EnableLogging(IntercomLogLevel level = IntercomLogLevel.Verbose) => Calls.Add($"EnableLogging({level})");

        public bool IsUserLoggedIn => throw new NotSupportedException();

        public int UnreadConversationCount => throw new NotSupportedException();

        public event EventHandler<int> UnreadConversationCountChanged
        {
            add => throw new NotSupportedException();
            remove => throw new NotSupportedException();
        }

        public void ChangeWorkspace(string apiKey, string appId) => throw new NotSupportedException();

        public Task LoginUnidentifiedUserAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task LoginUserAsync(IntercomUserAttributes attributes, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task UpdateUserAsync(IntercomUserAttributes attributes, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SetAuthTokensAsync(IReadOnlyDictionary<string, string> tokens, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void SetUserHash(string userHash) => throw new NotSupportedException();

        public void SetUserJwt(string jwt) => throw new NotSupportedException();

        public void Logout() => throw new NotSupportedException();

        public IntercomUserAttributes? FetchLoggedInUserAttributes() => throw new NotSupportedException();

        public void LogEvent(string name, IReadOnlyDictionary<string, object?>? metadata = null) => throw new NotSupportedException();

        public void Present(IntercomSpace space = IntercomSpace.Home) => throw new NotSupportedException();

        public void PresentContent(IntercomContent content) => throw new NotSupportedException();

        public void PresentMessageComposer(string? initialMessage = null) => throw new NotSupportedException();

        public void HideIntercom() => throw new NotSupportedException();

        public void SetLauncherVisible(bool visible) => throw new NotSupportedException();

        public void SetInAppMessagesVisible(bool visible) => throw new NotSupportedException();

        public void SetBottomPaddingDp(double bottomPaddingDp) => throw new NotSupportedException();

        public void SetThemeMode(IntercomThemeMode mode) => throw new NotSupportedException();

        public Task<IReadOnlyList<HelpCenterCollection>> FetchHelpCenterCollectionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<HelpCenterCollectionContent> FetchHelpCenterCollectionAsync(string collectionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<HelpCenterArticleSearchResult>> SearchHelpCenterAsync(string searchTerm, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SendPushTokenToIntercomAsync(string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public bool IsIntercomPush(IReadOnlyDictionary<string, string> payload) => throw new NotSupportedException();

        public void HandlePush(IReadOnlyDictionary<string, string> payload) => throw new NotSupportedException();
    }
}
