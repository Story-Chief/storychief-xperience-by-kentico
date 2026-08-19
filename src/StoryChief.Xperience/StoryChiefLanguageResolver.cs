using System.Text.Json;

namespace StoryChief.Xperience;

internal static class StoryChiefLanguageResolver
{
    public static string Resolve(JsonElement story, StoryChiefPageOptions options)
    {
        if (options.LanguageMappings.Count == 0
            || !story.TryGetProperty("language", out var language)
            || language.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return options.LanguageName;
        }

        if (language.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(language.GetString()))
        {
            throw new JsonException("The StoryChief language must be a non-empty string.");
        }

        string storyChiefLanguage = language.GetString()!;
        if (options.LanguageMappings.TryGetValue(storyChiefLanguage, out string? xperienceLanguageName)
            && !string.IsNullOrWhiteSpace(xperienceLanguageName))
        {
            return xperienceLanguageName;
        }

        throw new StoryChiefPublisherNotConfiguredException(
            $"No Xperience language mapping is configured for StoryChief language '{storyChiefLanguage}'.");
    }
}
