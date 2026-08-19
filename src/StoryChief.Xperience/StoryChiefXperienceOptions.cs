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
}
