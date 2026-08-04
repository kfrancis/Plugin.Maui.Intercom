using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    public async Task ComputeUserJwtSignsThePayloadWithThePlatformSecret()
    {
        var options = Configured(IntercomPlatform.IOS);

        var jwt = options.ComputeUserJwt("user-123", "user@example.com");
        var (header, payload, _) = Decode(jwt);

        await Assert.That(header.GetProperty("alg").GetString()).IsEqualTo("HS256");
        await Assert.That(header.GetProperty("typ").GetString()).IsEqualTo("JWT");
        await Assert.That(payload.GetProperty("user_id").GetString()).IsEqualTo("user-123");
        await Assert.That(payload.GetProperty("email").GetString()).IsEqualTo("user@example.com");
        await Assert.That(SignatureIsValid(jwt, "ios-secret")).IsTrue();
        await Assert.That(SignatureIsValid(jwt, "droid-secret")).IsFalse();
    }

    [Test]
    public async Task ComputeUserJwtExpiresAnHourOutByDefault()
    {
        var (_, byDefault, _) = Decode(Configured(IntercomPlatform.Android).ComputeUserJwt("user-123"));
        var (_, explicitly, _) = Decode(Configured(IntercomPlatform.Android).ComputeUserJwt("user-123", lifetime: TimeSpan.FromMinutes(5)));

        await Assert.That(byDefault.GetProperty("exp").GetInt64() - byDefault.GetProperty("iat").GetInt64()).IsEqualTo(3600);
        await Assert.That(explicitly.GetProperty("exp").GetInt64() - explicitly.GetProperty("iat").GetInt64()).IsEqualTo(300);
    }

    [Test]
    public async Task ComputeUserJwtOmitsTheIdentifierThatWasNotGiven()
    {
        var (_, payload, _) = Decode(Configured(IntercomPlatform.Android).ComputeUserJwt(email: "user@example.com"));

        await Assert.That(payload.TryGetProperty("user_id", out _)).IsFalse();
        await Assert.That(payload.GetProperty("email").GetString()).IsEqualTo("user@example.com");
    }

    [Test]
    public async Task ComputeUserJwtCarriesAdditionalClaims()
    {
        var stamp = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var (_, payload, _) = Decode(Configured(IntercomPlatform.Android).ComputeUserJwt("user-123", additionalClaims: new Dictionary<string, object?>
        {
            ["sensitive_attribute1"] = "medical-record-42",
            ["plan_tier"] = 3,
            ["is_beta_tester"] = true,
            ["signed_up_at"] = stamp,
            ["absent"] = null
        }));

        await Assert.That(payload.GetProperty("sensitive_attribute1").GetString()).IsEqualTo("medical-record-42");
        await Assert.That(payload.GetProperty("plan_tier").GetInt32()).IsEqualTo(3);
        await Assert.That(payload.GetProperty("is_beta_tester").GetBoolean()).IsTrue();
        await Assert.That(payload.GetProperty("signed_up_at").GetInt64()).IsEqualTo(1_700_000_000);
        await Assert.That(payload.TryGetProperty("absent", out _)).IsFalse();
    }

    [Test]
    public async Task ComputeUserJwtRejectsBadInput()
    {
        var options = Configured(IntercomPlatform.Android);

        await Assert.That(() => options.ComputeUserJwt()).Throws<ArgumentException>();
        await Assert.That(() => options.ComputeUserJwt("user-123", lifetime: TimeSpan.Zero)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => options.ComputeUserJwt("user-123", additionalClaims: new Dictionary<string, object?> { ["exp"] = 1 }))
            .Throws<ArgumentException>();
        await Assert.That(() => options.ComputeUserJwt("user-123", additionalClaims: new Dictionary<string, object?> { ["thing"] = new object() }))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ComputeUserJwtWithoutASecretThrows()
    {
        var options = Configured(IntercomPlatform.IOS);
        options.IosSecret = null;

        await Assert.That(() => options.ComputeUserJwt("user-123")).Throws<InvalidOperationException>();
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

    private static (JsonElement Header, JsonElement Payload, byte[] Signature) Decode(string jwt)
    {
        var segments = jwt.Split('.');
        return (
            JsonDocument.Parse(Base64Url.DecodeFromChars(segments[0])).RootElement,
            JsonDocument.Parse(Base64Url.DecodeFromChars(segments[1])).RootElement,
            Base64Url.DecodeFromChars(segments[2]));
    }

    // Verifies the token the way Intercom's backend would, rather than by re-running the
    // production code path.
    private static bool SignatureIsValid(string jwt, string secret)
    {
        var lastDot = jwt.LastIndexOf('.');
        var expected = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.ASCII.GetBytes(jwt[..lastDot]));

        return CryptographicOperations.FixedTimeEquals(expected, Base64Url.DecodeFromChars(jwt[(lastDot + 1)..]));
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
