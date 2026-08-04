namespace Plugin.Maui.Intercom;

/// <summary>
///     Overrides the light/dark appearance of the Intercom Messenger.
/// </summary>
/// <remarks>
///     <para>
///         Android only. The Intercom iOS SDK 18.7.2 has no theme override on its public
///         ObjC surface — the documentation site describes <c>setThemeOverride:</c> and
///         <c>ICMThemeOverride*</c>, but neither appears in the shipped umbrella headers,
///         so there is nothing to bind. On iOS the Messenger follows the workspace setting.
///     </para>
/// </remarks>
public enum IntercomThemeMode
{
    /// <summary>Follow the device's light/dark setting.</summary>
    System = 0,

    /// <summary>Always use the light theme.</summary>
    Light = 1,

    /// <summary>Always use the dark theme.</summary>
    Dark = 2
}
