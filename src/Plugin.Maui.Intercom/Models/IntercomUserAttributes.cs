namespace Plugin.Maui.Intercom;

/// <summary>
///     The attributes of an Intercom user, used both to log a user in and to update one.
/// </summary>
/// <remarks>
///     Maps to <c>UserAttributes.Builder</c> on Android and <c>ICMUserAttributes</c> on iOS.
///     Both platforms expose the same set of default attributes.
/// </remarks>
public sealed class IntercomUserAttributes
{
    /// <summary>
    ///     Your identifier for the user. Either this or <see cref="Email" /> must be set to
    ///     log an identified user in.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    ///     The user's email address. Either this or <see cref="UserId" /> must be set to log
    ///     an identified user in.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>The user's name.</summary>
    public string? Name { get; set; }

    /// <summary>The user's phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>
    ///     A language code that overrides the Messenger's language for this user.
    /// </summary>
    /// <remarks>
    ///     Must be a language code Intercom recognises; an unrecognised code is ignored by
    ///     the server rather than reported as an error.
    /// </remarks>
    public string? LanguageOverride { get; set; }

    /// <summary>When the user signed up.</summary>
    public DateTimeOffset? SignedUpAt { get; set; }

    /// <summary>Whether the user has unsubscribed from emails.</summary>
    public bool? UnsubscribedFromEmails { get; set; }

    /// <summary>The companies this user belongs to.</summary>
    public IList<IntercomCompany> Companies { get; } = [];

    /// <summary>
    ///     Custom attributes for the user.
    /// </summary>
    /// <remarks>
    ///     Values must be <see cref="string" />, a numeric type, <see cref="bool" /> or
    ///     <see cref="DateTimeOffset" />. Anything else throws <see cref="ArgumentException" />
    ///     when the attributes are sent to Intercom.
    /// </remarks>
    public IDictionary<string, object?> CustomAttributes { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    ///     Whether these attributes carry an identifier Intercom can log a user in with.
    /// </summary>
    public bool HasIdentifier => !string.IsNullOrEmpty(UserId) || !string.IsNullOrEmpty(Email);
}
