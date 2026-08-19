using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using CMS.ContentEngine;
using CMS.DataEngine;

namespace StoryChief.Xperience;

internal sealed record StoryChiefTaxonomyTerm(
    string Identifier,
    string Name,
    string? Slug);

internal static class StoryChiefTaxonomyParser
{
    public static IReadOnlyList<StoryChiefTaxonomyTerm>? Parse(JsonElement story, string path)
    {
        if (!TryGetPropertyPath(story, path, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("data", out var data))
        {
            value = data;
        }

        if (value.ValueKind is JsonValueKind.Null)
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            return [ParseTerm(value, path)];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException(
                $"StoryChief taxonomy field '{path}' must contain an object, array, or null value.");
        }

        var terms = new List<StoryChiefTaxonomyTerm>();
        var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in value.EnumerateArray())
        {
            var term = ParseTerm(item, path);
            if (identifiers.Add(term.Identifier))
            {
                terms.Add(term);
            }
        }

        return terms;
    }

    private static StoryChiefTaxonomyTerm ParseTerm(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"StoryChief taxonomy field '{path}' contains a non-object term.");
        }

        string? name = GetOptionalString(value, "name")?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new JsonException($"StoryChief taxonomy field '{path}' contains a term without a name.");
        }

        string? storyChiefId = GetIdentifier(value, "storychief_id");
        string? slug = GetOptionalString(value, "slug")?.Trim();
        string identifier = storyChiefId ?? slug ?? name;

        return new StoryChiefTaxonomyTerm(identifier, name, slug);
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

    private static string? GetIdentifier(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? property.ToString()
            : throw new JsonException($"StoryChief taxonomy term {propertyName} must be a string or number.");
    }

    private static string? GetOptionalString(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}

