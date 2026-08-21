using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsCheck;
using Microsoft.Extensions.Configuration;

namespace Plugin.Maui.Intercom.Tests;

public sealed class IntercomOptionsPropertyTests
{
    private static readonly Gen<string> s_text = Gen.String[Gen.Char.AlphaNumeric, 1, 24];

    private static readonly Gen<object?> s_claimValue = Gen.OneOf<object?>(
        s_text.Select(value => (object?)value),
        Gen.Bool.Select(value => (object?)value),
        Gen.Int.Select(value => (object?)value),
        Gen.Long.Select(value => (object?)value),
        Gen.Double.Where(double.IsFinite).Select(value => (object?)value),
        Gen.DateTimeOffset.Select(value => (object?)value),
        Gen.Const<object?>(static () => null));

    [Test]
    public void JwtPreservesGeneratedAdditionalClaimsAndVerifiesSignature() =>
        s_text.Select(s_text, s_claimValue.Array[0, 8]).Sample((secret, userId, values) =>
        {
            var claims = values.Select((value, index) => new KeyValuePair<string, object?>($"claim{index}", value))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var options = new IntercomOptions { AndroidSecret = secret, PlatformOverride = IntercomPlatform.Android };

            var jwt = options.ComputeUserJwt(userId, additionalClaims: claims);
            var parts = jwt.Split('.');
            using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));

            var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            var expectedSignature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signingInput);
            var signatureMatches = Base64Url.DecodeFromChars(parts[2]).AsSpan().SequenceEqual(expectedSignature);

            return parts.Length == 3
                && payload.RootElement.GetProperty("user_id").GetString() == userId
                && claims.All(claim => ClaimMatches(payload.RootElement, claim))
                && signatureMatches;
        });

    [Test]
    public void BindMergesGeneratedNonBlankValues() =>
        s_text.Select(s_text).Sample(values =>
        {
            var (initial, configured) = values;
            var options = new IntercomOptions { AppId = initial, AndroidApiKey = initial };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppId"] = configured,
                ["AndroidApiKey"] = " ",
                ["AutoInitialize"] = "false"
            }).Build();

            options.Bind(configuration);
            return options.AppId == configured && options.AndroidApiKey == initial && !options.AutoInitialize;
        });

    private static bool ClaimMatches(JsonElement payload, KeyValuePair<string, object?> claim)
    {
        if (claim.Value is null)
        {
            return !payload.TryGetProperty(claim.Key, out _);
        }

        var value = payload.GetProperty(claim.Key);
        return claim.Value switch
        {
            string text => value.ValueKind is JsonValueKind.String && value.GetString() == text,
            bool flag => value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean() == flag,
            DateTimeOffset timestamp => value.GetInt64() == timestamp.ToUnixTimeSeconds(),
            _ => value.GetDouble() == Convert.ToDouble(claim.Value, System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}

public sealed class IntercomContentPropertyTests
{
    private static readonly Gen<string> s_id = Gen.String[Gen.Char.AlphaNumeric, 1, 24];

    [Test]
    public void ContentReferencesPreserveEveryGeneratedId() =>
        s_id.Sample(id =>
            new IntercomContent.Article(id).Id == id
            && new IntercomContent.Carousel(id).Id == id
            && new IntercomContent.Survey(id).Id == id
            && new IntercomContent.Conversation(id).Id == id
            && new IntercomContent.Ticket(id).Id == id);

    [Test]
    public void HelpCenterCollectionsSnapshotGeneratedLists() =>
        s_id.Array[1, 12].Sample(ids =>
        {
            var source = ids.ToList();
            var content = new IntercomContent.HelpCenterCollections(source);
            source.Reverse();
            source.Clear();
            return content.Ids.SequenceEqual(ids);
        });
}

public sealed class HelpCenterJsonPropertyTests
{
    private static readonly Gen<string> s_text = Gen.String[Gen.Char.AlphaNumeric, 1, 24];

    [Test]
    public void CollectionJsonRoundTripsGeneratedValues() =>
        s_text.Select(s_text, s_text, Gen.Int[0, 1_000], Gen.Int[0, 1_000]).Sample((id, title, summary, articles, collections) =>
        {
            var json = JsonSerializer.Serialize(new
            {
                id,
                title,
                summary,
                articleCount = articles,
                collectionCount = collections
            });
            var parsed = HelpCenterJson.ParseCollections($"[{json}]").Single();
            return parsed.Id == id && parsed.Title == title && parsed.Summary == summary
                && parsed.ArticleCount == articles && parsed.CollectionCount == collections;
        });

    [Test]
    public void CollectionParserRejectsGeneratedNonJson() =>
        s_text.Sample(text =>
        {
            try
            {
                HelpCenterJson.ParseCollections($"!{text}");
                return false;
            }
            catch (JsonException)
            {
                return true;
            }
        });
}

public sealed class IntercomMetadataPropertyTests
{
    private static readonly Gen<object> s_supportedValue = Gen.OneOf<object>(
        Gen.String[Gen.Char.AlphaNumeric, 1, 24].Select(value => (object)value),
        Gen.Bool.Select(value => (object)value),
        Gen.Int.Select(value => (object)value),
        Gen.Long.Select(value => (object)value),
        Gen.Short.Select(value => (object)value),
        Gen.Byte.Select(value => (object)value),
        Gen.Float.Where(float.IsFinite).Select(value => (object)value),
        Gen.Double.Where(double.IsFinite).Select(value => (object)value),
        Gen.Decimal.Select(value => (object)value),
        Gen.DateTimeOffset.Select(value => (object)value),
        Gen.DateTime.Select(value => (object)value));

    [Test]
    public void NormalizationAcceptsEveryGeneratedSupportedType() =>
        s_supportedValue.Sample(value =>
        {
            var normalized = IntercomMetadata.Normalize(value, "attribute", "metadata");
            return normalized.Type == ExpectedType(value);
        });

    [Test]
    public void NormalizationPreservesGeneratedTimestamps() =>
        Gen.DateTimeOffset.Sample(timestamp =>
        {
            var normalized = IntercomMetadata.Normalize(timestamp, "created_at", "metadata");
            return normalized.Type == IntercomMetadataType.Timestamp && Equals(normalized.Value, timestamp);
        });

    [Test]
    public void NormalizationRejectsGeneratedUnsupportedObjects() =>
        Gen.Int.Sample(value =>
        {
            try
            {
                IntercomMetadata.Normalize(new Version(value, 0), "bad", "metadata");
                return false;
            }
            catch (ArgumentException)
            {
                return true;
            }
        });

    private static IntercomMetadataType ExpectedType(object value) => value switch
    {
        string => IntercomMetadataType.String,
        bool => IntercomMetadataType.Boolean,
        int => IntercomMetadataType.Int32,
        long => IntercomMetadataType.Int64,
        short => IntercomMetadataType.Int16,
        byte => IntercomMetadataType.Byte,
        float => IntercomMetadataType.Single,
        double => IntercomMetadataType.Double,
        decimal => IntercomMetadataType.Decimal,
        DateTimeOffset or DateTime => IntercomMetadataType.Timestamp,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
