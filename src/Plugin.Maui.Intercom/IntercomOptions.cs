using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Plugin.Maui.Intercom;

/// <summary>
///     The platforms Intercom issues separate credentials for.
/// </summary>
public enum IntercomPlatform
{
    /// <summary>Neither Android nor iOS — no Intercom credentials apply.</summary>
    Unsupported = 0,

    /// <summary>Android.</summary>
    Android = 1,

    /// <summary>iOS (and Mac Catalyst, which uses the iOS credentials).</summary>
    IOS = 2
}

/// <summary>
///     The credentials and startup behaviour handed to <c>UseIntercom</c>.
/// </summary>
/// <remarks>
///     <para>
///         Intercom issues one API key per platform and one identity-verification secret per
///         platform, against a single App ID. Set all of them here and the plugin picks the
///         pair that matches the running platform, so app code never needs <c>#if ANDROID</c>.
///     </para>
///     <para>
///         Registered as a singleton by <c>UseIntercom</c>, so any type can constructor-inject
///         <see cref="IntercomOptions" /> to read <see cref="Secret" /> — needed when signing
///         identity-verification material for <see cref="IIntercom.SetUserHash" /> or
///         <see cref="IIntercom.SetUserJwt" />.
///     </para>
///     <para>
///         Anything in this object ships inside the app binary and is extractable. That is fine
///         for the API keys, which are client-side by design; it is not fine for the
///         identity-verification secret. Intercom's own guidance is to sign the hash or JWT on
///         your server and hand the app the result. <see cref="AndroidSecret" /> and
///         <see cref="IosSecret" /> exist because a hash computed on device is still better
///         than no identity verification during development — treat them as a development
///         convenience.
///     </para>
/// </remarks>
public sealed class IntercomOptions
{
    private int _initialized;

    /// <summary>
    ///     Your Intercom App ID. The same value on both platforms.
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    ///     The Intercom Android API key. Ignored on iOS.
    /// </summary>
    public string? AndroidApiKey { get; set; }

    /// <summary>
    ///     The Intercom iOS API key. Ignored on Android.
    /// </summary>
    public string? IosApiKey { get; set; }

    /// <summary>
    ///     The Android identity-verification secret. Ignored on iOS.
    /// </summary>
    /// <remarks>Only used by <see cref="ComputeUserHash" />; never sent to Intercom.</remarks>
    public string? AndroidSecret { get; set; }

    /// <summary>
    ///     The iOS identity-verification secret. Ignored on Android.
    /// </summary>
    /// <remarks>Only used by <see cref="ComputeUserHash" />; never sent to Intercom.</remarks>
    public string? IosSecret { get; set; }

    /// <summary>
    ///     How much the native SDK should log. Applied immediately before
    ///     <see cref="IIntercom.Initialize" />, which is the only point at which it takes
    ///     effect on iOS.
    /// </summary>
    /// <remarks>Defaults to <see cref="IntercomLogLevel.Disabled" />. Do not ship a release build with this raised.</remarks>
    public IntercomLogLevel LogLevel { get; set; } = IntercomLogLevel.Disabled;

    /// <summary>
    ///     Whether <c>UseIntercom</c> initializes the SDK during app startup.
    /// </summary>
    /// <remarks>
    ///     <see langword="true" /> by default, which initializes from the platform lifecycle —
    ///     <c>Application.OnCreate</c> on Android and <c>FinishedLaunching</c> on iOS — the
    ///     earliest point at which each native SDK accepts the call.
    ///     <para>
    ///         Set to <see langword="false" /> when the credentials are not known at startup
    ///         (fetched from your backend, chosen per tenant), then call
    ///         <see cref="IntercomInitializationExtensions.Initialize(IIntercom, IntercomOptions)" />
    ///         once they are.
    ///     </para>
    /// </remarks>
    public bool AutoInitialize { get; set; } = true;

    /// <summary>
    ///     The platform this process is running on.
    /// </summary>
    public static IntercomPlatform CurrentPlatform =>
        OperatingSystem.IsAndroid() ? IntercomPlatform.Android
        : OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() ? IntercomPlatform.IOS
        : IntercomPlatform.Unsupported;

