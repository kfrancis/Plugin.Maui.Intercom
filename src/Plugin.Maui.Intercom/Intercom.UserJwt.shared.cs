namespace Plugin.Maui.Intercom;

/// <summary>
///     Answers one native request for a fresh user JWT from the consumer's provider.
/// </summary>
/// <remarks>
///     Both SDKs hand the provider a completion that must be called exactly once and has no
///     error channel: "no JWT" is the only failure they understand. A provider that throws is
///     therefore answered with <see langword="null" /> rather than leaving the native request
///     pending until it times out — on Android, where there is no documented timeout, that
///     would block every later refresh.
/// </remarks>
internal static class UserJwtProviderBridge
{
    public static async Task ProvideAsync(Func<Task<string?>> provider, Action<string?> complete)
    {
        string? jwt;
        try
        {
            jwt = await provider().ConfigureAwait(false);
        }
        catch (Exception)
        {
            jwt = null;
        }

        complete(string.IsNullOrEmpty(jwt) ? null : jwt);
    }
}
