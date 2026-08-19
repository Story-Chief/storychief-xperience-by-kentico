namespace StoryChief.Xperience;

/// <summary>
/// Configures the StoryChief webhook receiver.
/// </summary>
public sealed class StoryChiefXperienceOptions
{
    /// <summary>
    /// The default configuration section name.
    /// </summary>
    public const string SectionName = "StoryChief";

    /// <summary>
    /// The shared signing key generated for the StoryChief destination.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Maximum accepted webhook body size. Defaults to 10 MiB.
    /// </summary>
    public int MaxRequestBodyBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Configures the Xperience website page created for each StoryChief story.
    /// </summary>
    public StoryChiefPageOptions Page { get; } = new();
}

/// <summary>
/// Configures StoryChief publishing into an Xperience website channel.
/// </summary>
public sealed class StoryChiefPageOptions
{
    /// <summary>
    /// The code name of the target website channel.
    /// </summary>
    public string WebsiteChannelName { get; set; } = string.Empty;

    /// <summary>
    /// The full code name of the target page content type, for example <c>Acme.ArticlePage</c>.
    /// </summary>
    public string ContentTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Optional identifier of the default page template assigned to newly created pages.
    /// </summary>
    public string? PageTemplateIdentifier { get; set; }

    /// <summary>
    /// The code name of the content language used for created page variants.
    /// </summary>
    public string LanguageName { get; set; } = "en";

    /// <summary>
    /// Maps StoryChief language codes to Xperience content-language code names.
    /// When empty, all stories use <see cref="LanguageName"/>.
    /// </summary>
    public IDictionary<string, string> LanguageMappings { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The parent page identifier. A value of zero creates pages at the channel root.
    /// </summary>
    public int ParentWebPageItemId { get; set; }

    /// <summary>
    /// The Xperience user recorded in the audit fields of page operations.
    /// </summary>
    public string AuditUserName { get; set; } = "Administrator";

    /// <summary>
    /// Whether delete events permanently remove the page instead of moving it to the recycle bin.
    /// </summary>
    public bool PermanentlyDelete { get; set; }

    /// <summary>
    /// Maps StoryChief JSON property paths to Xperience field code names.
    /// </summary>
    public IDictionary<string, StoryChiefFieldMapping> FieldMappings { get; } =
        new Dictionary<string, StoryChiefFieldMapping>(StringComparer.Ordinal);

    /// <summary>
    /// Configures StoryChief cover images as reusable Xperience content item assets.
    /// </summary>
    public StoryChiefCoverImageOptions CoverImage { get; } = new();

    /// <summary>
    /// Adds or replaces a StoryChief-to-Xperience language mapping.
    /// </summary>
    public void MapLanguage(string storyChiefLanguage, string xperienceLanguageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyChiefLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(xperienceLanguageName);

        LanguageMappings[storyChiefLanguage] = xperienceLanguageName;
    }

    /// <summary>
    /// Adds or replaces a StoryChief-to-Xperience field mapping.
    /// </summary>
    public void MapField(
        string storyFieldPath,
        string xperienceFieldName,
        StoryChiefFieldValueKind valueKind = StoryChiefFieldValueKind.Auto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyFieldPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(xperienceFieldName);

        FieldMappings[storyFieldPath] = new StoryChiefFieldMapping(xperienceFieldName, valueKind);
    }
}

/// <summary>
/// Configures storage and page linking for StoryChief cover images.
/// </summary>
public sealed class StoryChiefCoverImageOptions
{
    /// <summary>
    /// The full code name of the reusable content type that stores images.
    /// Leave empty to disable cover-image publishing.
    /// </summary>
    public string ContentTypeName { get; set; } = string.Empty;

    /// <summary>
    /// The code name of the content item asset field on <see cref="ContentTypeName"/>.
    /// </summary>
    public string AssetFieldName { get; set; } = string.Empty;

    /// <summary>
    /// The code name of the page field that links to the reusable image content item.
    /// </summary>
    public string PageFieldName { get; set; } = string.Empty;

    /// <summary>
    /// Optional text field on the reusable image content type that stores alternative text.
    /// </summary>
    public string? AltTextFieldName { get; set; }

    /// <summary>
    /// The Content hub workspace code name used for created cover-image items.
    /// </summary>
    public string WorkspaceName { get; set; } = string.Empty;

    /// <summary>
    /// Maximum permitted cover-image download size. Defaults to 10 MiB.
    /// </summary>
    public int MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
