using Plugin.Maui.Intercom;

namespace Plugin.Maui.Intercom.Tests;

/// <summary>
///     Invariants of the shared model types.
/// </summary>
/// <remarks>
///     These validate arguments before they ever reach native code, which is the only place
///     the failure would otherwise show up — as a Messenger error screen with no detail.
/// </remarks>
public sealed class IntercomContentTests
{
    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task ArticleRejectsBlankId(string id) =>
        await Assert.That(() => new IntercomContent.Article(id)).Throws<ArgumentException>();

    [Test]
    public async Task ArticleRejectsNullId() =>
        await Assert.That(() => new IntercomContent.Article(null!)).Throws<ArgumentException>();

    [Test]
    public async Task HelpCenterCollectionsRejectsAnEmptyList() =>
        await Assert.That(() => new IntercomContent.HelpCenterCollections([])).Throws<ArgumentException>();

    [Test]
    public async Task HelpCenterCollectionsRejectsABlankIdInTheList() =>
        await Assert.That(() => new IntercomContent.HelpCenterCollections(["good", ""])).Throws<ArgumentException>();

    [Test]
    public async Task HelpCenterCollectionsCopiesTheList()
    {
        var ids = new List<string> { "a", "b" };
        var content = new IntercomContent.HelpCenterCollections(ids);
        ids.Add("c");

        // Presenting content must not depend on what the caller does to their list afterwards.
        await Assert.That(content.Ids.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ContentTypesCompareByValue()
    {
        await Assert.That(new IntercomContent.Article("42")).IsEqualTo(new IntercomContent.Article("42"));
        await Assert.That((IntercomContent)new IntercomContent.Article("42"))
            .IsNotEqualTo(new IntercomContent.Survey("42"));
    }
}

public sealed class IntercomUserAttributesTests
{
    [Test]
    public async Task HasIdentifierIsFalseWhenNeitherIdentifierIsSet() =>
        await Assert.That(new IntercomUserAttributes { Name = "Bob" }.HasIdentifier).IsFalse();

    [Test]
    public async Task HasIdentifierIsTrueForAUserId() =>
        await Assert.That(new IntercomUserAttributes { UserId = "42" }.HasIdentifier).IsTrue();

    [Test]
    public async Task HasIdentifierIsTrueForAnEmail() =>
        await Assert.That(new IntercomUserAttributes { Email = "bob@example.com" }.HasIdentifier).IsTrue();

    [Test]
    public async Task HasIdentifierIgnoresEmptyStrings() =>
        await Assert.That(new IntercomUserAttributes { UserId = "", Email = "" }.HasIdentifier).IsFalse();

    [Test]
    public async Task CollectionsStartEmptyRatherThanNull()
    {
        var attributes = new IntercomUserAttributes();
        await Assert.That(attributes.Companies).IsEmpty();
        await Assert.That(attributes.CustomAttributes).IsEmpty();
    }
}

public sealed class HelpCenterModelTests
{
    [Test]
    public async Task CollectionContentListsDefaultToEmpty()
    {
        // The Android and iOS payloads populate different subsets — Sections is Android-only —
        // so every list has to be safe to enumerate on both.
        var content = new HelpCenterCollectionContent { Id = "1", Title = "Getting started" };

        await Assert.That(content.Articles).IsEmpty();
        await Assert.That(content.Sections).IsEmpty();
        await Assert.That(content.SubCollections).IsEmpty();
        await Assert.That(content.Authors).IsEmpty();
    }
}
