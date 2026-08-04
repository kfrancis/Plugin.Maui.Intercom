namespace Plugin.Maui.Intercom;

/// <summary>
///     A Help Center collection, as returned by
///     <see cref="IIntercom.FetchHelpCenterCollectionsAsync" />.
/// </summary>
public sealed class HelpCenterCollection
{
    /// <summary>The collection ID.</summary>
    public required string Id { get; init; }

    /// <summary>The collection title.</summary>
    public required string Title { get; init; }

    /// <summary>The collection summary, when the workspace has one.</summary>
    public string? Summary { get; init; }

    /// <summary>How many articles the collection holds.</summary>
    public int ArticleCount { get; init; }

    /// <summary>How many sub-collections the collection holds.</summary>
    public int CollectionCount { get; init; }
}

/// <summary>
///     The contents of a Help Center collection, as returned by
///     <see cref="IIntercom.FetchHelpCenterCollectionAsync" />.
/// </summary>
public sealed class HelpCenterCollectionContent
{
    /// <summary>The collection ID.</summary>
    public required string Id { get; init; }

    /// <summary>The collection title.</summary>
    public required string Title { get; init; }

    /// <summary>The collection summary, when the workspace has one.</summary>
    public string? Summary { get; init; }

    /// <summary>Articles directly in this collection.</summary>
    public IReadOnlyList<HelpCenterArticle> Articles { get; init; } = [];

    /// <summary>
    ///     Sections within this collection.
    /// </summary>
    /// <remarks>
    ///     Android only. The iOS SDK's <c>ICMHelpCenterCollectionContent</c> has no sections
    ///     concept, so this is always empty on iOS.
    /// </remarks>
    public IReadOnlyList<HelpCenterSection> Sections { get; init; } = [];

    /// <summary>Collections nested inside this one.</summary>
    public IReadOnlyList<HelpCenterCollection> SubCollections { get; init; } = [];

    /// <summary>How many articles the collection holds in total.</summary>
    public int ArticleCount { get; init; }

    /// <summary>The authors who contributed to the collection's articles.</summary>
    public IReadOnlyList<HelpCenterArticleAuthor> Authors { get; init; } = [];
}

/// <summary>A section within a Help Center collection.</summary>
/// <remarks>Android only; see <see cref="HelpCenterCollectionContent.Sections" />.</remarks>
public sealed class HelpCenterSection
{
    /// <summary>The section title.</summary>
    public required string Title { get; init; }

    /// <summary>The articles in this section.</summary>
    public IReadOnlyList<HelpCenterArticle> Articles { get; init; } = [];
}

/// <summary>A Help Center article listed inside a collection or section.</summary>
public sealed class HelpCenterArticle
{
    /// <summary>The article ID. Pass this to <c>IntercomContent.Article</c> to present it.</summary>
    public required string ArticleId { get; init; }

    /// <summary>The article title.</summary>
    public required string Title { get; init; }
}

/// <summary>An author of Help Center articles.</summary>
public sealed class HelpCenterArticleAuthor
{
    /// <summary>The author's Intercom ID.</summary>
    public required string AuthorId { get; init; }

    /// <summary>The author's display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The author's avatar image, when they have one.</summary>
    public string? AvatarUrl { get; init; }
}

/// <summary>
///     A Help Center search hit, as returned by
///     <see cref="IIntercom.SearchHelpCenterAsync" />.
/// </summary>
public sealed class HelpCenterArticleSearchResult
{
    /// <summary>The article ID. Pass this to <c>IntercomContent.Article</c> to present it.</summary>
    public required string ArticleId { get; init; }

    /// <summary>The article title.</summary>
    public required string Title { get; init; }

    /// <summary>The article summary.</summary>
    public string? Summary { get; init; }

    /// <summary>The portion of the article that matched the search term.</summary>
    public string? MatchingSnippet { get; init; }
}
