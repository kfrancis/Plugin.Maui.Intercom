using System.Reflection;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Intercom;

namespace MauiSample;

public static class MauiProgram
{
#if DEBUG
    private const string DefaultEnvironmentName = "Development";
#else
    private const string DefaultEnvironmentName = "Production";
#endif

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Built before UseIntercom: the credentials have to exist by the time the plugin
        // reads them, and MauiAppBuilder.Configuration is still empty at this point.
        var a = Assembly.GetExecutingAssembly();
        var config = new ConfigurationBuilder()
            .AddJsonFile(new EmbeddedFileProvider(a), "appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(new EmbeddedFileProvider(a), $"appsettings.{DefaultEnvironmentName}.json", optional: true, reloadOnChange: true)
            .Build();

        builder.Configuration.AddConfiguration(config);

        builder
            .UseMauiApp<App>()
            .UseIntercom(config.GetSection("Intercom"), options =>
            {
#if DEBUG
                options.LogLevel = IntercomLogLevel.Verbose;
#endif
                // The checked-in appsettings.json ships blank, and UseIntercom refuses to
                // arm the startup hook without credentials. A real app would leave
                // AutoInitialize alone and let a missing key fail loudly.
                options.AutoInitialize = options.HasCredentials;
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddTransient<MainPage>();

        var mauiApp = builder.Build();

        Ioc.Default.ConfigureServices(mauiApp.Services);

        return mauiApp;
    }
}
