using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;

namespace Plugin.Maui.Intercom;

/// <summary>
///     Registers Intercom with a <see cref="MauiAppBuilder" />.
/// </summary>
public static class IntercomServiceExtensions
{
    /// <summary>
    ///     Registers <see cref="IIntercom" /> without credentials. Call
    ///     <see cref="IIntercom.Initialize" /> yourself.
    /// </summary>
    /// <param name="builder">The app builder.</param>
    public static MauiAppBuilder UseIntercom(this MauiAppBuilder builder) =>
        builder.UseIntercom(options => options.AutoInitialize = false);

    /// <summary>
    ///     Registers <see cref="IIntercom" /> and, unless
    ///     <see cref="IntercomOptions.AutoInitialize" /> is turned off, initializes it during
    ///     app startup with the running platform's credentials.
    /// </summary>
    /// <param name="builder">The app builder.</param>
    /// <param name="configure">Sets the credentials and startup behaviour.</param>
    /// <exception cref="InvalidOperationException">
    ///     <see cref="IntercomOptions.AutoInitialize" /> is on and the App ID or the running
    ///     platform's API key is missing. Thrown here, at startup configuration, rather than
    ///     from the lifecycle hook — the stack trace then points at your <c>MauiProgram</c>
    ///     instead of at platform launch code.
    /// </exception>
    /// <example>
    ///     <code>
    ///     builder.UseIntercom(options =>
    ///     {
    ///         options.AppId = "abc12345";
    ///         options.AndroidApiKey = "android_sdk-...";
    ///         options.IosApiKey = "ios_sdk-...";
    ///         options.AndroidSecret = "...";   // identity verification, development only
    ///         options.IosSecret = "...";
    ///     });
    ///     </code>
    /// </example>
    public static MauiAppBuilder UseIntercom(this MauiAppBuilder builder, Action<IntercomOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new IntercomOptions();
        configure(options);

        return builder.UseIntercom(options);
    }

    /// <summary>
    ///     Registers <see cref="IIntercom" /> with credentials read from configuration.
    /// </summary>
    /// <param name="builder">The app builder.</param>
    /// <param name="configuration">
    ///     The section holding the values, keyed <c>AppId</c>, <c>AndroidApiKey</c>,
    ///     <c>IosApiKey</c>, <c>AndroidSecret</c>, <c>IosSecret</c>, <c>LogLevel</c> and
    ///     <c>AutoInitialize</c>. See <see cref="IntercomOptions.Bind" />.
    /// </param>
    /// <param name="configure">Runs after the configuration is read, to override or complete it.</param>
    /// <remarks>
    ///     Pass a configuration that is already populated. <see cref="MauiAppBuilder.Configuration" />
    ///     is only useful here if your sources were added to it before this call.
    /// </remarks>
    public static MauiAppBuilder UseIntercom(this MauiAppBuilder builder, IConfiguration configuration, Action<IntercomOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new IntercomOptions().Bind(configuration);
        configure?.Invoke(options);

        return builder.UseIntercom(options);
    }

    private static MauiAppBuilder UseIntercom(this MauiAppBuilder builder, IntercomOptions options)
    {
        // The implementation is the process-wide singleton behind Intercom.Default, so the
        // DI registration and the static accessor hand back the same object.
        builder.Services.AddSingleton(Intercom.Default);
        builder.Services.AddSingleton(options);

        if (!options.AutoInitialize)
        {
            return builder;
        }

        if (!options.HasCredentials)
        {
            throw new InvalidOperationException(
                $"UseIntercom is set to initialize at startup but Intercom is missing {options.DescribeMissingCredentials()}. " +
                $"Set it, or set {nameof(IntercomOptions.AutoInitialize)} to false and call Initialize once the credentials are known.");
        }

        // Initialize from the platform lifecycle rather than here: this runs inside
        // CreateMauiApp, before the platform has finished handing the app its own startup
        // callbacks. Android wants the Application, iOS wants didFinishLaunching.
        builder.ConfigureLifecycleEvents(events =>
        {
#if ANDROID
            events.AddAndroid(android => android.OnApplicationCreate(_ => Intercom.Default.Initialize(options)));
#elif IOS || MACCATALYST
            events.AddiOS(ios => ios.FinishedLaunching((_, _) =>
            {
                Intercom.Default.Initialize(options);
                return true;
            }));
#endif
        });

        return builder;
    }
}
