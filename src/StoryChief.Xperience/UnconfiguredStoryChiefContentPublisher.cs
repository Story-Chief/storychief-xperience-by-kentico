using System.Text.Json;

namespace StoryChief.Xperience;

internal sealed class UnconfiguredStoryChiefContentPublisher : IStoryChiefContentPublisher
{
    public Task<StoryChiefPublishResult> PublishAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken) => throw CreateException();

    public Task<StoryChiefPublishResult> UpdateAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken) => throw CreateException();

    public Task<StoryChiefPublishResult> DeleteAsync(
        JsonElement story,
        StoryChiefPublishingContext context,
        CancellationToken cancellationToken) => throw CreateException();

    private static StoryChiefPublisherNotConfiguredException CreateException() => new(
        "Register an IStoryChiefContentPublisher implementation that maps StoryChief fields to your Xperience content type.");
}

internal sealed class StoryChiefPublisherNotConfiguredException(string message) : InvalidOperationException(message);
