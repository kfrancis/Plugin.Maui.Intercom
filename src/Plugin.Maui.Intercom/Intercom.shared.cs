namespace Plugin.Maui.Intercom;

public static class Intercom
{
    private static IIntercom? s_defaultImplementation;

    /// <summary>
    ///     Provides the default implementation for static usage of this API.
    /// </summary>
    public static IIntercom Default
    {
        get => s_defaultImplementation ??= new IntercomImplementation();
    }

    /// <summary>
    ///     Replaces the implementation behind <see cref="Default" />.
    /// </summary>
    /// <param name="implementation">
    ///     The implementation to use, or <see langword="null" /> to reset to the platform
    ///     default on the next <see cref="Default" /> access.
    /// </param>
    /// <remarks>
    ///     A test and DI seam: pass a fake so code that reaches for the <see cref="Default" />
    ///     static — rather than an injected <see cref="IIntercom" /> — can be exercised off a
    ///     device. Call it from test setup and reset it with <see langword="null" /> in
    ///     teardown; the accessor is process-wide.
    /// </remarks>
    public static void SetDefault(IIntercom? implementation)
    {
        s_defaultImplementation = implementation;
    }
}
