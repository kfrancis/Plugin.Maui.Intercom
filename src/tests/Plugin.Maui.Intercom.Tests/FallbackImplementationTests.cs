using System.Reflection;

namespace Plugin.Maui.Intercom.Tests;

/// <summary>
///     Every <see cref="IIntercom" /> member must be present in the generic .NET fallback and
///     must throw rather than quietly do nothing.
/// </summary>
public sealed class FallbackImplementationTests
{
    [Test]
    public async Task EveryInterfaceMemberThrowsPlatformNotSupported()
    {
        var implementation = Intercom.Default;
        var failures = new List<string>();

        foreach (var method in typeof(IIntercom).GetMethods())
        {
            // IsSupported is the one member that must answer rather than throw — it exists so
            // callers can detect the unsupported platform without provoking an exception.
            if (method.Name == "get_IsSupported")
            {
                continue;
            }

            var arguments = method.GetParameters()
                .Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
                .ToArray();

            try
            {
                method.Invoke(implementation, arguments);
                failures.Add($"{method.Name} did not throw");
            }
            catch (TargetInvocationException e) when (e.InnerException is PlatformNotSupportedException)
            {
                // Expected.
            }
            catch (Exception e)
            {
                failures.Add($"{method.Name} threw {e.InnerException?.GetType().Name ?? e.GetType().Name}");
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task IsSupportedIsFalseOnFallback() =>
        await Assert.That(Intercom.Default.IsSupported).IsFalse();

    [Test]
    public async Task DefaultIsASingleton() =>
        await Assert.That(Intercom.Default).IsSameReferenceAs(Intercom.Default);
}
