namespace Plugin.Maui.Intercom;

/// <summary>
///     Verbosity of the native Intercom SDK's own logging.
/// </summary>
/// <remarks>
///     <para>
///         Android maps these one-for-one onto <c>Intercom.LogLevel</c>. iOS has only an
///         on/off switch (<c>enableLogging</c>), so any level other than
///         <see cref="Disabled" /> turns iOS logging on and <see cref="Disabled" /> leaves
///         it off — iOS logging cannot be turned back off once enabled.
///     </para>
/// </remarks>
public enum IntercomLogLevel
{
    /// <summary>No Intercom logging.</summary>
    Disabled = 0,

    /// <summary>Errors only.</summary>
    Error = 1,

    /// <summary>Warnings and errors.</summary>
    Warn = 2,

    /// <summary>Informational messages and above.</summary>
    Info = 3,

    /// <summary>Debug messages and above.</summary>
    Debug = 4,

    /// <summary>Everything the SDK emits.</summary>
    Verbose = 5,

    /// <summary>Assertions only.</summary>
    Assert = 6
}
