namespace Plugin.Maui.Intercom;

public static class IntercomServiceExtensions
{
    public static MauiAppBuilder UseIntercom(this MauiAppBuilder builder)
    {
        // Register the IIntercom implementation with the DI container.
        // This ensures that whenever IIntercom is injected, the specific platform implementation is provided.
        builder.Services.AddSingleton(Intercom.Default);

        return builder;
    }
}