namespace Plugin.Maui.Intercom;

/// <summary>
///     Overrides the light/dark appearance of the Intercom Messenger.
/// </summary>
/// <remarks>
///     <para>
///         Maps to <c>ThemeMode</c> on Android and to <c>ICMThemeOverride</c> on iOS, where
///         it landed in SDK 19.x — earlier versions had no theme override on their public
///         ObjC surface despite the documentation site describing one.
///     </para>
///     <para>
///         There is deliberately no member for iOS's <c>ICMThemeOverrideNone</c>, which
///         clears the override and hands the decision back to the workspace: Android has no
///         counterpart, so it would be a one-sided member on a three-value enum.
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
