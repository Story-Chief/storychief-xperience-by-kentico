using System.Text.Json;

namespace StoryChief.Xperience;

/// <summary>
/// Receives authenticated StoryChief publishing events and maps them to the project's content model.
/// </summary>
public interface IStoryChiefContentPublisher
{
    /// <summary>
    /// Creates content from a StoryChief story.
    /// </summary>
    public Task<StoryChiefPublishResult> PublishAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates content previously created from a StoryChief story.
    /// </summary>
    public Task<StoryChiefPublishResult> UpdateAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes or unpublishes content previously created from a StoryChief story.
    /// </summary>
    public Task<StoryChiefPublishResult> DeleteAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Metadata accompanying a StoryChief publishing event.
/// </summary>
/// <param name="Event">The StoryChief event name.</param>
/// <param name="Status">The requested publishing status, when supplied.</param>
/// <param name="LockUpdates">Whether StoryChief requested a status-only update.</param>
public sealed record StoryChiefPublishingContext(string Event, string? Status, bool LockUpdates);

/// <summary>
/// Identifies content created or changed in Xperience.
/// </summary>
/// <param name="Id">The stable external identifier returned to StoryChief.</param>
/// <param name="Permalink">The published or preview URL.</param>
/// <param name="Status">The resulting status, such as published or draft.</param>
public sealed record StoryChiefPublishResult(string Id, string? Permalink, string? Status = null);
