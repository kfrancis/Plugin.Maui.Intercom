using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsCheck;

namespace Plugin.Maui.Intercom.Tests;

/// <summary>
///     Property-based coverage of the identity-verification crypto, which the example-based
///     tests only spot-check with fixed vectors. CsCheck shrinks any counterexample to a
///     minimal failing input.
/// </summary>
public sealed class CryptoPropertyTests
{
    // Alphanumeric so identifiers and secrets are never whitespace-only (which the methods
    // reject by contract) and the round-trip is about the crypto, not input validation.
    private static readonly Gen<string> s_text = Gen.String[Gen.Char.AlphaNumeric, 1, 24];

    [Test]
    public void JwtRoundTripsAndVerifies() =>
        s_text.Select(s_text, s_text, Gen.Int[1, 86_400])
            .Sample((secret, userId, email, lifetimeSeconds) =>
            {
                var options = new IntercomOptions
                {
                    AndroidSecret = secret, PlatformOverride = IntercomPlatform.Android
                };

                var jwt = options.ComputeUserJwt(userId, email, TimeSpan.FromSeconds(lifetimeSeconds));
                var parts = jwt.Split('.');
                if (parts.Length != 3)
                {
                    return false;
                }

                // Header is the fixed HS256 header.
                using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
                if (header.RootElement.GetProperty("alg").GetString() != "HS256"
                    || header.RootElement.GetProperty("typ").GetString() != "JWT")
                {
                    return false;
                }

                // Payload carries the identifiers and an exp exactly lifetime past iat.
                using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
                var claims = payload.RootElement;
                var identifiersMatch =
                    claims.GetProperty("user_id").GetString() == userId
                    && claims.GetProperty("email").GetString() == email;
                var lifetimeMatches =
                    claims.GetProperty("exp").GetInt64() - claims.GetProperty("iat").GetInt64() == lifetimeSeconds;

                // Signature is HMAC-SHA256 over "header.payload" keyed with the secret.
                var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
                var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signingInput);
                var signatureVerifies = Base64Url.DecodeFromChars(parts[2]).AsSpan().SequenceEqual(expected);

                return identifiersMatch && lifetimeMatches && signatureVerifies;
            });

    [Test]
    public void UserHashIsDeterministicAndMatchesHmac() =>
        s_text.Select(s_text).Sample((secret, identifier) =>
        {
            var options = new IntercomOptions { AndroidSecret = secret, PlatformOverride = IntercomPlatform.Android };

            var first = options.ComputeUserHash(identifier);
            var second = options.ComputeUserHash(identifier);
            var expected = Convert.ToHexStringLower(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(identifier)));

            return first == second && first == expected;
        });
}
