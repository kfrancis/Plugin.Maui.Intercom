using System.Reflection;
using CommunityToolkit.Maui;
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
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseIntercom()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif
        var a = Assembly.GetExecutingAssembly();

        var configBuilder = new ConfigurationBuilder()
            .AddJsonFile(new EmbeddedFileProvider(a), "appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(new EmbeddedFileProvider(a), $"appsettings.{DefaultEnvironmentName}.json", optional: true, reloadOnChange: true);

        var config = configBuilder.Build();

        builder.Configuration.AddConfiguration(config);
        builder.Services.AddTransient<MainPage>();

        var mauiApp = builder.Build();

        Ioc.Default.ConfigureServices(mauiApp.Services);

        return mauiApp;
    }
}
