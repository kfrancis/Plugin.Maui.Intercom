namespace Plugin.Maui.Intercom;

/// <summary>
///     A piece of Intercom content that can be presented directly, without going through
///     the Messenger home screen.
/// </summary>
/// <remarks>
///     Maps to <c>IntercomContent</c> on Android and to the <c>IntercomContent</c> factory
///     methods on iOS. The hierarchy is closed: only the nested types below exist.
/// </remarks>
public abstract record IntercomContent
{
    // Private constructor: the nested records can call it, nothing outside can derive.
    private IntercomContent()
    {
    }

    private static string Require(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{paramName}' cannot be null or empty.", paramName);
        }

        return value;
    }

    /// <summary>A Help Center article.</summary>
    public sealed record Article : IntercomContent
    {
        /// <summary>Creates a reference to the article with the given ID.</summary>
        /// <param name="id">The Intercom article ID.</param>
        public Article(string id) => Id = Require(id, nameof(id));

        /// <summary>The Intercom article ID.</summary>
        public string Id { get; }
    }

    /// <summary>A mobile carousel.</summary>
    public sealed record Carousel : IntercomContent
    {
        /// <summary>Creates a reference to the carousel with the given ID.</summary>
        /// <param name="id">The Intercom carousel ID.</param>
        public Carousel(string id) => Id = Require(id, nameof(id));

        /// <summary>The Intercom carousel ID.</summary>
        public string Id { get; }
    }

    /// <summary>A survey.</summary>
    public sealed record Survey : IntercomContent
    {
        /// <summary>Creates a reference to the survey with the given ID.</summary>
        /// <param name="id">The Intercom survey ID.</param>
        public Survey(string id) => Id = Require(id, nameof(id));

        /// <summary>The Intercom survey ID.</summary>
        public string Id { get; }
    }

    /// <summary>An existing conversation.</summary>
    public sealed record Conversation : IntercomContent
    {
        /// <summary>Creates a reference to the conversation with the given ID.</summary>
        /// <param name="id">The Intercom conversation ID.</param>
        public Conversation(string id) => Id = Require(id, nameof(id));

        /// <summary>The Intercom conversation ID.</summary>
        public string Id { get; }
    }

    /// <summary>
    ///     A ticket.
    /// </summary>
    /// <remarks>
    ///     Android only — <c>IntercomContent.Ticket</c> has no counterpart on the iOS
    ///     <c>IntercomContent</c> factory. Presenting it on iOS throws
    ///     <see cref="PlatformNotSupportedException" />; use
    ///     <see cref="IntercomSpace.Tickets" /> there instead, which both platforms have.
    /// </remarks>
    public sealed record Ticket : IntercomContent
    {
        /// <summary>Creates a reference to the ticket with the given ID.</summary>
        /// <param name="id">The Intercom ticket ID.</param>
        public Ticket(string id) => Id = Require(id, nameof(id));

        /// <summary>The Intercom ticket ID.</summary>
        public string Id { get; }
    }

    /// <summary>A list of Help Center collections.</summary>
    public sealed record HelpCenterCollections : IntercomContent
    {
        /// <summary>Creates a reference to the given Help Center collections.</summary>
        /// <param name="ids">The Intercom collection IDs to display. Must contain at least one ID.</param>
        public HelpCenterCollections(IReadOnlyList<string> ids)
        {
            ArgumentNullException.ThrowIfNull(ids);
            if (ids.Count == 0)
            {
                throw new ArgumentException("At least one collection ID is required.", nameof(ids));
            }

            for (var i = 0; i < ids.Count; i++)
            {
                Require(ids[i], $"{nameof(ids)}[{i}]");
            }

            Ids = [.. ids];
        }

        /// <summary>The Intercom collection IDs to display.</summary>
        public IReadOnlyList<string> Ids { get; }
    }
}
