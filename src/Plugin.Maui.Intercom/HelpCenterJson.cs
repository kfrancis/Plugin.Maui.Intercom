using System.Text.Json;

namespace Plugin.Maui.Intercom;

internal static class HelpCenterJson
{
    internal static IReadOnlyList<HelpCenterCollection> ParseCollections(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(ParseCollection)];
    }

    internal static HelpCenterCollectionContent ParseCollectionContent(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new HelpCenterCollectionContent
        {
            Id = ReadString(root, "id") ?? string.Empty,
            Title = ReadString(root, "title") ?? string.Empty,
            Summary = ReadString(root, "summary"),
            ArticleCount = ReadInt(root, "articleCount"),
            Articles = [.. root.GetProperty("articles").EnumerateArray().Select(ParseArticle)],
            Sections =
            [
                .. root.GetProperty("sections").EnumerateArray().Select(section => new HelpCenterSection
                {
                    Title = ReadString(section, "title") ?? string.Empty,
                    Articles = [.. section.GetProperty("articles").EnumerateArray().Select(ParseArticle)]
                })
            ],
            SubCollections = [.. root.GetProperty("subCollections").EnumerateArray().Select(ParseCollection)],
            Authors =
            [
                .. root.GetProperty("authors").EnumerateArray().Select(author => new HelpCenterArticleAuthor
                {
                    AuthorId = ReadString(author, "authorId") ?? string.Empty,
                    DisplayName = ReadString(author, "displayName") ?? string.Empty,
                    AvatarUrl = ReadString(author, "avatarUrl")
                })
            ]
        };
    }

    internal static IReadOnlyList<HelpCenterArticleSearchResult> ParseSearchResults(string json)
    {
        using var document = JsonDocument.Parse(json);
        return
        [
            .. document.RootElement.EnumerateArray().Select(element => new HelpCenterArticleSearchResult
            {
                ArticleId = ReadString(element, "articleId") ?? string.Empty,
                Title = ReadString(element, "title") ?? string.Empty,
                Summary = ReadString(element, "summary"),
                MatchingSnippet = ReadString(element, "matchingSnippet")
            })
        ];
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;

    private static int ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static HelpCenterCollection ParseCollection(JsonElement element) => new()
    {
        Id = ReadString(element, "id") ?? string.Empty,
        Title = ReadString(element, "title") ?? string.Empty,
        Summary = ReadString(element, "summary"),
        ArticleCount = ReadInt(element, "articleCount"),
        CollectionCount = ReadInt(element, "collectionCount")
    };

    private static HelpCenterArticle ParseArticle(JsonElement element) => new()
    {
        ArticleId = ReadString(element, "articleId") ?? string.Empty,
        Title = ReadString(element, "title") ?? string.Empty
    };
}
