namespace Plugin.Maui.Intercom;

/// <summary>
///     A kind of proactive content that Intercom can push at the user unprompted.
/// </summary>
/// <remarks>
///     Passed to <see cref="IIntercom.SuppressProactiveContent" />. Maps to
///     <c>Intercom.ContentType</c> on Android and <c>IntercomProactiveContentType</c> on iOS;
///     the two agree on both members and their order, so the ordinal crosses unchanged.
///     <para>
///         In-app messages are not on this list — they are controlled separately, by
///         <see cref="IIntercom.SetInAppMessagesVisible" />.
///     </para>
/// </remarks>
public enum IntercomProactiveContentType
{
    /// <summary>A mobile carousel.</summary>
    Carousel = 0,

    /// <summary>A survey.</summary>
    Survey = 1
}
