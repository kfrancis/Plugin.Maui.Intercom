namespace Plugin.Maui.Intercom;

public static class Intercom
{
    static IIntercom? defaultImplementation;

    /// <summary>
    /// Provides the default implementation for static usage of this API.
    /// </summary>
    public static IIntercom Default =>
        defaultImplementation ??= new IntercomImplementation();

    internal static void SetDefault(IIntercom? implementation) =>
        defaultImplementation = implementation;
}