    /// <summary>
    ///     The API key for the running platform, or <see langword="null" /> if it was not set.
    /// </summary>
    public string? ApiKey => ApiKeyFor(Platform);

    /// <summary>
    ///     The identity-verification secret for the running platform, or <see langword="null" />
    ///     if it was not set.
    /// </summary>
    public string? Secret => SecretFor(Platform);

    /// <summary>
    ///     Pretends the process is running on a given platform.
    /// </summary>
    /// <remarks>
    ///     A testing seam. The unit tests run on a plain net10.0 host, where
    ///     <see cref="CurrentPlatform" /> is <see cref="IntercomPlatform.Unsupported" /> and
    ///     nothing about credential resolution could otherwise be exercised.
    /// </remarks>
    internal IntercomPlatform? PlatformOverride { get; set; }

    private IntercomPlatform Platform => PlatformOverride ?? CurrentPlatform;

    /// <summary>
    ///     Whether <see cref="AppId" /> and the running platform's API key are both set.
    /// </summary>
    public bool HasCredentials => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    ///     Whether <see cref="IIntercom.Initialize" /> has already been driven from these
    ///     options, either by <c>UseIntercom</c> or by a manual call.
    /// </summary>
    public bool IsInitialized => Volatile.Read(ref _initialized) != 0;

    /// <summary>
    ///     The API key for a specific platform.
    /// </summary>
    /// <param name="platform">The platform to read.</param>
    public string? ApiKeyFor(IntercomPlatform platform) => platform switch
    {
        IntercomPlatform.Android => AndroidApiKey,
        IntercomPlatform.IOS => IosApiKey,
        _ => null
    };

    /// <summary>
    ///     The identity-verification secret for a specific platform.
    /// </summary>
    /// <param name="platform">The platform to read.</param>
    public string? SecretFor(IntercomPlatform platform) => platform switch
    {
        IntercomPlatform.Android => AndroidSecret,
        IntercomPlatform.IOS => IosSecret,
        _ => null
    };

