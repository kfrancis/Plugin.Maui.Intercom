namespace Plugin.Maui.Intercom;

/// <summary>
///     A top-level area of the Intercom Messenger.
/// </summary>
/// <remarks>
///     Maps to <c>IntercomSpace</c> on Android and the <c>Space</c> enum on iOS. Both
///     platforms expose the same four spaces.
/// </remarks>
public enum IntercomSpace
{
    /// <summary>The Messenger home screen. This is what <c>Present()</c> opens by default.</summary>
    Home = 0,

    /// <summary>The list of the user's conversations.</summary>
    Messages = 1,

    /// <summary>The Help Center.</summary>
    HelpCenter = 2,

    /// <summary>The user's tickets.</summary>
    Tickets = 3
}
