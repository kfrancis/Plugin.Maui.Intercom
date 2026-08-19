namespace Plugin.Maui.Intercom;

/// <summary>
///     A failure reported by the native Intercom SDK.
/// </summary>
/// <remarks>
///     <para>
///         Carries the native error code, which is the only way to tell the failure modes
///         apart — the Messenger reports nearly all of them to the user as the same generic
///         "something went wrong" screen.
///     </para>
///     <para>
///         On Android the code comes from <c>IntercomError.getErrorCode()</c>; on iOS from
///         <c>NSError.Code</c>, with <see cref="NativeDomain" /> holding <c>NSError.Domain</c>.
///     </para>
/// </remarks>
public sealed class IntercomException : Exception
{
    /// <summary>Creates an exception with no message.</summary>
    public IntercomException()
    {
    }

    /// <summary>Creates an exception with the given message.</summary>
    /// <param name="message">The error message.</param>
    public IntercomException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the given message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public IntercomException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an exception describing a native Intercom failure.</summary>
    /// <param name="message">The native error message.</param>
    /// <param name="errorCode">The native error code, when the platform reported one.</param>
    /// <param name="nativeDomain">The <c>NSError</c> domain on iOS; <see langword="null" /> on Android.</param>
    /// <param name="innerException">The underlying platform exception, when there was one.</param>
    public IntercomException(
        string message,
        int? errorCode = null,
        string? nativeDomain = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        NativeDomain = nativeDomain;
    }

    /// <summary>
    ///     The native error code, or <see langword="null" /> when the failure did not come
    ///     from the SDK (for example a marshalling error on the way in).
    /// </summary>
    public int? ErrorCode { get; }

    /// <summary>
    ///     The <c>NSError</c> domain on iOS. Always <see langword="null" /> on Android, whose
    ///     <c>IntercomError</c> has no domain concept.
    /// </summary>
    public string? NativeDomain { get; }
}