internal sealed class StoryChiefTaxonomyManager(
    ITaxonomyRetriever taxonomyRetriever,
    IInfoProvider<TaxonomyInfo> taxonomyInfoProvider,
    IInfoProvider<TagInfo> tagInfoProvider,
    IInfoProvider<ContentLanguageInfo> contentLanguageInfoProvider)
{
    private const int MaximumTagTitleLength = 200;
    private static readonly SemaphoreSlim tagMutationLock = new(1, 1);

    public async Task ApplyAsync(
        JsonElement story,
        string languageName,
        StoryChiefPageOptions pageOptions,
        IDictionary<string, object> fields,
        CancellationToken cancellationToken)
    {
        foreach ((string path, var mapping) in pageOptions.TaxonomyMappings)
        {
            Validate(path, mapping);
            var terms = StoryChiefTaxonomyParser.Parse(story, path);
            if (terms is null)
            {
                continue;
            }

            var taxonomy = await taxonomyInfoProvider.GetAsync(mapping.TaxonomyName, cancellationToken)
                ?? throw CreateConfigurationException(
                    $"The Xperience taxonomy '{mapping.TaxonomyName}' configured for StoryChief field '{path}' does not exist.");
            var taxonomyData = await taxonomyRetriever.RetrieveTaxonomy(
                mapping.TaxonomyName,
                languageName,
                cancellationToken);
            var availableTags = taxonomyData.Tags.ToList();
            var identifiers = new HashSet<Guid>();

            foreach (var term in terms)
            {
                identifiers.Add(await ResolveTagIdentifier(
                    path,
                    term,
                    mapping,
                    taxonomy,
                    availableTags,
                    languageName,
                    cancellationToken));
            }

            fields[mapping.XperienceFieldName] = CreateTagReferences(identifiers);
        }
    }

    internal static IReadOnlyList<TagReference> CreateTagReferences(IEnumerable<Guid> identifiers) =>
        identifiers.Select(identifier => new TagReference { Identifier = identifier }).ToList();

    internal static bool IsConfigured(
        ICollection<KeyValuePair<string, StoryChiefTaxonomyMapping>> mappings) =>
        mappings.Count > 0 && mappings.All(mapping =>
            !string.IsNullOrWhiteSpace(mapping.Key)
            && !string.IsNullOrWhiteSpace(mapping.Value.XperienceFieldName)
            && !string.IsNullOrWhiteSpace(mapping.Value.TaxonomyName));

    internal static string GetManagedTagName(
        string path,
        string taxonomyName,
        string storyChiefIdentifier)
    {
        string family = path.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() switch
        {
            "category" or "categories" => "categories",
            "tags" => "tags",
            var value => value ?? path,
        };
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{taxonomyName}\n{family}\n{storyChiefIdentifier}"));

        return $"StoryChief_{Convert.ToHexString(hash)[..24]}";
    }

    private async Task<Guid> ResolveTagIdentifier(
        string path,
        StoryChiefTaxonomyTerm term,
        StoryChiefTaxonomyMapping mapping,
        TaxonomyInfo taxonomy,
        IReadOnlyCollection<Tag> availableTags,
        string languageName,
        CancellationToken cancellationToken)
    {
        if (TryGetExplicitTagName(term, mapping, out string? explicitTagName))
        {
            var explicitTag = FindUniqueTag(
                availableTags,
                tag => tag.Name.Equals(explicitTagName, StringComparison.OrdinalIgnoreCase),
                path,
                term,
                $"code name '{explicitTagName}'");

            return explicitTag.Identifier;
        }

        string managedTagName = GetManagedTagName(path, mapping.TaxonomyName, term.Identifier);
        if (mapping.CreateMissingTags)
        {
            var managedTag = FindTagInfo(managedTagName, taxonomy.TaxonomyID);
            if (managedTag is not null)
            {
                await UpdateManagedTagTitle(managedTag, term.Name, languageName, cancellationToken);
                return managedTag.TagGUID;
            }
        }

        var matchingTags = availableTags.Where(tag =>
            (!string.IsNullOrWhiteSpace(term.Slug)
                && tag.Name.Equals(term.Slug, StringComparison.OrdinalIgnoreCase))
            || tag.Name.Equals(term.Name, StringComparison.OrdinalIgnoreCase)
            || tag.Title.Equals(term.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matchingTags.Count == 1)
        {
            return matchingTags[0].Identifier;
        }

        if (matchingTags.Count > 1)
        {
            throw CreateConfigurationException(
                $"StoryChief taxonomy term '{term.Name}' in field '{path}' matches multiple tags in "
                + $"Xperience taxonomy '{mapping.TaxonomyName}'. Add an explicit tag mapping.");
        }

        if (!mapping.CreateMissingTags)
        {
            throw CreateConfigurationException(
                $"StoryChief taxonomy term '{term.Name}' in field '{path}' has no matching tag in "
                + $"Xperience taxonomy '{mapping.TaxonomyName}'. Add a tag mapping or enable CreateMissingTags.");
        }

        return await CreateManagedTag(
            managedTagName,
            term.Name,
            taxonomy,
            languageName,
            cancellationToken);
    }

    private async Task<Guid> CreateManagedTag(
        string tagName,
        string title,
        TaxonomyInfo taxonomy,
        string languageName,
        CancellationToken cancellationToken)
    {
        await tagMutationLock.WaitAsync(cancellationToken);
        try
        {
            var existing = FindTagInfo(tagName, taxonomy.TaxonomyID);
            if (existing is not null)
            {
                await UpdateManagedTagTitle(existing, title, languageName, cancellationToken);
                return existing.TagGUID;
            }

            var language = GetLanguage(languageName);
            var tag = new TagInfo
            {
                TagName = tagName,
                TagTitle = NormalizeTitle(title),
                TagDescription = string.Empty,
                TagTaxonomyID = taxonomy.TaxonomyID,
                TagOrder = 0,
            };
            if (!language.ContentLanguageIsDefault)
            {
                tag.TagMetadata = CreateTranslationMetadata(language.ContentLanguageGUID, title).Serialize();
            }

            await tagInfoProvider.SetAsync(tag, cancellationToken);
            return tag.TagGUID;
        }
        finally
        {
            tagMutationLock.Release();
        }
    }

    private async Task UpdateManagedTagTitle(
        TagInfo tag,
        string title,
        string languageName,
        CancellationToken cancellationToken)
    {
        var language = GetLanguage(languageName);
        string normalizedTitle = NormalizeTitle(title);
        if (language.ContentLanguageIsDefault)
        {
            if (!tag.TagTitle.Equals(normalizedTitle, StringComparison.Ordinal))
            {
                tag.TagTitle = normalizedTitle;
                await tagInfoProvider.SetAsync(tag, cancellationToken);
            }

            return;
        }

        var metadata = string.IsNullOrWhiteSpace(tag.TagMetadata)
            ? new TagMetadata()
            : TagMetadata.Deserialize(tag.TagMetadata);
        metadata.Translations ??= [];
        metadata.Translations.TryGetValue(language.ContentLanguageGUID, out var existingTranslation);
        if (existingTranslation?.Title.Equals(normalizedTitle, StringComparison.Ordinal) == true)
        {
            return;
        }

        metadata.Translations[language.ContentLanguageGUID] = new TagTranslation
        {
            Title = normalizedTitle,
            Description = existingTranslation?.Description ?? string.Empty,
        };
        tag.TagMetadata = metadata.Serialize();
        await tagInfoProvider.SetAsync(tag, cancellationToken);
    }

    private ContentLanguageInfo GetLanguage(string languageName) => contentLanguageInfoProvider
        .Get()
        .WhereEquals(nameof(ContentLanguageInfo.ContentLanguageName), languageName)
        .TopN(1)
        .FirstOrDefault() ?? throw CreateConfigurationException(
            $"The Xperience content language '{languageName}' does not exist.");

    private TagInfo? FindTagInfo(string tagName, int taxonomyId) => tagInfoProvider
        .Get()
        .WhereEquals(nameof(TagInfo.TagName), tagName)
        .WhereEquals(nameof(TagInfo.TagTaxonomyID), taxonomyId)
        .TopN(1)
        .FirstOrDefault();

    private static Tag FindUniqueTag(
        IEnumerable<Tag> tags,
        Func<Tag, bool> predicate,
        string path,
        StoryChiefTaxonomyTerm term,
        string target)
    {
        var matches = tags.Where(predicate).Take(2).ToList();
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw CreateConfigurationException(
                $"StoryChief taxonomy term '{term.Name}' in field '{path}' maps to {target}, but that tag does not exist."),
            _ => throw CreateConfigurationException(
                $"StoryChief taxonomy term '{term.Name}' in field '{path}' maps to multiple tags with {target}."),
        };
    }

    private static bool TryGetExplicitTagName(
        StoryChiefTaxonomyTerm term,
        StoryChiefTaxonomyMapping mapping,
        out string? tagName)
    {
        foreach (string key in new[] { term.Identifier, term.Slug, term.Name }.OfType<string>())
        {
            if (mapping.TagMappings.TryGetValue(key, out tagName) && !string.IsNullOrWhiteSpace(tagName))
            {
                return true;
            }
        }

        tagName = null;
        return false;
    }

    private static TagMetadata CreateTranslationMetadata(Guid languageIdentifier, string title) => new()
    {
        Translations = new Dictionary<Guid, TagTranslation>
        {
            [languageIdentifier] = new TagTranslation
            {
                Title = NormalizeTitle(title),
                Description = string.Empty,
            },
        },
    };

    private static string NormalizeTitle(string title)
    {
        string normalized = title.Trim();
        return normalized.Length <= MaximumTagTitleLength
            ? normalized
            : normalized[..MaximumTagTitleLength];
    }

    private static void Validate(string path, StoryChiefTaxonomyMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(path)
            || string.IsNullOrWhiteSpace(mapping.XperienceFieldName)
            || string.IsNullOrWhiteSpace(mapping.TaxonomyName))
        {
            throw CreateConfigurationException(
                "StoryChief taxonomy mappings require a story field path, XperienceFieldName, and TaxonomyName.");
        }
    }

    private static StoryChiefPublisherNotConfiguredException CreateConfigurationException(string message) => new(message);
}
