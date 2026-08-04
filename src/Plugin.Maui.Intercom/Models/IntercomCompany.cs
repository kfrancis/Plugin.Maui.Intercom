namespace Plugin.Maui.Intercom;

/// <summary>
///     A company a user belongs to.
/// </summary>
/// <remarks>
///     The properties match <c>Company.Builder</c> on Android and <c>ICMCompany</c> on iOS
///     exactly; both platforms expose the same six attributes.
/// </remarks>
public sealed class IntercomCompany
{
    /// <summary>
    ///     Your identifier for the company. Required — Intercom rejects a company without one.
    /// </summary>
    public string? CompanyId { get; set; }

    /// <summary>The company's name.</summary>
    public string? Name { get; set; }

    /// <summary>When the company was created.</summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>The company's monthly spend.</summary>
    public int? MonthlySpend { get; set; }

    /// <summary>The company's plan.</summary>
    public string? Plan { get; set; }

    /// <summary>
    ///     Custom attributes for the company.
    /// </summary>
    /// <remarks>
    ///     Values must be <see cref="string" />, a numeric type, <see cref="bool" /> or
    ///     <see cref="DateTimeOffset" />. Anything else throws <see cref="ArgumentException" />
    ///     when the company is sent to Intercom.
    /// </remarks>
    public IDictionary<string, object?> CustomAttributes { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}
