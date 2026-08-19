using System.Reflection;
using System.Text.Json;

namespace Plugin.Maui.Intercom.Tests;

/// <summary>
///     Checks that eng/api-coverage.json tells the truth.
/// </summary>
/// <remarks>
///     eng/api-coverage.sh proves the map covers every native symbol. It cannot prove the
///     other direction — that a "covered" entry names something that actually exists in this
///     library. Without that, the gate is satisfied by writing a plausible member name into
///     the JSON, which is exactly the failure the gate is meant to prevent.
/// </remarks>
public sealed class ApiCoverageMapTests
{
    private static readonly Lazy<JsonElement> s_map = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "api-coverage.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    });

    private static IEnumerable<(string Symbol, string Status, string? Member, string? Reason)> Entries()
    {
        foreach (var property in s_map.Value.GetProperty("symbols").EnumerateObject())
        {
            var value = property.Value;
            yield return (
                property.Name,
                value.GetProperty("status").GetString() ?? "",
                value.TryGetProperty("member", out var member) ? member.GetString() : null,
                value.TryGetProperty("reason", out var reason) ? reason.GetString() : null);
        }
    }

    [Test]
    public async Task MapIsNotEmpty()
    {
        // A parse or path mistake would otherwise make every other test in this class pass
        // by iterating nothing.
        await Assert.That(Entries().Count()).IsGreaterThan(200);
    }

    [Test]
    public async Task EveryCoveredEntryNamesSomethingThatExists()
    {
        var assembly = typeof(IIntercom).Assembly;
        var unresolved = new List<string>();

        foreach (var (symbol, status, member, _) in Entries())
        {
            if (status != "covered")
            {
                continue;
            }

            if (member is null || !Resolves(assembly, member))
            {
                unresolved.Add($"{symbol} -> {member ?? "(no member)"}");
            }
        }

        await Assert.That(unresolved).IsEmpty();
    }

    [Test]
    public async Task EverySkippedEntryGivesAReason()
    {
        var unexplained = Entries()
            .Where(entry => entry.Status == "skipped" && string.IsNullOrWhiteSpace(entry.Reason))
            .Select(entry => entry.Symbol)
            .ToList();

        await Assert.That(unexplained).IsEmpty();
    }

    [Test]
    public async Task EveryStatusIsKnown()
    {
        var bad = Entries()
            .Where(entry => entry.Status is not ("covered" or "skipped" or "todo"))
            .Select(entry => $"{entry.Symbol} -> {entry.Status}")
            .ToList();

        await Assert.That(bad).IsEmpty();
    }

    /// <summary>
    ///     Resolves a "Type.Member" reference, where the member may also be a nested type
    ///     (IntercomContent.Article) or the type itself (IntercomSpace).
    /// </summary>
    private static bool Resolves(Assembly assembly, string reference)
    {
        var separator = reference.LastIndexOf('.');
        if (separator < 0)
        {
            return FindType(assembly, reference) is not null;
        }

        var typeName = reference[..separator];
        var memberName = reference[(separator + 1)..];

        var type = FindType(assembly, typeName);
        if (type is null)
        {
            // The whole reference may itself be a type name containing a dot only because
            // it is nested, e.g. IntercomContent.Article.
            return FindType(assembly, reference) is not null;
        }

        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        return type.GetMember(memberName, Flags).Length > 0
               || type.GetNestedType(memberName, BindingFlags.Public) is not null;
    }

    private static Type? FindType(Assembly assembly, string name) =>
        assembly.GetType($"Plugin.Maui.Intercom.{name}")
        ?? assembly.GetType($"Plugin.Maui.Intercom.{name.Replace('.', '+')}")
        ?? assembly.GetTypes().FirstOrDefault(t => t.Name == name);
}
