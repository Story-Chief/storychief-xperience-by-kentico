using System.Globalization;
using System.Text;
using System.Text.Json;

namespace StoryChief.Xperience;

internal static class PhpJsonSerializer
{
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static byte[] SerializeToUtf8Bytes(object value)
    {
        var element = JsonSerializer.SerializeToElement(value, serializerOptions);
        var output = new StringBuilder();
        WriteElement(output, element);
        return Encoding.UTF8.GetBytes(output.ToString());
    }

    private static void WriteElement(StringBuilder output, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(output, element);
                break;
            case JsonValueKind.Array:
                WriteArray(output, element);
                break;
            case JsonValueKind.String:
                WriteString(output, element.GetString()!);
                break;
            case JsonValueKind.Number:
                output.Append(element.GetRawText());
                break;
            case JsonValueKind.True:
                output.Append("true");
                break;
            case JsonValueKind.False:
                output.Append("false");
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                output.Append("null");
                break;
            default:
                throw new JsonException($"Unsupported JSON value kind: {element.ValueKind}.");
        }
    }

    private static void WriteObject(StringBuilder output, JsonElement element)
    {
        output.Append('{');
        bool needsComma = false;

        foreach (var property in element.EnumerateObject())
        {
            if (needsComma)
            {
                output.Append(',');
            }

            WriteString(output, property.Name);
            output.Append(':');
            WriteElement(output, property.Value);
            needsComma = true;
        }

        output.Append('}');
    }

    private static void WriteArray(StringBuilder output, JsonElement element)
    {
        output.Append('[');
        bool needsComma = false;

        foreach (var item in element.EnumerateArray())
        {
            if (needsComma)
            {
                output.Append(',');
            }

            WriteElement(output, item);
            needsComma = true;
        }

        output.Append(']');
    }

    private static void WriteString(StringBuilder output, string value)
    {
        output.Append('"');

        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    output.Append("\\\"");
                    break;
                case '\\':
                    output.Append("\\\\");
                    break;
                case '/':
                    output.Append("\\/");
                    break;
                case '\b':
                    output.Append("\\b");
                    break;
                case '\f':
                    output.Append("\\f");
                    break;
                case '\n':
                    output.Append("\\n");
                    break;
                case '\r':
                    output.Append("\\r");
                    break;
                case '\t':
                    output.Append("\\t");
                    break;
                default:
                    if (character is < (char)0x20 or > (char)0x7f)
                    {
                        output.Append("\\u");
                        output.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        output.Append(character);
                    }

                    break;
            }
        }

        output.Append('"');
    }
}