    /// <summary>
    ///     Computes the identity-verification hash for a user identifier, using the running
    ///     platform's secret.
    /// </summary>
    /// <param name="identifier">The user ID or email the user will be logged in with — whichever your workspace verifies against.</param>
    /// <returns>The HMAC-SHA256 digest as a lowercase hex string, ready for <see cref="IIntercom.SetUserHash" />.</returns>
    /// <exception cref="InvalidOperationException">No secret is set for the running platform.</exception>
    /// <remarks>See the type-level remarks: prefer signing this on your server.</remarks>
    public string ComputeUserHash(string identifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        var secret = Secret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                $"No identity-verification secret is configured for {Platform}. Set {nameof(AndroidSecret)}/{nameof(IosSecret)} in UseIntercom.");
        }

        var encoding = new UTF8Encoding();
        return Convert.ToHexStringLower(HMACSHA256.HashData(encoding.GetBytes(secret), encoding.GetBytes(identifier)));
    }

    /// <summary>
    ///     Reads options out of configuration, leaving properties absent from it alone.
    /// </summary>
    /// <param name="configuration">
    ///     The section holding the values — keys named after the properties:
    ///     <c>AppId</c>, <c>AndroidApiKey</c>, <c>IosApiKey</c>, <c>AndroidSecret</c>,
    ///     <c>IosSecret</c>, <c>LogLevel</c>, <c>AutoInitialize</c>.
    /// </param>
    /// <exception cref="ArgumentException"><c>LogLevel</c> or <c>AutoInitialize</c> could not be parsed.</exception>
    /// <remarks>
    ///     Hand-rolled rather than <c>IConfiguration.Bind</c> so the plugin does not drag in
    ///     <c>Microsoft.Extensions.Configuration.Binder</c>, and so an unparseable value fails
    ///     loudly instead of being silently dropped. Empty strings count as absent: a checked-in
    ///     <c>appsettings.json</c> with blank placeholders should not overwrite a key set in code.
    /// </remarks>
    public IntercomOptions Bind(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        AppId = Read(configuration, nameof(AppId)) ?? AppId;
        AndroidApiKey = Read(configuration, nameof(AndroidApiKey)) ?? AndroidApiKey;
        IosApiKey = Read(configuration, nameof(IosApiKey)) ?? IosApiKey;
        AndroidSecret = Read(configuration, nameof(AndroidSecret)) ?? AndroidSecret;
        IosSecret = Read(configuration, nameof(IosSecret)) ?? IosSecret;

        if (Read(configuration, nameof(LogLevel)) is { } logLevel)
        {
            LogLevel = Enum.TryParse<IntercomLogLevel>(logLevel, ignoreCase: true, out var parsed)
                ? parsed
                : throw new ArgumentException(
                    $"'{logLevel}' is not a valid {nameof(IntercomLogLevel)}. Expected one of: {string.Join(", ", Enum.GetNames<IntercomLogLevel>())}.",
                    nameof(configuration));
        }

        if (Read(configuration, nameof(AutoInitialize)) is { } autoInitialize)
        {
            AutoInitialize = bool.TryParse(autoInitialize, out var parsed)
                ? parsed
                : throw new ArgumentException(
                    $"'{autoInitialize}' is not a valid boolean for {nameof(AutoInitialize)}.",
                    nameof(configuration));
        }

        return this;
    }

    /// <summary>
    ///     Describes what is missing, for an error message.
    /// </summary>
    internal string DescribeMissingCredentials()
    {
        var apiKeyProperty = Platform == IntercomPlatform.Android ? nameof(AndroidApiKey) : nameof(IosApiKey);
        var missing = new List<string>(2);
        if (string.IsNullOrWhiteSpace(AppId))
        {
            missing.Add(nameof(AppId));
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            missing.Add(Platform == IntercomPlatform.Unsupported ? "an API key (this platform is not Android or iOS)" : apiKeyProperty);
        }

        return string.Join(" and ", missing);
    }

    /// <summary>
    ///     Claims the right to initialize, so a manual call and the startup hook cannot both
    ///     run <see cref="IIntercom.Initialize" />.
    /// </summary>
    internal bool TryClaimInitialization() => Interlocked.Exchange(ref _initialized, 1) == 0;

    private static string? Read(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key]) ? null : configuration[key];
}

/// <summary>
///     Drives <see cref="IIntercom.Initialize" /> from an <see cref="IntercomOptions" />.
/// </summary>
public static class IntercomInitializationExtensions
{
    /// <summary>
    ///     Initializes Intercom with the running platform's credentials.
    /// </summary>
    /// <param name="intercom">The Intercom instance.</param>
    /// <param name="options">The configured credentials.</param>
    /// <returns>
    ///     <see langword="true" /> if this call initialized the SDK, <see langword="false" />
    ///     if these options had already been used to initialize it.
    /// </returns>
    /// <exception cref="InvalidOperationException">The App ID or the platform's API key is missing.</exception>
    /// <remarks>
    ///     Applies <see cref="IntercomOptions.LogLevel" /> first: iOS only honours
    ///     <c>enableLogging</c> before <c>setApiKey</c>. Idempotent per options instance, so
    ///     calling it after <c>UseIntercom</c> has already auto-initialized is harmless.
    /// </remarks>
    public static bool Initialize(this IIntercom intercom, IntercomOptions options)
    {
        ArgumentNullException.ThrowIfNull(intercom);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.HasCredentials)
        {
            throw new InvalidOperationException(
                $"Intercom is missing {options.DescribeMissingCredentials()}. Set it in UseIntercom(options => ...).");
        }

        if (!options.TryClaimInitialization())
        {
            return false;
        }

        if (options.LogLevel != IntercomLogLevel.Disabled)
        {
            intercom.EnableLogging(options.LogLevel);
        }

        intercom.Initialize(options.ApiKey!, options.AppId!);
        return true;
    }
}
