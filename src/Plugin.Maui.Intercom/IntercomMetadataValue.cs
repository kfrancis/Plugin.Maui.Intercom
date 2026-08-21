namespace Plugin.Maui.Intercom;

/// <summary>
///     A platform-neutral representation of a metadata value accepted by Intercom.
/// </summary>
internal readonly record struct IntercomMetadataValue(IntercomMetadataType Type, object Value);

internal enum IntercomMetadataType
{
    String,
    Boolean,
    Int32,
    Int64,
    Int16,
    Byte,
    Single,
    Double,
    Decimal,
    Timestamp
}

internal static class IntercomMetadata
{
    internal static IntercomMetadataValue Normalize(object value, string key, string paramName) => value switch
    {
        string text => new(IntercomMetadataType.String, text),
        bool flag => new(IntercomMetadataType.Boolean, flag),
        int number => new(IntercomMetadataType.Int32, number),
        long number => new(IntercomMetadataType.Int64, number),
        short number => new(IntercomMetadataType.Int16, number),
        byte number => new(IntercomMetadataType.Byte, number),
        float number => new(IntercomMetadataType.Single, number),
        double number => new(IntercomMetadataType.Double, number),
        decimal number => new(IntercomMetadataType.Decimal, number),
        DateTimeOffset timestamp => new(IntercomMetadataType.Timestamp, timestamp),
        DateTime timestamp => new(IntercomMetadataType.Timestamp, new DateTimeOffset(timestamp.ToUniversalTime())),
        _ => throw new ArgumentException(
            $"'{paramName}[\"{key}\"]' is a {value.GetType().Name}. Intercom accepts strings, numbers, booleans and dates.",
            paramName)
    };
}
