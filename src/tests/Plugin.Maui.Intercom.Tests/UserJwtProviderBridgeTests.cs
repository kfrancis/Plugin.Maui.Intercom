namespace Plugin.Maui.Intercom.Tests;

/// <summary>
///     The bridge behind <see cref="IIntercom.SetUserJwtProvider" />: the native completion has
///     no error channel and must be answered exactly once, whatever the provider does.
/// </summary>
public sealed class UserJwtProviderBridgeTests
{
    [Test]
    public async Task ForwardsTheJwt()
    {
        var answers = await Answers(() => Task.FromResult<string?>("header.payload.signature"));

        await Assert.That(answers).IsEquivalentTo(new string?[] { "header.payload.signature" });
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public async Task BlankAnswersNull(string? jwt)
    {
        var answers = await Answers(() => Task.FromResult(jwt));

        await Assert.That(answers).IsEquivalentTo(new string?[] { null });
    }

    [Test]
    public async Task FaultedProviderAnswersNull()
    {
        var answers = await Answers(() => Task.FromException<string?>(new HttpRequestException("token endpoint down")));

        await Assert.That(answers).IsEquivalentTo(new string?[] { null });
    }

    [Test]
    public async Task ThrowingProviderAnswersNull()
    {
        var answers = await Answers(() => throw new InvalidOperationException("not signed in"));

        await Assert.That(answers).IsEquivalentTo(new string?[] { null });
    }

    private static async Task<List<string?>> Answers(Func<Task<string?>> provider)
    {
        var answers = new List<string?>();
        await UserJwtProviderBridge.ProvideAsync(provider, answers.Add);
        return answers;
    }
}
