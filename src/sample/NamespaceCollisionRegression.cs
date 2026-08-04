using Plugin.Maui.Intercom;

namespace MauiSample.Pages;

/// <summary>
///     Compile-time regression guard, not sample code.
/// </summary>
/// <remarks>
///     Using <c>Intercom.Default</c> unqualified from a nested namespace used to fail on Android:
///     <code>
///     error CS0234: The type or namespace name 'Default' does not exist in the namespace 'Intercom'
///     </code>
///     The Android binding's generated Resource class lived in namespace
///     <c>Intercom.Android.Binding</c> (the default RootNamespace, taken from the project name),
///     which declared an <c>Intercom</c> namespace in the global namespace of every consuming app.
///     C# searches the members declared in each enclosing namespace before that level's using
///     directives, so the namespace won over the class imported by <c>using Plugin.Maui.Intercom;</c>.
///     Fixed by setting RootNamespace on the binding project. Deliberately in a nested namespace —
///     a single-segment namespace does not reproduce it as reliably.
/// </remarks>
internal sealed class NamespaceCollisionRegression
{
    public static void UsesUnqualifiedStaticAccessor() => Intercom.Default.SetLauncherVisible(false);
}
