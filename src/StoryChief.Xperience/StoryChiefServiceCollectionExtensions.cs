using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the StoryChief integration.
/// </summary>
public static class StoryChiefServiceCollectionExtensions
{
    /// <summary>
    /// Registers StoryChief webhook services.
    /// </summary>
    public static IServiceCollection AddStoryChiefXperience(
        this IServiceCollection services,
        Action<StoryChief.Xperience.StoryChiefXperienceOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.TryAddScoped<StoryChief.Xperience.IStoryChiefContentPublisher, StoryChief.Xperience.KenticoStoryChiefContentPublisher>();

        return services;
    }
}
