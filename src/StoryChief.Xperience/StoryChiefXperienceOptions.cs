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
