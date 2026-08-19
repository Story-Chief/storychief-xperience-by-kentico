using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StoryChief.Xperience;

/// <summary>
/// Verifies and creates signatures compatible with StoryChief's webhook protocol.
/// </summary>
public static class StoryChiefWebhookSignature
{
    /// <summary>
    /// Validates the <c>meta.mac</c> signature without reserializing the request body.
    /// </summary>
    public static bool TryValidate(
        ReadOnlySpan<byte> body,
        string signingKey,
        out JsonDocument? payload)
    {
        payload = null;

        if (body.IsEmpty || string.IsNullOrWhiteSpace(signingKey))
        {
            return false;
        }

        try
        {
            var parsedPayload = JsonDocument.Parse(body.ToArray());
            var root = parsedPayload.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("meta", out var metadata)
                || metadata.ValueKind != JsonValueKind.Object
                || !metadata.TryGetProperty("mac", out var macElement)
                || macElement.ValueKind != JsonValueKind.String
                || !TryRemoveMac(body, out byte[] unsignedBody))
            {
                parsedPayload.Dispose();
                return false;
            }

            byte[] suppliedMac;
            try
            {
                suppliedMac = Convert.FromHexString(macElement.GetString()!);
            }
            catch (FormatException)
            {
                parsedPayload.Dispose();
                return false;
            }

            byte[] expectedMac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), unsignedBody);
            if (!CryptographicOperations.FixedTimeEquals(suppliedMac, expectedMac))
            {
                parsedPayload.Dispose();
                return false;
            }

            payload = parsedPayload;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Serializes a response using PHP-compatible JSON rules and appends its top-level MAC.
    /// </summary>
    public static byte[] Sign(object payload, string signingKey)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);

        byte[] unsignedBody = PhpJsonSerializer.SerializeToUtf8Bytes(payload);
        if (unsignedBody.Length < 2 || unsignedBody[0] != (byte)'{' || unsignedBody[^1] != (byte)'}')
        {
            throw new ArgumentException("The signed StoryChief response must be a JSON object.", nameof(payload));
        }

        string mac = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), unsignedBody))
            .ToLowerInvariant();
        string suffix = unsignedBody.Length == 2
            ? $"\"mac\":\"{mac}\"}}"
            : $",\"mac\":\"{mac}\"}}";
        byte[] suffixBytes = Encoding.UTF8.GetBytes(suffix);
        byte[] signedBody = new byte[unsignedBody.Length - 1 + suffixBytes.Length];

        unsignedBody.AsSpan(0, unsignedBody.Length - 1).CopyTo(signedBody);
        suffixBytes.CopyTo(signedBody.AsSpan(unsignedBody.Length - 1));

        return signedBody;
    }

    private static bool TryRemoveMac(ReadOnlySpan<byte> body, out byte[] unsignedBody)
    {
        unsignedBody = [];
        var reader = new Utf8JsonReader(body);
        bool expectingRootMetadata = false;
        bool insideRootMetadata = false;
        int metadataDepth = -1;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (!insideRootMetadata && reader.CurrentDepth == 1 && reader.ValueTextEquals("meta"))
                {
                    expectingRootMetadata = true;
                    continue;
                }

                if (insideRootMetadata
                    && reader.CurrentDepth == metadataDepth + 1
                    && reader.ValueTextEquals("mac"))
                {
                    int propertyStart = checked((int)reader.TokenStartIndex);
                    if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                    {
                        return false;
                    }

                    int propertyEnd = checked((int)reader.BytesConsumed);
                    return RemoveJsonProperty(body, propertyStart, propertyEnd, out unsignedBody);
                }
            }
            else if (expectingRootMetadata)
            {
                expectingRootMetadata = false;
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    insideRootMetadata = true;
                    metadataDepth = reader.CurrentDepth;
                }
            }
            else if (insideRootMetadata
                && reader.TokenType == JsonTokenType.EndObject
                && reader.CurrentDepth == metadataDepth)
            {
                insideRootMetadata = false;
            }
        }

        return false;
    }

    private static bool RemoveJsonProperty(
        ReadOnlySpan<byte> body,
        int propertyStart,
        int propertyEnd,
        out byte[] unsignedBody)
    {
        int removalStart = propertyStart;
        int removalEnd = propertyEnd;
        int previous = propertyStart - 1;

        while (previous >= 0 && IsJsonWhitespace(body[previous]))
        {
            previous--;
        }

        if (previous >= 0 && body[previous] == (byte)',')
        {
            removalStart = previous;
        }
        else
        {
            int next = propertyEnd;
            while (next < body.Length && IsJsonWhitespace(body[next]))
            {
                next++;
            }

            if (next < body.Length && body[next] == (byte)',')
            {
                removalEnd = next + 1;
            }
        }

        unsignedBody = new byte[body.Length - (removalEnd - removalStart)];
        body[..removalStart].CopyTo(unsignedBody);
        body[removalEnd..].CopyTo(unsignedBody.AsSpan(removalStart));
        return true;
    }

    private static bool IsJsonWhitespace(byte value) =>
        value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
}
