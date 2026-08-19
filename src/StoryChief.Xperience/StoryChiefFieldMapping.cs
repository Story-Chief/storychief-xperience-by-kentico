using System.Globalization;
using System.Text.Json;

namespace StoryChief.Xperience;

/// <summary>
/// Maps a StoryChief value to an Xperience content-type field.
/// </summary>
/// <param name="XperienceFieldName">The Xperience field code name.</param>
/// <param name="ValueKind">The conversion applied before writing the field.</param>
public sealed record StoryChiefFieldMapping(
    string XperienceFieldName,
    StoryChiefFieldValueKind ValueKind = StoryChiefFieldValueKind.Auto);

/// <summary>
/// Controls how a StoryChief JSON value is converted for Xperience.
/// </summary>
public enum StoryChiefFieldValueKind
{
    /// <summary>
    /// Preserves scalar JSON types and stores objects or arrays as JSON text.
    /// </summary>
    Auto,

    /// <summary>
    /// Stores the value as text.
    /// </summary>
    String,

    /// <summary>
    /// Stores the value as a 32-bit integer.
    /// </summary>
    Integer,

    /// <summary>
    /// Stores the value as a decimal number.
    /// </summary>
    Decimal,

    /// <summary>
    /// Stores the value as a boolean.
    /// </summary>
    Boolean,

    /// <summary>
    /// Parses an ISO 8601 value and stores it as a UTC <see cref="DateTime"/>.
    /// </summary>
    DateTime,

    /// <summary>
    /// Stores the complete JSON representation as text.
    /// </summary>
    Json,
}

internal static class StoryChiefFieldMapper
{
    public static Dictionary<string, object> Map(
        JsonElement story,
        IEnumerable<KeyValuePair<string, StoryChiefFieldMapping>> mappings)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach ((string path, var mapping) in mappings)
        {
            if (!TryGetPropertyPath(story, path, out var value)
                || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            result[mapping.XperienceFieldName] = ConvertValue(value, mapping.ValueKind, path);
        }

        return result;
    }

    private static bool TryGetPropertyPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;

        foreach (string segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static object ConvertValue(
        JsonElement value,
        StoryChiefFieldValueKind valueKind,
        string path) => valueKind switch
        {
            StoryChiefFieldValueKind.Auto => ConvertAutomatically(value),
            StoryChiefFieldValueKind.String => ConvertToString(value),
            StoryChiefFieldValueKind.Integer when value.TryGetInt32(out int integer) => integer,
            StoryChiefFieldValueKind.Decimal when value.TryGetDecimal(out decimal number) => number,
            StoryChiefFieldValueKind.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
            StoryChiefFieldValueKind.DateTime => ConvertToDateTime(value, path),
            StoryChiefFieldValueKind.Json => value.GetRawText(),
            _ => throw new JsonException($"StoryChief field '{path}' cannot be converted to {valueKind}."),
        };

    private static object ConvertAutomatically(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()!;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (value.TryGetInt32(out int integer))
            {
                return integer;
            }

            if (value.TryGetInt64(out long longInteger))
            {
                return longInteger;
            }

            if (value.TryGetDecimal(out decimal number))
            {
                return number;
            }

            return value.GetDouble();
        }

        return value.GetRawText();
    }

    private static string ConvertToString(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? value.GetString()!
        : value.ToString();

    private static DateTime ConvertToDateTime(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedValue))
        {
            return parsedValue.UtcDateTime;
        }

        throw new JsonException($"StoryChief field '{path}' is not a valid ISO 8601 date and time.");
    }
}
